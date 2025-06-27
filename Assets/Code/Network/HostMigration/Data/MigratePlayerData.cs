using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.HostMigration.Data
{
    [Serializable]
    public class MigratePlayerData
    {
        public List<NetworkObjectData> objects = new();
    }

    [Serializable]
    public class NetworkObjectData
    {
        public bool isSceneObject;
        public int networkObjectId;
        public int prefabId; // Для динамических объектов
        public int ownerId; // Владелец (игрок)
        public List<MigratableComponentData> componentsData = new();
    }

    [Serializable]
    public class MigratableComponentData
    {
        public string componentName;
        public string json;
    }
}