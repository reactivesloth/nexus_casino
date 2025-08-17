using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;

namespace Code.InteractionSystem
{
    public abstract class Interactable : NetworkBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField, Tooltip("Max distance for interaction.")] private float _interactionDistance = 3f;
        public float InteractionDistance => _interactionDistance;

        public GameObject[] outlineGameObjects;

        [Header("Enable/Disable")]
        [SerializeField, Tooltip("Enable or disable this interactable.")] private bool _interactableEnabled = true;
        public bool IsEnabled => _interactableEnabled;

        [Header("Release Mode")]
        [SerializeField, Tooltip("If true, requires manual EndInteract to free the interactable.")]
        private bool _manualRelease = false;
        public bool ManualRelease => _manualRelease;

        protected int OccupiedConnectionId;

        protected readonly SyncVar<bool> _isOccupied = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });
        public bool IsOccupied => _isOccupied.Value;
        public bool IsBusy { get; set; }

        public virtual string InteractionPrompt
        {
            get
            {
                if (!_interactableEnabled) return "Disabled";
                if (!_isOccupied.Value)    return "Press E to interact";
                return _manualRelease ? "Press E to end" : "Occupied";
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += ServerManagerOnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= ServerManagerOnRemoteConnectionState;
        }

        public virtual void InteractionStateMigrate()
        {
            
        }
        
        [Server]
        private void ServerManagerOnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs stateArgs)
        {
            if (stateArgs.ConnectionState == RemoteConnectionState.Stopped &&
                stateArgs.ConnectionId == OccupiedConnectionId)
                ReleaseInteractable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
#endif

        public void RequestInteract()
        {
            if (!_interactableEnabled || _isOccupied.Value) return;
            Server_HandleInteract(ClientManager.Connection);
        }

        public void RequestEndInteract()
        {
            if (!_interactableEnabled || !_isOccupied.Value || !_manualRelease) return;
            Server_HandleEndInteract(ClientManager.Connection);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Server_HandleInteract(NetworkConnection conn)
        {
            if (!_interactableEnabled || _isOccupied.Value || conn == null) return;
            OccupiedConnectionId = conn.ClientId;
            _isOccupied.Value = true;
            OnInteract(conn);

            if (!ManualRelease)
                _isOccupied.Value = false; // автосброс
        }

        [ServerRpc(RequireOwnership = false)]
        private void Server_HandleEndInteract(NetworkConnection conn)
        {
            if (!_manualRelease || !_isOccupied.Value) return;
            OnEndInteract(conn);
            _isOccupied.Value = false;
        }

        [Server] public void ReleaseInteractable()
        {
            _isOccupied.Value = false;
            OnEndInteract();
        }

        [Server] public void SetEnabled(bool enabled) => _interactableEnabled = enabled;

        protected internal virtual void OnInteract(NetworkConnection conn) => GiveOwnership(conn);

        protected internal virtual void OnEndInteract(NetworkConnection conn = null)
        {
            OccupiedConnectionId = -1;
            RemoveOwnership();
        }
    }
}
