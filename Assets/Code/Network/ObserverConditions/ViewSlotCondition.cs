using System;
using System.Linq;
using Code.InteractionSystem;
using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace Code.Network.ObserverConditions
{
    [CreateAssetMenu(menuName = "Nexus/Observers/View Slot Condition", fileName = "View Slot Condition")]
    public class ViewSlotCondition : ObserverCondition
    {
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;

            if (!NetworkObject.TryGetComponent(out SlotMachineInteractable slotMachineInteractable))
                return true;

            if (!ServerPlayerSlotVisibleController.ServerInfoForPlayerViewSlots.TryGetValue(connection, out var info))
                return false;

            return info.viewSlotsNumbers.Contains((byte)slotMachineInteractable.IDNumber);
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Timed;
    }
}