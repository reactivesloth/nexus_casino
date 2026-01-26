using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.UI;
using Code.UI.Popup;
using Code.Utility;
using Newtonsoft.Json.Linq;
using UnityEngine;
using PlayFlow.SDK.Servers;
using PurrNet;
using Ricimi;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
        
        private NexusModularPopupOpener _popupOpener;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
        }
        
        void Start()
        {
            ApiClient = new PlayflowServerApiClient(playflowApiKey);

            SelectMatch();
        }

        private void Update()
        {
            _leftTime += Time.deltaTime;

            if (_leftTime >= timeout)
                OnMatchMakingError("timeout");
        }

        private async void SelectMatch()
        {
            LoadingScreenUI.Instance.Show("loading.find_server", "loading");

            var builds = await ApiClient.GetBuildsAsync(Application.version);
            if (builds.total_builds == 0)
            {
                OnMatchMakingError("version");
                return;
            }

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
                    OnMatchMakingError("data null");
                else
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
                if(e.StatusCode == 404)
                    OnMatchMakingError("not found");
                else
                    OnMatchMakingError();
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
                    OnMatchMakingError("data null");
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

        //TODO: Обработать экран обновлений по удалению старой версии с сервера. Пока закинул на ошибку 404
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
    }
}