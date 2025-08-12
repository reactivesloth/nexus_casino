using System;
using Code.InteractionSystem;
using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace Code.Network.ObserverConditions
{
    /// <summary>
    /// Без LINQ/аллоцирующих Contains; аккуратные null-чекаи.
    /// Возвращает true, если для данного слота игрок сообщил видимость (по кэшу на сервере).
    /// </summary>
    [CreateAssetMenu(menuName = "Nexus/Observers/View Slot Condition", fileName = "View Slot Condition")]
    public sealed class ViewSlotCondition : ObserverCondition
    {
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;

            if (connection == null)
                return true; // не блокируем наблюдение по отсутствующему коннекту

            if (!NetworkObject)
                return true;

            SlotMachineInteractable slot;
            if (!NetworkObject.TryGetComponent(out slot) || slot == null)
                return true; // нет компонента — не ограничиваем

            PlayerSlotViewInfo info;
            if (!ServerPlayerSlotVisibleController.ServerInfoForPlayerViewSlots.TryGetValue(connection, out info)
                || info.viewSlotsNumbers == null)
                return false;

            // Без LINQ: обычный цикл
            var id = (byte)slot.IDNumber;
            var arr = info.viewSlotsNumbers;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == id)
                    return true;
            }

            return false;
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Timed;
    }
}