using System;
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
        private static ServerManager   ServerManager   => NetworkManager != null ? NetworkManager.ServerManager : null;
        private static PrefabObjects   SpawnablePrefabs=> NetworkManager != null ? NetworkManager.SpawnablePrefabs : null;

        public static void RestorePlayerData(MigratePlayerData state, NetworkConnection sender)
        {
            if (state.objects == null || sender == null || ServerManager == null || NetworkManager == null)
                return;

            for (int i = 0; i < state.objects.Count; i++)
            {
                var data = state.objects[i];
                if (data.isSceneObject)
                    ProcessSceneObject(data, sender);
                else
                    ProcessSpawnedObject(data, sender);
            }
        }

        private static void ProcessSceneObject(NetworkObjectData data, NetworkConnection sender)
        {
            if (string.IsNullOrEmpty(data.sceneObjectId))
            {
                Debug.LogWarning($"[HostSessionRestorer] sceneObjectId is empty for '{data.objectName}'. Skip.");
                return;
            }

            var so = SceneObject.GetObjectById(data.sceneObjectId);
            if (so == null)
            {
                Debug.LogWarning($"[HostSessionRestorer] SceneObject '{data.sceneObjectId}' not found. Skip.");
                return;
            }

            var nob = so.GetComponent<NetworkObject>();
            if (nob == null)
            {
                Debug.LogWarning($"[HostSessionRestorer] NetworkObject not found on '{so.name}'. Skip.");
                return;
            }

            // владелец и трансформ (мировые)
            TryGiveOwnership(nob, sender);
            ApplyTransformWorld(nob.transform, data.transformData);

            // компоненты
            ProcessComponents(data, nob);
        }

        private static void ProcessSpawnedObject(NetworkObjectData data, NetworkConnection sender)
        {
            if (SpawnablePrefabs == null)
                return;

            var prefab = SpawnablePrefabs.GetObject(true, data.prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[HostSessionRestorer] Prefab not found by id {data.prefabId} for '{data.objectName}'.");
                return;
            }

            var t = data.transformData;
            var nob = NetworkManager.GetPooledInstantiated(prefab, t.GetUnityPosition, t.GetUnityRotation, true);
            if (nob == null)
            {
                Debug.LogWarning($"[HostSessionRestorer] Failed to instantiate '{data.objectName}'.");
                return;
            }

            ServerManager.Spawn(nob, sender);
            NetworkManager.SceneManager.AddOwnerToDefaultScene(nob);

            // компоненты
            ProcessComponents(data, nob);
        }

        private static void ProcessComponents(NetworkObjectData source, NetworkObject nob)
        {
            if (source.componentsData == null || nob == null) return;

            for (int i = 0; i < source.componentsData.Count; i++)
            {
                var cData = source.componentsData[i];
                if (string.IsNullOrEmpty(cData.componentName)) continue;

                var comp = nob.GetComponent(cData.componentName) as IMigratableBase;
                if (comp == null)
                {
                    Debug.LogWarning($"[HostSessionRestorer] Component '{cData.componentName}' not found on '{nob.name}'.");
                    continue;
                }

                comp.OnMigrateDataReceived(cData.jsonData);
            }
        }

        private static void TryGiveOwnership(NetworkObject nob, NetworkConnection owner)
        {
            if (nob == null || owner == null) return;
            try { nob.GiveOwnership(owner); }
            catch (Exception e) { Debug.LogWarning($"[HostSessionRestorer] GiveOwnership failed: {e.Message}"); }
        }

        private static void ApplyTransformWorld(Transform tf, SerializableTransform s)
        {
            if (tf == null) return;
            tf.position   = s.GetUnityPosition;
            tf.rotation   = s.GetUnityRotation;
            tf.localScale = s.GetUnityScale;
        }
    }
}
