using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;

namespace Code.InteractionSystem
{
    public abstract class Interactable : NetworkBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField, Tooltip("Max distance for interaction.")] private float _interactionDistance = 3f;
        public float InteractionDistance => _interactionDistance;

        [Header("Enable/Disable")]
        [SerializeField, Tooltip("Enable or disable this interactable.")] private bool _interactableEnabled = true;
        public bool IsEnabled => _interactableEnabled;

        [Header("Release Mode")]
        [SerializeField, Tooltip("If true, requires manual EndInteract to free the interactable.")] private bool _manualRelease = false;
        public bool ManualRelease => _manualRelease;

        // Synchronize occupied state across clients using SyncVar
        protected readonly SyncVar<bool> _isOccupied = new SyncVar<bool>(new SyncTypeSettings()
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });
        public bool IsOccupied => _isOccupied.Value;

        public virtual string InteractionPrompt
        {
            get
            {
                if (!_interactableEnabled) return "Disabled";
                if (!_isOccupied.Value) return "Press E to interact";
                return _manualRelease ? "Press E to end" : "Occupied";
            }
        }

        /// <summary>Client-side call to request interaction start.</summary>
        public void RequestInteract()
        {
            if (!_interactableEnabled || _isOccupied.Value) return;
            Server_HandleInteract();
        }

        /// <summary>Client-side call to request interaction end (for manualRelease).</summary>
        public void RequestEndInteract()
        {
            if (!_interactableEnabled || !_isOccupied.Value || !_manualRelease) return;
            Server_HandleEndInteract();
        }

        [ServerRpc(RequireOwnership = false)]
        private void Server_HandleInteract(NetworkConnection conn = null)
        {
            if (!_interactableEnabled || _isOccupied.Value) return;
            _isOccupied.Value = true;
            OnInteract(conn);
            if (!ManualRelease)
            {
                // auto release immediately
                _isOccupied.Value = false;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void Server_HandleEndInteract(NetworkConnection conn = null)
        {
            if (!_manualRelease || !_isOccupied.Value) return;
            OnEndInteract(conn);
            _isOccupied.Value = false;
        }

        /// <summary>Releases occupancy, making object free again.</summary>
        [Server]
        protected void ReleaseInteractable()
        {
            _isOccupied.Value = false;
        }

        /// <summary>Optional server call to toggle enabled state.</summary>
        [Server]
        public void SetEnabled(bool enabled)
        {
            _interactableEnabled = enabled;
        }

        // Override for custom logic on start
        protected abstract void OnInteract(NetworkConnection conn);
        // Override for custom logic on end (for manualRelease)
        protected virtual void OnEndInteract(NetworkConnection conn) { }
    }
}