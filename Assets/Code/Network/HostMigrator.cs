using System;
using System.Collections;
using System.Linq;
using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;

namespace Code.Network
{
    public class HostMigrator : MonoBehaviour
    {
        [SerializeField] private float reconnectDelay = 5f;
        [SerializeField] private int maxReconnectAttempts = 1;

        private int _currentAttempts;
        private PlayerCharacterState _playerCharacterState;

        private void Awake()
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionChanged;
        }

        private void OnDestroy()
        {
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionChanged;
        }

        private void OnClientConnectionChanged(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && _playerCharacterState != null)
                SessionStateSender.Instance.SendSessionStateToHost(_playerCharacterState);
            
            if(args.ConnectionState is LocalConnectionState.Stopping or LocalConnectionState.Stopped)
            {
                SavePlayerData();
            }
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                Debug.Log("Соединение потеряно. Начинаю переподключение...");
                _currentAttempts = 0;
                StartCoroutine(TryReconnect());
            }
        }

        private IEnumerator TryReconnect()
        {
            /*while (_currentAttempts < maxReconnectAttempts)
            {
                _currentAttempts++;
                Debug.Log($"Попытка переподключения {_currentAttempts}/{maxReconnectAttempts}");

                InstanceFinder.ClientManager.StartConnection();

                // Ждём reconnectDelay секунд перед следующей попыткой
                yield return new WaitForSeconds(reconnectDelay);

                if (InstanceFinder.ClientManager.Connection.IsActive)
                {
                    Debug.Log("Переподключение успешно.");
                    yield break;
                }
            }

            Debug.LogWarning("Не удалось переподключиться.");*/
            yield return null;
            OnReconnectFailed();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnReconnectFailed()
        {
            var isNewHost = true; //TODO: select new host logic

            if (isNewHost)
                StartCoroutine(UpdateHost());
            else
                ConnectToNewHost();
        }

        private IEnumerator UpdateHost()
        {
            var productId = LobbyVariables.Instance.ProductUserId.ToString();
            var lobbyId = LobbyVariables.Instance.currentLobby.lobbyId;

            yield return LobbyUpdateLobby.Run(out var updateLobbyHostId, lobbyId, "HOST_ID", productId);
            if (updateLobbyHostId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning(
                    $"[HostMigrator] Failed to set lobby member host id: {updateLobbyHostId.CallbackInfo?.ResultCode}");

            AutoLobbyConnector.StartHostConnection();
        }

        private void ConnectToNewHost()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.AddPersistentListener(OnLobbyUpdateReceived);
        }

        private void OnLobbyUpdateReceived(LobbyUpdateReceivedCallbackInfo e)
        {
            var localUserId = LobbyVariables.Instance.ProductUserId;
            var currentLobby = LobbyVariables.Instance.currentLobby;
            var result = Lobby.GetLobbyDetails(out var lobbyDetails, e.LobbyId, localUserId);

            if (result != Result.Success)
            {
                Debug.LogWarning($"[LobbyCode] Failed to get lobby details. {result}");
                return;
            }

            var oldHostId = currentLobby.attributeValues[Array.IndexOf(currentLobby.attributeKeys, "HOST_ID")];

            var attributes = Lobby.GetAttributes(lobbyDetails);
            lobbyDetails.Release();
            currentLobby.attributeKeys = new string[attributes.Count];
            currentLobby.attributeValues = new string[attributes.Count];

            for (var i = 0; i < attributes.Count; i++)
            {
                currentLobby.attributeKeys[i] = attributes[i]?.Data?.Key;
                currentLobby.attributeValues[i] = attributes[i]?.Data?.Value.AsUtf8;
            }

            var newHostId = currentLobby.attributeValues[Array.IndexOf(currentLobby.attributeKeys, "HOST_ID")];

            if (newHostId != oldHostId)
                AutoLobbyConnector.StartClientConnection();
        }

        private void SavePlayerData()
        {
            var playerCharacterNetworkObject =
                InstanceFinder.ClientManager.Connection.Objects.FirstOrDefault(o => o.CompareTag("Player"));

            if (playerCharacterNetworkObject == null)
            {
                Debug.LogError($"[SessionStateSender] PlayerCharacterState not found!");
                return;
            }

            _playerCharacterState = new PlayerCharacterState
            {
                Position = playerCharacterNetworkObject.transform.position,
                Rotation = playerCharacterNetworkObject.transform.rotation,
            };
        }
    }
}