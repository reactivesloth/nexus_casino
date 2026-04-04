using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Code.API;
using Code.Network;
using Code.UI;
using Code.UI.Popup;
using Code.Utility;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Ricimi;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EdgegapServerBrowser : MonoBehaviour
{

    [SerializeField] private float timeout = 60f;
    private float _leftTime = 0f;
    private NexusModularPopupOpener _popupOpener;

    private const string GameSceneName = "Main";
    private const string MenuSceneName = "Init";



    [Header("Создание нового сервера")]
    public string EdgegapApiToken = "token YOUR_EDGEGAP_API_TOKEN"; // основной API токен

    public string AppName = "your-app-name"; // имя приложения в Edgegap

    public string ServerBrowserUrl = "https://sb-XXXXXXXX.edgegap.net";

    public string ClientToken = "YOUR_CLIENT_TOKEN";
    public string ServerToken = "YOUR_SERVER_TOKEN";
    
    public string GamePortName = "gameport";
    public string StreamPortName = "stream_peer";

    public bool RetryIfNoServer = true;
    public float RetryInterval = 5f;
    public int MaxRetries = 12;

    public event Action<string> OnStatus;
    public event Action<string, int> OnReadyToConnect;
    public event Action<string> OnFailed;

    private string _currentRequestId;

    public void Start()
    {
        // Сохраняем настройки для AdminAPI
        EdgegapAdminAPI.Configure(ServerBrowserUrl, ClientToken, ServerToken, EdgegapApiToken, AppName);

        _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
        LoadingScreenUI.Instance.Show("loading.find_server", "loading");
        StartCoroutine(Run());
        OnFailed += OnMatchMakingError;
    }

    private void Update()
    {
        _leftTime += Time.deltaTime;

        if (_leftTime >= timeout && !_popupOpener.Opened)
            OnFailed.Invoke("timeout");
    }

    private void OnDisable()
    {
        OnFailed -= OnMatchMakingError;
    }

    private void UpdateReadyPopup()
    {
        CursorManager.Instance.SetForceShowCursor(true);
        _popupOpener.Title = LocalizationHelper.GetLocalizedString("errors.update_nexus_title");
        _popupOpener.Subtitle = "";
        _popupOpener.Message = LocalizationHelper.GetLocalizedString("errors.update_nexus");
        _popupOpener.ManualyCloseAction = LoadMainMenu;

        var okButton = new ButtonInfo
        {
            Label = LocalizationHelper.GetLocalizedString("buttons.update"),
            ClosePopupWhenClicked = true,
            OnClickedEvent = new Button.ButtonClickedEvent()
        };
        okButton.OnClickedEvent.AddListener(() =>
        {
            Application.OpenURL("https://nexusmetaclub.com/update#download");
            CursorManager.Instance.SetForceShowCursor(false);
            LoadMainMenu();
        });
        _popupOpener.Buttons.Add(okButton);
        _popupOpener.OpenPopup();
    }


    private void ShowPopup(string title, string message)
    {
        CursorManager.Instance.SetForceShowCursor(true);
        _popupOpener.Title = title;
        _popupOpener.Subtitle = "";
        _popupOpener.Message = message;
        _popupOpener.ManualyCloseAction = LoadMainMenu;

        var okButton = new ButtonInfo
        {
            Label = "OK",
            ClosePopupWhenClicked = true,
            OnClickedEvent = new Button.ButtonClickedEvent()
        };
        okButton.OnClickedEvent.AddListener(() =>
        {
            CursorManager.Instance.SetForceShowCursor(false);
            LoadMainMenu();
        });
        _popupOpener.Buttons.Add(okButton);
        _popupOpener.OpenPopup();
    }

    private void LoadMainMenu()
    {
        CursorManager.Instance.SetForceShowCursor(true);
        LoadingScreenUI.Instance.LoadScene(MenuSceneName);
    }

    private void OnMatchMakingError(string error = "unknown")
    {
        switch (error)
        {
            case "version":
                UpdateReadyPopup();
                break;
            case "timeout":
                ShowPopup("Timeout error", "The server is not responding. Please try again later.");
                break;
            default:
                ShowPopup("Unknown error", "An unknown error occurred. Please try again later.");
                break;
        }
    }

    IEnumerator Run()
    {
        string targetRequestId = PlayerPrefs.GetString("Target_Server_RequestId", "");
        bool forceNewServer = PlayerPrefs.GetInt("Force_New_Server", 0) == 1;

        // Сбрасываем флаги сразу
        PlayerPrefs.DeleteKey("Target_Server_RequestId");
        PlayerPrefs.DeleteKey("Force_New_Server");
        PlayerPrefs.Save();

        // Если есть конкретный сервер — коннектимся к нему напрямую
        if (!string.IsNullOrEmpty(targetRequestId))
        {
            SetStatus($"Подключение к серверу {targetRequestId}...");
            yield return StartCoroutine(ConnectToSpecificServer(targetRequestId));
            yield break;
        }

        int attempts = 0;
        bool deploymentCreated = forceNewServer; // если только что создали — пропускаем первый deploy
        int deploymentWaitAttempts = 0;
        int maxDeploymentWaitAttempts = 6;

        while (true)
        {
            attempts++;
            SetStatus($"Поиск серверов... (попытка {attempts})");

            ServerInstanceItem instance = null;
            yield return StartCoroutine(FindAvailableInstance(r => instance = r));

            if (instance == null)
            {
                if (deploymentCreated)
                {
                    deploymentWaitAttempts++;
                    if (deploymentWaitAttempts >= maxDeploymentWaitAttempts)
                    {
                        SetStatus("Сервер не запустился. Пробую создать новый...");
                        deploymentCreated = false;
                        deploymentWaitAttempts = 0;
                    }
                    else
                    {
                        SetStatus(
                            $"Ожидание запуска сервера ({deploymentWaitAttempts}/{maxDeploymentWaitAttempts})...");
                        yield return new WaitForSeconds(RetryInterval);
                        continue;
                    }
                }

                SetStatus("Свободных серверов нет. Создаю новый...");
                bool created = false;
                yield return StartCoroutine(CreateDeployment(r => created = r));

                if (!created)
                {
                    if (!RetryIfNoServer || attempts >= MaxRetries)
                    {
                        OnFailed?.Invoke("");
                        yield break;
                    }

                    SetStatus($"Не удалось создать сервер. Повтор через {RetryInterval}с...");
                    yield return new WaitForSeconds(RetryInterval);
                    continue;
                }

                deploymentCreated = true;
                deploymentWaitAttempts = 0;
                SetStatus("Ожидание запуска сервера...");
                yield return new WaitForSeconds(RetryInterval);
                continue;
            }

            deploymentCreated = false;
            deploymentWaitAttempts = 0;

            SlotItem slot = null;
            yield return StartCoroutine(FindAvailableSlot(instance.request_id, r => slot = r));

            if (slot != null)
            {
                _currentRequestId = instance.request_id;

                bool reserved = false;
                yield return StartCoroutine(ReserveSeat(instance.request_id, slot.name, r => reserved = r));

                if (reserved)
                {
                    ServerConnectionInfo info = null;
                    yield return StartCoroutine(GetServerInfo(instance.request_id, r => info = r));

                    if (info != null)
                    {
                        SetStatus($"Подключение к {info.host}:{info.port}...");
                        ConnectToGameServer(info.host, info.port, info.streamPort);
                        yield break;
                    }
                }
            }

            if (!RetryIfNoServer || attempts >= MaxRetries)
            {
                OnFailed?.Invoke("");
                yield break;
            }

            SetStatus($"Сервер не найден. Повтор через {RetryInterval}с...");
            yield return new WaitForSeconds(RetryInterval);
        }
    }

    IEnumerator ConnectToSpecificServer(string requestId)
    {
        bool isNew = PlayerPrefs.GetInt("Target_Server_IsNew", 0) == 1;
        PlayerPrefs.DeleteKey("Target_Server_IsNew");
        PlayerPrefs.Save();

        int attempts = 0;
        int maxAttempts = isNew ? 20 : 5; // для существующего сервера не ждём долго

        SetStatus(isNew
            ? "Ожидание запуска нового сервера..."
            : $"Подключение к серверу {requestId}...");

        // ── Для существующего сервера сначала проверяем что он живой ──
        if (!isNew)
        {
            ServerInstanceItem existingCheck = null;
            yield return StartCoroutine(FindSpecificInstance(requestId, r => existingCheck = r));

            if (existingCheck == null)
            {
                // Сервера нет в Server Browser — он уже мёртв
                SetStatus($"Сервер {requestId} не существует, ищу другой...");
                PlayerPrefs.DeleteKey("Current_Server_RequestId");
                PlayerPrefs.Save();
                StartCoroutine(Run()); // идём в общий поиск
                yield break;
            }
        }

        while (attempts < maxAttempts)
        {
            attempts++;

            // Для нового сервера ждём появления в Server Browser
            if (isNew)
            {
                ServerInstanceItem instance = null;
                yield return StartCoroutine(FindSpecificInstance(requestId, r => instance = r));

                if (instance == null)
                {
                    SetStatus($"Ожидание регистрации сервера... ({attempts}/{maxAttempts})");
                    yield return new WaitForSeconds(RetryInterval);
                    continue;
                }

                isNew = false;
            }

            SlotItem slot = null;
            yield return StartCoroutine(FindAvailableSlot(requestId, r => slot = r));

            if (slot != null)
            {
                bool reserved = false;
                yield return StartCoroutine(ReserveSeat(requestId, slot.name, r => reserved = r));

                if (reserved)
                {
                    ServerConnectionInfo info = null;
                    yield return StartCoroutine(GetServerInfo(requestId, r => info = r));

                    if (info != null)
                    {
                        _currentRequestId = requestId;
                        SetStatus($"Подключение к {info.host}:{info.port}...");
                        ConnectToGameServer(info.host, info.port, info.streamPort);
                        yield break;
                    }
                }
            }

            SetStatus($"Ожидание сервера... ({attempts}/{maxAttempts})");
            yield return new WaitForSeconds(RetryInterval);
        }

        SetStatus("Целевой сервер недоступен, ищу другой...");
        PlayerPrefs.DeleteKey("Current_Server_RequestId");
        PlayerPrefs.Save();
        StartCoroutine(Run());
    }

    IEnumerator FindSpecificInstance(string requestId, Action<ServerInstanceItem> callback)
    {
        string url = $"{ServerBrowserUrl}/server-instances/{requestId}";

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", ClientToken);
        yield return req.SendWebRequest();

        // 404 — сервер ещё не зарегистрировался, это нормально
        if (req.responseCode == 404)
        {
            callback(null);
            yield break;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SB] FindSpecificInstance {requestId}: {req.responseCode}");
            callback(null);
            yield break;
        }

        var inst = JsonConvert.DeserializeObject<ServerInstanceItem>(req.downloadHandler.text);
        callback(inst?.total_joinable_seats > 0 ? inst : null);
    }

    IEnumerator CreateDeployment(Action<bool> callback)
    {
        string url = "https://api.edgegap.com/v1/deploy";
        string body = JsonConvert.SerializeObject(new
        {
            app_name = AppName,
            version_name = Application.version,
            filters = new[]
            {
                new
                {
                    filter_type = "any", // 'any' | 'all' | 'not'
                    field = "continent",
                    values = new[] { "Europe" }
                }
            }
        });

        using var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Authorization", EdgegapApiToken);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.responseCode == 200 || req.responseCode == 201)
        {
            Debug.Log($"[SB] ✅ Деплоймент создан: {req.downloadHandler.text}");
            callback(true);
        }
        else
        {
            Debug.LogError($"[SB] ❌ Ошибка создания деплоймента: {req.responseCode}\n{req.downloadHandler.text}");
            callback(false);
        }
    }

    IEnumerator FindAvailableInstance(Action<ServerInstanceItem> callback)
    {
        // Без filter — берём всё и фильтруем сами
        string url = $"{ServerBrowserUrl}/server-instances?limit=20";

        yield return Get(url, json =>
        {
            Debug.Log($"[SB] server-instances response: {json}"); // временный лог

            var resp = JsonConvert.DeserializeObject<ServerInstanceListResponse>(json);
            if (resp?.items == null || resp.items.Count == 0)
            {
                Debug.Log("[SB] Список пустой");
                callback(null);
                return;
            }

            // Фильтруем и сортируем на клиенте
            var available = resp.items.FindAll(i => i.total_joinable_seats > 0);
            available.Sort((a, b) => b.total_joinable_seats.CompareTo(a.total_joinable_seats));

            Debug.Log($"[SB] Найдено серверов: {resp.items.Count}, с местами: {available.Count}");
            callback(available.Count > 0 ? available[0] : null);
        });
    }

    IEnumerator FindAvailableSlot(string requestId, Action<SlotItem> callback)
    {
        string filter = Uri.EscapeDataString("joinable_seats gt 0");
        string url = $"{ServerBrowserUrl}/server-instances/{requestId}/slots?filter={filter}&limit=20";
        yield return Get(url, json =>
        {
            var resp = JsonConvert.DeserializeObject<SlotListResponse>(json);
            callback(resp?.items?.Count > 0 ? resp.items[0] : null);
        });
    }

    IEnumerator ReserveSeat(string requestId, string slotName, Action<bool> callback)
    {
        string url =
            $"{ServerBrowserUrl}/server-instances/{requestId}/slots/{Uri.EscapeDataString(slotName)}/reservations";
        string body = JsonConvert.SerializeObject(new
        {
            users = new[] { new { user_id = ClientDataStorage.UserData.username } }
        });

        SetStatus($"Резервирую место в слоте '{slotName}'...");

        bool ok = false;
        yield return Post(url, body, _ => ok = true);
        callback(ok);
    }

    IEnumerator GetServerInfo(string requestId, Action<ServerConnectionInfo> callback)
    {
        string url = $"{ServerBrowserUrl}/server-instances/{requestId}";

        yield return Get(url, json =>
        {
            var inst = JsonConvert.DeserializeObject<ServerInstanceFull>(json);
            if (inst?.server == null)
            {
                callback(null);
                return;
            }

            callback(new ServerConnectionInfo
            {
                host = inst.server.fqdn,
                port = inst.server.ports[GamePortName].external,
                streamPort = inst.server.ports[StreamPortName].external,
            });

            Debug.Log($"JSON: " + json);
        });
    }

    private void ConnectToGameServer(string host, int port, int portStream)
    {
        PlayerPrefs.SetString("ServerIDName", "name");
        PlayerPrefs.SetString("Server_IP", host);
        PlayerPrefs.SetString("Server_Port", port.ToString());
        PlayerPrefs.SetString("Stream_Port", portStream.ToString());
        PlayerPrefs.SetString("Current_Server_RequestId", _currentRequestId);
        PlayerPrefs.Save();

        SceneManager.LoadScene(GameSceneName);
    }

    IEnumerator Get(string url, Action<string> onSuccess)
    {
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", ClientToken);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess(req.downloadHandler.text);
        else
        {
            Debug.LogError($"[SB] GET {url} → {req.responseCode}: {req.error}");
            OnFailed?.Invoke(req.error);
        }
    }

    IEnumerator Post(string url, string body, Action<string> onSuccess)
    {
        using var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Authorization", ClientToken);
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if ((int)req.responseCode == 201 || (int)req.responseCode == 409 ||
            req.result == UnityWebRequest.Result.Success)
            onSuccess(req.downloadHandler.text);
        else
            Debug.LogError($"[SB] POST {url} → {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
    }

    void SetStatus(string msg)
    {
        Debug.Log($"[SB] {msg}");
        OnStatus?.Invoke(msg);
    }

    class ServerInstanceListResponse
    {
        public List<ServerInstanceItem> items;
    }

    class ServerInstanceItem
    {
        public string request_id;
        public int total_available_seats;
        public int total_reserved_seats;
        public int total_joinable_seats;
        public object metadata;
    }

    class SlotListResponse
    {
        public List<SlotItem> items;
    }

    class SlotItem
    {
        public string name;
        public int available_seats;
        public int reserved_seats;
        public int joinable_seats;
    }

    class ServerInstanceFull
    {
        public string request_id;
        public int total_joinable_seats;
        public ServerData server;
        public List<SlotItem> slots;
    }

    class ServerData
    {
        public string fqdn;
        public string public_ip;
        public Dictionary<string, PortInfo> ports;
    }

    class PortInfo
    {
        public int @internal;
        public int external;
        public string link;
        public string protocol;
    }

    class ServerConnectionInfo
    {
        public string host;
        public int port;
        public int streamPort;
    }
}