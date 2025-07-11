using System;
using System.Collections;
using Code.Network.HostMigration;
using Code.Network.Lobby;
using Code.Network.Lobby.EOSCoroutines;
using Epic.OnlineServices;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network
{
    public class DisconnectHandler : MonoBehaviour
    {
        private ClientManager _clientManager;
        private HostMigrator _hostMigrator;
        
        private bool _isUserInitiatedDisconnect = false;
        
        private void Awake()
        {
            InstanceFinder.RegisterInstance(this);
            
            _clientManager = InstanceFinder.ClientManager;
            _hostMigrator = InstanceFinder.NetworkManager.GetComponent<HostMigrator>();
        }

        private void OnEnable()
        {
            _clientManager.OnClientConnectionState += ClientManagerOnOnClientConnectionState;
        }

        private void OnDisable()
        {
            _clientManager.OnClientConnectionState -= ClientManagerOnOnClientConnectionState;
        }
        
        /// <summary>
        /// Must be executed when a user initiates disconnect
        /// </summary>
        public void MarkAsUserInitiatedDisconnect() => _isUserInitiatedDisconnect = true;

        private void Migrate()
        {
            _hostMigrator.MarkMigrating();
            if (HasCurrentClientIsNewHost())
                StartCoroutine(MigrateAsHost());
        }
        
        private void ClientManagerOnOnClientConnectionState(ClientConnectionStateArgs obj)
        {
            Debug.Log(obj.ConnectionState.ToString());
            if(obj.ConnectionState == LocalConnectionState.Stopped) 
                if(!_isUserInitiatedDisconnect)
                    Migrate();
            _isUserInitiatedDisconnect = false;
        }
        
        private IEnumerator MigrateAsHost()
        {
            var productId = LobbyVariables.Instance.ProductUserId.ToString();
            var lobbyId = LobbyVariables.Instance.currentLobby.lobbyId;
            
            FishNetConnectionManager.StartHostConnection();
            
            yield return LobbyUpdateLobby.Run(out var updateLobbyHostId, lobbyId, "HOST_ID", productId);
            if (updateLobbyHostId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning(
                    $"[HostMigrator] Failed to set lobby member host id: {updateLobbyHostId.CallbackInfo?.ResultCode}");
        }

        private bool HasCurrentClientIsNewHost()
        {
            var currentLobby = LobbyVariables.Instance.currentLobby;

            if (!currentLobby.Attributes.TryGetValue("NEXT_HOST_ID", out var value))
                return false;
            
            var ownId = LobbyVariables.Instance.productUserId;
            
            return value == ownId;
        }
    }
}