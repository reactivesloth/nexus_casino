using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network.HostMigration
{
    public class ClientObjectsSaver : MonoBehaviour
    {
        public static readonly List<NetworkObject> OwnObjects = new List<NetworkObject>(64);

        [SerializeField] private float checkInterval = 2f;
        private Coroutine _updateLoopCoroutine;

        public static event Action<List<NetworkObject>> OnOwnObjectsUpdated;

        private void Awake()
        {
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDestroy()
        {
            var cm = InstanceFinder.ClientManager;
            if (cm != null)
                cm.OnClientConnectionState -= OnClientConnectionState;

            StopLoop();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    StartLoop();
                    break;

                case LocalConnectionState.Stopping:
                case LocalConnectionState.Stopped:
                    StopLoop();
                    OwnObjects.Clear();
                    break;
            }
        }

        private void StartLoop()
        {
            if (_updateLoopCoroutine == null)
                _updateLoopCoroutine = StartCoroutine(CheckLoop());
        }

        private void StopLoop()
        {
            if (_updateLoopCoroutine != null)
            {
                StopCoroutine(_updateLoopCoroutine);
                _updateLoopCoroutine = null;
            }
        }

        private IEnumerator CheckLoop()
        {
            var cm = InstanceFinder.ClientManager;
            while (cm != null && cm.Started)
            {
                yield return new WaitForSeconds(checkInterval);
                CheckObjects();
            }
        }

        public void CheckObjects()
        {
            var cm = InstanceFinder.ClientManager;
            if (cm == null || !cm.Started) return;

            var conn = cm.Connection;
            if (conn == null) return;

            // удаляем неактуальные
            for (int i = OwnObjects.Count - 1; i >= 0; i--)
            {
                var obj = OwnObjects[i];
                if (obj == null || !conn.Objects.Contains(obj))
                    OwnObjects.RemoveAt(i);
            }

            // добавляем новые
            foreach (var obj in conn.Objects)
            {
                if (obj != null && !OwnObjects.Contains(obj))
                    OwnObjects.Add(obj);
            }

            OnOwnObjectsUpdated?.Invoke(OwnObjects);
        }
    }
}
