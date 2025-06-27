using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Network.HostMigration.Data;
using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network.HostMigration
{
    public class HostMigrator : MonoBehaviour
    {
        [SerializeField] private float reconnectDelay = 5f;
        [SerializeField] private int maxReconnectAttempts = 1;

        private int _currentAttempts;

        private MigratePlayerData _migrateData;

        private void Awake()
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionChanged;
            ClientObjectsSaver.OnOwnObjectsUpdated += UpdateLastPlayerSessionState;
        }

        private void OnDestroy()
        {
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionChanged;
            ClientObjectsSaver.OnOwnObjectsUpdated -= UpdateLastPlayerSessionState;
        }

        private void OnClientConnectionChanged(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && _migrateData != null)
                SessionStateSender.Instance.SendSessionStateToHost(_migrateData);

            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                Debug.Log("Соединение потеряно. Начинаю переподключение...");
                _currentAttempts = 0;
                StartCoroutine(TryReconnect());
            }
        }

        private IEnumerator TryReconnect()
        {
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
            HostSessionRestorer.SetSpawnerEnable(false);
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

        private void UpdateLastPlayerSessionState(List<NetworkObject> ownObjects)
        {
            _migrateData = new MigratePlayerData();

            foreach (var currentGameObject in ownObjects)
            {
                var migratableComponents =
                    currentGameObject.GetComponents<MonoBehaviour>().OfType<IMigratableBase>().ToList();

                if (migratableComponents.Count == 0)
                    continue;

                var migrateObject = new NetworkObjectData
                {
                    objectName = currentGameObject.name,
                    networkObjectId = currentGameObject.ObjectId,
                    prefabId = currentGameObject.PrefabId,
                    ownerId = currentGameObject.OwnerId,
                    isSceneObject = currentGameObject.IsSceneObject
                };

                _migrateData.objects.Add(migrateObject);

                foreach (var migratableComponent in migratableComponents)
                {
                    var componentAbstractData = migratableComponent.GetMigrateData();
                    var migratableComponentData = new MigratableComponentData
                    {
                        componentName = migratableComponent.GetType().FullName,
                        json = migratableComponent.GetJson(componentAbstractData)
                    };
                    migrateObject.componentsData.Add(migratableComponentData);
                }
            }
        }
    }
}