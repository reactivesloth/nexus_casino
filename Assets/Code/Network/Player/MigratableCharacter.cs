using Code.Network.HostMigration;
using Code.Network.HostMigration.Data;
using Code.Player;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.Player
{
    [RequireComponent(typeof(PlayerMovementController))]
    public class MigratableCharacter : NetworkBehaviour, IMigratable<CharacterMigrateData>
    {
        [SerializeField] private PlayerMovementController thirdPersonController;

        protected override void OnValidate()
        {
            base.OnValidate();
            thirdPersonController = GetComponent<PlayerMovementController>();
        }

        public void OnMigrateDataReceived(CharacterMigrateData data)
        {
            if(NetworkManager.IsServerStarted)
                SetPlayerState(Owner, data);
        }

        [TargetRpc]
        private void SetPlayerState(NetworkConnection conn, CharacterMigrateData data)
        {
            thirdPersonController.FirstPersonView = data.isFirstPersonView;
            thirdPersonController.CinemachineCameraTarget.transform.SetLocalPositionAndRotation(
                data.cameraRootTransformData.GetUnityPosition, data.cameraRootTransformData.GetUnityRotation);
        }

        public CharacterMigrateData GetMigrateData()
        {
            return new CharacterMigrateData
            {
                cameraRootTransformData =
                    SerializableTransform.SetFromUnityTransformLocal(thirdPersonController.CinemachineCameraTarget.transform),
                isFirstPersonView = thirdPersonController.FirstPersonView
            };
        }
    }
}