using UnityEngine;
using System.Collections;
using FishNet.Object;
using FishNet.Connection;

namespace Code.InteractionSystem
{
    public class DoorInteractable : Interactable
    {
        [Header("Scripted Door Settings")]
        [SerializeField]
        private Transform doorTransform;    // трансформ двери (обычно сам объект)
        // Запоминаем исходную и целевую ротации
        public Vector3 ClosedRot;
        public Vector3 OpenRot;
        
        [SerializeField, Tooltip("Скорость анимации открытия/закрытия")]
        private float animationSpeed = 2f;

        private bool _isOpen;
        private Coroutine _doorRoutine;


        private void Awake()
        {
            if (doorTransform == null)
                doorTransform = transform;
        }

        protected override void OnInteract(NetworkConnection conn)
        {
            // Отметить занятость и кикнуть клиентский RPC
            base.OnInteract(conn);
            TargetToggleDoor(conn);
        }

        [TargetRpc]
        private void TargetToggleDoor(NetworkConnection connection)
        {
            // прервать текущую анимацию, если есть
            if (_doorRoutine != null)
                StopCoroutine(_doorRoutine);

            _doorRoutine = StartCoroutine(
                _isOpen
                    ? CloseDoorFlow()
                    : OpenDoorFlow()
            );
        }

        private IEnumerator OpenDoorFlow()
        {
            _isOpen = true;
            float t = 0f;
            // Линейно увеличиваем t от 0 до 1
            while (t < 1f)
            {
                t += Time.deltaTime * animationSpeed;
                doorTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(ClosedRot), Quaternion.Euler(OpenRot), t);
                yield return null;
            }
            _doorRoutine = null;
        }

        private IEnumerator CloseDoorFlow()
        {
            _isOpen = false;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * animationSpeed;
                doorTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(OpenRot), Quaternion.Euler(ClosedRot), t);
                yield return null;
            }
            _doorRoutine = null;
        }
    }
}
