using Cinemachine;
using Code.Network.HostMigration;
using Code.Network.Player;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SRF;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : NetworkBehaviour, IMigratable<CharacterMigrateData>
    {
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
        [SerializeField] private float cameraAngleOverride = 0f;
        [SerializeField] private Transform headTarget;

        [Header("Audio")]
        [SerializeField] private AudioClip landingAudioClip;
        [SerializeField] private AudioClip[] footstepAudioClips;
        [Range(0, 1)] [SerializeField] private float footstepAudioVolume = 0.5f;

        public bool CanMove = true;
        public bool LockCameraPosition = true;
// backing-field
        private bool _firstPersonView = true;
        public bool FirstPersonView
        {
            get => _firstPersonView;
            set
            {
                if (_firstPersonView == value) return;
                _firstPersonView = value;

                // если мы в режиме «сидя» и переключаемся в первый-лицо
                if (value && LookCameraLimitRotation)
                {
                    // берём yaw из направления тела игрока
                    float modelYaw = transform.eulerAngles.y;
                    cinemachineTargetYaw = sitBaseYaw = modelYaw;

                    // pitch можно оставить прежним или сбросить на ноль.
                    // здесь обнулим — камера будет смотреть по горизонтали тела
                    cinemachineTargetPitch = sitBasePitch = 0f;
                }
            }
        }

        public float CameraDistance => cameraDistance;
        
        private bool lookCameraLimitRotation = false;
        public bool LookCameraLimitRotation
        {
            get => lookCameraLimitRotation;
            set
            {
                if (value && !lookCameraLimitRotation)
                {
                    // при первом вхождении в режим «сидя» запоминаем базовые углы
                    sitBaseYaw   = cinemachineTargetYaw;
                    sitBasePitch = cinemachineTargetPitch;
                }
                lookCameraLimitRotation = value;
            }
        }
        public bool LookCameraLimitRotationRKM { get; set; } = false;

        public bool LockCursor { get; set; } = true;
        
        [Header("Sit Camera Limits")]
        [SerializeField] private float sitYawRange = 45f;      // ±45° по горизонтали
        [SerializeField] private float sitMinPitch = -10f;     // минимальный подъём
        [SerializeField] private float sitMaxPitch = 30f;      // максимальный подъём
        
        public GameObject CinemachineCameraTarget => cinemachineCameraTarget;
        
        [Header("IK Settings")]
        [SerializeField, Tooltip("Скорость перехода веса IK (1 = за 1 секунду)")]
        private float ikTransitionSpeed = 5f;
        [SerializeField, Tooltip("Скорость сглаживания позиции точки взгляда")]
        private float lookAtSmoothSpeed = 5f;
        [SerializeField, Range(0f,1f), Tooltip("Насколько жёстко ограничивать поворот головы")]
        private float lookAtClampWeight = 0.5f;

        private float currentIkWeight = 0f;
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

        private GameObject mainCamera;
        private PlayerInput input;
        private Animator animator;
        private CharacterController controller;
        private CinemachineVirtualCamera virtualCamera;

        private readonly SyncVar<Vector3> networkLookAtPos = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });
        private readonly SyncVar<float>   networkIkWeight = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
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

        private void Awake()
        {
            mainCamera = Camera.main?.gameObject;
            input = GetComponent<PlayerInput>();
        }

        private void Start()
        {
            cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            AssignAnimationIDs();

            jumpTimeoutDelta = jumpTimeout;
            fallTimeoutDelta = fallTimeout;
            
            if (animator != null)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                currentLookAtPos = head.position + cinemachineCameraTarget.transform.forward * 10f;
            }
            
            gameObject.SetLayerRecursive(LayerMask.NameToLayer("Player"));
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner)
                virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        private void Update()
        {
            if (!IsOwner || !CanMove) return;

            virtualCamera ??= FindObjectOfType<CinemachineVirtualCamera>();

            GroundedCheck();
            JumpAndGravity();
            Move();
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            
            Cursor.lockState = LockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !LockCursor;
            
            if (CanMove || LookCameraLimitRotation)
                UpdateCameraDistance();
            
            if (LookCameraLimitRotation && FirstPersonView)
                SitCameraRotation();
            else if (CanMove || !FirstPersonView)
                CameraRotation();
            else
                ResetFirstPersonViewRotation();
        }
        
        private void SitCameraRotation()
        {
            if (input.look.sqrMagnitude >= Threshold)
            {
                if (LookCameraLimitRotationRKM)
                    if (!Input.GetMouseButton(1))
                    {
                        return;
                    }
                
                float mul = Input.mousePositionDelta.magnitude > 0 ? 1f : Time.deltaTime;
                cinemachineTargetYaw   += input.look.x * mul;
                cinemachineTargetPitch += input.look.y * mul;
            }

            // ОГРАНИЧЕНИЕ ОТ БАЗОВОГО УГЛА
            cinemachineTargetYaw   = Mathf.Clamp(
                cinemachineTargetYaw,
                sitBaseYaw - sitYawRange,
                sitBaseYaw + sitYawRange
            );
            cinemachineTargetPitch = Mathf.Clamp(
                cinemachineTargetPitch,
                sitBasePitch + sitMinPitch,
                sitBasePitch + sitMaxPitch
            );

            // Только меняем ТАРГЕТ, НИКОГДА transform игрока
            cinemachineCameraTarget.transform.rotation =
                Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride,
                    cinemachineTargetYaw,
                    0f);
        }

        private void AssignAnimationIDs()
        {
            animIDSpeed = Animator.StringToHash("Speed");
            animIDGrounded = Animator.StringToHash("Grounded");
            animIDJump = Animator.StringToHash("Jump");
            animIDFreeFall = Animator.StringToHash("FreeFall");
            animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            animIDVertical = Animator.StringToHash("Vertical");
            animIDHorizontal = Animator.StringToHash("Horizontal");
            animIDTurn = Animator.StringToHash("TurnAngle");
            animIDFPV = Animator.StringToHash("FirstPerson");
        }

        private void UpdateCameraDistance()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (FirstPersonView)
                    cameraDistance = savedDistance;
                else
                {
                    savedDistance = cameraDistance;
                    cameraDistance = 0;
                }
            }

            cameraDistance -= Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * 100;
            cameraDistance = Mathf.Clamp(cameraDistance, 0, 1);

            if (cameraDistance < 0.05f) smoothedFirstPerson = true;
            else if (cameraDistance > 0.15f) smoothedFirstPerson = false;

            FirstPersonView = smoothedFirstPerson;

            var follow = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0, FirstPersonView ? 0 : -0.2f, 0);
            follow.CameraDistance = FirstPersonView ? 0 : Mathf.Lerp(follow.CameraDistance, Mathf.Lerp(minCameraDistance, maxCameraDistance, cameraDistance), Time.deltaTime * 3);
            virtualCamera.Follow = cinemachineCameraTarget.transform;
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, Mathf.MoveTowards(minFOV, maxFOV, (cameraDistance * 0.7f) + (controller.velocity.normalized.magnitude * 0.3f)), Time.deltaTime * 15);

            foreach (var o in hideForFirstPersonViewLocal)
                o.SetActive(!FirstPersonView);
        }

        private void ResetFirstPersonViewRotation()
        {
            if (!FirstPersonView) return;

            cinemachineTargetPitch = cinemachineCameraTarget.transform.rotation.eulerAngles.x - cameraAngleOverride;
            cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;
            cinemachineCameraTarget.transform.localRotation = Quaternion.identity;
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = transform.position + Vector3.down * groundedOffset;
            grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

            animator?.SetBool(animIDGrounded, grounded);
        }

        private void Move()
        {
            bool canSprint = !FirstPersonView || (Mathf.Abs(input.move.x) < 0.1f && input.move.y > 0.1f);
            float targetSpeed = input.sprint && canSprint ? sprintSpeed : moveSpeed;

            if (input.move == Vector2.zero) targetSpeed = 0;

            float currentSpeed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;
            float inputMagnitude = input.analogMovement ? input.move.magnitude : 1f;

            if (Mathf.Abs(currentSpeed - targetSpeed) > 0.1f)
                speed = Mathf.Round(Mathf.Lerp(currentSpeed, targetSpeed * inputMagnitude, Time.deltaTime * speedChangeRate) * 1000f) / 1000f;
            else
                speed = targetSpeed;

            animationBlend = Mathf.Lerp(animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);
            if (animationBlend < 0.01f) animationBlend = 0f;

            Vector3 inputDir = new Vector3(input.move.x, 0, input.move.y).normalized;
            targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;

            if (input.move != Vector2.zero)
            {
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationSmoothTime);
                if (!FirstPersonView)
                    transform.rotation = Quaternion.Euler(0, rotation, 0);
            }

            if (FirstPersonView)
                transform.rotation = Quaternion.Euler(0, mainCamera.transform.eulerAngles.y, 0);

            Vector3 moveDir = Quaternion.Euler(0, targetRotation, 0) * Vector3.forward;
            controller.Move(moveDir.normalized * (speed * Time.deltaTime) + Vector3.up * verticalVelocity * Time.deltaTime);

            if (animator)
            {
                Vector3 velocity = transform.InverseTransformDirection(controller.velocity);
                vertical = Mathf.Lerp(vertical, velocity.normalized.z * (speed > moveSpeed ? 2 : 1), Time.deltaTime * 5);
                horizontal = Mathf.Lerp(horizontal, velocity.normalized.x, Time.deltaTime * 5);

                animator.SetFloat(animIDSpeed, animationBlend);
                animator.SetFloat(animIDMotionSpeed, inputMagnitude);
                animator.SetFloat(animIDVertical, vertical);
                animator.SetFloat(animIDHorizontal, horizontal);
                animator.SetFloat(animIDFPV, FirstPersonView ? 1 : 0);
            }
        }

        private void JumpAndGravity()
        {
            if (grounded)
            {
                fallTimeoutDelta = fallTimeout;

                animator?.SetBool(animIDJump, false);
                animator?.SetBool(animIDFreeFall, false);

                if (verticalVelocity < 0) verticalVelocity = -2f;

                if (input.jump && jumpTimeoutDelta <= 0)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    animator?.SetBool(animIDJump, true);
                }

                if (jumpTimeoutDelta > 0) jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                jumpTimeoutDelta = jumpTimeout;

                if (fallTimeoutDelta > 0)
                    fallTimeoutDelta -= Time.deltaTime;
                else
                    animator?.SetBool(animIDFreeFall, true);

                input.jump = false;
            }

            if (verticalVelocity < terminalVelocity)
                verticalVelocity += gravity * Time.deltaTime;
        }

        private void CameraRotation()
        {
            if (input.look.sqrMagnitude >= Threshold && !LockCameraPosition)
            {
                float multiplier = Input.mousePositionDelta.magnitude > 0 ? 1f : Time.deltaTime;
                cinemachineTargetYaw += input.look.x * multiplier;
                cinemachineTargetPitch += input.look.y * multiplier;
            }

            cinemachineTargetYaw = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
            cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);

            cinemachineCameraTarget.transform.rotation =
                Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride, cinemachineTargetYaw, 0);
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
            if (evt.animatorClipInfo.weight > 0.5f && footstepAudioClips.Length > 0)
            {
                int index = Random.Range(0, footstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(footstepAudioClips[index], transform.TransformPoint(controller.center), footstepAudioVolume);
            }
        }

        private void OnLand(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(landingAudioClip, transform.TransformPoint(controller.center), footstepAudioVolume);
            }
        }
        
        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            if (IsOwner)
            {
                // 1) Считаем целевой вес IK
                float targetWeight = FirstPersonView ? 1f : 0f;
                currentIkWeight = Mathf.MoveTowards(currentIkWeight, targetWeight, Time.deltaTime * ikTransitionSpeed);

                // 2) Если IK активен, пересчитываем точку взгляда
                if (currentIkWeight > 0.01f)
                {
                    Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                    // позиция вдоль текущего направления камеры
                    headTarget.position = headBone.position + cinemachineCameraTarget.transform.forward * 10f;
                    // сглаживаем движение взгляда
                    currentLookAtPos = Vector3.Lerp(
                        currentLookAtPos,
                        headTarget.position,
                        Time.deltaTime * lookAtSmoothSpeed
                    );
                }

                // 3) Применяем IK на owner
                animator.SetLookAtWeight(
                    currentIkWeight,    // overall weight
                    0f,                 // body weight
                    currentIkWeight,    // head weight
                    currentIkWeight,    // eyes weight
                    lookAtClampWeight   // clamp
                );
                animator.SetLookAtPosition(currentLookAtPos);

                // 4) Синхронизируем результат
                networkLookAtPos.Value  = currentLookAtPos;
                networkIkWeight.Value   = currentIkWeight;
            }
            else
            {
                // На наблюдающих просто применяем уже синхронизированные значения
                animator.SetLookAtWeight(
                    networkIkWeight.Value,
                    0f,
                    networkIkWeight.Value,
                    networkIkWeight.Value,
                    lookAtClampWeight
                );
                animator.SetLookAtPosition(networkLookAtPos.Value);
            }
        }

        #region IMigratable

        public void OnMigrateDataReceived(CharacterMigrateData data)
        {
            if(NetworkManager.IsServerStarted)
                SetPlayerState(Owner, data);
        }

        [TargetRpc]
        private void SetPlayerState(NetworkConnection conn, CharacterMigrateData data)
        {
            Debug.Log("[MigratableCharacter] Migrate");

            cinemachineTargetPitch = data.cinemachineTargetPitch;
            cinemachineTargetYaw = data.cinemachineTargetYaw;
            
            cameraDistance = data.cameraDistance;
            FirstPersonView = data.isFirstPersonView;
        }

        public CharacterMigrateData GetMigrateData()
        {
            return new CharacterMigrateData
            {
                cinemachineTargetPitch = cinemachineTargetPitch,
                cinemachineTargetYaw = cinemachineTargetYaw,
                
                cameraDistance = cameraDistance,
                isFirstPersonView = FirstPersonView
            };
        }

        #endregion
    }
}
