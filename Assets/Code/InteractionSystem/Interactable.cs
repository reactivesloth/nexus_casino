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
        [Header("Interaction Settings")] [SerializeField, Tooltip("Max distance for interaction.")]
        private float _interactionDistance = 3f;

        public float InteractionDistance => _interactionDistance;

        public GameObject[] outlineGameObjects;

        [Header("Enable/Disable")] [SerializeField, Tooltip("Enable or disable this interactable.")]
        private bool _interactableEnabled = true;

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

        public delegate void OnInteractCallback(bool success);
        public event OnInteractCallback InteractCallback; // Callback for clients

        public event Action OnInteractEndOnServer;

        public virtual string InteractionPrompt
        {
            get
            {
                if (!_interactableEnabled) return "Disabled";
                if (!_isOccupied.Value) return "Press E to interact";
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

        [Server]
        private void ServerManagerOnRemoteConnectionState(NetworkConnection connection,
            RemoteConnectionStateArgs stateArgs)
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
            if (!_interactableEnabled || _isOccupied.Value)
            {
                InteractCallback?.Invoke(false);
                return;
            }
            Server_HandleInteract(ClientManager.Connection);
        }

        public void RequestEndInteract()
        {
            if (!_interactableEnabled || !_isOccupied.Value || !_manualRelease)
            {
                InteractCallback?.Invoke(false);
                return;
            }
            Server_HandleEndInteract(ClientManager.Connection);
        }

        [Server]
        public void ServerForceInteract(NetworkConnection conn) => HandleInteract(conn, true);

        [ServerRpc(RequireOwnership = false)]
        private void Server_HandleInteract(NetworkConnection conn) => HandleInteract(conn);

        private void HandleInteract(NetworkConnection conn, bool force = false)
        {
            if (!_interactableEnabled || _isOccupied.Value || conn == null)
            {
                OnInteractionCallbackFromServer(conn, false);
                return;
            }
            OccupiedConnectionId = conn.ClientId;
            _isOccupied.Value = true;
            OnInteract(conn, force);

            if (!ManualRelease)
                _isOccupied.Value = false; // автосброс
            
            OnInteractionCallbackFromServer(conn, true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Server_HandleEndInteract(NetworkConnection conn)
        {
            if (!_manualRelease || !_isOccupied.Value)
            {
                OnEndInteractionCallbackFromServer(conn, false);
                return;
            }
            OnEndInteract(conn);
            _isOccupied.Value = false;
            OnEndInteractionCallbackFromServer(conn, true);
        }

        [Server]
        public void ReleaseInteractable()
        {
            _isOccupied.Value = false;
            OnEndInteract();
        }

        [Server]
        public void SetEnabled(bool enabled) => _interactableEnabled = enabled;

        protected internal virtual void OnInteract(NetworkConnection conn, bool force)
        {
            GiveOwnership(conn);
        }

        protected internal virtual void OnEndInteract(NetworkConnection conn = null)
        {
            OnInteractEndOnServer?.Invoke();
            OccupiedConnectionId = -1;
            RemoveOwnership();
        }

        [TargetRpc]
        private void OnInteractionCallbackFromServer(NetworkConnection target, bool success) =>
            OnInteractionCallbackFromServer(success);

        protected virtual void OnInteractionCallbackFromServer(bool success)
        {
            InteractCallback?.Invoke(success);
        }
        
        [TargetRpc]
        private void OnEndInteractionCallbackFromServer(NetworkConnection target, bool success) =>
            OnEndInteractionCallbackFromServer(success);

        protected virtual void OnEndInteractionCallbackFromServer(bool success)
        {
            InteractCallback?.Invoke(success);
        }
    }
}