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
            if (InstanceHandler.NetworkManager == null) return;
            InstanceHandler.NetworkManager.onPlayerJoined -= OnPlayerJoined;
            InstanceHandler.NetworkManager.onPlayerLeft -= OnPlayerLeft;
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

            LoadingScreenUI.Instance?.Show("loading.start_scene", "loading.please_wait");
            transport.StartClient();

            yield return new WaitUntil(() =>
                InstanceHandler.NetworkManager.clientState == ConnectionState.Connected);

            Debug.Log("[Client] Connected to server!");
            FindAnyObjectByType<PlayerSpawner>().SpawnPlayer();
            LoadingScreenUI.Instance?.Hide();
        }

        // ── Регистрация + Keep-alive ──────────────────────────

        IEnumerator RegisterAndKeepAlive()
        {
            _requestId = Env("ARBITRIUM_REQUEST_ID", DebugRequestId);
            string publicIp = Env("ARBITRIUM_PUBLIC_IP", DebugPublicIp);

            int gamePort = ResolvePort(GamePortName, DebugGamePort);
            int streamPort = ResolvePort(StreamPortName, DebugStreamPort);

            Debug.Log($"[ServerReg] id={_requestId} ip={publicIp} gamePort={gamePort} streamPort={streamPort}");

            yield return StartCoroutine(RegisterInstance(_requestId, publicIp, gamePort, streamPort));

            while (true)
            {
                yield return new WaitForSeconds(KeepAliveInterval);
                yield return StartCoroutine(SendKeepAlive(_requestId));
            }
        }

        private int ResolvePort(string portName, int fallback)
        {
            // Вариант 1: ARBITRIUM_PORT_GAMEPORT_EXTERNAL / ARBITRIUM_PORT_STREAM_PEER_EXTERNAL
            string envKey = $"ARBITRIUM_PORT_{portName.ToUpper().Replace("-", "_")}_EXTERNAL";
            string val = System.Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(val) && int.TryParse(val, out int p1))
            {
                Debug.Log($"[ServerReg] Порт '{portName}' из ENV {envKey} = {p1}");
                return p1;
            }

            // Вариант 2: ARBITRIUM_PORTS_MAPPING (JSON формат)
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
            // Локация из ARBITRIUM_DEPLOYMENT_LOCATION (JSON)
            object location;
            string locationJson = System.Environment.GetEnvironmentVariable("ARBITRIUM_DEPLOYMENT_LOCATION");
            if (!string.IsNullOrEmpty(locationJson))
            {
                try
                {
                    location = JsonConvert.DeserializeObject(locationJson);
                }
                catch
                {
                    location = new
                    {
                        city = "Unknown", country = "Unknown", continent = "Unknown",
                        administrative_division = "Unknown", timezone = "UTC"
                    };
                }
            }
            else
            {
                location = new
                {
                    city = "Unknown", country = "Unknown", continent = "Unknown", administrative_division = "Unknown",
                    timezone = "UTC"
                };
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
                        [GamePortName] = new
                        {
                            @internal = DebugGamePort, external = gamePort,
                            link = $"{requestId}.pr.edgegap.net:{gamePort}", protocol = "UDP"
                        },
                        [StreamPortName] = new
                        {
                            @internal = DebugStreamPort, external = streamPort,
                            link = $"{requestId}.pr.edgegap.net:{streamPort}", protocol = "UDP"
                        }
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
            int freeSeats = Mathf.Max(0, MaxPlayers - InstanceHandler.NetworkManager.playerCount);
            StartCoroutine(UpdateSlotSeats("default", freeSeats));
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

        private void UpdateEmptyTimer()
        {
            if (InstanceHandler.NetworkManager.playerCount > 0)
            {
                _emptyTime = 0f;
                return;
            }

            _emptyTime += Time.deltaTime;
            if (_emptyTime >= emptyServerLifeTime) Application.Quit();
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