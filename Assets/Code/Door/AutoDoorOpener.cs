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

        [SerializeField] private AnimationCurve openCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private AnimationCurve closeCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField, Tooltip("Чувствительность смены направления (0.01-0.1)")]
        private float sensitivity = 0.05f;
        [SerializeField, Tooltip("Время анимации смены направления (сек)")]
        private float animationDuration = 0.5f;

        private float _openDegree;
        private float _prevOpenDegree;
        private float _currentCurveDegree;
        private float _curveDegreeVelocity;
        private float _accumulatedDelta;
        private int _prevDirection; // 1 - открытие, -1 - закрытие, 0 - не определено
        private PlayerMovementController _localPlayer;
        private AnimationCurve _currentCurve;

        private void Update()
        {
            if (!_localPlayer)
                _localPlayer = FindObjectsByType<PlayerMovementController>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.IsOwner);
            if(!_localPlayer)
                return;
            
            var distance = Vector3.Distance(_localPlayer.transform.position, transform.position);
            _prevOpenDegree = _openDegree;
            _openDegree = 1f - Mathf.Clamp01((distance - fullOpenDistance) / (closeDistance - fullOpenDistance));
            int direction = Math.Sign(_openDegree - _prevOpenDegree);
            if (_prevDirection != 0 && direction != 0 && direction != _prevDirection)
            {
                // Смена направления — накапливаем дельту
                _accumulatedDelta += _openDegree - _prevOpenDegree;
                if (Mathf.Abs(_accumulatedDelta) < sensitivity)
                    return;
                // Превысили чувствительность — разрешаем смену направления
                _prevDirection = direction;
                _accumulatedDelta = 0f;
            }
            else
            {
                // Нет смены направления — реагируем сразу
                _prevDirection = direction;
                _accumulatedDelta = 0f;
            }
            UpdateDoorState();
        }

        private void UpdateDoorState()
        {
            foreach (var doorElementsSetting in doorElementsSettings)
                UpdateDoorElement(doorElementsSetting);
        }

        private void UpdateDoorElement(DoorElementSettings doorElementSettings)
        {
            if (_openDegree > _prevOpenDegree)
                _currentCurve = openCurve;
            else if(_openDegree < _prevOpenDegree)
                _currentCurve = closeCurve;
            
            if(_currentCurve == null)
                return;
            
            float targetCurveValue = _currentCurve.Evaluate(_openDegree);
            _currentCurveDegree = Mathf.MoveTowards(_currentCurveDegree, targetCurveValue, Time.deltaTime / animationDuration);
            Vector3 targetRot = Vector3.Lerp(doorElementSettings.closedRot, doorElementSettings.openRot, _currentCurveDegree);
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