using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

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

        [Tooltip("0 - Sit in place (back to sit, stand in entry point)\n" +
                 "1 - Sit with turn in place (front to sit, stand in entry point)\n" +
                 "2 - Sit from back-left\n" +
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

        private void Awake() => SetupSitPoints();

        private void OnDisable()
        {
            if (_sitRoutine != null)
            {
                StopCoroutine(_sitRoutine);
                _sitRoutine = null;
            }

            _isSitting = false;
        }

        private void SetupSitPoints()
        {
            if (sitPoint == null)
            {
                var t = transform.Find("SitPoint");
                if (t != null) sitPoint = t;
            }
        }

        private void Update()
        {
            if (_isSitting && allowRotateCamera && useRightMouseButtonToRotate)
            {
                var cm = CursorManager.Instance;
                if (cm != null) cm.ShowCursor();
            }
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
            TargetToggleSit(conn, true);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            base.OnEndInteract(conn);
            TargetToggleSit(conn, false);
        }

        [TargetRpc]
        private void TargetToggleSit(NetworkConnection conn, bool isSitdown)
        {
            // Ищем локального PlayerMovementController без LINQ.First
            Player.PlayerMovementController movement = null;
            var all = FindObjectsOfType<Player.PlayerMovementController>();
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m != null && m.Owner.IsLocalClient)
                {
                    movement = m;
                    break;
                }
            }

            if (movement == null) return;

            var cc = movement.GetComponent<CharacterController>();
            var anim = movement.GetComponent<Animator>();
            var tf = movement.transform;

            if (_sitRoutine != null) StopCoroutine(_sitRoutine);
            
            _sitRoutine = StartCoroutine(
                isSitdown
                    ? SitDownFlow(movement, anim, cc, tf)
                    : StandUpFlow(movement, anim, cc, tf)
            );
        }

        private IEnumerator SitDownFlow(Player.PlayerMovementController move, Animator anim, CharacterController cc,
            Transform tf)
        {
            IsBusy = true;

            move.SuppressLookAtIK = true;

            _savedPos = tf.position;
            _savedRot = tf.rotation;

            if (cc != null) cc.enabled = false;
            move.CanMove = false;

            _selectedEntry = FindClosestEntryPoint(tf.position);
            if (_selectedEntry == null)
            {
                Debug.LogWarning("No entry point found");
                IsBusy = false;
                yield break;
            }

            var entryPoint = _selectedEntry.entryPoint;
            
            yield return RotateTowardPointIfNeeded(tf, entryPoint.position);
            yield return MoveToPoint(tf, entryPoint.position, anim);
            yield return RotateToTarget(tf, entryPoint.rotation);

            if (anim != null)
            {
                anim.applyRootMotion = true;
                int style = 0;
                int.TryParse(_selectedEntry.animationID, out style);
                anim.SetFloat(SIT_STYLE, style);
                anim.SetBool(SIT_TRIGGER, true);
            }

            if (anim != null)
            {
                yield return new WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).IsName(SIT_STATE));
                yield return null;
            }

            float duration = anim != null ? anim.GetAnimatorTransitionInfo(0).duration : 0.25f;
            float elapsed = 0f;

            Vector3 startPos = tf.position;
            Quaternion startRot = tf.rotation;

            float footOffset = ComputeFootOffset(anim, tf, sitPoint);
            Vector3 targetPos = (sitPoint != null ? sitPoint.position : tf.position) + Vector3.up * footOffset +
                                Vector3.up * sitAdjustHeight;
            Quaternion targetRot = sitPoint != null ? sitPoint.rotation : tf.rotation;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = (duration > 0.0001f) ? Mathf.Clamp01(elapsed / duration) : 1f;
                tf.position = Vector3.Lerp(startPos, targetPos, t);
                tf.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            tf.position = targetPos;
            tf.rotation = targetRot;

            if (anim != null) anim.applyRootMotion = false;
            _sitRoutine = null;
            _isSitting = true;
            move.SuppressLookAtIK = !move.FirstPersonView;

            // ВКЛЮЧАЕМ ОГРАНИЧЕНИЯ ТОЛЬКО ТЕПЕРЬ (после посадки!)
            if (allowRotateCamera)
            {
                move.LookCameraLimitRotation = true;
                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = true;
                    move.LockCursor = false; // курсор видим, поворот — только при зажатой ПКМ
                }
            }

            move.sitBaseYaw = move.cinemachineTargetYaw;
            move.sitBasePitch = move.cinemachineTargetPitch;

            if (forceFPV)
                move.ForceEnterFPV(true, snap: true);

            IsBusy = false;
        }

        private IEnumerator StandUpFlow(Player.PlayerMovementController move, Animator anim, CharacterController cc,
            Transform tf)
        {
            move.SuppressLookAtIK = true;

            if (allowRotateCamera)
            {
                move.LookCameraLimitRotation = false;
                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = false;
                    move.LockCursor = true;
                }

                float preservedPitch = 0f;
                float preservedYaw = move.cinemachineTargetYaw;
                var camT = move.CinemachineCameraTarget.transform;
                camT.rotation = Quaternion.Euler(
                    preservedPitch + move.cameraAngleOverride,
                    preservedYaw,
                    0f);
            }

            IsBusy = true;
            _isSitting = false;

            if (anim != null)
            {
                anim.applyRootMotion = true;
                int style = 0;
                int.TryParse(_selectedEntry != null ? _selectedEntry.animationID : "0", out style);
                anim.SetFloat(SIT_STYLE, style);
                anim.SetBool(SIT_TRIGGER, false);
            }

            float duration = anim != null ? anim.GetAnimatorTransitionInfo(0).duration : 0.25f;
            float elapsed = 0f;

            Vector3 startPos = tf.position;
            Quaternion startRot = tf.rotation;

            Vector3 targetPos = _selectedEntry != null && _selectedEntry.entryPoint != null
                ? _selectedEntry.entryPoint.position
                : _savedPos;
            Quaternion targetRot = _selectedEntry != null && _selectedEntry.entryPoint != null
                ? _selectedEntry.entryPoint.rotation
                : _savedRot;

            float footOffset = ComputeFootOffset(anim, tf, _selectedEntry != null ? _selectedEntry.entryPoint : tf);
            targetPos += Vector3.up * footOffset;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = (duration > 0.0001f) ? Mathf.Clamp01(elapsed / duration) : 1f;
                tf.position = Vector3.Lerp(startPos, targetPos, t);
                tf.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            if (anim != null)
                yield return new WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).IsName(STAND_STATE));

            if (anim != null) anim.applyRootMotion = false;
            if (cc != null) cc.enabled = true;
            move.CanMove = true;

            _sitRoutine = null;
            IsBusy = false;
            move.SuppressLookAtIK = !move.FirstPersonView;

            // после восстановления контроллера и движения
            move.SnapAimToCurrentCamera(); // выравниваем таргеты под текущую камеру
            move.BeginIkGrace(0.2f); // 200 мс без IK, чтобы камера «встала» стабильно

            // завершаем флаги RMB-режима
            if (allowRotateCamera)
            {
                move.LookCameraLimitRotation = false;
                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = false;
                    move.LockCursor = true;
                    if (CursorManager.Instance != null)
                        CursorManager.Instance.HideCursor();
                }
            }

            // финальный статус подавления IK: в FPV разрешаем, в 3Л — выключено
            move.SuppressLookAtIK = !move.FirstPersonView;
        }

        private EntryData FindClosestEntryPoint(Vector3 from)
        {
            if (entries == null || entries.Length == 0) return null;

            float minDist = float.MaxValue;
            EntryData closest = null;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.entryPoint == null) continue;
                float d = Vector3.Distance(from, e.entryPoint.position);
                if (d < minDist)
                {
                    minDist = d;
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

            float vertical = 0f, horizontal = 0f;

            while (Vector3.Distance(tf.position, targetPos) > stopDistance && elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;

                Vector3 dir = targetPos - tf.position;
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

                if (anim != null)
                {
                    anim.SetFloat("Vertical", vertical);
                    anim.SetFloat("Horizontal", horizontal);
                }

                yield return null;
            }

            tf.position = targetPos;

            if (anim != null)
            {
                anim.SetFloat("Vertical", 0f);
                anim.SetFloat("Horizontal", 0f);
            }
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
            Vector3 toTarget = targetPosition - tf.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) yield break;

            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
            float angle = Quaternion.Angle(tf.rotation, targetRotation);
            if (angle < angleThreshold) yield break;

            while (Quaternion.Angle(tf.rotation, targetRotation) > 0.5f)
            {
                tf.rotation = Quaternion.RotateTowards(tf.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                yield return null;
            }

            tf.rotation = targetRotation;
        }

        private float ComputeFootOffset(Animator animator, Transform playerTf, Transform refPoint)
        {
            if (animator == null || playerTf == null || refPoint == null) return 0f;

            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (!foot) return 0f;

            Vector3 footWorld = foot.position;
            float localFootY = playerTf.InverseTransformPoint(footWorld).y;
            float localRefY = playerTf.InverseTransformPoint(refPoint.position).y;
            return localRefY - localFootY;
        }
    }
}