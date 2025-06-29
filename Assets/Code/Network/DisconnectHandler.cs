using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Network.HostMigration;
using Code.Network.HostMigration.Utility;
using EOSLobby;
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
        private ServerManager _serverManager;
        private ClientManager _clientManager;
        private HostMigrator _hostMigrator;
        
        private bool _isUserInitiatedDisconnect = false;
        
        private void Awake()
        {
            InstanceFinder.RegisterInstance(this);
            
            _serverManager = InstanceFinder.ServerManager;
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
        }
        
        private IEnumerator MigrateAsHost()
        {
            var productId = LobbyVariables.Instance.ProductUserId.ToString();
            var lobbyId = LobbyVariables.Instance.currentLobby.lobbyId;

            AutoLobbyConnector.StartHostConnection();
            
            yield return LobbyUpdateLobby.Run(out var updateLobbyHostId, lobbyId, "HOST_ID", productId);
            if (updateLobbyHostId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning(
                    $"[HostMigrator] Failed to set lobby member host id: {updateLobbyHostId.CallbackInfo?.ResultCode}");
        }

        private bool HasCurrentClientIsNewHost()
        {
            var currentLobby = LobbyVariables.Instance.currentLobby;
            var lobbyMembers = currentLobby.lobbyMembers;
            
            var next = currentLobby.attributeValues[Array.IndexOf(currentLobby.attributeKeys, "NEXT_HOST_ID")];
            var ownId = LobbyVariables.Instance.productUserId;

            if (lobbyMembers.Count == 0)
                return false;
            
            return next == ownId;
        }
    }
}