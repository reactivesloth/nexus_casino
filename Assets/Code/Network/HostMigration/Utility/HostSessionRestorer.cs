using System;
using System.Linq;
using Code.Network.HostMigration.Components;
using Code.Network.HostMigration.Data;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Server;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.HostMigration.Utility
{
    public static class HostSessionRestorer
    {
        private static NetworkManager NetworkManager => InstanceFinder.NetworkManager;
        private static ServerManager ServerManager => NetworkManager.ServerManager;
        private static PrefabObjects SpawnablePrefabs => NetworkManager.SpawnablePrefabs;

        public static void RestorePlayerData(MigratePlayerData state, NetworkConnection sender)
        {
            foreach (var networkObjectData in state.objects)
            {
                Debug.Log(
                    $"[HostSessionRestorer] Object {networkObjectData.objectName} is scened: {networkObjectData.isSceneObject}");
                if (networkObjectData.isSceneObject)
                    ProcessSceneObject(networkObjectData, sender);
                else
                    ProcessSpawnedObject(networkObjectData, sender);
            }
        }

        private static void ProcessSceneObject(MigratableObjectData migratableObjectData, NetworkConnection sender)
        {
            var migratableObject = MigratableObject.FindSceneObject(migratableObjectData.sceneObjectId);
            
            migratableObject.NetworkObject.GiveOwnership(sender);
            
            migratableObject.RestoreData(migratableObjectData);
        }

        private static void ProcessSpawnedObject(MigratableObjectData migratableObjectData, NetworkConnection sender)
        {
            Debug.Log($"[HostSessionRestorer] Process {migratableObjectData.objectName}");

            var prefab = SpawnablePrefabs.GetObject(true, migratableObjectData.prefabId);
            var objectTransformData = migratableObjectData.transformData;
            var nob = NetworkManager.GetPooledInstantiated(prefab, objectTransformData.GetUnityPosition,
                objectTransformData.GetUnityRotation, true);
            ServerManager.Spawn(nob, sender);
            Debug.Log($"[HostSessionRestorer] {nob.name} Spawned");

            nob.GetComponent<MigratableObject>().RestoreData(migratableObjectData);
        }
    }
}