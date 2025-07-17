using System;
using System.Linq;
using Code.Player;
using UnityEngine;

namespace Code.Door
{
    public class AutoDoorOpener : MonoBehaviour
    {
        [SerializeField] private float closeDistance = 10f, fullOpenDistance = 1f;
        [SerializeField] private DoorElementSettings[] doorElementsSettings;

        private float _openDegree;
        private PlayerMovementController _localPlayer;

        private void Update()
        {
            if (!_localPlayer)
                _localPlayer = FindObjectsByType<PlayerMovementController>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.IsOwner);
            if(!_localPlayer)
                return;
            
            var distance = Vector3.Distance(_localPlayer.transform.position, transform.position);
            _openDegree = 1f - Mathf.Clamp01((distance - fullOpenDistance) / (closeDistance - fullOpenDistance));
            UpdateDoorState();
        }

        private void UpdateDoorState()
        {
            foreach (var doorElementsSetting in doorElementsSettings)
                UpdateDoorElement(doorElementsSetting);
        }

        private void UpdateDoorElement(DoorElementSettings doorElementSettings)
        {
            // Интерполируем между закрытым и открытым углом в зависимости от _openDegree
            Vector3 targetRot = Vector3.Lerp(doorElementSettings.closedRot, doorElementSettings.openRot, _openDegree);
            doorElementSettings.doorTransform.localRotation = Quaternion.Euler(targetRot);
        }
    }

    [Serializable]
    public class DoorElementSettings
    {
        public Transform doorTransform; // трансформ двери (обычно сам объект)

        public Vector3 closedRot;
        public Vector3 openRot;
    }
}