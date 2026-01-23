using System;
using PurrNet;
using UnityEngine;

namespace Code.Network.InteractionSystem
{
    public class Interactable : NetworkBehaviour
    {
        [SerializeField] public string interactableKey;
        [SerializeField] private float _interactionDistance = 3f;
        [SerializeField] private bool _interactableEnabled = true;
        [SerializeField] private bool _manualRelease = false;

        public GameObject[] outlineGameObjects;
        
        private PlayerID OccupierConnection;// => NetworkManager.Clients.TryGetValue(_occupiedConnectionId, out var conn) ? conn : null;
        public string Key => interactableKey;

        protected readonly SyncVar<bool> _isOccupied = new SyncVar<bool>(false);

        public bool IsBusy;

        public bool IsEnabled => _interactableEnabled;
        public bool ManualRelease => _manualRelease;
        public bool IsOccupied => _isOccupied.value;

        public delegate void InteractCallback(bool success);
        
        public event InteractCallback InteractCallback_Client;
        public event InteractCallback StartInteractCallback_Server;
        public event InteractCallback EndInteractCallback_Server;

        private void Start()
        {
#if UNITY_SERVER
            InstanceHandler.NetworkManager.onPlayerLeft += OnPlayerLeft;
#endif
        }

        public void RequestInteract(bool force = false) => RequestInteract(force, localPlayerForced);
        
        public void RequestInteract(bool force,  PlayerID requester) => 
            RequestInteract_ServerRpc(requester, force);

        public void RequestEndInteract() => RequestEndInteract_ServerRpc(localPlayerForced);

        #region Server Methods
        
        [Server]
        public void ReleaseInteractable(PlayerID requester)
        {
            OccupierConnection = requester;
            _isOccupied.value = false;
            
            SendRequestEndInteractCallbacks(requester, true);
        }
        
        [Server]
        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            if(!asServer)
                return;
            
            if (player == OccupierConnection)
                ReleaseInteractable(player);
        }
        
        #endregion

        #region RPC

        [ServerRpc(requireOwnership: false)]
        private void RequestInteract_ServerRpc(PlayerID requester, bool force = false)
        {
            if (!_interactableEnabled || _isOccupied.value)
            {
                SendRequestInteractCallbacks(requester, false, force);
                return;
            }

            OccupierConnection = requester;
            _isOccupied.value = true;

            SendRequestInteractCallbacks(requester, true, force);
        }

        [Server]
        private void SendRequestInteractCallbacks(PlayerID requester, bool success, bool force = false)
        {
            OnInteractCallback_Server(requester, success, force);
            
                RequestInteractCallback_TargetRpc(requester, success, force);
            RequestInteractCallback_ObserversRpc(true, success, force);
        }

        [ServerRpc(requireOwnership: false)]
        private void RequestEndInteract_ServerRpc(PlayerID requester)
        {
            if (requester != OccupierConnection)
            {
                SendRequestEndInteractCallbacks(requester, false);
                return;
            }
            
            ReleaseInteractable(requester);
        }

        [Server]
        private void SendRequestEndInteractCallbacks(PlayerID requester, bool success)
        {
            OnInteractEndCallback_Server(requester, success);
            RequestEndInteractCallback_TargetRpc(requester, success);
            RequestInteractCallback_ObserversRpc(false, success);
        }

        [TargetRpc]
        private void RequestInteractCallback_TargetRpc(PlayerID target, bool success, bool force = false) =>
            OnInteractCallback_Client(success, force);

        [TargetRpc]
        private void RequestEndInteractCallback_TargetRpc(PlayerID target, bool success) =>
            OnInteractEndCallback_Client(success);

        [ObserversRpc(bufferLast: true)]
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
        protected virtual void OnInteractCallback_Server(PlayerID requester, bool success, bool force = false)
        {
            StartInteractCallback_Server?.Invoke(success);
            if(!success)
                return;
            GiveOwnership(requester);
        }

        [Server]
        protected virtual void OnInteractEndCallback_Server(PlayerID requester, bool success)
        {
            EndInteractCallback_Server?.Invoke(success);
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