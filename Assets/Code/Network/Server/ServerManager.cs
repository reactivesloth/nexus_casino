using System.Collections;
using System.Collections.Generic;
using System.Text;
using Code.UI;
using Newtonsoft.Json;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.Networking;
using PlayerSpawner = Code.Network.Player.PlayerSpawner;

namespace Code.Network.Server
{
    public class ServerManager : MonoBehaviour
    {
        [Header("Server Browser")] public string ServerBrowserUrl = "https://sb-XXXXXXXX.edgegap.net";
        public string ServerToken = "YOUR_SERVER_TOKEN";
        public string GamePortName = "gameport";
        public string StreamPortName = "stream_peer";
        public int MaxPlayers = 100;

        [Header("Keep-Alive")] public float KeepAliveInterval = 30f;

        [Header("Локальный тест (в контейнере берётся из ENV)")]
        public string DebugRequestId = "local-test-01";
        public string DebugPublicIp = "127.0.0.1";
        public int DebugGamePort = 7770;
        public int DebugStreamPort = 9000;

        [Header("Авто-выключение пустого сервера")] [SerializeField]
        private float emptyServerLifeTime = 600f;

        private string _requestId;
        private float _emptyTime;

        // ── Unity lifecycle ───────────────────────────────────

        private void Start()
        {
            InstanceHandler.NetworkManager.Subscribe<ChangeServerInfo>(HandleServerCustomData);
            ConnectToServer();

#if UNITY_SERVER
            InstanceHandler.NetworkManager.onPlayerJoined += OnPlayerJoined;
            InstanceHandler.NetworkManager.onPlayerLeft += OnPlayerLeft;
            Application.quitting += OnApplicationQuitting;
#endif
        }

        private void Update()
        {
#if UNITY_SERVER
            UpdateEmptyTimer();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_SERVER

            if (InstanceHandler.NetworkManager == null) return;
            InstanceHandler.NetworkManager.onPlayerJoined -= OnPlayerJoined;
            InstanceHandler.NetworkManager.onPlayerLeft -= OnPlayerLeft;
            Application.quitting -= OnApplicationQuitting;
            if (!_isShuttingDown)
                DeleteFromServerBrowserSync();
#endif
        }

        private void OnApplicationQuitting()
        {
            _isShuttingDown = true;
            DeleteFromServerBrowserSync();
        }

        private void DeleteFromServerBrowserSync()
        {
            if (string.IsNullOrEmpty(_requestId)) return;

            // UnityWebRequest не успеет — используем System.Net напрямую
            try
            {
                var url = $"{ServerBrowserUrl}/server-instances/{_requestId}";
                var req = System.Net.WebRequest.Create(url) as System.Net.HttpWebRequest;
                req.Method = "DELETE";
                req.Headers["Authorization"] = ServerToken;
                req.Timeout = 3000; // 3 секунды максимум

                using var resp = req.GetResponse();
                Debug.Log($"[ServerReg] Cleanup on quit: {((System.Net.HttpWebResponse)resp).StatusCode}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerReg] Cleanup on quit failed: {e.Message}");
            }
        }
        
        // ── Connect / Spawn ───────────────────────────────────

        private void ConnectToServer()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();

#if UNITY_SERVER
            transport.address = "";
            transport.serverPort = (ushort)DebugGamePort;
            transport.StartServer();
            StartCoroutine(RegisterAndKeepAlive());
#else
            StartCoroutine(ConnectAndSpawnPlayer(
                PlayerPrefs.GetString("Server_IP", "127.0.0.1"),
                ushort.Parse(PlayerPrefs.GetString("Server_Port", DebugGamePort.ToString()))
            ));
#endif
        }

        private IEnumerator ConnectAndSpawnPlayer(string ip = "127.0.0.1", ushort port = 7770)
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();
            transport.address = ip;
            transport.serverPort = port;

            yield return null;

            LoadingScreenUI.Instance?.Show("loading.start_scene", "loading.please_wait");
            transport.StartClient();

            float elapsed = 0f;
            float connectTimeout = 10f;

            while (InstanceHandler.NetworkManager.clientState != ConnectionState.Connected)
            {
                elapsed += Time.deltaTime;

                if (elapsed >= connectTimeout)
                {
                    Debug.LogWarning("[Client] Таймаут подключения — сервер не отвечает");
                    transport.StopClient();

                    // Чистим протухшие данные
                    PlayerPrefs.DeleteKey("Server_IP");
                    PlayerPrefs.DeleteKey("Server_Port");
                    PlayerPrefs.DeleteKey("Current_Server_RequestId");
                    PlayerPrefs.Save();

                    // Идём на матчмейкер искать живой сервер
                    LoadingScreenUI.Instance?.LoadScene("Matchmaker");
                    yield break;
                }

                yield return null;
            }

            Debug.Log("[Client] Connected to server!");
            FindAnyObjectByType<PlayerSpawner>().SpawnPlayer();
            LoadingScreenUI.Instance?.Hide();
        }

        // ── Регистрация + Keep-alive ──────────────────────────

        // В RegisterAndKeepAlive() — добавь синхронизацию слотов вместе с keep-alive
        IEnumerator RegisterAndKeepAlive()
        {
            _requestId = Env("ARBITRIUM_REQUEST_ID", DebugRequestId);
            string publicIp = Env("ARBITRIUM_PUBLIC_IP", DebugPublicIp);

            int gamePort = ResolvePort(GamePortName, DebugGamePort);
            int streamPort = ResolvePort(StreamPortName, DebugStreamPort);

            yield return StartCoroutine(RegisterInstance(_requestId, publicIp, gamePort, streamPort));

            while (true)
            {
                yield return new WaitForSeconds(KeepAliveInterval);
                yield return StartCoroutine(SendKeepAlive(_requestId));
        
                // Синхронизируем слоты каждый keep-alive цикл
                // Это перезаписывает любые истёкшие резервации актуальным числом игроков
                yield return StartCoroutine(SyncSlots());
            }
        }

        private IEnumerator SyncSlots()
        {
            int occupied = InstanceHandler.NetworkManager.playerCount;
            int freeSeats = Mathf.Max(0, MaxPlayers - occupied);
            yield return StartCoroutine(UpdateSlotSeats("default", freeSeats));
            Debug.Log($"[ServerReg] Sync slots: {occupied} игроков, {freeSeats} свободных");
        }

        private int ResolvePort(string portName, int fallback)
        {
            string envKey = $"ARBITRIUM_PORT_{portName.ToUpper().Replace("-", "_")}_EXTERNAL";
            string val = System.Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(val) && int.TryParse(val, out int p1))
            {
                Debug.Log($"[ServerReg] Порт '{portName}' из ENV {envKey} = {p1}");
                return p1;
            }

            string mapping = System.Environment.GetEnvironmentVariable("ARBITRIUM_PORTS_MAPPING");
            if (!string.IsNullOrEmpty(mapping))
            {
                try
                {
                    var map = JsonConvert.DeserializeObject<PortsMappingEnv>(mapping);
                    if (map?.ports != null && map.ports.TryGetValue(portName, out var portData))
                    {
                        Debug.Log($"[ServerReg] Порт '{portName}' из PORTS_MAPPING = {portData.external}");
                        return portData.external;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ServerReg] Ошибка парсинга PORTS_MAPPING: {e.Message}");
                }
            }

            Debug.LogWarning($"[ServerReg] Порт '{portName}' не найден, дефолт {fallback}");
            return fallback;
        }

        // ── Регистрация сервера ───────────────────────────────

        IEnumerator RegisterInstance(string requestId, string publicIp, int gamePort, int streamPort)
        {
            object location;
            string locationJson = System.Environment.GetEnvironmentVariable("ARBITRIUM_DEPLOYMENT_LOCATION");
            if (!string.IsNullOrEmpty(locationJson))
            {
                try { location = JsonConvert.DeserializeObject(locationJson); }
                catch { location = new { city = "Unknown", country = "Unknown", continent = "Unknown", administrative_division = "Unknown", timezone = "UTC" }; }
            }
            else
            {
                location = new { city = "Unknown", country = "Unknown", continent = "Unknown", administrative_division = "Unknown", timezone = "UTC" };
            }

            var body = new
            {
                request_id = requestId,
                metadata = new { max_players = MaxPlayers, name = "Game Server", policy_name = "default" },
                slots = new[] { new { name = "default", available_seats = MaxPlayers, metadata = new { } } },
                server = new
                {
                    fqdn = $"{requestId}.pr.edgegap.net",
                    public_ip = publicIp,
                    ports = new Dictionary<string, object>
                    {
                        [GamePortName] = new { @internal = DebugGamePort, external = gamePort, link = $"{requestId}.pr.edgegap.net:{gamePort}", protocol = "UDP" },
                        [StreamPortName] = new { @internal = DebugStreamPort, external = streamPort, link = $"{requestId}.pr.edgegap.net:{streamPort}", protocol = "UDP" }
                    },
                    location = location
                }
            };

            yield return Post("/server-instances", body, code =>
            {
                if (code == 201) Debug.Log("[ServerReg] ✅ Зарегистрирован");
                else if (code == 409) Debug.LogWarning("[ServerReg] ⚠️ Уже зарегистрирован");
                else Debug.LogError($"[ServerReg] ❌ Ошибка регистрации: {code}");
            });
        }

        // ── Keep-alive ────────────────────────────────────────

        IEnumerator SendKeepAlive(string requestId)
        {
            yield return Post($"/server-instances/{requestId}/keep-alive", new { }, code =>
            {
                if (code != 200) Debug.LogWarning($"[ServerReg] Keep-alive: {code}");
            });
        }

        // ── Слоты (обновление при join/leave) ─────────────────

        void OnPlayerJoined(PlayerID playerId, bool isReconnect, bool asServer)
        {
            if (isReconnect) return;
            UpdateSlots();
        }

        void OnPlayerLeft(PlayerID playerId, bool isTimeout)
        {
            UpdateSlots();
        }

        void UpdateSlots()
        {
            // playerCount — реальное число подключённых игроков
            int occupied = InstanceHandler.NetworkManager.playerCount;
            int freeSeats = Mathf.Max(0, MaxPlayers - occupied);
            StartCoroutine(UpdateSlotSeats("default", freeSeats));
            Debug.Log($"[ServerReg] Игроков: {occupied}, свободно: {freeSeats}");
        }

        IEnumerator UpdateSlotSeats(string slotName, int availableSeats)
        {
            yield return Patch($"/server-instances/{_requestId}/slots/{slotName}",
                new { available_seats = availableSeats }, code =>
                {
                    Debug.Log(code == 201
                        ? $"[ServerReg] Слот: {availableSeats} свободных"
                        : $"[ServerReg] Ошибка слота: {code}");
                });
        }

        // ── Авто-выключение пустого сервера ───────────────────

        private bool _isShuttingDown = false;

        private void UpdateEmptyTimer()
        {
            if (_isShuttingDown) return;

            // Считаем только реальных клиентов, без host-соединения
            int clientCount = InstanceHandler.NetworkManager.playerCount;
    
            if (clientCount > 0)
            {
                _emptyTime = 0f;
                return;
            }

            _emptyTime += Time.deltaTime;

            // Лог каждые 60 секунд чтобы видеть что таймер работает
            if (Mathf.FloorToInt(_emptyTime) % 60 == 0 && _emptyTime > 1f)
                Debug.Log($"[ServerReg] Пустой сервер: {Mathf.FloorToInt(_emptyTime)}с / {emptyServerLifeTime}с");

            if (_emptyTime >= emptyServerLifeTime)
            {
                _isShuttingDown = true;
                Debug.Log("[ServerReg] Сервер пуст — завершение...");
                Shutdown(); // вместо Application.Quit() — чистит SB перед выходом
            }
        }

        // ── Shutdown (вызывается AdminPanelHandler) ───────────

        public void Shutdown()
        {
            if (_isShuttingDown && !isActiveAndEnabled) return;
            _isShuttingDown = true;
            StartCoroutine(ShutdownCoroutine());
        }

        private IEnumerator ShutdownCoroutine()
        {
            // Шаг 1: удаляем запись из Server Browser (используем ServerToken — без 403)
            yield return Delete($"/server-instances/{_requestId}", code =>
                Debug.Log($"[ServerReg] SB Delete: {code}"));

            // Шаг 2: останавливаем контейнер через ARBITRIUM_DELETE_URL
            string deleteUrl   = Env("ARBITRIUM_DELETE_URL", "");
            string deleteToken = Env("ARBITRIUM_DELETE_TOKEN", "");

            if (!string.IsNullOrEmpty(deleteUrl))
            {
                using var req = new UnityWebRequest(deleteUrl, "DELETE")
                    { downloadHandler = new DownloadHandlerBuffer() };
                req.SetRequestHeader("Authorization", deleteToken);
                yield return req.SendWebRequest();
                Debug.Log($"[ServerReg] Stop deployment: {req.responseCode}");
            }

            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        // ── Custom data handler ───────────────────────────────

        private void HandleServerCustomData(PlayerID player, ChangeServerInfo data, bool asServer)
        {
            if (!asServer) return;
            var parts = data.NewServerDataString?.Split('|');
            if (parts is { Length: 2 })
                Debug.Log($"[ServerReg] Custom data: {parts[0]} | {parts[1]}");
        }

        // ── HTTP helpers ──────────────────────────────────────

        IEnumerator Post(string path, object body, System.Action<long> onDone)
            => Send("POST", path, body, onDone);

        IEnumerator Patch(string path, object body, System.Action<long> onDone)
            => Send("PATCH", path, body, onDone);

        IEnumerator Delete(string path, System.Action<long> onDone)
        {
            using var req = new UnityWebRequest(ServerBrowserUrl + path, "DELETE")
                { downloadHandler = new DownloadHandlerBuffer() };
            req.SetRequestHeader("Authorization", ServerToken);
            yield return req.SendWebRequest();
            onDone(req.responseCode);
        }

        IEnumerator Send(string method, string path, object body, System.Action<long> onDone)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
            using var req = new UnityWebRequest(ServerBrowserUrl + path, method)
            {
                uploadHandler = new UploadHandlerRaw(bytes),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Authorization", ServerToken);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            onDone(req.responseCode);
        }

        static string Env(string key, string fallback) =>
            System.Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

        // ── Structs ───────────────────────────────────────────

        public struct ChangeServerInfo : IPackedAuto
        {
            public string NewServerDataString;
        }

        class PortsMappingEnv
        {
            public Dictionary<string, PortsMappingItem> ports;
        }

        class PortsMappingItem
        {
            public string name;
            public int @internal;
            public int external;
            public string protocol;
        }
    }
}