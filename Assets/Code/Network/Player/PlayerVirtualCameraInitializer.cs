using Cinemachine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.Player
{
    /// <summary>
    /// Включает виртуальную камеру ТОЛЬКО у владельца.
    /// Корректно реагирует на смену владения и выключение клиента.
    /// </summary>
    public sealed class PlayerVirtualCameraInitializer : NetworkBehaviour
    {
        [SerializeField] private CinemachineVirtualCameraBase virtualCamera;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (virtualCamera == null)
                virtualCamera = GetComponentInChildren<CinemachineVirtualCameraBase>(true);
        }
#endif

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyOwnerState();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyOwnerState();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (virtualCamera != null)
                virtualCamera.enabled = false;
        }

        private void ApplyOwnerState()
        {
            if (virtualCamera == null)
                return;

            // камеру держим активной только у локального владельца
            virtualCamera.enabled = IsOwner;
        }
    }
}