using System;
using System.Linq;
using Code.Network.HostMigration.Data;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Server;
using FishNet.Object;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Code.Network.HostMigration
{
    public class HostSessionRestorer : MonoBehaviour
    {
        public static HostSessionRestorer Instance;

        private PrefabObjects _spawnablePrefabs;
        private ServerManager _serverManager;
        private NetworkManager _networkManager;

        private void Awake()
        {
            Instance = this;
            _spawnablePrefabs = InstanceFinder.NetworkManager.SpawnablePrefabs;
            _serverManager = InstanceFinder.ServerManager;
            _networkManager = InstanceFinder.NetworkManager;
        }

        public void RestorePlayerData(MigratePlayerData state, NetworkConnection sender)
        {
            foreach (var networkObjectData in state.objects)
            {
                if (networkObjectData.isSceneObject)
                    ProcessSceneObject(networkObjectData, sender);
                else
                    ProcessSpawnedObject(networkObjectData, sender);
            }
        }

        private void ProcessSceneObject(NetworkObjectData networkObjectData, NetworkConnection sender)
        {
            throw new NotImplementedException();
        }

        private void ProcessSpawnedObject(NetworkObjectData networkObjectData, NetworkConnection sender)
        {
            var prefab = _spawnablePrefabs.GetObject(true, networkObjectData.prefabId);
            var nob = _networkManager.GetPooledInstantiated(prefab, Vector3.zero, Quaternion.identity, true);
            _serverManager.Spawn(nob, sender);
            
            _networkManager.SceneManager.AddOwnerToDefaultScene(nob);
            
            ProcessComponents(networkObjectData, nob);
        }

        private void ProcessComponents(NetworkObjectData networkObjectData, NetworkObject networkObject)
        {
            foreach (var data in networkObjectData.componentsData)
            {
                if(networkObject.GetComponent(data.componentName) is not IMigratableBase migratableComponent)
                    continue;
                
                migratableComponent.SetMigrateData(data.json);
            }
        }
    }
}