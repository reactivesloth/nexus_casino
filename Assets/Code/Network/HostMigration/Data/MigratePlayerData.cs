using System;
using System.Collections.Generic;
using FishNet.Broadcast;

namespace Code.Network.HostMigration.Data
{
    [Serializable]
    public struct MigratePlayerData: IBroadcast
    {
        public List<NetworkObjectData> objects;
    }

    [Serializable]
    public struct NetworkObjectData: IBroadcast
    {
        public string objectName;
        public bool isSceneObject;
        public int networkObjectId;
        public int prefabId; // Для динамических объектов
        public int ownerId; // Владелец (игрок)
        public List<MigratableComponentData> componentsData;
    }

    [Serializable]
    public struct MigratableComponentData: IBroadcast
    {
        public string componentName;
        public string json;
    }
}