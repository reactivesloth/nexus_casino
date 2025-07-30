using UnityEngine;
using System.Collections;
using System.Linq;
using FishNet.Object;
using FishNet.Connection;
using Code.Player;
using FishNet.Component.Animating;
using FishNet.Object.Synchronizing;

namespace Code.InteractionSystem
{
    public class SitInteractable : Interactable
    {
        [System.Serializable]
        public class EntryData
        {
            public Transform entryPoint;
            public string animationID;
        }

        [Header("Sit Settings")] [SerializeField]
        private Transform sitPoint;

        [SerializeField] private float sitAdjustHeight = 0.0f;
        [SerializeField] private bool allowRotateCamera = true;
        [SerializeField] private bool useRightMouseButtonToRotate = false;

        [Tooltip("0 - Sit in place (back to sit, stand in entry point)" +
                 "1 - Sit with turn in place (front to sit, stand in entry point)" +
                 "2 - Sit from back-left" +
                 "3 - Sit with back-right")]
        [SerializeField]
        private EntryData[] entries;

        [SerializeField] private bool forceFPV;

        const string SIT_TRIGGER = "TriggerSit";
        const string SIT_STATE = "Sitting";
        const string SIT_STYLE = "SitStyle";
        const string STAND_STATE = "Movement";

        private bool _isSitting;
        private Coroutine _sitRoutine;
        private Vector3 _savedPos;
        private Quaternion _savedRot;
        private EntryData _selectedEntry;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetupSitPoints();
        }
#endif

        private void Awake()
        {
            SetupSitPoints();
        }

        private void SetupSitPoints()
        {
            sitPoint ??= transform.Find("SitPoint");
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isSitting = false;
            _sitRoutine = null;
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            base.OnInteract(conn);
            TargetToggleSit(conn);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            base.OnEndInteract(conn);
            TargetToggleSit(conn);
        }

        [TargetRpc]
        private void TargetToggleSit(NetworkConnection conn)
        {
            var movement = FindObjectsOfType<PlayerMovementController>()
                .First(m => m.Owner.IsLocalClient);
            var cc = movement.GetComponent<CharacterController>();
            var anim = movement.GetComponent<Animator>();
            var tf = movement.transform;

            if (_sitRoutine != null)
                StopCoroutine(_sitRoutine);

            _sitRoutine = StartCoroutine(
                _isSitting
                    ? StandUpFlow(movement, anim, cc, tf)
                    : SitDownFlow(movement, anim, cc, tf)
            );
        }

        private IEnumerator SitDownFlow(PlayerMovementController move, Animator anim,
            CharacterController cc, Transform tf)
        {
            IsBusy = true;

            if (forceFPV)
                move.ForceSetCameraDistance(0);

            _savedPos = tf.position;
            _savedRot = tf.rotation;

            cc.enabled = false;
            move.CanMove = false;

            _selectedEntry = FindClosestEntryPoint(tf.position);
            if (_selectedEntry == null)
            {
                Debug.LogWarning("No entry point found");
                yield break;
            }

            var entryPoint = _selectedEntry.entryPoint;

            yield return StartCoroutine(RotateTowardPointIfNeeded(tf, entryPoint.position));
            yield return StartCoroutine(MoveToPoint(tf, entryPoint.position, anim));
            yield return StartCoroutine(RotateToTarget(tf, entryPoint.rotation));

            anim.applyRootMotion = true;
            anim.SetFloat(SIT_STYLE, int.Parse(_selectedEntry.animationID));
            anim.SetBool(SIT_TRIGGER, true);

            yield return new WaitUntil(() =>
                anim.GetCurrentAnimatorStateInfo(0).IsName(SIT_STATE)
            );

            yield return null;

            float duration = anim.GetAnimatorTransitionInfo(0).duration;
            float elapsed = 0f;

            Vector3 startPos = tf.position;
            Quaternion startRot = tf.rotation;

            float footOffset = ComputeFootOffset(anim, tf, sitPoint);
            Vector3 targetPos = sitPoint.position + Vector3.up * footOffset + Vector3.up * sitAdjustHeight;
            Quaternion targetRot = sitPoint.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tf.position = Vector3.Lerp(startPos, targetPos, t);
                tf.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            tf.position = targetPos;
            tf.rotation = targetRot;

            anim.applyRootMotion = false;
            _sitRoutine = null;
            _isSitting = true;

            if (allowRotateCamera)
            {
                move.LookCameraLimitRotation = true;
                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = true;
                    move.LockCursor = false;
                }
            }

            move.sitBaseYaw = move.cinemachineTargetYaw;
            move.sitBasePitch = move.cinemachineTargetPitch;

            IsBusy = false;
        }

        private IEnumerator StandUpFlow(PlayerMovementController move, Animator anim,
            CharacterController cc, Transform tf)
        {
            if (allowRotateCamera) 
            {
                // отключаем сидячие ограничения
                move.LookCameraLimitRotation = false; 
                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = false;
                    move.LockCursor = true;
                }
                
                float preservedPitch = 0;
                float preservedYaw   = move.cinemachineTargetYaw;
                var camT = move.CinemachineCameraTarget.transform;
                camT.rotation = Quaternion.Euler(
                    preservedPitch + move.cameraAngleOverride,
                    preservedYaw,
                    0f
                    );
            }
            
            IsBusy = true;
            _isSitting = false;

            anim.applyRootMotion = true;
            anim.SetFloat(SIT_STYLE, int.Parse(_selectedEntry.animationID));
            anim.SetBool(SIT_TRIGGER, false);

            float duration = anim.GetAnimatorTransitionInfo(0).duration;
            float elapsed = 0f;

            Vector3 startPos = tf.position;
            Quaternion startRot = tf.rotation;
            Vector3 targetPos = _selectedEntry.entryPoint.position;
            Quaternion targetRot = _selectedEntry.entryPoint.rotation;

            float footOffset = ComputeFootOffset(anim, tf, _selectedEntry.entryPoint);
            targetPos += Vector3.up * footOffset;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tf.position = Vector3.Lerp(startPos, targetPos, t);
                tf.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            yield return new WaitUntil(() =>
                anim.GetCurrentAnimatorStateInfo(0).IsName(STAND_STATE)
            );

            anim.applyRootMotion = false;

            cc.enabled = true;
            move.CanMove = true;
            
            _sitRoutine = null;
            IsBusy = false;
        }

        private EntryData FindClosestEntryPoint(Vector3 from)
        {
            if (entries == null || entries.Length == 0) return null;

            float minDist = float.MaxValue;
            EntryData closest = null;

            foreach (var e in entries)
            {
                if (e.entryPoint == null) continue;

                float dist = Vector3.Distance(from, e.entryPoint.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = e;
                }
            }

            return closest;
        }

        private IEnumerator MoveToPoint(Transform tf, Vector3 targetPos, Animator anim, float stopDistance = 0.25f,
            float maxDuration = 2f)
        {
            float walkSpeed = 1.5f;
            float animBlendSpeed = 8f;
            float elapsed = 0f;

            float vertical = 0f;
            float horizontal = 0f;

            while (Vector3.Distance(tf.position, targetPos) > stopDistance && elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;

                Vector3 dir = (targetPos - tf.position);
                dir.y = 0f;
                float distance = dir.magnitude;

                if (distance > 0.001f)
                {
                    dir.Normalize();
                    tf.position += dir * walkSpeed * Time.deltaTime;

                    vertical = Mathf.MoveTowards(vertical, 1f, animBlendSpeed * Time.deltaTime);
                    horizontal = Mathf.MoveTowards(horizontal, 0f, animBlendSpeed * Time.deltaTime);
                }
                else
                {
                    vertical = Mathf.MoveTowards(vertical, 0f, animBlendSpeed * Time.deltaTime);
                    horizontal = Mathf.MoveTowards(horizontal, 0f, animBlendSpeed * Time.deltaTime);
                }

                anim.SetFloat("Vertical", vertical);
                anim.SetFloat("Horizontal", horizontal);

                yield return null;
            }

            tf.position = targetPos;

            anim.SetFloat("Vertical", 0f);
            anim.SetFloat("Horizontal", 0f);
        }

        private IEnumerator RotateToTarget(Transform tf, Quaternion targetRot, float rotationSpeed = 360f,
            float maxDuration = 1f)
        {
            float elapsed = 0f;

            while (Quaternion.Angle(tf.rotation, targetRot) > 0.5f && elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;
                tf.rotation = Quaternion.RotateTowards(tf.rotation, targetRot, rotationSpeed * Time.deltaTime);
                yield return null;
            }

            tf.rotation = targetRot;
        }

        private IEnumerator RotateTowardPointIfNeeded(Transform tf, Vector3 targetPosition, float angleThreshold = 15f,
            float rotationSpeed = 360f)
        {
            Vector3 toTarget = (targetPosition - tf.position);
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f)
                yield break;

            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
            float angle = Quaternion.Angle(tf.rotation, targetRotation);

            if (angle < angleThreshold)
                yield break;

            while (Quaternion.Angle(tf.rotation, targetRotation) > 0.5f)
            {
                tf.rotation = Quaternion.RotateTowards(tf.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                yield return null;
            }

            tf.rotation = targetRotation;
        }

        private float ComputeFootOffset(Animator animator, Transform playerTf, Transform sitPoint)
        {
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (!foot) return 0f;

            Vector3 footWorld = foot.position;
            float localFootY = playerTf.InverseTransformPoint(footWorld).y;
            float localSitY = playerTf.InverseTransformPoint(sitPoint.position).y;
            return localSitY - localFootY;
        }
    }
}