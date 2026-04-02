using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Code.UI;
using Code.UI.Popup;
using Code.Utility;
using Ricimi;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Matchmaking : MonoBehaviour
{
    private const string GameSceneName = "Main";
    private const string MenuSceneName = "Init";

    [SerializeField] private float timeout = 60f;
    private float _leftTime = 0f;
        
    [Header("Edgegap Matchmaker")]
    [SerializeField] private string matchmakerApiUrl = "https://om-94wi0wxxb0.edgegap.net"; // без слеша в конце
    [SerializeField] private string authToken = "YOUR_AUTH_TOKEN"; // из Edgegap dashboard
    [SerializeField] private float pollIntervalSeconds = 2f;

    private string playerIp = null;
    private string _currentTicketId;
    private Coroutine _pollCoroutine;
    
    private NexusModularPopupOpener _popupOpener;
    
    private void Start()
    {
        _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
        LoadingScreenUI.Instance.Show("loading.find_server", "loading");
        StartMatchmaking();
    }

    private void Update()
    {
        _leftTime += Time.deltaTime;

        if (_leftTime >= timeout && !_popupOpener.Opened)
            OnMatchMakingError("timeout");
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


    private void ShowPopup (string title, string message)
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
    
    
    [ContextMenu("Start Matchmaking")]
    public void StartMatchmaking()
    {
        if (_pollCoroutine != null)
        {
            Debug.LogWarning("Matchmaking already in progress.");
            return;
        }

        StartCoroutine(CreateTicketRoutine());
    }

    [ContextMenu("Cancel Matchmaking")]
    public void CancelMatchmaking()
    {
        if (string.IsNullOrEmpty(_currentTicketId))
            return;

        StartCoroutine(DeleteTicketRoutine(_currentTicketId));
        if (_pollCoroutine != null)
            StopCoroutine(_pollCoroutine);

        _pollCoroutine = null;
        _currentTicketId = null;
    }

    private IEnumerator CreateTicketRoutine()
    {
        // 1. Получаем IP игрока
        playerIp = null;
        using (var ipReq = UnityWebRequest.Get("https://api.ipify.org"))
        {
            yield return ipReq.SendWebRequest();
            if (ipReq.result == UnityWebRequest.Result.Success)
                playerIp = ipReq.downloadHandler.text.Trim();
            else
            {
                Debug.LogError("Failed to get player IP");
                yield break;
            }
        }

        // 2. Пингуем беаконы через HTTPS
        var beaconLatencies = new Dictionary<string, float>();
        using (var beaconReq = UnityWebRequest.Get($"{matchmakerApiUrl}/locations/beacons"))
        {
            beaconReq.SetRequestHeader("Authorization", authToken);
            yield return beaconReq.SendWebRequest();

            if (beaconReq.result == UnityWebRequest.Result.Success)
            {
                var beaconJson = beaconReq.downloadHandler.text;
                var fqdns  = Regex.Matches(beaconJson, "\"fqdn\"\\s*:\\s*\"([^\"]+)\"");
                var cities = Regex.Matches(beaconJson, "\"city\"\\s*:\\s*\"([^\"]+)\"");

                for (int i = 0; i < fqdns.Count && i < cities.Count; i++)
                {
                    string city = cities[i].Groups[1].Value;
                    string host = fqdns[i].Groups[1].Value;

                    float latency = 999f;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    // ✅ HTTPS без порта — Unity не блокирует
                    using var pingReq = UnityWebRequest.Head($"https://{host}");
                    pingReq.timeout = 5;
                    yield return pingReq.SendWebRequest();
                    sw.Stop();

                    // Любой ответ (даже 404/403) означает что сервер доступен
                    latency = (float)sw.ElapsedMilliseconds;
                    beaconLatencies[city] = latency;
                    Debug.Log($"[Matchmaking] Beacon {city}: {latency}ms");
                }
            }

            // Если беаконы не получили — фолбэк с одинаковыми значениями
            // (правило difference:100 пройдёт, т.к. разница = 0)
            if (beaconLatencies.Count == 0)
            {
                Debug.LogWarning("[Matchmaking] Beacon list empty, using fallback latencies");
                beaconLatencies["Montreal"] = 50f;
                beaconLatencies["Toronto"]  = 50f;
                beaconLatencies["Quebec"]   = 50f;
            }
        }

        // 3. Создаём тикет с реальными latency
        var beaconParts = new System.Text.StringBuilder();
        bool firstBeacon = true;
        foreach (var kv in beaconLatencies)
        {
            if (!firstBeacon) beaconParts.Append(",");
            beaconParts.Append($"\"{kv.Key}\":{kv.Value:F1}");
            firstBeacon = false;
        }

        var ticketJson = "{"
                         + $"\"player_ip\":\"{playerIp}\","
                         + "\"profile\":\"backfill-example\","
                         + "\"attributes\":{"
                         + "\"backfill_group_size\":[\"value 1\"],"
                         + $"\"beacons\":{{{beaconParts}}}"
                         + "}}";

        var bodyRaw = Encoding.UTF8.GetBytes(ticketJson);
        using (var req = new UnityWebRequest($"{matchmakerApiUrl}/tickets", UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", authToken);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"CreateTicket error: {req.responseCode} {req.downloadHandler.text}");
                OnMatchMakingError();
                yield break;
            }

            Debug.Log($"[Matchmaking] Ticket created: {req.downloadHandler.text}");
            var ticketResponse = JsonUtility.FromJson<TicketResponse>(req.downloadHandler.text);
            _currentTicketId = ticketResponse.id;
        }

        _pollCoroutine = StartCoroutine(PollTicketRoutine(_currentTicketId));
    }

    private IEnumerator PollTicketRoutine(string ticketId)
    {
        var url = $"{matchmakerApiUrl}/tickets/{ticketId}";

        while (true)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", authToken);
                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    Debug.LogError($"GetTicket error: {req.responseCode} {req.error} {req.downloadHandler.text}");
                    yield break;
                }

                var json = req.downloadHandler.text;
                Debug.Log($"Ticket status: {json}");

                var ticket = JsonUtility.FromJson<TicketResponse>(json);
                
                if (ticket.status == "HOST_ASSIGNED") // или "ASSIGNED" — сверь со Swagger
                {
                    if (ticket.assignment?.ports?.gameport != null)
                    {
                        string host = ticket.assignment.fqdn;
                        int port = ticket.assignment.ports.gameport.external;
                        int portStream = ticket.assignment.ports.stream_peer.external;
                        
                        Debug.Log($"Match found! Connect to {host}:{port}:{portStream}");

                        ConnectToGameServer(host, port, portStream);
                    }

                    _pollCoroutine = null;
                    yield break;
                }
                else if (ticket.status == "CANCELLED" || ticket.status == "EXPIRED" || ticket.status == "FAILED")
                {
                    Debug.LogWarning($"Ticket finished with status {ticket.status}");
                    _pollCoroutine = null;
                    yield break;
                }
            }

            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private IEnumerator DeleteTicketRoutine(string ticketId)
    {
        var url = $"{matchmakerApiUrl}/tickets/{ticketId}";
        using (var req = UnityWebRequest.Delete(url))
        {
            req.SetRequestHeader("Authorization", authToken);
            yield return req.SendWebRequest();
        }
    }

    private void ConnectToGameServer(string host, int port, int portStream)
    {
        PlayerPrefs.SetString("ServerIDName", "name");
        PlayerPrefs.SetString("Server_IP", host);
        PlayerPrefs.SetString("Server_Port", port.ToString());
        PlayerPrefs.SetString("Stream_Port", portStream.ToString());
        PlayerPrefs.SetString("Ticket_ID", _currentTicketId);
        PlayerPrefs.SetString("Player_IP", playerIp);
        
        /*var builds = await ApiClient.GetBuildsAsync(Application.version);
        if (builds.total_builds == 0)
        {
            OnMatchMakingError("version");
            return;
        }

        var savedServerId = PlayerPrefs.GetString("PrefsServerIDName", null);

        if (string.IsNullOrEmpty(savedServerId))
        {
            FindServer();
            return;
        }

        var instanceData = await GetInstanceData(savedServerId);
            
        if (instanceData == null || instanceData.status == "stopped")
        {
            FindServer();
            return;
        }*/
        SceneManager.LoadScene(GameSceneName);
    }

    #region DTO

    [Serializable]
    private class CreateTicketRequest
    {
        public string player_ip;
        public string profile;
        public Attributes attributes;
    }

    [Serializable]
    private class Attributes
    {
        public string[] backfill_group_size;
        public Beacons beacons;
    }

    [Serializable]
    private class Beacons
    {
        public float Montenegro; // или Montreal, как в твоих данных
        public float Toronto;
        public float Quebec;
        // Подправь имена полей под твой JSON
    }

    [Serializable]
    private class TicketResponse
    {
        public string id;
        public string profile;
        public string group_id;
        public string team_id;
        public string player_ip;
        public Assignment assignment;
        public string created_at;
        public string status;
        public string match_id;
    }

    [Serializable]
    private class Assignment
    {
        public string fqdn;
        public string public_ip;
        public Ports ports;
        public Location location;
    }

    [Serializable]
    private class Ports
    {
        public GamePort gameport;
        public StreamPeer stream_peer;
    }

    [Serializable]
    private class GamePort
    {
        public int internal_;
        public int external;
        public string link;
        public string protocol;
    }

    [Serializable]
    private class StreamPeer
    {
        public int internal_;
        public int external;
        public string link;
        public string protocol;
    }

    [Serializable]
    private class Location
    {
        public string city;
        public string country;
        public string continent;
        public string administrative_division;
        public string timezone;
    }

    #endregion
}