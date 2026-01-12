using System;
using System.Collections.Generic;
using Code.Network.InteractionSystem;
using UnityEngine;

namespace Code.Network
{
    public class ServerPlayerSlotVisibleController : MonoBehaviour
    {
        [SerializeField] private float updateIntervalSecs = 1f;

        //public static readonly Dictionary<NetworkConnection, PlayerSlotViewInfo> ServerInfoForPlayerViewSlots = new();

        private float _currentIntervalSecs = 0;

        // private void OnEnable()
        // {
        //     InstanceFinder.ServerManager.RegisterBroadcast<PlayerSlotViewInfo>(PlayerSlotViewReceive);
        //     InstanceFinder.ServerManager.OnRemoteConnectionState += ServerManagerOnOnRemoteConnectionState;
        // }
        //
        // private void OnDisable()
        // {
        //     InstanceFinder.ServerManager.UnregisterBroadcast<PlayerSlotViewInfo>(PlayerSlotViewReceive);
        //     InstanceFinder.ServerManager.OnRemoteConnectionState -= ServerManagerOnOnRemoteConnectionState;
        // }

        private void Update()
        {
            _currentIntervalSecs += Time.deltaTime;

            if (_currentIntervalSecs >= updateIntervalSecs)
            {
                _currentIntervalSecs = 0;
                UpdateVisibilityForServer();
            }
        }

        private void UpdateVisibilityForServer()
        {
            var visibleSlots = GetVisibleTargets();
            var ids = new byte[visibleSlots.Count];

            for (var i = 0; i < visibleSlots.Count; i++)
                ids[i] = (byte)visibleSlots[i].IDNumber;

            // InstanceFinder.ClientManager.Broadcast(new PlayerSlotViewInfo
            // {
            //     viewSlotsNumbers = ids
            // });
        }

        public List<SlotMachineInteractable> GetVisibleTargets()
        {
            var visibleTargets = new List<SlotMachineInteractable>();
            var allTargets = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var target in allTargets)
            {
                var viewportPos = Camera.main.WorldToViewportPoint(target.transform.position);

                // Проверка: объект в передней полуплоскости камеры и внутри экрана
                var isVisible = viewportPos is { z: > 0, x: >= 0 and <= 1, y: >= 0 and <= 1 };

                if (isVisible)
                    visibleTargets.Add(target);
            }

            return visibleTargets;
        }

        // private void PlayerSlotViewReceive(NetworkConnection sender, PlayerSlotViewInfo slotViewInfo, Channel channel)
        // {
        //     if (!ServerInfoForPlayerViewSlots.TryAdd(sender, slotViewInfo))
        //         ServerInfoForPlayerViewSlots[sender] = slotViewInfo;
        // }
        //
        // private void ServerManagerOnOnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs stateArgs)
        // {
        //     if (stateArgs.ConnectionState == RemoteConnectionState.Stopped)
        //         ServerInfoForPlayerViewSlots.Remove(conn);
        // }
    }

    [Serializable]
    public struct PlayerSlotViewInfo// : IBroadcast
    {
        public byte[] viewSlotsNumbers;
    }
}