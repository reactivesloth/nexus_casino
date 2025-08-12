using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;

namespace Code.InteractionSystem
{
    public class DoorInteractable : Interactable
    {
        [Header("Scripted Door Settings")]
        [SerializeField] private Transform doorTransform;

        public Vector3 ClosedRot;
        public Vector3 OpenRot;

        [SerializeField, Tooltip("Скорость анимации открытия/закрытия (доля в секунду)")]
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
            _openDegree.OnChange += OpenDegree_OnChange;
        }

        private void OnDisable()
        {
            _openDegree.OnChange -= OpenDegree_OnChange;
            if (_interpolationRoutine != null) { StopCoroutine(_interpolationRoutine); _interpolationRoutine = null; }
            if (_doorRoutine != null) { StopCoroutine(_doorRoutine); _doorRoutine = null; }
        }

        private void OpenDegree_OnChange(float prev, float next, bool asServer)
        {
            if (_interpolationRoutine != null)
                StopCoroutine(_interpolationRoutine);
            _interpolationRoutine = StartCoroutine(InterpolateRotation(prev, next));
        }

        private IEnumerator InterpolateRotation(float from, float to)
        {
            float delta = Mathf.Abs(to - from);
            float duration = (animationSpeed > 0f) ? (delta / animationSpeed) : 0f;

            Quaternion startRot = Quaternion.Slerp(Quaternion.Euler(ClosedRot), Quaternion.Euler(OpenRot), from);
            Quaternion endRot   = Quaternion.Slerp(Quaternion.Euler(ClosedRot), Quaternion.Euler(OpenRot), to);

            if (duration <= 0.0001f)
            {
                if (doorTransform != null) doorTransform.localRotation = endRot;
                _interpolationRoutine = null;
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                if (doorTransform != null)
                    doorTransform.localRotation = Quaternion.Slerp(startRot, endRot, Mathf.Clamp01(t));
                yield return null;
            }

            if (doorTransform != null) doorTransform.localRotation = endRot;
            _interpolationRoutine = null;
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            base.OnInteract(conn);
            ToggleDoor();
        }

        private void ToggleDoor()
        {
            if (_doorRoutine != null) StopCoroutine(_doorRoutine);
            _doorRoutine = StartCoroutine(_isOpen ? CloseDoorFlow() : OpenDoorFlow());
        }

        private IEnumerator OpenDoorFlow()
        {
            _isOpen = true;
            float t = _openDegree.Value;

            while (t < 1f)
            {
                t += Time.deltaTime * Mathf.Max(0.0001f, animationSpeed);
                _openDegree.Value = Mathf.Clamp01(t);
                yield return null;
            }
            _doorRoutine = null;
        }

        private IEnumerator CloseDoorFlow()
        {
            _isOpen = false;
            float t = _openDegree.Value;

            while (t > 0f)
            {
                t -= Time.deltaTime * Mathf.Max(0.0001f, animationSpeed);
                _openDegree.Value = Mathf.Clamp01(t);
                yield return null;
            }
            _doorRoutine = null;
        }
    }
}
