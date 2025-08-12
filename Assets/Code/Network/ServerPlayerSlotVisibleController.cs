using System;
using System.Collections.Generic;
using Code.InteractionSystem;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network
{
    /// <summary>
    /// Клиент: периодически отправляет на сервер ID видимых SlotMachineInteractable.
    /// Сервер: поддерживает карту "соединение -> список видимых слотов".
    /// </summary>
    public sealed class ServerPlayerSlotVisibleController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float updateIntervalSecs = 1f;

        public static readonly Dictionary<NetworkConnection, PlayerSlotViewInfo> ServerInfoForPlayerViewSlots = new();

        private float _timer;
        private readonly List<SlotMachineInteractable> _visibleTargetsCache = new(32);

        private void OnEnable()
        {
            // Регистрируем серверный обработчик, если сервер существует.
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.RegisterBroadcast<PlayerSlotViewInfo>(OnPlayerSlotViewReceive);
                InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            }
        }

        private void OnDisable()
        {
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.UnregisterBroadcast<PlayerSlotViewInfo>(OnPlayerSlotViewReceive);
                InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }
        }

        private void Update()
        {
            // Работает только на клиенте: отправляем серверу свою видимость.
            var cm = InstanceFinder.ClientManager;
            if (cm == null || !cm.Started || InstanceFinder.IsServerStarted) // не слать с хоста-сервера
                return;

            _timer += Time.deltaTime;
            if (_timer < updateIntervalSecs)
                return;

            _timer = 0f;
            SendVisibilityToServer();
        }

        private void SendVisibilityToServer()
        {
            var cm = InstanceFinder.ClientManager;
            if (cm == null || !cm.Started)
                return;

            var visibleSlots = GetVisibleTargets(_visibleTargetsCache);
            int count = visibleSlots.Count;
            if (count == 0)
            {
                // Отправим пустой список — сервер воспримет как «ничего не видно».
                cm.Broadcast(new PlayerSlotViewInfo { viewSlotsNumbers = Array.Empty<byte>() });
                return;
            }

            // Собираем компактный массив ID (byte).
            var ids = new byte[count];
            for (int i = 0; i < count; i++)
            {
                var s = visibleSlots[i];
                ids[i] = (byte)(s != null ? s.IDNumber : 0);
            }

            cm.Broadcast(new PlayerSlotViewInfo { viewSlotsNumbers = ids });
        }

        /// <summary>
        /// Собирает видимые цели в переданный список; без LINQ, с нулевыми аллокациями.
        /// </summary>
        private static List<SlotMachineInteractable> GetVisibleTargets(List<SlotMachineInteractable> buffer)
        {
            buffer.Clear();

            var cam = Camera.main;
            if (cam == null)
                return buffer;

            // FindObjectsByType с IncludeInactive, как было — но не каждый кадр, а по интервалу.
            var allTargets = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allTargets == null || allTargets.Length == 0)
                return buffer;

            for (int i = 0; i < allTargets.Length; i++)
            {
                var t = allTargets[i];
                if (t == null) continue;

                Vector3 vp = cam.WorldToViewportPoint(t.transform.position);
                // Внутри фрустума и перед камерой.
                if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    buffer.Add(t);
            }

            return buffer;
        }

        // === Серверная часть ===

        private static void OnPlayerSlotViewReceive(NetworkConnection sender, PlayerSlotViewInfo slotViewInfo, Channel channel)
        {
            if (sender == null)
                return;

            // Обновляем или добавляем.
            if (!ServerInfoForPlayerViewSlots.TryAdd(sender, slotViewInfo))
                ServerInfoForPlayerViewSlots[sender] = slotViewInfo;
        }

        private static void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs stateArgs)
        {
            if (stateArgs.ConnectionState == RemoteConnectionState.Stopped && conn != null)
                ServerInfoForPlayerViewSlots.Remove(conn);
        }
    }

    [Serializable]
    public struct PlayerSlotViewInfo : IBroadcast
    {
        public byte[] viewSlotsNumbers;
    }
}
