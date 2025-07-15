using System;
using UnityEngine;
using System.Collections;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;

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
        
        protected readonly SyncVar<float> _openDegree = new (new SyncTypeSettings()
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });


        private void Awake()
        {
            if (doorTransform == null)
                doorTransform = transform;
        }

        private void OnEnable()
        {
            _openDegree.OnChange += OpenDegreeOnOnChange;
        }

        private void OnDisable()
        {
            _openDegree.OnChange -= OpenDegreeOnOnChange;
        }

        private void OpenDegreeOnOnChange(float prev, float next, bool asServer)
        {
            doorTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(ClosedRot), Quaternion.Euler(OpenRot), next);
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            // Отметить занятость и кикнуть клиентский RPC
            base.OnInteract(conn);
            ToggleDoor();
        }

        private void ToggleDoor()
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
                _openDegree.Value = t;
                yield return null;
            }
            _doorRoutine = null;
        }

        private IEnumerator CloseDoorFlow()
        {
            _isOpen = false;
            float t = 1f;
            while (t >= 0f)
            {
                t -= Time.deltaTime * animationSpeed;
                _openDegree.Value = t;
                yield return null;
            }
            _doorRoutine = null;
        }
    }
}
