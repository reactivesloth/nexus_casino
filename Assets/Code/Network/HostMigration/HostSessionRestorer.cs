using System;
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

        private Player.PlayerSpawner _playerSpawner;
        
        private void Awake()
        {
            Instance = this;
            _spawnablePrefabs = InstanceFinder.NetworkManager.SpawnablePrefabs;
            _serverManager = InstanceFinder.ServerManager;
            _networkManager = InstanceFinder.NetworkManager;
            _playerSpawner = _networkManager.GetComponent<Player.PlayerSpawner>();
        }
        
        public static void SetSpawnerEnable(bool value) => Instance._playerSpawner.enabled = value;

        public void RestorePlayerData(MigratePlayerData state, NetworkConnection sender)
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

        private void ProcessSceneObject(NetworkObjectData networkObjectData, NetworkConnection sender)
        {
            throw new NotImplementedException();
        }

        private void ProcessSpawnedObject(NetworkObjectData networkObjectData, NetworkConnection sender)
        {
            Debug.Log($"[HostSessionRestorer] Process {networkObjectData.objectName}");

            var prefab = _spawnablePrefabs.GetObject(true, networkObjectData.prefabId);
            var nob = _networkManager.GetPooledInstantiated(prefab, true);
            _serverManager.Spawn(nob, sender);
            Debug.Log($"[HostSessionRestorer] {nob.name} Spawned");

            _networkManager.SceneManager.AddOwnerToDefaultScene(nob);

            ProcessComponents(networkObjectData, nob);
        }

        private void ProcessComponents(NetworkObjectData networkObjectData, NetworkObject networkObject)
        {
            foreach (var data in networkObjectData.componentsData)
            {
                Debug.Log($"[HostSessionRestorer] Process {data.componentName} component");

                if (networkObject.GetComponent(data.componentName) is not IMigratableBase migratableComponent)
                {
                    Debug.LogError($"Component {data.componentName} not found on {networkObject.name}");
                    continue;
                }

                migratableComponent.OnMigrateDataReceived(data.json);
            }
        }
    }
}