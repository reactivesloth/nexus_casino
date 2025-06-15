using System;
using Cinemachine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.Player
{
    public class PlayerVirtualCameraInitializer : NetworkBehaviour
    {
        [SerializeField] private CinemachineVirtualCameraBase virtualCamera;

        public override void OnStartClient()
        {
            base.OnStartClient();
            virtualCamera.enabled = IsOwner;
        }
    }
}
