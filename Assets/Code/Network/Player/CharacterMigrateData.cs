
using System;
using Code.Network.HostMigration.Data;

namespace Code.Network.Player
{
    [Serializable]
    public struct CharacterMigrateData
    {
        // public SerializableTransform cameraRootTransformData;
        
        //Data for camera rotation
        public float cinemachineTargetYaw;
        public float cinemachineTargetPitch;

        public float cameraDistance;
        public bool isFirstPersonView;
    }
}