using System;
using Code.UI;
using FishNet;
using PlayFlow;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Network
{
    public class PlayFlowFishnet : MonoBehaviour
    {
        public int maxPlayersPerLobby = 100;

        void Start()
        {
#if !UNITY_SERVER

            string playerId = SystemInfo.deviceUniqueIdentifier;
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
            PlayFlowLobbyManagerV2.Instance.GetAvailableLobbies(
                onSuccess: lobbies =>
                {
                    LoadingScreenUI.Instance.Show("loading.search_lobby", "loading.please_wait");
                    Debug.Log($"Найдено лобби: {lobbies.Count}");
                    // Ищем лобби с местом
                    foreach (var lobby in lobbies)
                    {
                        if (lobby.currentPlayers < lobby.maxPlayers)
                        {
                            Debug.Log($"Подключаемся к лобби {lobby.name} с ID {lobby.id}...");
                            PlayFlowLobbyManagerV2.Instance.JoinLobby(lobby.id,
                                onSuccess: lobbyJoined => {
                                    Debug.Log("Успешно подключились к лобби");
                                }, 
                                onError: error => Debug.LogError("Ошибка при подключении к лобби: " + error));
                            return;
                        }
                    }
                    // Если свободных комнат нет - создаём новую
                    CreateLobby();
                },
                onError: error =>
                {
                    Debug.LogError("Ошибка получения списка лобби: " + error);
                    // Можно попытаться создать лобби, если список не получен
                    CreateLobby();
                });
        }

        void CreateLobby()
        {
            LoadingScreenUI.Instance.Show("loading.create_lobby", "loading.please_wait");
            Debug.Log("Создаем новую лобби...");
            PlayFlowLobbyManagerV2.Instance.CreateLobby(
                name: "Lobby_" + Random.Range(000000, 999999),
                maxPlayers: maxPlayersPerLobby,
                isPrivate: false,
                onSuccess: lobby =>
                {
                    Debug.Log($"Лобби создано с ID: {lobby.id}");
                    PlayFlowLobbyManagerV2.Instance.StartMatch(
                        onSuccess: (lobby) => Debug.Log("Match starting! Waiting for server..."),
                        onError: (error) => Debug.LogError(error)
                    );
                },
                onError: error =>
                {
                    LoadingScreenUI.Instance.Show("loading.start_scene", "error");
                    Debug.LogError("Ошибка создания лобби: " + error);
                    TryJoinOrCreateLobby();
                });
        }

        void OnServerReady(ConnectionInfo connectionInfo)
        {
            LoadingScreenUI.Instance.Show("loading.start_scene", "loading.please_wait");
            Debug.Log($"Сервер готов! Подключаемся к {connectionInfo.Ip}:{connectionInfo.Port}");
            InstanceFinder.NetworkManager.ClientManager.StartConnection(connectionInfo.Ip, (ushort) connectionInfo.Port);
            LoadingScreenUI.Instance.Invoke("Hide", 1);
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
            Destroy(gameObject);
        }

        public void Disconnect()
        {
            PlayFlowLobbyManagerV2.Instance.Disconnect();
            Destroy(gameObject);
        }

        public void LeftLobby()
        {
            PlayFlowLobbyManagerV2.Instance.LeaveLobby();
            Destroy(gameObject);
        }

        public void EndMatch()
        {
            PlayFlowLobbyManagerV2.Instance.EndMatch();
        }
    }
}
