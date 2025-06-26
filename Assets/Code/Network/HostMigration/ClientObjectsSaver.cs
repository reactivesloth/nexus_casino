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
        public static readonly List<NetworkObject> OwnObjects = new List<NetworkObject>();

        [SerializeField] private float checkInterval = 2f;

        private Coroutine _updateLoopCoroutine;
        
        private void Awake()
        {
            InstanceFinder.ClientManager.OnClientConnectionState += ClientManagerOnOnClientConnectionState;
        }

        private void OnDestroy()
        {
            InstanceFinder.ClientManager.OnClientConnectionState -= ClientManagerOnOnClientConnectionState;
        }

        private void ClientManagerOnOnClientConnectionState(ClientConnectionStateArgs obj)
        {
            switch (obj.ConnectionState)
            {
                case LocalConnectionState.Stopped:
                    OwnObjects.Clear();
                    break;
                case LocalConnectionState.Stopping:
                    StopCoroutine(_updateLoopCoroutine);
                    _updateLoopCoroutine = null;
                    break;
                case LocalConnectionState.Starting:
                    _updateLoopCoroutine = StartCoroutine(CheckLoop());
                    break;
                case LocalConnectionState.Started:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
        }

        private IEnumerator CheckLoop()
        {
            while (InstanceFinder.ClientManager.Started)
            {
                ChekObjects();
                yield return new WaitForSeconds(checkInterval);
            }
        }

        public void ChekObjects()
        {
            if(!InstanceFinder.ClientManager.Started)
                return;
            
            // Удаляем неактуальные
            for (int i = OwnObjects.Count - 1; i >= 0; i--)
            {
                var obj = OwnObjects[i];
                if (obj == null || !InstanceFinder.ClientManager.Connection.Objects.Contains(obj))
                    OwnObjects.RemoveAt(i);
            }

            // Добавляем новые
            foreach (var obj in InstanceFinder.ClientManager.Connection.Objects)
            {
                if (obj != null && !OwnObjects.Contains(obj))
                    OwnObjects.Add(obj);
            }
        }
    }
}