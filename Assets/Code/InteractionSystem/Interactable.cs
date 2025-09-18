using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Code.InteractionSystem
{
    public class Interactable : NetworkBehaviour
    {
        [SerializeField] private float _interactionDistance = 3f;
        [SerializeField] private bool _interactableEnabled = true;
        [SerializeField] private bool _manualRelease = false;

        public GameObject[] outlineGameObjects;
        
        private int _occupiedConnectionId = -1;
        private NetworkConnection OccupierConnection => ServerManager.Clients.TryGetValue(_occupiedConnectionId, out var conn) ? conn : null;

        protected readonly SyncVar<bool> _isOccupied = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers,
        });

        public bool IsBusy;

        public bool IsEnabled => _interactableEnabled;
        public bool ManualRelease => _manualRelease;
        public bool IsOccupied => _isOccupied.Value;
        
        public event Action<bool> InteractCallback_Client;
        public event Action<bool> InteractCallback_Server;

        private void Awake()
        {
            _isOccupied.SetInitialValues(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += ServerManagerOnRemoteConnectionState;
            _isOccupied.Value = false;
            _occupiedConnectionId = -1;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= ServerManagerOnRemoteConnectionState;
        }
        
        public void RequestInteract(bool force = false, NetworkConnection requester = null) => 
            RequestInteract_ServerRpc(requester != null ? requester : ClientManager.Connection, force);

        public void RequestEndInteract() => RequestEndInteract_ServerRpc(ClientManager.Connection);

        #region Server Methods
        
        [Server]
        public void ReleaseInteractable(NetworkConnection requester = null)
        {
            var occupier = requester != null ? requester : OccupierConnection;
            _isOccupied.Value = false;
            _occupiedConnectionId = -1;
            
            SendRequestEndInteractCallbacks(occupier, true);
        }
        
        [Server]
        private void ServerManagerOnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs stateArgs)
        {
            if (stateArgs.ConnectionState == RemoteConnectionState.Stopped && stateArgs.ConnectionId == _occupiedConnectionId)
                ReleaseInteractable();
        }
        
        #endregion

        #region RPC

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteract_ServerRpc(NetworkConnection requester, bool force = false)
        {
            if (!_interactableEnabled || _isOccupied.Value || requester == null)
            {
                SendRequestInteractCallbacks(requester, false, force);
                return;
            }

            _occupiedConnectionId = requester.ClientId;
            _isOccupied.Value = true;

            SendRequestInteractCallbacks(requester, true, force);
        }

        [Server]
        private void SendRequestInteractCallbacks(NetworkConnection requester, bool success, bool force = false)
        {
            OnInteractCallback_Server(requester, success, force);
            if(requester != null)
                RequestInteractCallback_TargetRpc(requester, success, force);
            RequestInteractCallback_ObserversRpc(true, success, force);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestEndInteract_ServerRpc(NetworkConnection requester)
        {
            if (requester.ClientId != _occupiedConnectionId)
            {
                SendRequestEndInteractCallbacks(requester, false);
                return;
            }
            
            ReleaseInteractable(requester);
        }

        [Server]
        private void SendRequestEndInteractCallbacks(NetworkConnection requester, bool success)
        {
            OnInteractEndCallback_Server(requester, success);
            if(requester != null)
                RequestEndInteractCallback_TargetRpc(requester, success);
            RequestInteractCallback_ObserversRpc(false, success);
        }

        [TargetRpc]
        private void RequestInteractCallback_TargetRpc(NetworkConnection target, bool success, bool force = false) =>
            OnInteractCallback_Client(success, force);

        [TargetRpc]
        private void RequestEndInteractCallback_TargetRpc(NetworkConnection target, bool success) =>
            OnInteractEndCallback_Client(success);

        [ObserversRpc(BufferLast = true)]
        private void RequestInteractCallback_ObserversRpc(bool isStartInteract, bool success, bool force = false)
        {
            if(isStartInteract)
                OnInteractCallback_Observers(success, force);
            else
                OnInteractEndCallback_Observers(success);
        }

        #endregion

        #region Server Callbacks

        [Server]
        protected virtual void OnInteractCallback_Server(NetworkConnection requester, bool success, bool force = false)
        {
            InteractCallback_Server?.Invoke(success);
            if(!success)
                return;
            GiveOwnership(requester);
        }

        [Server]
        protected virtual void OnInteractEndCallback_Server(NetworkConnection requester, bool success)
        {
            InteractCallback_Server?.Invoke(success);
            if(!success)
                return;
            RemoveOwnership();
        }

        #endregion

        #region Clients Callbacks

        protected virtual void OnInteractCallback_Client(bool success, bool force = false)
        {
            InteractCallback_Client?.Invoke(success);
        }

        protected virtual void OnInteractEndCallback_Client(bool success)
        {
            InteractCallback_Client?.Invoke(success);
        }

        protected virtual void OnInteractCallback_Observers(bool success, bool force = false)
        {
        }

        protected virtual void OnInteractEndCallback_Observers(bool success)
        {
        }

        #endregion
    }
}