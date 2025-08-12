using System;
using Code.Network.HostMigration;
using Code.Network.Lobby;
using FishNet;
using FishNet.Object;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;

namespace Code.Network
{
    /// <summary>
    /// Управляет подключением хоста/клиента, безопасно подписывается на события LobbyController,
    /// корректно сбрасывает состояние сети и избегает лишних аллокаций/повторов.
    /// </summary>
    public sealed class FishNetConnectionManager : MonoBehaviour
    {
        [SerializeField] private LobbyController lobbyController;

        private HostMigrator HostMigrator => HostMigrator.Instance;
        private bool _subscribed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (lobbyController == null)
                lobbyController = FindAnyObjectByType<LobbyController>();
        }
#endif

        private void OnEnable()
        {
            if (lobbyController == null)
            {
                Debug.LogWarning("[FishNetConnectionManager] LobbyController reference is missing.");
                return;
            }

            if (_subscribed) return;

            lobbyController.OnHostReady += StartHostConnection;
            lobbyController.OnClientReady += StartClientConnection;
            lobbyController.OnHostChanged += OnHostChanged;
            lobbyController.OnCurrentHostDisconnected += OnCurrentHostDisconnected;

            _subscribed = true;
            Debug.Log("[FishNetConnectionManager] Subscribed to lobby events.");
        }

        private void OnDisable()
        {
            if (!_subscribed || lobbyController == null) return;

            lobbyController.OnHostReady -= StartHostConnection;
            lobbyController.OnClientReady -= StartClientConnection;
            lobbyController.OnHostChanged -= OnHostChanged;
            lobbyController.OnCurrentHostDisconnected -= OnCurrentHostDisconnected;
            _subscribed = false;

            Debug.Log("[FishNetConnectionManager] Unsubscribed from lobby events.");
        }

        public void StartHostConnection()
        {
            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Debug.LogError("[FishNetConnectionManager] NetworkManager not found.");
                return;
            }

            var lobbyVars = LobbyVariables.Instance;
            if (lobbyVars == null)
            {
                Debug.LogError("[FishNetConnectionManager] LobbyVariables.Instance is null.");
                return;
            }

            Debug.Log("[FishNetConnectionManager] Starting HostConnection");

            ResetAllNetworkObjectsInScene(asServer: true);
            ClearOldConnections();

            var fishyEOS = networkManager.GetComponent<FishyEOS>();
            if (fishyEOS == null)
            {
                Debug.LogError("[FishNetConnectionManager] FishyEOS component not found on NetworkManager.");
                return;
            }

            fishyEOS.RemoteProductUserId = lobbyVars.ProductUserId.ToString();
            fishyEOS.AuthConnectData.loginCredentialType = lobbyVars.AuthData.loginCredentialType;
            fishyEOS.AuthConnectData.externalCredentialType = lobbyVars.AuthData.externalCredentialType;
            fishyEOS.AuthConnectData.id = lobbyVars.AuthData.id;
            fishyEOS.AuthConnectData.token = lobbyVars.AuthData.token;
            fishyEOS.AuthConnectData.displayName =
                lobbyVars.AuthData.loginCredentialType == Epic.OnlineServices.Auth.LoginCredentialType.Developer
                    ? string.Empty
                    : lobbyVars.AuthData.displayName;

            if (!fishyEOS.gameObject.activeSelf)
                fishyEOS.gameObject.SetActive(true);

            // Защита от двойных запусков
            if (!networkManager.ServerManager.Started)
                networkManager.ServerManager.StartConnection();
            if (!networkManager.ClientManager.Started)
                networkManager.ClientManager.StartConnection();

            Debug.Log("[FishNetConnectionManager] Host started.");
        }

        public void StartClientConnection()
        {
            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Debug.LogError("[FishNetConnectionManager] NetworkManager not found.");
                return;
            }

            var lobbyVars = LobbyVariables.Instance;
            if (lobbyVars == null || lobbyVars.currentLobby == null)
            {
                Debug.LogWarning("[FishNetConnectionManager] LobbyVariables or currentLobby is null.");
                return;
            }

            if (!lobbyVars.currentLobby.Attributes.TryGetValue("HOST_ID", out var hostId) || string.IsNullOrEmpty(hostId))
            {
                Debug.LogWarning("[FishNetConnectionManager] HOST_ID not found in lobby attributes.");
                return;
            }

            ResetAllNetworkObjectsInScene(asServer: true);
            ClearOldConnections();

            var fishyEOS = networkManager.GetComponent<FishyEOS>();
            if (fishyEOS == null)
            {
                Debug.LogError("[FishNetConnectionManager] FishyEOS component not found on NetworkManager.");
                return;
            }

            fishyEOS.RemoteProductUserId = hostId;
            fishyEOS.AuthConnectData.loginCredentialType = lobbyVars.AuthData.loginCredentialType;
            fishyEOS.AuthConnectData.externalCredentialType = lobbyVars.AuthData.externalCredentialType;
            fishyEOS.AuthConnectData.id = lobbyVars.AuthData.id;
            fishyEOS.AuthConnectData.token = lobbyVars.AuthData.token;
            fishyEOS.AuthConnectData.displayName =
                lobbyVars.AuthData.loginCredentialType == Epic.OnlineServices.Auth.LoginCredentialType.Developer
                    ? string.Empty
                    : lobbyVars.AuthData.displayName;

            if (!fishyEOS.gameObject.activeSelf)
                fishyEOS.gameObject.SetActive(true);

            if (!networkManager.ClientManager.Started)
                networkManager.ClientManager.StartConnection();

            Debug.Log("[FishNetConnectionManager] Client started.");
        }

        private void ClearOldConnections()
        {
            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null) return;

            var clientManager = networkManager.ClientManager;
            if (clientManager != null && clientManager.Started)
                clientManager.StopConnection();

            var serverManager = networkManager.ServerManager;
            if (serverManager != null && serverManager.Started)
                serverManager.StopConnection(false);
        }

        private void ResetAllNetworkObjectsInScene(bool asServer)
        {
            // FindObjectsByType без LINQ; проход единожды
            var networkObjectsInScene = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < networkObjectsInScene.Length; i++)
            {
                var no = networkObjectsInScene[i];
                if (no != null)
                    no.ResetState(asServer);
            }
        }

        private void OnHostChanged(string nextHostId)
        {
            var lobbyVars = LobbyVariables.Instance;
            if (lobbyVars == null)
            {
                Debug.LogWarning("[FishNetConnectionManager] LobbyVariables.Instance is null in OnHostChanged.");
                return;
            }

            if (nextHostId == lobbyVars.productUserId)
                return;

            Debug.Log($"[FishNetConnectionManager] Connect to new host. ID: {nextHostId}");

            HostMigrator?.MarkMigrating();
            StartClientConnection();
        }

        private void OnCurrentHostDisconnected(string newHostId)
        {
            var lobbyVars = LobbyVariables.Instance;
            if (lobbyVars == null)
            {
                Debug.LogWarning("[FishNetConnectionManager] LobbyVariables.Instance is null in OnCurrentHostDisconnected.");
                return;
            }

            if (newHostId != lobbyVars.productUserId)
                return;

            Debug.Log($"[FishNetConnectionManager] I am a new host! {newHostId}");

            HostMigrator?.MarkMigrating();
            lobbyController.UpdateHost(newHostId);
            StartHostConnection();
        }
    }
}
