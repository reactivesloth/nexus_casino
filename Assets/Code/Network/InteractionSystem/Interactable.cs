using System.Linq;
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

        [SerializeField] private SyncVar<PlayerID> OccupierConnection = new SyncVar<PlayerID>();// => NetworkManager.Clients.TryGetValue(_occupiedConnectionId, out var conn) ? conn : null;
        public string Key => interactableKey;

        [SerializeField] protected SyncVar<bool> _isOccupied = new SyncVar<bool>(false);

        public bool IsBusy;
        
        public bool IsEnabled => _interactableEnabled;
        public bool ManualRelease => _manualRelease;
        public bool IsOccupied => _isOccupied.value;

        public delegate void InteractCallback(bool success);
        
        public event InteractCallback InteractCallback_Client;
        public event InteractCallback StartInteractCallback_Server;
        public event InteractCallback EndInteractCallback_Server;

        protected override void OnSpawned(bool asServer)
        {
            base.OnSpawned(asServer);
            InstanceHandler.NetworkManager.onPlayerLeft += OnPlayerLeft;
            InstanceHandler.NetworkManager.onPlayerJoined += OnPlayerJoined;
        }

        protected override void OnDespawned(bool asServer)
        {
            base.OnDespawned(asServer);
            InstanceHandler.NetworkManager.onPlayerLeft -= OnPlayerLeft;
            InstanceHandler.NetworkManager.onPlayerJoined -= OnPlayerJoined;
        }
        
        public void RequestInteract(bool force = false) => RequestInteract(force, localPlayerForced);
        
        public void RequestInteract(bool force, PlayerID requester)
        {
            RequestInteract_ServerRpc(requester, force);
        }
            

        public void RequestEndInteract() => RequestEndInteract_ServerRpc(localPlayerForced);

        #region Server Methods
        
        [Server]
        public void ReleaseInteractable()
        {
            SendRequestEndInteractCallbacks(OccupierConnection.value, true);
            
            _isOccupied.value = false;
            OccupierConnection.value = PlayerID.Server;

        }
        
        [ServerOnly]
        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            if(!asServer) return;
            if (player == OccupierConnection.value)
                ReleaseInteractable();
        }
        
        [Server]
        private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            
        }
        
        #endregion

        #region RPC

        [ServerRpc(requireOwnership: false)]
        private void RequestInteract_ServerRpc(PlayerID target, bool force = false)
        {
            if (!_interactableEnabled || _isOccupied.value)
            {
                SendRequestInteractCallbacks(target, false, force);
                return;
            }

            OccupierConnection.value = target;
            _isOccupied.value = true;

            SendRequestInteractCallbacks(target, true, force);
        }

        [Server]
        private void SendRequestInteractCallbacks(PlayerID target, bool success, bool force = false)
        {
            OnInteractCallback_Server(target, success, force);
            RequestInteractCallback_TargetRpc(target, success, force);
            RequestInteractCallback_ObserversRpc(true, success, force);
        }

        [ServerRpc(requireOwnership: false)]
        private void RequestEndInteract_ServerRpc(PlayerID target)
        {
            if (target != OccupierConnection.value)
            {
                SendRequestEndInteractCallbacks(target, false);
                return;
            }
            
            ReleaseInteractable();
        }

        [Server]
        private void SendRequestEndInteractCallbacks(PlayerID target, bool success)
        {
            RequestInteractCallback_ObserversRpc(false, success);
            OnInteractEndCallback_Server(target, success);
            
            if(NetworkManager.main.players.Contains(target))
                RequestEndInteractCallback_TargetRpc(target, success);
        }

        [TargetRpc]
        private void RequestInteractCallback_TargetRpc(PlayerID target, bool success, bool force = false)
        {
            OnInteractCallback_Client(success, force);
        }
            

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
            if(!isServer) return;
            
            StartInteractCallback_Server?.Invoke(success);
            if(!success)
                return;
            GiveOwnership(requester, false, true);
        }

        [Server]
        protected virtual void OnInteractEndCallback_Server(PlayerID requester, bool success)
        {
            if(!isServer) return;
            
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
            if(!success)
                Debug.LogError($"[{gameObject}] Interactable not sucsess");
            else
                Debug.Log($"[{gameObject}] Interactable sucsess");
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