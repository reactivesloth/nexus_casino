using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using PlayFlow.SDK.Servers;
using PurrNet;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Code.Network.PlayFlow
{
    public class PlayFlowLobby : MonoBehaviour
    {
        public const string PrefsServerIDName = "PlayFlow_ID";
        public const string PrefsServerIPName = "PlayFlow_IP";
        public const string PrefsServerPortName = "PlayFlow_Port";

        public const string GameSceneName = "Main";
        public const string MenuSceneName = "Init";

        [SerializeField] private string playflowApiKey = "YOUR_API_KEY_HERE";
        [SerializeField] private float timeout = 60f;

        public static PlayflowServerApiClient ApiClient;

        private float _leftTime = 0f;

        public static InstanceData CurrentServerData { get; private set; }

        void Start()
        {
            ApiClient = new PlayflowServerApiClient(playflowApiKey);

            SelectMatch();
        }

        private void Update()
        {
            _leftTime += Time.deltaTime;

            if (_leftTime >= timeout)
                OnMatchMakingError();
        }

        private async void SelectMatch()
        {
            LoadingScreenUI.Instance.Show("loading.find_server", "loading");

            var savedServerId = PlayerPrefs.GetString(PrefsServerIDName, null);

            // Если id нет ищем сервер
            if (string.IsNullOrEmpty(savedServerId))
            {
                FindServer();
                return;
            }

            var instanceData = await GetInstanceData(savedServerId);

            // Если сервера с сохр id нет - ищем сервер
            if (instanceData == null || instanceData.status == "stopped")
            {
                FindServer();
                return;
            }

            // если есть ждём запуска при необходимости и подключаемся
            WaitWhenServerIsReadyAndConnect(savedServerId);
        }

        private async Task<InstanceData> GetInstanceData(string serverId)
        {
            try
            {
                var serverInfo = await ApiClient.GetServerDetailsAsync(serverId);
                return serverInfo;
            }
            catch (PlayFlowApiException playFlowException)
            {
                if (playFlowException.StatusCode == 404)
                    return null;
                OnMatchMakingError();
            }

            return null;
        }

        public async void FindServer()
        {
            try
            {
                ServerList response = await ApiClient.ListServersAsync(includeLaunching: true);
                Debug.Log($"Found {response.total_servers} total servers.");

                // Server Filter
                var availableServers = response.servers
                    .Where(s =>
                        s.version_tag == Application.version
                        && s.status != "stopped"
                        && (!s.custom_data.TryGetValue("private", out var isPrivate) || !(bool)isPrivate)
                        && GetFreeSlotsInServerCount(s) > 0)
                    .OrderBy(GetFreeSlotsInServerCount)
                    .ToList();


                if (availableServers.Count == 0)
                {
                    StartNewServer();
                    return;
                }

                var server = availableServers[0]; // Выбор сервера

                WaitWhenServerIsReadyAndConnect(server.instance_id);
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to list servers: {e.Message}");
            }
        }

        private async void StartNewServer()
        {
            var serverRequest = new ServerCreateRequest
            {
                name = $"Server {Random.Range(0, 10_000)}",
                region = "eu-west",
                compute_size = "large",
                version_tag = Application.version,
                custom_data = new Dictionary<string, object> { { "max_players", 64 } },
            };

            try
            {
                var response = await ApiClient.StartServerAsync(serverRequest);
                Debug.Log($"Server is starting! Instance ID: {response.instance_id}");
                WaitWhenServerIsReadyAndConnect(response.instance_id);
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to start server: {e.Message}");
            }
        }

        private async void WaitWhenServerIsReadyAndConnect(string serverId)
        {
            LoadingScreenUI.Instance.Show("loading.wait_server", "loading");

            while (SceneManager.GetActiveScene().name == "Matchmaker")
            {
                var data = await GetInstanceData(serverId);

                if (data == null)
                {
                    OnMatchMakingError();
                    break;
                }

                if (data.status == "running")
                {
                    CurrentServerData = data;

                    PlayerPrefs.SetString(PrefsServerIDName, data.instance_id);
                    PlayerPrefs.SetString(PrefsServerIPName, data.network_ports[0].host);
                    PlayerPrefs.SetString(PrefsServerPortName, data.network_ports[0].external_port.ToString());

                    MatchReady();
                    break;
                }

                await Task.Delay(1_000);
                Debug.Log("Waiting For Server Ready...");
            }
        }

        private async void MatchReady()
        {
            await Task.Delay(5_000);
            SceneManager.LoadScene(GameSceneName);
        }

        private void OnMatchMakingError()
        {
            SceneManager.LoadScene(MenuSceneName);
        }

        private static int GetFreeSlotsInServerCount(InstanceData instanceData)
        {
            var customData = instanceData.custom_data;
            var allSlots = (int)customData["max_players"];

            if (!customData.TryGetValue("players", out var players)
                || players is not JArray playersArray)
                return allSlots;

            var isAdmin = TryGetArray(customData, "admins", out var adminsArray)
                          && adminsArray.Count > 0;
            var isHost = TryGetArray(customData, "hosts", out var hostsArray)
                         && hostsArray.Count > 0;
            var isModerator = TryGetArray(customData, "moderators", out var moderatorsArray)
                              && moderatorsArray.Count > 0;

            return allSlots - playersArray.Count
                            - (!isAdmin ? 1 : 0)
                            - (!isHost ? 1 : 0)
                            - (!isModerator ? 1 : 0);
        }

        private static bool TryGetArray(
            Dictionary<string, object> data,
            string key,
            out JArray array)
        {
            array = null;

            if (!data.TryGetValue(key, out var value) || value is null)
                return false;

            if (value is JArray jArr)
            {
                array = jArr;
                return true;
            }

            if (value is JToken token && token is JArray tokenArr)
            {
                array = tokenArr;
                return true;
            }

            return false;
        }
    }
}