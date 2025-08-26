using System.Collections;
using Code.Player;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
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
        [SerializeField] private bool forceFPV;

        [Tooltip("0 - Sit in place (back to sit, stand in entry point)\n" +
                 "1 - Sit with turn in place (front to sit, stand in entry point)\n" +
                 "2 - Sit from back-left\n" +
                 "3 - Sit with back-right")]
        [SerializeField]
        private EntryData[] entries = System.Array.Empty<EntryData>();

        private const string SIT_TRIGGER = "TriggerSit";
        private const string SIT_STATE = "Sitting";
        private const string SIT_STYLE = "SitStyle";
        private const string STAND_STATE = "Movement";

        private readonly SyncVar<bool> _isSittingNet = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        private readonly SyncVar<int> _entryIndexNet = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        private bool _isSittingLocal;
        private Coroutine _sitRoutine;
        private Vector3 _savedPos;
        private Quaternion _savedRot;

        private bool _initSit;
        private bool _didInitialApply;

        private void EnsureInit()
        {
            if (_initSit) return;
            _initSit = true;
            if (sitPoint == null)
            {
                var t = transform.Find("SitPoint");
                if (t != null) sitPoint = t;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (entries == null) entries = System.Array.Empty<EntryData>();
            if (sitPoint == null)
            {
                var t = transform.Find("SitPoint");
                if (t != null) sitPoint = t;
            }
        }
#endif

        private void Awake() => EnsureInit();

        private void OnEnable()
        {
            EnsureInit();
            _isSittingNet.OnChange += OnIsSittingChanged;
            _entryIndexNet.OnChange += OnEntryIndexChanged;
        }

        private void OnDisable()
        {
            _isSittingNet.OnChange -= OnIsSittingChanged;
            _entryIndexNet.OnChange -= OnEntryIndexChanged;

            if (_sitRoutine != null)
            {
                StopCoroutine(_sitRoutine);
                _sitRoutine = null;
            }

            _isSittingLocal = false;
            _didInitialApply = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureInit();
            if (!_didInitialApply)
            {
                ApplySitStateImmediate(_isSittingNet.Value, _entryIndexNet.Value);
                _didInitialApply = true;
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isSittingLocal = false;
            _sitRoutine = null;
            _didInitialApply = false;
        }

        private void Update()
        {
            if (_isSittingLocal && allowRotateCamera && useRightMouseButtonToRotate)
            {
                var cm = CursorManager.Instance;
                if (cm != null) cm.ShowCursor();
            }
        }

        protected internal override void OnInteract(NetworkConnection conn, bool force)
        {
            base.OnInteract(conn, force);
            if (!IsServer) return;

            // Выбор входа и установка истины
            int idx = PickEntryIndexFor(conn);
            _entryIndexNet.Value = idx;

            if (!_isSittingNet.Value)
                _isSittingNet.Value = true;

            // Плавный визуал на владельце
            if (IsOwner) force = false;
            
            TargetToggleSit(conn, true, force);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            // Порядок: истина/визуал -> базовый End
            if (!IsServer)
            {
                base.OnEndInteract(conn);
                return;
            }

            if (_isSittingNet.Value)
            {
                _isSittingNet.Value = false;
                TargetToggleSit(conn, false);
            }

            base.OnEndInteract(conn);
        }

        private void OnIsSittingChanged(bool prev, bool next, bool asServer)
        {
            EnsureInit();
            // runtime — не форсим мгновенно, чтобы не рвать переходы
            if (!_didInitialApply) return;
            if (_sitRoutine != null) return;
        }

        private void OnEntryIndexChanged(int prev, int next, bool asServer)
        {
            EnsureInit();
            if (!_didInitialApply) return;
            if (_sitRoutine != null) return;
        }

        private void ApplySitStateImmediate(bool sit, int entryIndex)
        {
            if (!IsOwner) return;
            var move = FindLocalOwnerMovement();
            if (move == null) return;

            var cc = move.GetComponent<CharacterController>();
            var anim = move.GetComponent<Animator>();
            var tf = move.transform;

            var entry = GetEntry(entryIndex) ?? FindClosestEntryPoint(tf.position) ?? GetEntry(0);

            if (sit)
            {
                _savedPos = tf.position;
                _savedRot = tf.rotation;
                ForceSit(move, anim, cc, tf, entry);
                _isSittingLocal = true;
            }
            else
            {
                ForceStand(move, anim, cc, tf, entry);
                _isSittingLocal = false;
            }
        }

        [TargetRpc]
        private void TargetToggleSit(NetworkConnection conn, bool isSitDown, bool isForce = false)
        {
            var move = FindLocalOwnerMovement();
            if (move == null) return;

            var cc = move.GetComponent<CharacterController>();
            var anim = move.GetComponent<Animator>();
            var tf = move.transform;

            if (_sitRoutine != null) StopCoroutine(_sitRoutine);

            var entry = GetEntry(_entryIndexNet.Value) ?? FindClosestEntryPoint(tf.position) ?? GetEntry(0);

            if (isSitDown && isForce)
            {
                _savedPos = tf.position;
                _savedRot = tf.rotation;
                ForceSit(move, anim, cc, tf, entry);
            }
            else
            {
                _sitRoutine = StartCoroutine(
                    isSitDown
                        ? SitDownFlow(move, anim, cc, tf, entry)
                        : StandUpFlow(move, anim, cc, tf, entry)
                );
            }
        }

        private void ForceSit(PlayerMovementController move, Animator anim, CharacterController cc, Transform tf,
            EntryData entry)
        {
            if (entry == null) return;

            if (cc != null) cc.enabled = false;
            move.CanMove = false;

            var targetPos = (sitPoint ? sitPoint.position : tf.position) + Vector3.up * sitAdjustHeight;
            var targetRot = sitPoint ? sitPoint.rotation : tf.rotation;
            tf.position = targetPos;
            tf.rotation = targetRot;

            if (anim != null)
            {
                int style = 0;
                int.TryParse(entry.animationID, out style);
                anim.SetFloat(SIT_STYLE, style);
                anim.SetBool(SIT_TRIGGER, true);
                anim.Play(SIT_STATE, 0, 0f);
                anim.Update(0f);
                anim.applyRootMotion = false;
            }

            move.SuppressLookAtIK = !move.FirstPersonView;

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

            if (forceFPV)
                move.ForceEnterFPV(true, snap: true);

            IsBusy = false;
        }

        private void ForceStand(PlayerMovementController move, Animator anim, CharacterController cc, Transform tf,
            EntryData entry)
        {
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

            if (anim != null)
            {
                int style = 0;
                int.TryParse(entry != null ? entry.animationID : "0", out style);
                anim.SetFloat(SIT_STYLE, style);
                anim.SetBool(SIT_TRIGGER, false);
                anim.Play(STAND_STATE, 0, 0f);
                anim.Update(0f);
                anim.applyRootMotion = false;
            }

            if (cc != null) cc.enabled = true;
            move.CanMove = true;

            move.SnapAimToCurrentCamera();
            move.BeginIkGrace(0.2f);

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

            move.SuppressLookAtIK = !move.FirstPersonView;
        }

        private IEnumerator SitDownFlow(PlayerMovementController move, Animator anim, CharacterController cc,
            Transform tf, EntryData entry)
        {
            IsBusy = true;
            move.SuppressLookAtIK = true;

            _savedPos = tf.position;
            _savedRot = tf.rotation;

            if (cc != null) cc.enabled = false;
            move.CanMove = false;

            if (entry == null)
            {
                Debug.LogWarning("SitInteractable: No entry point found");
                IsBusy = false;
                yield break;
            }

            var entryPoint = entry.entryPoint;

            yield return RotateTowardPointIfNeeded(tf, entryPoint.position);
            yield return MoveToPoint(tf, entryPoint.position, anim);
            yield return RotateToTarget(tf, entryPoint.rotation);

            if (anim != null)
            {
                anim.applyRootMotion = true;
                int style = 0;
                int.TryParse(entry.animationID, out style);
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

            float footOffset = ComputeFootOffset(anim, tf, sitPoint ? sitPoint : tf);
            Vector3 targetPos = (sitPoint ? sitPoint.position : tf.position) + Vector3.up * footOffset +
                                Vector3.up * sitAdjustHeight;
            Quaternion targetRot = sitPoint ? sitPoint.rotation : tf.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;
                tf.position = Vector3.Lerp(startPos, targetPos, t);
                tf.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            tf.position = targetPos;
            tf.rotation = targetRot;

            if (anim != null) anim.applyRootMotion = false;
            _sitRoutine = null;
            _isSittingLocal = true;
            move.SuppressLookAtIK = false;

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

            if (forceFPV)
                move.ForceEnterFPV(true, snap: true);

            IsBusy = false;
        }

        private IEnumerator StandUpFlow(PlayerMovementController move, Animator anim, CharacterController cc,
            Transform tf, EntryData entry)
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
            _isSittingLocal = false;

            if (anim != null)
            {
                anim.applyRootMotion = true;
                int style = 0;
                int.TryParse(entry != null ? entry.animationID : "0", out style);
                anim.SetFloat(SIT_STYLE, style);
                anim.SetBool(SIT_TRIGGER, false);
            }

            float duration = anim != null ? anim.GetAnimatorTransitionInfo(0).duration : 0.25f;
            float elapsed = 0f;

            Vector3 startPos = tf.position;
            Quaternion startRot = tf.rotation;

            Vector3 targetPos = entry != null && entry.entryPoint != null ? entry.entryPoint.position : _savedPos;
            Quaternion targetRot = entry != null && entry.entryPoint != null ? entry.entryPoint.rotation : _savedRot;

            float footOffset = ComputeFootOffset(anim, tf, entry != null ? entry.entryPoint : tf);
            targetPos += Vector3.up * footOffset;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;
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
            move.SuppressLookAtIK = false;

            move.SnapAimToCurrentCamera();
            move.BeginIkGrace(0.2f);

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
        }

        private int PickEntryIndexFor(NetworkConnection conn)
        {
            var all = FindObjectsOfType<PlayerMovementController>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].Owner == conn)
                {
                    var tf = all[i].transform;
                    var closest = FindClosestEntryPoint(tf.position);
                    if (closest == null) return 0;
                    for (int e = 0; e < entries.Length; e++)
                        if (entries[e] == closest)
                            return e;
                    return 0;
                }

            return 0;
        }

        private EntryData GetEntry(int index)
        {
            if (entries == null || entries.Length == 0) return null;
            if (index < 0 || index >= entries.Length) return null;
            return entries[index];
        }

        private EntryData FindClosestEntryPoint(Vector3 from)
        {
            if (entries == null || entries.Length == 0) return null;

            float min = float.MaxValue;
            EntryData closest = null;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.entryPoint == null) continue;
                float d = Vector3.Distance(from, e.entryPoint.position);
                if (d < min)
                {
                    min = d;
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

            float v = 0f, h = 0f;

            while (Vector3.Distance(tf.position, targetPos) > stopDistance && elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;

                Vector3 dir = targetPos - tf.position;
                dir.y = 0f;
                float dist = dir.magnitude;

                if (dist > 0.001f)
                {
                    dir.Normalize();
                    tf.position += dir * walkSpeed * Time.deltaTime;

                    v = Mathf.MoveTowards(v, 1f, animBlendSpeed * Time.deltaTime);
                    h = Mathf.MoveTowards(h, 0f, animBlendSpeed * Time.deltaTime);
                }
                else
                {
                    v = Mathf.MoveTowards(v, 0f, animBlendSpeed * Time.deltaTime);
                    h = Mathf.MoveTowards(h, 0f, animBlendSpeed * Time.deltaTime);
                }

                if (anim != null)
                {
                    anim.SetFloat("Vertical", v);
                    anim.SetFloat("Horizontal", h);
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

        private PlayerMovementController FindLocalOwnerMovement()
        {
            var all = FindObjectsOfType<PlayerMovementController>();
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m != null && m.Owner.IsLocalClient)
                    return m;
            }

            return null;
        }

        private PlayerMovementController FindServerSideMovement(NetworkConnection conn)
        {
            var all = FindObjectsOfType<PlayerMovementController>();
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m != null && m.Owner == conn)
                    return m;
            }

            return null;
        }
    }
}