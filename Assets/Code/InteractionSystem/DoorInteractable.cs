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
        private Transform doorTransform; // трансформ двери (обычно сам объект)

        public Vector3 ClosedRot;
        public Vector3 OpenRot;

        [SerializeField, Tooltip("Скорость анимации открытия/закрытия")]
        private float animationSpeed = 2f;

        private bool _isOpen;
        private Coroutine _doorRoutine;
        private Coroutine _interpolationRoutine;

        private readonly SyncVar<float> _openDegree = new(new SyncTypeSettings
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
            if (_interpolationRoutine != null)
                StopCoroutine(_interpolationRoutine);

            _interpolationRoutine = StartCoroutine(InterpolateRotation(prev, next));
        }

        private IEnumerator InterpolateRotation(float from, float to)
        {
            var t = 0f;
            var duration = Mathf.Abs(to - from) / animationSpeed;
            var startRot = Quaternion.Slerp(Quaternion.Euler(ClosedRot), Quaternion.Euler(OpenRot), from);
            var endRot = Quaternion.Slerp(Quaternion.Euler(ClosedRot), Quaternion.Euler(OpenRot), to);

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                doorTransform.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            doorTransform.localRotation = endRot;
            _interpolationRoutine = null;
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            base.OnInteract(conn);
            ToggleDoor();
        }

        private void ToggleDoor()
        {
            if (_doorRoutine != null)
                StopCoroutine(_doorRoutine);

            _doorRoutine = StartCoroutine(_isOpen ? CloseDoorFlow() : OpenDoorFlow());
        }

        private IEnumerator OpenDoorFlow()
        {
            _isOpen = true;
            var t = _openDegree.Value;

            while (t < 1f)
            {
                t += Time.deltaTime * animationSpeed;
                _openDegree.Value = Mathf.Clamp01(t);
                yield return null;
            }

            _doorRoutine = null;
        }

        private IEnumerator CloseDoorFlow()
        {
            _isOpen = false;
            var t = _openDegree.Value;

            while (t > 0f)
            {
                t -= Time.deltaTime * animationSpeed;
                _openDegree.Value = Mathf.Clamp01(t);
                yield return null;
            }

            _doorRoutine = null;
        }
    }
}