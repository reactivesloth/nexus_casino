using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Network
{
    /// <summary>
    /// Статический хелпер для Admin Panel — список серверов и создание деплоймента.
    /// Берёт настройки из EdgegapServerBrowser на сцене.
    /// </summary>
    public static class EdgegapAdminAPI
    {
        private static EdgegapServerBrowser _browser;

        private static EdgegapServerBrowser Browser
        {
            get
            {
                if (_browser == null)
                    _browser = UnityEngine.Object.FindAnyObjectByType<EdgegapServerBrowser>(
                        UnityEngine.FindObjectsInactive.Include);
                return _browser;
            }
        }

        private static string _serverBrowserUrl;
        private static string _clientToken;
        private static string _edgegapApiToken;
        private static string _appName;

        public static void Configure(string serverBrowserUrl, string clientToken,
            string edgegapApiToken, string appName)
        {
            _serverBrowserUrl = serverBrowserUrl;
            _clientToken = clientToken;
            _edgegapApiToken = edgegapApiToken;
            _appName = appName;
            Debug.Log($"[EdgegapAdmin] Configured: {serverBrowserUrl}");
        }

        // ── Список серверов ───────────────────────────────────

        public static IEnumerator GetServerList(Action<List<ServerInstanceItem>> callback)
        {
            if (string.IsNullOrEmpty(_serverBrowserUrl))
            {
                Debug.LogError("[EdgegapAdmin] Не настроен URL — вызови Configure() при старте браузера");
                callback(null);
                yield break;
            }

            string url = $"{_serverBrowserUrl}/server-instances?limit=50";

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", _clientToken);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EdgegapAdmin] GetServerList error: {req.error}");
                callback(null);
                yield break;
            }

            var resp = JsonConvert.DeserializeObject<ServerInstanceListResponse>(req.downloadHandler.text);
            callback(resp?.items ?? new List<ServerInstanceItem>());
        }

        public static IEnumerator CreateDeployment(Action<string> callback) // string вместо bool
        {
            string url = "https://api.edgegap.com/v1/deploy";
            string body = JsonConvert.SerializeObject(new
            {
                app_name = _appName,
                version_name = UnityEngine.Application.version,
                filters = new[]
                {
                    new { filter_type = "any", field = "continent", values = new[] { "Europe" } }
                }
            });

            using var req = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Authorization", _edgegapApiToken);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            bool ok = req.responseCode == 200 || req.responseCode == 201;
            if (!ok)
            {
                Debug.LogError($"[EdgegapAdmin] CreateDeployment: {req.responseCode}\n{req.downloadHandler.text}");
                callback(null);
                yield break;
            }

            var response = JsonConvert.DeserializeObject<DeployResponse>(req.downloadHandler.text);
            Debug.Log($"[EdgegapAdmin] Деплоймент создан: {response?.request_id}");
            callback(response?.request_id);
        }
        
        public static IEnumerator DeleteDeployment(string requestId, Action<bool> callback)
        {
            // Шаг 1: Останавливаем контейнер Edgegap
            string stopUrl = $"https://api.edgegap.com/v1/stop/{requestId}";

            using var stopReq = new UnityWebRequest(stopUrl, "DELETE")
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            stopReq.SetRequestHeader("Authorization", _edgegapApiToken);
            yield return stopReq.SendWebRequest();

            if (stopReq.responseCode != 200 && stopReq.responseCode != 204)
                Debug.LogWarning($"[EdgegapAdmin] Stop deployment {requestId}: {stopReq.responseCode}");

            // Шаг 2: Удаляем запись из Server Browser
            string sbUrl = $"{_serverBrowserUrl}/server-instances/{requestId}";

            using var sbReq = new UnityWebRequest(sbUrl, "DELETE")
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            sbReq.SetRequestHeader("Authorization", _clientToken);
            yield return sbReq.SendWebRequest();

            bool ok = sbReq.responseCode == 200 || sbReq.responseCode == 204 || sbReq.responseCode == 404;
            if (!ok) Debug.LogError($"[EdgegapAdmin] Delete SB instance {requestId}: {sbReq.responseCode}\n{sbReq.downloadHandler.text}");

            callback(ok);
        }

        [Serializable]
        public class DeployResponse
        {
            public string request_id;
            public string current_status;
        }

        // ── Модели (те же что в ServerBrowser.cs) ────────────

        [Serializable]
        public class ServerInstanceListResponse
        {
            public List<ServerInstanceItem> items;
        }

        [Serializable]
        public class ServerInstanceItem
        {
            public string request_id;
            public string created_at;
            public int total_available_seats;
            public int total_reserved_seats;
            public int total_joinable_seats;
            public InstanceMetadata metadata;
            public ServerData server;
        }

        [Serializable]
        public class InstanceMetadata
        {
            public string name;
            public int max_players;
            public string policy_name;
        }

        [Serializable]
        public class ServerData
        {
            public string fqdn;
            public string public_ip;
            public Dictionary<string, PortInfo> ports;
            public LocationData location;
        }

        [Serializable]
        public class LocationData
        {
            public string city;
            public string country;
            public string continent;
        }

        [Serializable]
        public class PortInfo
        {
            public int @internal;
            public int external;
            public string protocol;
        }
    }
}