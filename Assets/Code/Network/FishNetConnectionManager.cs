using System;
using Code.Network.HostMigration;
using Code.Network.Lobby;
using FishNet;
using FishNet.Object;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;

namespace Code.Network
{
    public class FishNetConnectionManager : MonoBehaviour
    {
        [SerializeField] private LobbyController lobbyController;

        private HostMigrator HostMigrator => HostMigrator.Instance;

        private void OnValidate()
        {
            lobbyController ??= FindAnyObjectByType<LobbyController>();
        }

        private void OnEnable()
        {
            Debug.Log("[FishNetConnectionManager] OnEnable");
            lobbyController.OnHostReady += StartHostConnection;
            lobbyController.OnClientReady += StartClientConnection;
            lobbyController.OnHostChanged += OnHostChanged;
            lobbyController.OnCurrentHostDisconnected += OnCurrentHostDisconnected;
        }

        private void OnDisable()
        {
            lobbyController.OnHostReady -= StartHostConnection;
            lobbyController.OnClientReady -= StartClientConnection;
            lobbyController.OnHostChanged -= OnHostChanged;
            lobbyController.OnCurrentHostDisconnected -= OnCurrentHostDisconnected;
        }

        public static void StartHostConnection()
        {
            var networkObjectsInScene =
                FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            foreach (var networkObject in networkObjectsInScene)
                networkObject.ResetState(true);
            
            Debug.Log("[LobbyPopup] Starting HostConnection");
            ClearOldConnections();
            var networkManager = InstanceFinder.NetworkManager;
            var localUserId = LobbyVariables.Instance.ProductUserId;
            var fishyEOS = networkManager.GetComponent<FishyEOS>();
            fishyEOS.RemoteProductUserId = localUserId.ToString();
            fishyEOS.AuthConnectData.loginCredentialType = LobbyVariables.Instance.AuthData.loginCredentialType;
            fishyEOS.AuthConnectData.externalCredentialType = LobbyVariables.Instance.AuthData.externalCredentialType;
            fishyEOS.AuthConnectData.id = LobbyVariables.Instance.AuthData.id;
            fishyEOS.AuthConnectData.token = LobbyVariables.Instance.AuthData.token;
            fishyEOS.AuthConnectData.displayName =
                LobbyVariables.Instance.AuthData.loginCredentialType ==
                Epic.OnlineServices.Auth.LoginCredentialType.Developer
                    ? ""
                    : LobbyVariables.Instance.AuthData.displayName;
            fishyEOS.gameObject.SetActive(true);

            networkManager.ServerManager.StartConnection();
            networkManager.ClientManager.StartConnection();

            Debug.Log("[FishNetConnectionManager] Host started");

            // UI/game activation можно оставить на стороне LobbyController
        }

        public static void StartClientConnection()
        {
            var currentLobby = LobbyVariables.Instance.currentLobby;
            if (currentLobby == null || !currentLobby.Attributes.TryGetValue("HOST_ID", out var hostId))
            {
                Debug.LogWarning("[FishNetConnectionManager] HOST_ID not found in lobby attributes.");
                return;
            }

            ClearOldConnections();

            var networkManager = InstanceFinder.NetworkManager;
            var fishyEOS = networkManager.GetComponent<FishyEOS>();
            fishyEOS.RemoteProductUserId = hostId;
            fishyEOS.AuthConnectData.loginCredentialType = LobbyVariables.Instance.AuthData.loginCredentialType;
            fishyEOS.AuthConnectData.externalCredentialType = LobbyVariables.Instance.AuthData.externalCredentialType;
            fishyEOS.AuthConnectData.id = LobbyVariables.Instance.AuthData.id;
            fishyEOS.AuthConnectData.token = LobbyVariables.Instance.AuthData.token;
            fishyEOS.AuthConnectData.displayName =
                LobbyVariables.Instance.AuthData.loginCredentialType ==
                Epic.OnlineServices.Auth.LoginCredentialType.Developer
                    ? ""
                    : LobbyVariables.Instance.AuthData.displayName;
            fishyEOS.gameObject.SetActive(true);

            networkManager.ClientManager.StartConnection();

            Debug.Log("[FishNetConnectionManager] Client started");
        }

        private static void ClearOldConnections()
        {
            var clientManager = InstanceFinder.ClientManager;
            if (clientManager.Started)
                clientManager.StopConnection();

            var serverManager = InstanceFinder.ServerManager;
            if (serverManager.Started)
                serverManager.StopConnection(false);
        }

        private void OnHostChanged(string nextHostId)
        {
            if (nextHostId == LobbyVariables.Instance.productUserId)
                return;

            Debug.Log($"[FishNetConnectionManager] Connect to new host. ID: {nextHostId}");

            HostMigrator.MarkMigrating();
            StartClientConnection();
        }

        private void OnCurrentHostDisconnected(string newHostId)
        {
            if (newHostId != LobbyVariables.Instance.productUserId)
                return;

            Debug.Log($"[FishNetConnectionManager] I am a new host! {newHostId}");

            HostMigrator.MarkMigrating();
            lobbyController.UpdateHost(newHostId);
            StartHostConnection();
        }
    }
}