
using System;
using Code.Network.HostMigration.Data;

namespace Code.Network.Player
{
    [Serializable]
    public class CharacterMigrateData
    {
        public SerializableTransform cameraRootTransformData;
        public bool isFirstPersonView;
    }
}