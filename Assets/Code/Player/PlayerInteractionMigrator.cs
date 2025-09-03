using Code.InteractionSystem;
using Code.Network.HostMigration;
using Code.Network.HostMigration.Components;
using Code.Network.Player;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player
{
    public class PlayerInteractionMigrator : NetworkBehaviour, IMigratable<CharacterInteractableMigrateData>
    {
        [FormerlySerializedAs("_playerInteraction")] [SerializeField] private PlayerInteraction playerInteraction;

        protected override void OnValidate()
        {
            base.OnValidate();
            playerInteraction ??= GetComponent<PlayerInteraction>();
        }
        
        #region IMigratable
        public void OnMigrateDataReceived(CharacterInteractableMigrateData data)
        {
            if (!NetworkManager.IsServerStarted || string.IsNullOrEmpty(data.activeId))
                return;

            var sceneObject = SceneObject.GetObjectById(data.activeId);
            if (!sceneObject) return;
            if (!sceneObject.TryGetComponent(out Interactable interactable)) return;

            if (interactable.IsOccupied)
                return;

            interactable.RequestInteract(true, Owner);
            SetInteractableOnMigrate(Owner, data);
        }

        [TargetRpc]
        public void SetInteractableOnMigrate(NetworkConnection conn, CharacterInteractableMigrateData data)
        {
            var sceneObject = SceneObject.GetObjectById(data.activeId);
            if (!sceneObject) return;
            if (!sceneObject.TryGetComponent(out Interactable interactable)) return;

            playerInteraction.Active = interactable;
        }

        public CharacterInteractableMigrateData GetMigrateData()
        {
            if (playerInteraction.Active == null) return default;
            if (!playerInteraction.Active.TryGetComponent<SceneObject>(out var sceneObject)) return default;
            return new CharacterInteractableMigrateData { activeId = sceneObject.ObjectGuid.ToString() };
        }
        #endregion
    }
}