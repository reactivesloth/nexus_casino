using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace Code.Network.ObserversConditions
{
    [CreateAssetMenu(menuName = "FishNet/Observers/Camera View Condition", fileName = "CameraViewCondition")]
    public class CameraViewCondition : ObserverCondition
    {
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            return true;
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Timed;
    }
}