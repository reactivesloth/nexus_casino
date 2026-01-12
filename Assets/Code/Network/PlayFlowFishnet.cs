using System.Collections;
using System.Collections.Generic;
using Code.API;
using Code.UI;
using Code.UI.Popup;
using Code.Utility;
using PlayFlow;
using PurrNet;
using Ricimi;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Code.Network
{
    public class PlayFlowFishnet : MonoBehaviour
    {
        public int maxPlayersPerLobby = 100;
        private NexusModularPopupOpener _popupOpener;
        
        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
        }

        void Start()
        {
#if !UNITY_SERVER
            string playerId = ClientDataStorage.UserData.username;
            PlayFlowLobbyManagerV2.Instance.DefaultLobbyConfig = Application.version;
            PlayFlowLobbyManagerV2.Instance.Initialize(playerId, OnInitialized);
            LoadingScreenUI.Instance.Show("loading", "loading.please_wait");
#endif
        }

        void OnInitialized()
        {
            Debug.Log("PlayFlow SDK готов. Получаем список лобби...");
            LoadingScreenUI.Instance.Show("loading.join_lobby.getting", "loading.please_wait");
            TryJoinOrCreateLobby();

            PlayFlowLobbyManagerV2.Instance.Events.OnMatchRunning.AddListener(OnServerReady);
            PlayFlowLobbyManagerV2.Instance.Events.OnLobbyJoined.AddListener(OnLobbyJoined);
            PlayFlowLobbyManagerV2.Instance.Events.OnLobbyUpdated.AddListener(OnLobbyUpdated);
            PlayFlowLobbyManagerV2.Instance.Events.OnPlayerJoined.AddListener(OnPlayerJoined);
            PlayFlowLobbyManagerV2.Instance.Events.OnError.AddListener(OnError);
            PlayFlowLobbyManagerV2.Instance.Events.OnDisconnected.AddListener(OnDisconnected);
            PlayFlowLobbyManagerV2.Instance.Events.OnPlayerLeft.AddListener(OnPlayerLeft);
        }

        void OnLobbyJoined(Lobby lobby)
        {
            Debug.Log($"Successfully joined lobby: {lobby.name}");
        }

        void OnPlayerJoined(PlayerAction action)
        {
            Debug.Log($"Player {action.PlayerId} joined the lobby!");
        }

        private void OnPlayerLeft(PlayerAction action)
        {
            Debug.Log($"[PlayFlowClientConnector] Игрок {action.PlayerId} отключен от сервера.");
        }

        void OnLobbyUpdated(Lobby lobby)
        {
            Debug.Log("Lobby data has been updated.");
        }

        private void OnDisconnected()
        {
            Debug.Log($"[PlayFlowClientConnector] Соединение разорвано");
        }

        private void OnError(string error)
        {
            Debug.Log($"[PlayFlowClientConnector] Ошибка: {error}.");
        }

        void TryJoinOrCreateLobby()
        {
            if (PlayerPrefs.HasKey("Playflow_NewLobbyInstantID"))
            {
                var lobbyId = PlayerPrefs.GetString("Playflow_NewLobbyInstantID");
                PlayerPrefs.DeleteKey("Playflow_NewLobbyInstantID");

                if (PlayerPrefs.GetString("Playflow_NewLobby_IsNewRoom") == "true")
                {
                    bool isPrivate = PlayerPrefs.GetString("Playflow_NewLobby_IsPrivate") == "true";
                    CreateLobby(lobbyId, isPrivate);
                }
                else
                {
                    JoinLobby(lobbyId);
                }

                PlayerPrefs.DeleteKey("Playflow_NewLobby_IsNewRoom");
                PlayerPrefs.DeleteKey("Playflow_NewLobby_IsPrivate");

                return;
            }

            PlayFlowLobbyManagerV2.Instance.GetAvailableLobbies(
                onSuccess: lobbies =>
                {
                    LoadingScreenUI.Instance.Show("loading.search_lobby", "loading.please_wait");
                    Debug.Log($"Найдено лобби: {lobbies.Count}");
                    // Ищем лобби с местом
                    foreach (var lobby in lobbies)
                    {
                        if (lobby.currentPlayers < lobby.maxPlayers
                            && lobby.currentPlayers > 0
                            && lobby.status == "in_game")
                        {
                            JoinLobby(lobby.id);
                            return;
                        }
                    }

                    // Если свободных комнат нет - создаём новую
                    CreateLobby();
                },
                onError: error =>
                {
                    Debug.LogError("Ошибка получения списка лобби: " + error);

                    if (error.Contains($"'{Application.version}' not found"))
                    {
                        UpdateReadyPopup();
                    }
                    else
                    {
                        // Можно попытаться создать лобби, если список не получен
                        CreateLobby();
                    }
                });
        }

        private void UpdateReadyPopup()
        {
            CursorManager.Instance.SetForceShowCursor(true);
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("errors.update_nexus_title");
            _popupOpener.Subtitle = "";
            _popupOpener.Message = LocalizationHelper.GetLocalizedString("errors.update_nexus");
            
            var okButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("buttons.update"),
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            okButton.OnClickedEvent.AddListener(()=>
            {
                Application.OpenURL("https://nexusmetaclub.com/update#download");
                CursorManager.Instance.SetForceShowCursor(false);
                SceneManager.LoadScene("Init");
            });
            _popupOpener.Buttons.Add(okButton);
            _popupOpener.OpenPopup();
        }

        private void JoinLobby(string lobbyId)
        {
            Debug.Log($"Подключаемся к лобби {lobbyId} с ID {lobbyId}...");
            PlayFlowLobbyManagerV2.Instance.JoinLobby(lobbyId,
                onSuccess: _ =>
                {
                    Debug.Log("Успешно подключились к лобби");
                    InitPlayerDataOnLobby();
                },
                onError: error =>
                {
                    Debug.LogError("Ошибка при подключении к лобби: " + error);
                    
                    // Можно попытаться повторить зайти в лобби
                    if (error.Contains ("not found"))
                        CreateLobby(lobbyId);
                    else
                        JoinLobby(lobbyId);
                });
        }

        private void CreateLobby(string lobbyName = null, bool isPrivate = false)
        {
            LoadingScreenUI.Instance.Show("loading.create_lobby", "loading.please_wait");
            Debug.Log("Создаем новую лобби...");
            PlayFlowLobbyManagerV2.Instance.CreateLobby(
                name: lobbyName ?? "Lobby_" + Random.Range(000000, 999999),
                maxPlayers: maxPlayersPerLobby,
                isPrivate: false, 
                allowLateJoin: true,
                region: "eu-west",
                customSettings: new Dictionary<string, object>(),
                onSuccess: lobby =>
                {
                    Debug.Log($"Лобби создано с ID: {lobby.id}");
                    PlayFlowLobbyManagerV2.Instance.StartMatch(
                        onSuccess: _ => Debug.Log("Match starting! Waiting for server..."),
                        onError: error =>
                        {
                            Debug.LogError(error);
                            
                            PlayFlowLobbyManagerV2.Instance.LeaveLobby();
                            TryJoinOrCreateLobby();
                        });
                    InitPlayerDataOnLobby();
                },
                onError: error =>
                {
                    if (error.Contains("exists"))
                        TryJoinOrCreateLobby();
                    else
                        CreateLobby(lobbyName, isPrivate);
                });
        }

        private void OnServerReady(ConnectionInfo connectionInfo)
        {
            LoadingScreenUI.Instance.Show("loading.start_scene", "loading.please_wait");
            Debug.Log($"Сервер готов! Подключаемся к {connectionInfo.Ip}:{connectionInfo.Port}");
            StartCoroutine(ConnectToServer(connectionInfo.Ip, (ushort)connectionInfo.Port));
        }

        private IEnumerator ConnectToServer(string ip, ushort port)
        {
            yield return new WaitForSeconds(2f);
            InstanceHandler.NetworkManager.currentTransport.Connect(ip, port);
        }

        private void InitPlayerDataOnLobby()
        {
            var playerDataDictionary = new Dictionary<string, object>
            {
                { "name", ClientDataStorage.UserData.username },
                { "role", ClientDataStorage.UserData.role }
            };

            PlayFlowLobbyManagerV2.Instance.UpdatePlayerState(playerDataDictionary);
        }

        void OnDisable()
        {
            var events = PlayFlowLobbyManagerV2.Instance.Events;
            if (events != null)
            {
                PlayFlowLobbyManagerV2.Instance.Events.OnMatchRunning.RemoveListener(OnServerReady);
                PlayFlowLobbyManagerV2.Instance.Events.OnLobbyJoined.RemoveListener(OnLobbyJoined);
                PlayFlowLobbyManagerV2.Instance.Events.OnLobbyUpdated.RemoveListener(OnLobbyUpdated);
                PlayFlowLobbyManagerV2.Instance.Events.OnPlayerJoined.RemoveListener(OnPlayerJoined);
                PlayFlowLobbyManagerV2.Instance.Events.OnError.RemoveListener(OnError);
                PlayFlowLobbyManagerV2.Instance.Events.OnDisconnected.RemoveListener(OnDisconnected);
                PlayFlowLobbyManagerV2.Instance.Events.OnPlayerLeft.RemoveListener(OnPlayerLeft);
            }

            Disconnect();
        }

        public void OnApplicationQuit()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            LeftLobby();
            PlayFlowLobbyManagerV2.Instance.Disconnect();
            Destroy(gameObject);
        }

        private void LeftLobby()
        {
            PlayFlowLobbyManagerV2.Instance.LeaveLobby();
        }

        private void EndMatch()
        {
            PlayFlowLobbyManagerV2.Instance.EndMatch();
        }
    }
}