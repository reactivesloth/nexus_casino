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
            if(state.objects == null)
                return;
                
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

        private static void ProcessSceneObject(NetworkObjectData networkObjectData, NetworkConnection sender)
        {
            throw new NotImplementedException();
        }

        private static void ProcessSpawnedObject(NetworkObjectData networkObjectData, NetworkConnection sender)
        {
            Debug.Log($"[HostSessionRestorer] Process {networkObjectData.objectName}");

            var prefab = SpawnablePrefabs.GetObject(true, networkObjectData.prefabId);
            var objectTransformData = networkObjectData.transformData;
            var nob = NetworkManager.GetPooledInstantiated(prefab, objectTransformData.GetUnityPosition,
                objectTransformData.GetUnityRotation, true);
            ServerManager.Spawn(nob, sender);
            Debug.Log($"[HostSessionRestorer] {nob.name} Spawned");

            NetworkManager.SceneManager.AddOwnerToDefaultScene(nob);

            ProcessComponents(networkObjectData, nob);
        }

        private static void ProcessComponents(NetworkObjectData networkObjectData, NetworkObject networkObject)
        {
            foreach (var data in networkObjectData.componentsData)
            {
                Debug.Log($"[HostSessionRestorer] Process {data.componentName} component");

                if (networkObject.GetComponent(data.componentName) is not IMigratableBase migratableComponent)
                {
                    Debug.LogError($"Component {data.componentName} not found on {networkObject.name}");
                    continue;
                }

                migratableComponent.OnMigrateDataReceived(data.jsonData);
            }
        }
    }
}