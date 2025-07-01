using System;
using System.Collections.Generic;
using System.Linq;
using Code.Network.HostMigration.Components;
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
    public class HostMigrationManager : MonoBehaviour
    {
        public static HostMigrationManager Instance;
        
        private ServerManager _serverManager;
        private ClientManager _clientManager;

        private MigratePlayerData _migrateData;
        
        private bool _isMigrating = false;
        
        private readonly HashSet<MigratableObject> _migratableObjects = new();

        public UnityEvent<NetworkConnection> hostMigrateProcessConnection;

        #region Unity Callbacks

        private void Awake()
        {
            Instance = this;
            
            _serverManager = InstanceFinder.ServerManager;
            _clientManager = InstanceFinder.ClientManager;
        }

        private void Update()
        {
            if(_clientManager.Started)
                PrepareMigrationData();
        }

        private void OnEnable()
        {
            //ClientObjectsSaver.OnOwnObjectsUpdated += UpdateLastPlayerSessionState;
            _clientManager.OnAuthenticated += ClientManagerOnOnAuthenticated;
            
            _serverManager.RegisterBroadcast<MigratePlayerData>(OnServerReceiveMigrateBroadcast);
        }
        
        private void OnDisable()
        {
            //ClientObjectsSaver.OnOwnObjectsUpdated -= UpdateLastPlayerSessionState;
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
        
        public void Register(MigratableObject obj)
        {
            _migratableObjects.Add(obj);
        }

        public void Unregister(MigratableObject obj)
        {
            _migratableObjects.Remove(obj);
        }
        
        public void PrepareMigrationData()
        {
            _migrateData = new MigratePlayerData
            {
                objects = new List<MigratableObjectData>()
            };

            foreach (var migratable in _migratableObjects.Where(migratable => migratable))
            {
                _migrateData.objects.Add(migratable.GetData());
            }
            
            Debug.Log(_migrateData.objects.Count);
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
    }
}