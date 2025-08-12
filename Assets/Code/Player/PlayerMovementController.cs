using CC;
using Cinemachine;
using Code.Network.HostMigration;
using Code.Network.Player;
using Code.Utility;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : NetworkBehaviour, IMigratable<CharacterMigrateData>
    {
        public static PlayerMovementController Own { get; private set; }

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 2.0f;
        [SerializeField] private float sprintSpeed = 5.335f;
        [SerializeField] private float rotationSmoothTime = 0.12f;
        [SerializeField] private float speedChangeRate = 10.0f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -15.0f;
        [SerializeField] private float jumpTimeout = 0.5f;
        [SerializeField] private float fallTimeout = 0.15f;
        [SerializeField] private float groundedOffset = -0.14f;
        [SerializeField] private float groundedRadius = 0.28f;
        [SerializeField] private LayerMask groundLayers;
        [SerializeField] private float terminalVelocity = 53.0f;

        [Header("Camera")]
        [SerializeField] private GameObject cinemachineCameraTarget;
        [SerializeField] private GameObject[] hideForFirstPersonViewLocal;
        [SerializeField] private float minCameraDistance = 1f;
        [SerializeField] private float maxCameraDistance = 4f;
        [SerializeField] private float minFOV = 40;
        [SerializeField] private float maxFOV = 65;
        [SerializeField] private float topClamp = 70f;
        [SerializeField] private float bottomClamp = -30f;
        [SerializeField] public float cameraAngleOverride = 0f;
        [SerializeField] private Transform headTarget;

        [Header("Audio")]
        [SerializeField] private AudioClip landingAudioClip;
        [SerializeField] private AudioClip[] footstepAudioClips;
        [Range(0, 1)] [SerializeField] private float footstepAudioVolume = 0.5f;

        public bool CanMove = true;
        public bool LockCameraPosition = true;

        private bool _firstPersonView = true;
        public bool FirstPersonView
        {
            get => _firstPersonView;
            set
            {
                if (_firstPersonView == value) return;
                _firstPersonView = value;

                if (value && LookCameraLimitRotation)
                {
                    float modelYaw = transform.eulerAngles.y;
                    cinemachineTargetYaw = sitBaseYaw = modelYaw;
                    cinemachineTargetPitch = sitBasePitch = 0f;
                }
            }
        }
        public float CameraDistance => cameraDistance;

        private bool lookCameraLimitRotation;
        public bool LookCameraLimitRotation
        {
            get => lookCameraLimitRotation;
            set
            {
                if (value && !lookCameraLimitRotation)
                {
                    sitBaseYaw = cinemachineTargetYaw;
                    sitBasePitch = cinemachineTargetPitch;
                }
                lookCameraLimitRotation = value;
            }
        }
        public bool LookCameraLimitRotationRKM { get; set; } = false;

        public bool LockCursor { get; set; } = true;

        [Header("Sit Camera Limits")]
        [SerializeField] private float sitYawRange = 45f;
        [SerializeField] private float sitMinPitch = -10f;
        [SerializeField] private float sitMaxPitch = 30f;

        public GameObject CinemachineCameraTarget => cinemachineCameraTarget;

        [Header("IK Settings")]
        [SerializeField] private float ikTransitionSpeed = 5f;
        [SerializeField] private float lookAtSmoothSpeed = 5f;
        [SerializeField, Range(0f,1f)] private float lookAtClampWeight = 0.5f;

        private float currentIkWeight;
        private Vector3 currentLookAtPos;

        private bool grounded;
        private float cameraDistance = 0.5f;
        private float savedDistance = 0.5f;
        private float verticalVelocity;
        private float jumpTimeoutDelta;
        private float fallTimeoutDelta;

        private float speed;
        private float animationBlend;
        private float targetRotation;
        private float rotationVelocity;
        public float cinemachineTargetYaw;
        public float cinemachineTargetPitch;

        public float sitBaseYaw;
        public float sitBasePitch;

        private float vertical;
        private float horizontal;

        private Camera _mainCamera;
        private PlayerInput input;
        private Animator animator;
        private CharacterController controller;
        private CinemachineVirtualCamera virtualCamera;

        private readonly SyncVar<Vector3> networkLookAtPos = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ClientUnsynchronized,
            ReadPermission = ReadPermission.Observers
        });
        private readonly SyncVar<float> networkIkWeight = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ClientUnsynchronized,
            ReadPermission = ReadPermission.Observers
        });

        private int animIDSpeed;
        private int animIDGrounded;
        private int animIDJump;
        private int animIDFreeFall;
        private int animIDMotionSpeed;
        private int animIDVertical;
        private int animIDHorizontal;
        private int animIDTurn;
        private int animIDFPV;

        private const float Threshold = 0.01f;
        private bool smoothedFirstPerson;
        private float _syncWeight;
        private Vector3 _lookPos;
        private bool _cursorUsable; // можно ли сейчас принимать мышь/тач
        private bool _snapToFpVPending;
        
        private void Awake()
        {
            _mainCamera = Camera.main;
            input = PlayerInput.Instance;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (!IsOwner) return;

            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            AssignAnimationIDs();

            jumpTimeoutDelta = jumpTimeout;
            fallTimeoutDelta = fallTimeout;

            if (cinemachineCameraTarget != null)
                cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;

            if (animator != null)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null && cinemachineCameraTarget != null)
                    currentLookAtPos = head.position + cinemachineCameraTarget.transform.forward * 10f;
            }

            Own = this;
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            // кэш камера/инпут на случай динамического создания
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (virtualCamera == null) virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            if (input == null) input = PlayerInput.Instance;

            // используем ли ввод (мобильный оверлей или спрятанный курсор)
            bool usingMobile = (PlayerInput.Instance != null && PlayerInput.Instance.IsUsingMobileFallback);
            bool cursorHidden = (CursorManager.Instance != null && !CursorManager.Instance.IsVisible());
            _cursorUsable = usingMobile || cursorHidden;

            if (CanMove || LookCameraLimitRotation)
                UpdateCameraDistance();
            
            if (!CanMove) return;

            GroundedCheck();
            JumpAndGravity();
            Move();
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;

            if (LookCameraLimitRotation)
            {
                if (FirstPersonView)
                    SitCameraRotation();   // FPV: с ограничениями, и при RKM — только когда RMB зажата
                else
                    CameraRotation();      // 3rd person: свободно, даже при видимом курсоре
            }
            else
            {
                if (CanMove || !FirstPersonView)
                    CameraRotation();
                else
                    ResetFirstPersonViewRotation();
            }
        }

        private void SitCameraRotation()
        {
            if (input == null || cinemachineCameraTarget == null) return;

            var lookInput = (LookCameraLimitRotationRKM && !input.IsRMBDown) ? Vector2.zero : input.Look;

            if (lookInput.sqrMagnitude >= Threshold)
            {
                // если нет данных движения мыши — применяем дельту по времени
                float mul = Time.deltaTime;
#if ENABLE_INPUT_SYSTEM
                // hint: можно проверять Mouse.current.delta, но оставим тайм
#endif
                cinemachineTargetYaw   += lookInput.x * 60f * Time.deltaTime;
                cinemachineTargetPitch += lookInput.y * 60f * Time.deltaTime;
            }

            cinemachineTargetYaw = Mathf.Clamp(
                cinemachineTargetYaw,
                sitBaseYaw - sitYawRange,
                sitBaseYaw + sitYawRange
            );
            cinemachineTargetPitch = Mathf.Clamp(
                cinemachineTargetPitch,
                sitBasePitch + sitMinPitch,
                sitBasePitch + sitMaxPitch
            );

            cinemachineCameraTarget.transform.rotation =
                Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride, cinemachineTargetYaw, 0f);
        }

        private void AssignAnimationIDs()
        {
            animIDSpeed       = Animator.StringToHash("Speed");
            animIDGrounded    = Animator.StringToHash("Grounded");
            animIDJump        = Animator.StringToHash("Jump");
            animIDFreeFall    = Animator.StringToHash("FreeFall");
            animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            animIDVertical    = Animator.StringToHash("Vertical");
            animIDHorizontal  = Animator.StringToHash("Horizontal");
            animIDTurn        = Animator.StringToHash("TurnAngle");
            animIDFPV         = Animator.StringToHash("FirstPerson");
        }

        public void ForceEnterFPV(bool enable, bool snap = true)
        {
            if (enable)
            {
                cameraDistance = 0f;
                smoothedFirstPerson = true;
                FirstPersonView = true;
                if (snap) _snapToFpVPending = true;
            }
            else
            {
                FirstPersonView = false;
                // выходим из FPV без изменения текущей cameraDistance — игрок сам отдалит колесом
            }
        }
        
        public void ForceSetCameraDistance(float distance) => cameraDistance = distance;

        private void UpdateCameraDistance()
        {
            if (input == null || virtualCamera == null) return;

            // Разрешаем ввод по зуму/переключению когда:
            //  - обычный игровой ввод доступен (_cursorUsable), ИЛИ
            //  - мы в сидячем режиме (LookCameraLimitRotation), даже если курсор виден (RMB-режим)
            bool allowZoomInput = _cursorUsable || LookCameraLimitRotation;

            if (allowZoomInput)
            {
                if (input.CameraSwitchDown)
                {
                    if (FirstPersonView) cameraDistance = savedDistance;
                    else { savedDistance = cameraDistance; cameraDistance = 0f; }
                }

                // колесо мыши
                cameraDistance -= Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * 100f;
            }

            cameraDistance = Mathf.Clamp01(cameraDistance);

            // 2) гистерезис FPV
            bool wasFPV = FirstPersonView;
            if (cameraDistance < 0.001f) smoothedFirstPerson = true;
            else if (cameraDistance > 0.002f) smoothedFirstPerson = false;

            bool enteringNow = (!wasFPV && smoothedFirstPerson);
            FirstPersonView = smoothedFirstPerson;

            // 3) применяем настройки к Cinemachine
            var follow = virtualCamera.GetCinemachineComponent<Cinemachine.Cinemachine3rdPersonFollow>();
            if (follow != null)
            {
                if (FirstPersonView && (enteringNow || _snapToFpVPending))
                {
                    // МГНОВЕННЫЙ переход: без Lerp, без «подлёта»
                    follow.ShoulderOffset = new Vector3(0f, 0f, 0f);
                    follow.CameraDistance = 0f;

                    // Сброс интерполяций Cinemachine, чтобы точно был «cut»
                    virtualCamera.PreviousStateIsValid = false;
                    _snapToFpVPending = false;
                }
                else
                {
                    // плавно только когда мы не в моменте входа в FPV
                    follow.ShoulderOffset = new Vector3(0f, FirstPersonView ? 0f : -0.2f, 0f);

                    float target = FirstPersonView
                        ? 0f
                        : Mathf.Lerp(minCameraDistance, maxCameraDistance, cameraDistance);
                    follow.CameraDistance = FirstPersonView
                        ? 0f
                        : Mathf.Lerp(follow.CameraDistance, target, Time.deltaTime * 3f);
                }
            }

            // 4) привязки камеры/обновление FOV — как раньше
            virtualCamera.Follow = cinemachineCameraTarget != null ? cinemachineCameraTarget.transform : null;

            float speedFactor = (controller != null) ? controller.velocity.normalized.magnitude : 0f;
            float tFov = Mathf.MoveTowards(minFOV, maxFOV, (cameraDistance * 0.7f) + (speedFactor * 0.3f));
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, tFov, Time.deltaTime * 15f);
        }

        private void ResetFirstPersonViewRotation()
        {
            if (!FirstPersonView || cinemachineCameraTarget == null) return;

            var eul = cinemachineCameraTarget.transform.rotation.eulerAngles;
            cinemachineTargetPitch = eul.x - cameraAngleOverride;
            cinemachineTargetYaw   = eul.y;
            cinemachineCameraTarget.transform.localRotation = Quaternion.identity;
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = transform.position + Vector3.down * groundedOffset;
            grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
            if (animator != null) animator.SetBool(animIDGrounded, grounded);
        }

        private void Move()
        {
            if (controller == null) return;

            Vector2 mv = (input != null) ? input.Move : Vector2.zero;
            Vector2 mvUsed = _cursorUsable ? mv : Vector2.zero;

            bool canSprint = !FirstPersonView || (Mathf.Abs(mvUsed.x) < 0.1f && mvUsed.y > 0.1f);
            float targetSpeed = (input != null && input.SprintHeld && canSprint) ? sprintSpeed : moveSpeed;
            if (mv == Vector2.zero) targetSpeed = 0f;

            float currentSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
            float inputMagnitude = mvUsed.magnitude;

            if (Mathf.Abs(currentSpeed - targetSpeed) > 0.1f)
                speed = Mathf.Round(Mathf.Lerp(currentSpeed, targetSpeed * inputMagnitude, Time.deltaTime * speedChangeRate) * 1000f) / 1000f;
            else
                speed = targetSpeed;

            animationBlend = Mathf.Lerp(animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);
            if (animationBlend < 0.01f) animationBlend = 0f;

            Vector3 inputDir = new Vector3(mvUsed.x, 0f, mvUsed.y).normalized;
            float camYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
            targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camYaw;

            if (mv != Vector2.zero)
            {
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationSmoothTime);
                if (!FirstPersonView && _cursorUsable)
                    transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            }

            if (FirstPersonView && _cursorUsable)
                transform.rotation = Quaternion.Euler(0f, camYaw, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetRotation, 0f) * Vector3.forward;
            if (_cursorUsable)
                controller.Move(moveDir.normalized * (speed * Time.deltaTime) + Vector3.up * verticalVelocity * Time.deltaTime);

            if (animator != null)
            {
                Vector3 vel = _cursorUsable ? transform.InverseTransformDirection(controller.velocity) : Vector3.zero;
                vertical = Mathf.Lerp(vertical, vel.normalized.z * (speed > moveSpeed ? 2f : 1f), Time.deltaTime * 5f);
                horizontal = Mathf.Lerp(horizontal, vel.normalized.x, Time.deltaTime * 5f);

                animator.SetFloat(animIDSpeed, animationBlend);
                animator.SetFloat(animIDMotionSpeed, inputMagnitude);
                animator.SetFloat(animIDVertical, vertical);
                animator.SetFloat(animIDHorizontal, horizontal);
                animator.SetFloat(animIDFPV, FirstPersonView ? 1f : 0f);
            }
        }

        private void JumpAndGravity()
        {
            if (grounded)
            {
                fallTimeoutDelta = fallTimeout;

                if (animator != null)
                {
                    animator.SetBool(animIDJump, false);
                    animator.SetBool(animIDFreeFall, false);
                }

                if (verticalVelocity < 0f) verticalVelocity = -2f;

                if (_cursorUsable && input != null && input.JumpDown && jumpTimeoutDelta <= 0f)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    if (animator != null) animator.SetBool(animIDJump, true);
                }

                if (jumpTimeoutDelta > 0f) jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                jumpTimeoutDelta = jumpTimeout;

                if (fallTimeoutDelta > 0f) fallTimeoutDelta -= Time.deltaTime;
                else if (animator != null) animator.SetBool(animIDFreeFall, true);
            }

            if (verticalVelocity < terminalVelocity)
                verticalVelocity += gravity * Time.deltaTime;
        }

        private void CameraRotation()
        {
            if (input == null || cinemachineCameraTarget == null) return;

            // свободное вращение, если:
            // - обычный ввод доступен (_cursorUsable), ИЛИ
            // - мы сидим (LookCameraLimitRotation) и находимся в 3rd person
            bool allowLook = _cursorUsable || (LookCameraLimitRotation && !FirstPersonView);

            Vector2 look = allowLook ? input.Look : Vector2.zero;
            if (look.sqrMagnitude >= Threshold && !LockCameraPosition)
            {
                float multiplier = Time.deltaTime * 60f;
                cinemachineTargetYaw   += look.x * multiplier;
                cinemachineTargetPitch += look.y * multiplier;
            }

            cinemachineTargetYaw   = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
            cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);

            cinemachineCameraTarget.transform.rotation =
                Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride, cinemachineTargetYaw, 0f);
        }


        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = grounded ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
            Gizmos.DrawSphere(transform.position + Vector3.down * groundedOffset, groundedRadius);
        }

        private void OnFootstep(AnimationEvent evt)
        {
            if (controller == null || footstepAudioClips == null || footstepAudioClips.Length == 0) return;
            if (evt.animatorClipInfo.weight > 0.5f)
            {
                int idx = Random.Range(0, footstepAudioClips.Length);
                var clip = footstepAudioClips[idx];
                if (clip != null)
                    AudioSource.PlayClipAtPoint(clip, transform.TransformPoint(controller.center), footstepAudioVolume);
            }
        }

        private void OnLand(AnimationEvent evt)
        {
            if (controller == null || landingAudioClip == null) return;
            if (evt.animatorClipInfo.weight > 0.5f)
                AudioSource.PlayClipAtPoint(landingAudioClip, transform.TransformPoint(controller.center), footstepAudioVolume);
        }

        // === IK sync ===
        [ServerRpc(RunLocally = true)]
        private void SyncIKServerRpc(Vector3 lookPos, float weight)
        {
            networkLookAtPos.Value = lookPos;
            networkIkWeight.Value  = weight;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            if (IsOwner)
            {
                float targetWeight = FirstPersonView ? 1f : 0f;
                currentIkWeight = Mathf.MoveTowards(currentIkWeight, targetWeight, Time.deltaTime * ikTransitionSpeed);

                if (currentIkWeight > 0.01f && cinemachineCameraTarget != null)
                {
                    Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (headBone != null)
                    {
                        Vector3 headWorldPos = headBone.position + cinemachineCameraTarget.transform.forward * 10f;
                        currentLookAtPos = Vector3.Lerp(currentLookAtPos, headWorldPos, Time.deltaTime * lookAtSmoothSpeed);
                    }
                }

                SyncIKServerRpc(currentLookAtPos, currentIkWeight);

                animator.SetLookAtWeight(currentIkWeight, 0f, currentIkWeight, currentIkWeight, lookAtClampWeight);
                animator.SetLookAtPosition(currentLookAtPos);
            }
            else
            {
                _syncWeight = Mathf.Lerp(_syncWeight, networkIkWeight.Value, Time.deltaTime * 5f);
                _lookPos    = Vector3.Lerp(_lookPos,    networkLookAtPos.Value, Time.deltaTime * 5f);

                animator.SetLookAtWeight(_syncWeight, 0f, _syncWeight, _syncWeight, lookAtClampWeight);
                animator.SetLookAtPosition(_lookPos);
            }
        }

        #region IMigratable
        public void OnMigrateDataReceived(CharacterMigrateData data)
        {
            if (NetworkManager.IsServerStarted) SetPlayerState(Owner, data);
        }

        [TargetRpc]
        private void SetPlayerState(NetworkConnection conn, CharacterMigrateData data)
        {
            cinemachineTargetPitch = data.cinemachineTargetPitch;
            cinemachineTargetYaw   = data.cinemachineTargetYaw;
            cameraDistance         = data.cameraDistance;
            FirstPersonView        = data.isFirstPersonView;
        }

        public CharacterMigrateData GetMigrateData()
        {
            return new CharacterMigrateData
            {
                cinemachineTargetPitch = cinemachineTargetPitch,
                cinemachineTargetYaw   = cinemachineTargetYaw,
                cameraDistance         = cameraDistance,
                isFirstPersonView      = FirstPersonView
            };
        }
        #endregion
    }
}
