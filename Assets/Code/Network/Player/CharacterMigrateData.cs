using System;

namespace Code.Network.Player
{
    [Serializable]
    public struct CharacterMigrateData
    {
        // Data for camera rotation
        public float cinemachineTargetYaw;
        public float cinemachineTargetPitch;

        // Data for camera distance
        public float cameraDistance;
        public bool isFirstPersonView;
    }
}