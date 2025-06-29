using System;
using Code.Network.HostMigration;
using Code.Network.HostMigration.Data;
using FishNet.Connection;
using FishNet.Object;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Network.Player
{
    [RequireComponent(typeof(ThirdPersonController))]
    public class MigratablePlayer : NetworkBehaviour, IMigratable<CharacterMigrateData>
    {
        [SerializeField] private ThirdPersonController thirdPersonController;

        protected override void OnValidate()
        {
            base.OnValidate();
            thirdPersonController = GetComponent<ThirdPersonController>();
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
            thirdPersonController.SetCamera();

            thirdPersonController.CinemachineCameraTarget.transform.SetPositionAndRotation(
                data.cameraRootTransformData.GetUnityPosition, data.cameraRootTransformData.GetUnityRotation);
        }

        public CharacterMigrateData GetMigrateData()
        {
            return new CharacterMigrateData
            {
                cameraRootTransformData =
                    SerializableTransform.SetFromUnityTransform(thirdPersonController.CinemachineCameraTarget
                        .transform),
                isFirstPersonView = thirdPersonController.FirstPersonView
            };
        }
    }
}