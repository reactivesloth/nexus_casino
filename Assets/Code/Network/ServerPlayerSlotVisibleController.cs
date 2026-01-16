using System;
using System.Collections.Generic;
using Code.Network.InteractionSystem;
using PurrNet;
using PurrNet.Packing;
using UnityEngine;

namespace Code.Network
{
    public class ServerPlayerSlotVisibleController : PurrMonoBehaviour
    {
        [SerializeField] private float updateIntervalSecs = 1f;

        // Словарь на сервере: хранит данные о видимости слотов для каждого игрока
        public static readonly Dictionary<PlayerID, PlayerSlotViewInfo> ServerInfoForPlayerViewSlots = new();

        private float _currentIntervalSecs = 0;
        private Camera _mainCamera;

        private void OnEnable()
        {
            if (InstanceHandler.NetworkManager == null) return;

            // === ЛОГИКА СЕРВЕРА: Слушаем данные от клиентов ===
            if (InstanceHandler.NetworkManager.isServer)
            {
                // Подписка на сообщение от клиента (true = asServer)
                InstanceHandler.NetworkManager.Subscribe<PlayerSlotViewInfo>(PlayerSlotViewReceive, true);
                // Подписка на отключение игрока
                InstanceHandler.NetworkManager.onPlayerLeft += OnPlayerLeft;
            }
        }
        
        private void OnDisable()
        {
            if (InstanceHandler.NetworkManager == null) return;

            if (InstanceHandler.NetworkManager.isServer)
            {
                InstanceHandler.NetworkManager.Unsubscribe<PlayerSlotViewInfo>(PlayerSlotViewReceive, true);
                InstanceHandler.NetworkManager.onPlayerLeft -= OnPlayerLeft;
            }
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            throw new NotImplementedException();
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            throw new NotImplementedException();
        }

        private void Update()
        {
            if (!InstanceHandler.NetworkManager.isClient) return;
            
            _currentIntervalSecs += Time.deltaTime;

            if (_currentIntervalSecs >= updateIntervalSecs)
            {
                _currentIntervalSecs = 0;
                SendVisibilityToServer();
            }
        }

        private void SendVisibilityToServer()
        {
            // Получаем слоты, видимые локальным игроком
            var visibleSlots = GetVisibleTargets();
            var ids = new byte[visibleSlots.Count];

            for (var i = 0; i < visibleSlots.Count; i++)
                ids[i] = (byte)visibleSlots[i].IDNumber;

            // Отправляем серверу
            InstanceHandler.NetworkManager.SendToServer(new PlayerSlotViewInfo
            {
                viewSlotsNumbers = ids
            });
        }

        public List<SlotMachineInteractable> GetVisibleTargets()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            
            var visibleTargets = new List<SlotMachineInteractable>();
            // Лучше кэшировать список слотов, FindObjectsByType медленный в Update
            var allTargets = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var target in allTargets)
            {
                if (_mainCamera == null) continue;

                var viewportPos = _mainCamera.WorldToViewportPoint(target.transform.position);

                // Проверка: объект в передней полуплоскости камеры и внутри экрана
                var isVisible = viewportPos is { z: > 0, x: >= 0 and <= 1, y: >= 0 and <= 1 };

                if (isVisible)
                    visibleTargets.Add(target);
            }

            return visibleTargets;
        }

        // === СЕРВЕР: Обработка получения данных ===
        private void PlayerSlotViewReceive(PlayerID sender, PlayerSlotViewInfo slotViewInfo, bool asServer)
        {
            // Обновляем или добавляем данные
            ServerInfoForPlayerViewSlots[sender] = slotViewInfo;
        }
        
        // === СЕРВЕР: Обработка отключения ===
        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            ServerInfoForPlayerViewSlots.Remove(player);
        }
    }

    [Serializable]
    public struct PlayerSlotViewInfo : IPackedAuto // Добавляем интерфейс для авто-сериализации PurrNet
    {
        public byte[] viewSlotsNumbers;
    }
}
