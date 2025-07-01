using System;
using System.Collections.Generic;
using FishNet.Broadcast;

namespace Code.Network.HostMigration.Data
{
    [Serializable]
    public struct MigratePlayerData: IBroadcast
    {
        public List<MigratableObjectData> objects;
    }

    [Serializable]
    public struct MigratableObjectData: IBroadcast
    {
        public string objectName;
        
        public bool isSceneObject;
        public string sceneObjectId; //Use if isSceneObject == true
        public int prefabId; //Use if isSceneObject == false
        public SerializableTransform transformData; //For spawn non-scene objects
        
        public List<MigratableComponentData> componentsData;
    }

    [Serializable]
    public struct MigratableComponentData: IBroadcast
    {
        public string componentName;
        public string jsonData;
    }
}