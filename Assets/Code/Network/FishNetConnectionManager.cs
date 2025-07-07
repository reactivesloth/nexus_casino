using System;
using Code.Network.Lobby;
using FishNet;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;

namespace Code.Network
{
    public class FishNetConnectionManager : MonoBehaviour
    {
        [SerializeField] private LobbyController lobbyController;

        private void OnValidate()
        {
            lobbyController ??= FindAnyObjectByType<LobbyController>();
        }

        private void OnEnable()
        {
            if (lobbyController)
            {
                lobbyController.OnHostReady += StartHostConnection;
                lobbyController.OnClientReady += StartClientConnection;
                lobbyController.OnHostChanged += OnHostChanged;
            }
        }

        private void OnDisable()
        {
            if (lobbyController)
            {
                lobbyController.OnHostReady -= StartHostConnection;
                lobbyController.OnClientReady -= StartClientConnection;
                lobbyController.OnHostChanged -= OnHostChanged;
            }
        }

        public static void StartHostConnection()
        {
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

            if (networkManager.IsClientStarted)
                networkManager.ClientManager.StopConnection();
            networkManager.ClientManager.StartConnection();
        }
        
        private void OnHostChanged(string newHostId)
        {
            if (newHostId == LobbyVariables.Instance.productUserId)
                OnLocalHost();
            else
                OnRemoteHost(newHostId);
        }

        private void OnLocalHost()
        {
            Debug.Log("I am a new host");
            //StartHostConnection();
        }
        
        private void OnRemoteHost(string newHostId)
        {
            Debug.Log($"Connect to new host. ID: {newHostId}");
            StartClientConnection();
        }
    }
}