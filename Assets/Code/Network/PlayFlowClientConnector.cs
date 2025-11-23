using FishNet;
using PlayFlow;
using UnityEngine;

namespace Code.Network
{
    public class PlayFlowClientConnector : MonoBehaviour
    {
#if !UNITY_SERVER
        public int maxPlayersPerLobby = 100;

        void Start()
        {
            string playerId = SystemInfo.deviceUniqueIdentifier;
            PlayFlowLobbyManagerV2.Instance.Initialize(playerId, OnInitialized);
        }
        
        void OnInitialized()
        {

            Debug.Log("PlayFlow SDK готов. Получаем список лобби...");
            TryJoinOrCreateLobby();
        
            PlayFlowLobbyManagerV2.Instance.Events.OnMatchRunning.AddListener(OnServerReady);
            PlayFlowLobbyManagerV2.Instance.Events.OnError.AddListener(OnError);
            PlayFlowLobbyManagerV2.Instance.Events.OnDisconnected.AddListener(OnDisconnected);
            PlayFlowLobbyManagerV2.Instance.Events.OnPlayerLeft.AddListener(OnPlayerLeft);
        }

        private void OnPlayerLeft(PlayerAction action)
        {
            Debug.Log($"[PlayFlowClientConnector] Игрок {action.PlayerId} отключен от сервера.");
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
                onError: error => Debug.LogError("Ошибка создания лобби: " + error)
            );
        }

        void OnServerReady(ConnectionInfo connectionInfo)
        {
            Debug.Log($"Сервер готов! Подключаемся к {connectionInfo.Ip}:{connectionInfo.Port}");
            InstanceFinder.NetworkManager.ClientManager.StartConnection(connectionInfo.Ip, (ushort) connectionInfo.Port);
        }
#endif
    }
}
