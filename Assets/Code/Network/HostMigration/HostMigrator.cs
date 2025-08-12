using System;
using System.Collections.Generic;
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
        private bool _isMigrating;

        public UnityEvent<NetworkConnection> hostMigrateProcessConnection;

        public bool IsHostMigrating => _isMigrating;

        private void Awake()
        {
            Instance        = this;
            _serverManager  = InstanceFinder.ServerManager;
            _clientManager  = InstanceFinder.ClientManager;
        }

        private void OnEnable()
        {
            ClientObjectsSaver.OnOwnObjectsUpdated += UpdateLastPlayerSessionState;

            if (_clientManager != null)
                _clientManager.OnAuthenticated += OnClientAuthenticated;

            if (_serverManager != null)
                _serverManager.RegisterBroadcast<MigratePlayerData>(OnServerReceiveMigrateBroadcast);
        }

        private void OnDisable()
        {
            ClientObjectsSaver.OnOwnObjectsUpdated -= UpdateLastPlayerSessionState;

            if (_clientManager != null)
                _clientManager.OnAuthenticated -= OnClientAuthenticated;

            if (_serverManager != null)
                _serverManager.UnregisterBroadcast<MigratePlayerData>(OnServerReceiveMigrateBroadcast);
        }

        /// <summary> Пометить, что сейчас идёт миграция. Вызывается до переподключения. </summary>
        public void MarkMigrating() => _isMigrating = true;

        /// <summary> Клиент успешно подсоединился к новому хосту. </summary>
        public void RunClient()
        {
            Debug.Log("[HostMigrator] Client start migrating");
            if (_clientManager != null)
                _clientManager.Broadcast(_migrateData);
            _isMigrating = false;
        }

        /// <summary> Хост поднят после миграции. Если нужна спец-логика — добавь тут. </summary>
        public void RunHost()
        {
            Debug.Log("[HostMigrator] Host ready after migration.");
            _isMigrating = false;
        }

        private void OnServerReceiveMigrateBroadcast(NetworkConnection connection, MigratePlayerData data, Channel _)
        {
            Debug.Log($"[HostMigrator] Received migrate data:\n{JsonConvert.SerializeObject(data, Formatting.Indented)}");

            hostMigrateProcessConnection?.Invoke(connection);
            HostSessionRestorer.RestorePlayerData(data, connection);
        }

        private void OnClientAuthenticated()
        {
            if (_isMigrating)
                RunClient();
        }

        private void UpdateLastPlayerSessionState(List<NetworkObject> ownObjects)
        {
            var result = new MigratePlayerData { objects = new List<NetworkObjectData>(ownObjects != null ? ownObjects.Count : 0) };
            if (ownObjects == null)
            {
                _migrateData = result;
                return;
            }

            for (int i = 0; i < ownObjects.Count; i++)
            {
                var nob = ownObjects[i];
                if (nob == null) continue;

                // собираем компоненты, реализующие IMigratableBase
                var migratables = nob.GetComponents<MonoBehaviour>();
                var compDatas = new List<MigratableComponentData>(migratables.Length);
                for (int c = 0; c < migratables.Length; c++)
                {
                    var mb = migratables[c] as IMigratableBase;
                    if (mb == null) continue;

                    var data = mb.GetMigrateData();
                    compDatas.Add(new MigratableComponentData
                    {
                        componentName = migratables[c].GetType().FullName,
                        jsonData      = mb.GetJson(data)
                    });
                }

                if (compDatas.Count == 0)
                    continue;

                string sceneGuid = null;
                if (nob.IsSceneObject)
                {
                    var so = nob.GetComponent<Code.Network.HostMigration.Components.SceneObject>();
                    if (so != null)
                        sceneGuid = so.ObjectGuid.ToString();
                }

                var objData = new NetworkObjectData
                {
                    objectName     = nob.name,
                    networkObjectId= nob.ObjectId,
                    prefabId       = nob.PrefabId,
                    ownerId        = nob.OwnerId,
                    isSceneObject  = nob.IsSceneObject,
                    isNetworkObject= true,
                    sceneObjectId  = sceneGuid,
                    transformData  = Data.SerializableTransform.SetFromUnityTransform(nob.transform),
                    componentsData = compDatas
                };

                result.objects.Add(objData);
            }

            _migrateData = result;
        }
    }
}
