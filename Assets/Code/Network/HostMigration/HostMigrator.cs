using System;
using System.Collections.Generic;
using System.Linq;
using Code.Network.HostMigration.Data;
using Code.Network.HostMigration.Utility;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace Code.Network.HostMigration
{
    public class HostMigrator : MonoBehaviour
    {
        public static HostMigrator Instance;
        
        private ServerManager _serverManager;
        private ClientManager _clientManager;

        private MigratePlayerData _migrateData;
        
        private bool _isMigrating = false;

        public UnityEvent<NetworkConnection> hostMigrateProcessConnection;

        #region Unity Callbacks

        private void Awake()
        {
            Instance = this;
            
            _serverManager = InstanceFinder.ServerManager;
            _clientManager = InstanceFinder.ClientManager;
        }

        private void OnEnable()
        {
            ClientObjectsSaver.OnOwnObjectsUpdated += UpdateLastPlayerSessionState;
            _clientManager.OnAuthenticated += ClientManagerOnOnAuthenticated;
            
            _serverManager.RegisterBroadcast<MigratePlayerData>(OnServerReceiveMigrateBroadcast);
        }
        
        private void OnDisable()
        {
            ClientObjectsSaver.OnOwnObjectsUpdated -= UpdateLastPlayerSessionState;
            _clientManager.OnAuthenticated -= ClientManagerOnOnAuthenticated;
            
            _serverManager.UnregisterBroadcast<MigratePlayerData>(OnServerReceiveMigrateBroadcast);
        }

        #endregion
            
        /// <summary>
        /// For auto run mark migrating
        /// </summary>
        public void MarkMigrating() => _isMigrating = true;
        
        /// <summary>
        /// Execute where the client connected to new host on migrate
        /// </summary>
        public void RunClient()
        {
            Debug.Log("Client start migrating");
            _clientManager.Broadcast(_migrateData);
            _isMigrating = false;
        }
        
        /// <summary>
        /// Execute where the host started on migrate
        /// </summary>
        public void RunHost()
        {
            throw new NotImplementedException("Host handle on migrate dont implemented");
            _isMigrating = false;
        }
        
        private void OnServerReceiveMigrateBroadcast(NetworkConnection connection, MigratePlayerData data,
            Channel channel)
        {
            hostMigrateProcessConnection?.Invoke(connection);
            Debug.Log(JsonConvert.SerializeObject(data, Formatting.Indented));
            HostSessionRestorer.RestorePlayerData(data, connection);
        }
        
        private void ClientManagerOnOnAuthenticated()
        {
            if(_isMigrating)
                RunClient();
        }

        private void UpdateLastPlayerSessionState(List<NetworkObject> ownObjects)
        {
            _migrateData = new MigratePlayerData
            {
                objects = new List<NetworkObjectData>()
            };

            foreach (var currentGameObject in ownObjects)
            {
                var migratableComponents =
                    currentGameObject.GetComponents<MonoBehaviour>().OfType<IMigratableBase>().ToList();

                if (migratableComponents.Count == 0)
                    continue;

                var migrateObject = new NetworkObjectData
                {
                    objectName = currentGameObject.name,
                    networkObjectId = currentGameObject.ObjectId,
                    prefabId = currentGameObject.PrefabId,
                    ownerId = currentGameObject.OwnerId,
                    isSceneObject = currentGameObject.IsSceneObject,
                    transformData = SerializableTransform.SetFromUnityTransform(currentGameObject.transform),
                    componentsData = new List<MigratableComponentData>()
                };

                _migrateData.objects.Add(migrateObject);

                foreach (var migratableComponent in migratableComponents)
                {
                    var componentAbstractData = migratableComponent.GetMigrateData();
                    var migratableComponentData = new MigratableComponentData
                    {
                        componentName = migratableComponent.GetType().FullName,
                        jsonData = migratableComponent.GetJson(componentAbstractData)
                    };
                    migrateObject.componentsData.Add(migratableComponentData);
                }
            }
        }
    }
}