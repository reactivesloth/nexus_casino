using Code.Network.HostMigration;
using Code.Network.HostMigration.Data;
using FishNet.Connection;
using FishNet.Object;
using Newtonsoft.Json;
using UnityEngine;

namespace Code.Network.Player
{
    [RequireComponent(typeof(PlayerMovementController))]
    public class MigratablePlayer : NetworkBehaviour, IMigratable<CharacterMigrateData>
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
                SetPlayerState(Owner, JsonConvert.SerializeObject(data));
        }

        [TargetRpc]
        private void SetPlayerState(NetworkConnection conn, string jsonData)
        {
            var data = JsonConvert.DeserializeObject<CharacterMigrateData>(jsonData);
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