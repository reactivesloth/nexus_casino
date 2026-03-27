using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Code.UI;
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
        private const string MatchmakerUrl   = "https://om-94wi0wxxb0.edgegap.net";
        private const string MatchmakerToken = "YOUR_AUTH_TOKEN";
        private const int    MaxPlayers      = 100;

        private readonly string _deleteUrl   = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_URL");
        private readonly string _deleteToken = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_TOKEN");

        private string    _backfillTicketId;
        private Coroutine _backfillCoroutine;
        private string    _matchGroupId; // ← НОВОЕ: group_id матча из MMCORE_TICKETS

        private readonly Dictionary<string, string>   _playerTickets    = new();
        private readonly Dictionary<PlayerID, string> _playerIdToTicket = new();

        private float _emptyTime;
        [SerializeField] private float emptyServerLifeTime = 600f;

        #region Unity lifecycle

        private void Start()
        {
            InstanceHandler.NetworkManager.Subscribe<ChangeServerInfo>(HandleServerCustomData);
            ConnectToServer();

#if UNITY_SERVER
            InstanceHandler.NetworkManager.onPlayerJoined += OnPurrPlayerJoined;
            InstanceHandler.NetworkManager.onPlayerLeft   += OnPurrPlayerLeft;
            ParseInitialTickets();
            _backfillCoroutine = StartCoroutine(BackfillRoutine());
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
            InstanceHandler.NetworkManager.onPlayerJoined -= OnPurrPlayerJoined;
            InstanceHandler.NetworkManager.onPlayerLeft   -= OnPurrPlayerLeft;
#endif
        }

        #endregion

        #region Connect / Spawn

        private void ConnectToServer()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();

#if UNITY_SERVER
            transport.address    = "";
            transport.serverPort = 7770;
            transport.StartServer();
#else
            StartCoroutine(ConnectAndSpawnPlayer(
                PlayerPrefs.GetString("Server_IP",   "127.0.0.1"),
                ushort.Parse(PlayerPrefs.GetString("Server_Port", "7770"))));
#endif
        }

        private IEnumerator ConnectAndSpawnPlayer(string ip = "127.0.0.1", ushort port = 7770)
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();
            transport.address    = ip;
            transport.serverPort = port;

            if (LoadingScreenUI.Instance != null)
            {
                LoadingScreenUI.Instance.Show("loading.start_scene", "loading.please_wait");
                transport.StartClient();

                yield return new WaitUntil(() =>
                    InstanceHandler.NetworkManager.clientState == ConnectionState.Connected);

                Debug.Log("[Client] Connected to server!");
                FindAnyObjectByType<PlayerSpawner>().SpawnPlayer();
                LoadingScreenUI.Instance.Hide();
            }
        }

        // Клиент отправляет: NewServerDataString = "{ticketId}|{playerIp}"
        private void HandleServerCustomData(PlayerID player, ChangeServerInfo data, bool asServer)
        {
            if (!asServer) return;
            var parts = data.NewServerDataString?.Split('|');
            if (parts is { Length: 2 })
            {
                _playerIdToTicket[player] = parts[0];
                OnPlayerJoined(parts[0], parts[1]);
            }
        }

        #endregion

        #region PurrNet player events

        private void OnPurrPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if (!asServer) return;
            Debug.Log($"[Server] PurrNet player connected: {player}");
        }

        private void OnPurrPlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer) return;

            if (_playerIdToTicket.TryGetValue(player, out var ticketId))
            {
                OnPlayerLeft(ticketId);
                _playerIdToTicket.Remove(player);
            }

            if (_backfillCoroutine != null)
                StopCoroutine(_backfillCoroutine);
            _backfillCoroutine = StartCoroutine(ImmediateBackfillUpdate());
        }

        private IEnumerator ImmediateBackfillUpdate()
        {
            if (string.IsNullOrEmpty(_backfillTicketId))
                yield return StartCoroutine(PostBackfill());
            else
                yield return StartCoroutine(PutBackfill());

            _backfillCoroutine = StartCoroutine(BackfillRoutine());
        }

        #endregion

        #region Player join / leave

        public void OnPlayerJoined(string ticketId, string playerIp)
        {
            _playerTickets[ticketId] = playerIp;
            Debug.Log($"[Server] Player joined ({ticketId}). Total: {_playerTickets.Count}");
        }

        public void OnPlayerLeft(string ticketId)
        {
            _playerTickets.Remove(ticketId);
            Debug.Log($"[Server] Player left ({ticketId}). Total: {_playerTickets.Count}");
        }

        #endregion

        #region Empty server timer

        private void UpdateEmptyTimer()
        {
            if (InstanceHandler.NetworkManager.playerCount > 0)
            {
                _emptyTime = 0f;
                return;
            }

            _emptyTime += Time.deltaTime;

            if (_emptyTime >= emptyServerLifeTime)
                StartCoroutine(SendDeleteWithRetry());
        }

        private IEnumerator SendDeleteWithRetry()
        {
            if (string.IsNullOrEmpty(_deleteUrl))
            {
                Debug.LogError("[Server] ARBITRIUM_DELETE_URL is not set.");
                yield break;
            }

            if (string.IsNullOrEmpty(_deleteToken))
            {
                Debug.LogError("[Server] ARBITRIUM_DELETE_TOKEN is not set.");
                yield break;
            }

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var req = UnityWebRequest.Delete(_deleteUrl);
                req.timeout = 30;
                req.SetRequestHeader("Authorization", _deleteToken);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[Server] Deployment deleted.");
                    yield break;
                }

                Debug.LogWarning($"[Server] DELETE attempt {attempt} failed: {req.error}");
            }

            Debug.LogError("[Server] DELETE failed after 3 attempts.");
        }

        #endregion

        #region Backfill

        private void ParseInitialTickets()
        {
            var raw = Environment.GetEnvironmentVariable("MMCORE_TICKETS");

            // Диагностика
            Debug.Log($"[Server] FQDN: {Environment.GetEnvironmentVariable("ARBITRIUM_SERVER_FQDN")}");
            Debug.Log($"[Server] IP: {Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP")}");
            Debug.Log($"[Server] PORT gameport: {Environment.GetEnvironmentVariable("ARBITRIUM_PORT_gameport_EXTERNAL")}");
            Debug.Log($"[Server] MMCORE_TICKETS: {raw}");

            if (string.IsNullOrEmpty(raw))
            {
                Debug.LogWarning("[Server] MMCORE_TICKETS is empty.");
                return;
            }

            // ← НОВОЕ: парсим group_id матча
            var groupMatch = Regex.Match(raw, @"""group_id""\s*:\s*""([^""]+)""");
            if (groupMatch.Success)
            {
                _matchGroupId = groupMatch.Groups[1].Value;
                Debug.Log($"[Server] Match group_id: {_matchGroupId}");
            }

            var ids = Regex.Matches(raw, @"""([a-z0-9]{20})""\s*:\s*\{");
            var ips = Regex.Matches(raw, @"""player_ip""\s*:\s*""([^""]+)""");

            for (int i = 0; i < ids.Count && i < ips.Count; i++)
                _playerTickets[ids[i].Groups[1].Value] = ips[i].Groups[1].Value;

            Debug.Log($"[Server] Parsed {_playerTickets.Count} initial player(s) from MMCORE_TICKETS.");
        }

        private IEnumerator BackfillRoutine()
        {
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(PostBackfill());

            while (true)
            {
                yield return new WaitForSeconds(5f);

                bool isFull = _playerTickets.Count >= MaxPlayers;

                if (isFull && !string.IsNullOrEmpty(_backfillTicketId))
                {
                    yield return StartCoroutine(DeleteBackfill());
                    _backfillTicketId = null;
                }
                else if (!isFull && string.IsNullOrEmpty(_backfillTicketId))
                    yield return StartCoroutine(PostBackfill());
                else if (!isFull && !string.IsNullOrEmpty(_backfillTicketId))
                    yield return StartCoroutine(PutBackfill());
            }
        }

        private string BuildBackfillBody()
        {
            var fqdn       = Environment.GetEnvironmentVariable("ARBITRIUM_SERVER_FQDN")               ?? "localhost";
            var publicIp   = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP")                 ?? "127.0.0.1";
            var portGame   = Environment.GetEnvironmentVariable("ARBITRIUM_PORT_gameport_EXTERNAL")    ?? "7770";
            var portStream = Environment.GetEnvironmentVariable("ARBITRIUM_PORT_stream_peer_EXTERNAL") ?? "9000";

            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in _playerTickets)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kv.Key}\":{{\"id\":\"{kv.Key}\",\"player_ip\":\"{kv.Value}\",\"attributes\":{{\"backfill_group_size\":[\"value 1\"]}}}}");
                first = false;
            }
            sb.Append("}");

            // ← НОВОЕ: включаем group_id если известен
            var groupIdPart = !string.IsNullOrEmpty(_matchGroupId)
                ? $"\"group_id\":\"{_matchGroupId}\","
                : "";

            var body = "{"
                + "\"profile\":\"backfill-example\","
                + groupIdPart
                + "\"attributes\":{"
                +     "\"backfill_group_size\":[\"value 1\",\"value 2\",\"value 3\"],"
                +     "\"assignment\":{"
                +         $"\"fqdn\":\"{fqdn}\","
                +         $"\"public_ip\":\"{publicIp}\","
                +         "\"ports\":{"
                +             $"\"gameport\":{{\"internal\":7770,\"external\":{portGame},\"link\":\"{fqdn}:{portGame}\",\"protocol\":\"UDP\"}},"
                +             $"\"stream_peer\":{{\"internal\":9000,\"external\":{portStream},\"link\":\"{fqdn}:{portStream}\",\"protocol\":\"TCP\"}}"
                +         "}}"
                +     "}"
                + "},"
                + $"\"tickets\":{sb}"
                + "}";

            Debug.Log($"[Server] Backfill body: {body}");
            return body;
        }

        private IEnumerator PostBackfill()
        {
            var bodyRaw = Encoding.UTF8.GetBytes(BuildBackfillBody());
            using var req = new UnityWebRequest($"{MatchmakerUrl}/backfills", UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", MatchmakerToken);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                _backfillTicketId = ParseField(req.downloadHandler.text, "id");
                Debug.Log($"[Server] Backfill created: {_backfillTicketId}");
            }
            else
                Debug.LogWarning($"[Server] Backfill POST failed: {req.responseCode} {req.downloadHandler.text}");
        }

        private IEnumerator PutBackfill()
        {
            var bodyRaw = Encoding.UTF8.GetBytes(BuildBackfillBody());
            using var req = new UnityWebRequest($"{MatchmakerUrl}/backfills/{_backfillTicketId}", "PUT");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", MatchmakerToken);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[Server] Backfill PUT failed: {req.responseCode} {req.downloadHandler.text}");
        }

        private IEnumerator DeleteBackfill()
        {
            using var req = UnityWebRequest.Delete($"{MatchmakerUrl}/backfills/{_backfillTicketId}");
            req.SetRequestHeader("Authorization", MatchmakerToken);
            yield return req.SendWebRequest();
            Debug.Log("[Server] Backfill deleted.");
        }

        private static string ParseField(string json, string field)
        {
            var m = Regex.Match(json, @"""" + field + @"""\s*:\s*""([^""]+)""");
            return m.Success ? m.Groups[1].Value : null;
        }

        #endregion
    }

    public struct ChangeServerInfo : IPackedAuto
    {
        public string NewServerDataString;
    }
}