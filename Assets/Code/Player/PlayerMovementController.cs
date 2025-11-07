using System;
using System.Collections;
using System.Text;
using Unity.Cinemachine;
using Code.API;
using Code.Network.HostMigration;
using Code.Network.Lobby;
using Code.Network.Player;
using Code.Utility;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Proyecto26;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : NetworkBehaviour, IMigratable<CharacterMigrateData>
    {
        public static PlayerMovementController Own { get; private set; }

        [Header("Settings")] [SerializeField] private float moveSpeed = 2.0f;
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
        [SerializeField] private bool spawnOnSawedPosition = true;

        [Header("Camera")] [SerializeField] private GameObject cinemachineCameraTarget;
        [SerializeField] private GameObject[] hideForFirstPersonViewLocal;
        [SerializeField] private float minCameraDistance = 1f;
        [SerializeField] private float maxCameraDistance = 4f;
        [SerializeField] private float minFOV = 40;
        [SerializeField] private float maxFOV = 65;
        [SerializeField] private float topClamp = 70f;
        [SerializeField] private float bottomClamp = -30f;
        [SerializeField] public float cameraAngleOverride = 0f;
        [SerializeField] private Transform headTarget;

        [Header("Audio")] [SerializeField] private AudioClip landingAudioClip;
        [SerializeField] private AudioClip[] footstepAudioClips;
        [Range(0, 1)] [SerializeField] private float footstepAudioVolume = 0.5f;

        public bool CanMove = true;
        public bool LockCameraPosition = true;

        [SerializeField] private int emotionAnimatorLayer = 0; // На каком слое проигрываются эмоции
        
        private int animIDEmotionActive;
        private int animIDEmotionIndex;
        private Coroutine _emotionRoutine;
        private bool _wasFPV = false;
        
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

        [Header("Sit Camera Limits")] [SerializeField]
        private float sitYawRange = 45f;

        [SerializeField] private float sitMinPitch = -10f;
        [SerializeField] private float sitMaxPitch = 30f;

        public GameObject CinemachineCameraTarget => cinemachineCameraTarget;

        [Header("IK Settings")] [SerializeField]
        private float ikTransitionSpeed = 5f;

        [SerializeField] private float lookAtSmoothSpeed = 5f;
        [SerializeField, Range(0f, 1f)] private float lookAtClampWeight = 0.5f;

        public bool SuppressLookAtIK { get; set; } = false;
        private float _ikSuppressUntil = 0f;
        public void BeginIkGrace(float seconds) => _ikSuppressUntil = Time.time + Mathf.Max(0f, seconds);

        private float currentIkWeight;

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

        private float _lastYaw;
        [SerializeField] private float speedLerp = 5f;
        [SerializeField] private float angLerp = 7f;
        private float _animSpeed;
        private float _animTurn;

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
        private bool _cursorUsable;
        private bool _snapToFpVPending;

        private float _spawnPositionTimer;
        [SerializeField] private float spawnPositionUpdateTime = 2f;

        private bool _initedPlayer;

        private void Awake()
        {
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (_initedPlayer) return;
            _initedPlayer = true;

            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            AssignAnimationIDs();

            _mainCamera = Camera.main;
            input = PlayerInput.Instance;

            if (cinemachineCameraTarget != null)
                cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;

            UpdateHeadTargetPos();

            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                Own = this;
                jumpTimeoutDelta = jumpTimeout;
                fallTimeoutDelta = fallTimeout;
             
                EnsureInit();   
                LobbyVariables.Instance.lobbyPopupUI.Hide();
                CursorManager.Instance.HideCursor();
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (!IsOwner) return;
            if (spawnOnSawedPosition)
                LoadSpawnPosition();
            
            EnsureInit();
            Own = this;
            jumpTimeoutDelta = jumpTimeout;
            fallTimeoutDelta = fallTimeout;
        }

        private void LoadSpawnPosition()
        {
            if (!IsOwner) return;

            /*CanMove = false;
             getSpawnRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetFileUrl($"SpawnPoint_{ClientDataStorage.UserData.id}.txt"),
                Headers = ClientDataStorage.GetJwtHeader(),
                Timeout = 5
            };

            RestClient.Get(getSpawnRequest).Then(spawnResponse =>
            {
                if (spawnResponse.StatusCode != 200)
                    return;

                var responseParts = spawnResponse.Text.Split(' ');
                var posX = float.Parse(responseParts[0]);
                var posY = float.Parse(responseParts[1]);
                var posZ = float.Parse(responseParts[2]);
                var rotX = float.Parse(responseParts[3]);
                var rotY = float.Parse(responseParts[4]);
                var rotZ = float.Parse(responseParts[5]);

                transform.position = new Vector3(posX, posY, posZ);
                Debug.Log($"[Spawn data] {spawnResponse.Text}");
                Debug.Log($"[Spawn data] {transform.position} {CanMove}");
                transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);
            }).Finally(() => CanMove = true);*/

            var spawnPos = Vector3.zero;
            
            if (PlayerPrefs.HasKey("SavedSpawnPosition"))
            {
                spawnPos = transform.position = new Vector3(
                    PlayerPrefs.GetFloat("SavedSpawnPositionX"),
                    PlayerPrefs.GetFloat("SavedSpawnPositionY"),
                    PlayerPrefs.GetFloat("SavedSpawnPositionZ")
                );
                transform.rotation = Quaternion.Euler(
                    PlayerPrefs.GetFloat("SavedSpawnRotationX"),
                    PlayerPrefs.GetFloat("SavedSpawnRotationY"),
                    PlayerPrefs.GetFloat("SavedSpawnRotationZ")
                );
                PlayerPrefs.DeleteKey("SavedSpawnPosition");
            }
            
            Debug.Log($"Spawn pos is {spawnPos}");

            spawned = true;
        }

        private void UpdateSpawnPositionTimer()
        {
            if (!IsOwner)
                return;
            
            if (_spawnPositionTimer > 0f)
            {
                _spawnPositionTimer -= Time.deltaTime;
            }
            else
            {
                _spawnPositionTimer = spawnPositionUpdateTime;
                SaveSpawnPosition();
            }
        }

        private void SaveSpawnPosition()
        {
            if (!IsOwner || !CanMove) return;
            var spawnPos = transform.position;
            var spawnRot = transform.rotation.eulerAngles;

            var dataText = $"{spawnPos.x} {spawnPos.y} {spawnPos.z} {spawnRot.x} {spawnRot.y} {spawnRot.z}";

            var loadSpawnForm = new WWWForm();
            loadSpawnForm.AddBinaryData("file", Encoding.UTF8.GetBytes(dataText),
                $"SpawnPoint_{ClientDataStorage.UserData.id}.txt");

            var loadSavedSpawnRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetLoadFileUrl(),
                FormData = loadSpawnForm,
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Post(loadSavedSpawnRequest);
            /*
            PlayerPrefs.SetFloat("SavedSpawnPositionX", spawnPos.x);
            PlayerPrefs.SetFloat("SavedSpawnPositionY", spawnPos.y);
            PlayerPrefs.SetFloat("SavedSpawnPositionZ", spawnPos.z);
            PlayerPrefs.SetFloat("SavedSpawnRotationX", spawnRot.x);
            PlayerPrefs.SetFloat("SavedSpawnRotationY", spawnRot.y);
            PlayerPrefs.SetFloat("SavedSpawnRotationZ", spawnRot.z);
            PlayerPrefs.SetInt("SavedSpawnPosition", 1);
            PlayerPrefs.Save();*/
        }

        private void Update()
        {
            if (transform.position.y is < -10 or > 10 && spawned)
            {
                Debug.Log($"[PlayerMovementController] Spawn position out of range");
                var point = GameObject.FindGameObjectWithTag("Respawn").transform;
                controller.enabled = false;
                transform.position = point.position;
                transform.rotation = point.rotation;
                controller.enabled = true;
                return;
            }
            
            if (!IsOwner) return;
            if (!_initedPlayer) EnsureInit();
            
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (virtualCamera == null) virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            if (input == null) input = PlayerInput.Instance;

            bool usingMobile = (PlayerInput.Instance != null && PlayerInput.Instance.IsUsingMobileFallback);
            bool cursorHidden = (CursorManager.Instance != null && !CursorManager.Instance.IsVisible());
            _cursorUsable = usingMobile || cursorHidden;

            if (CanMove || LookCameraLimitRotation)
                UpdateCameraDistance();
            
            if (!CanMove) return;
            
            GroundedCheck();
            JumpAndGravity();
            Move();

            if (spawnOnSawedPosition && spawned)
                UpdateSpawnPositionTimer();
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (!_initedPlayer) EnsureInit();

            if (LookCameraLimitRotation)
            {
                if (FirstPersonView)
                    SitCameraRotation();
                else
                    CameraRotation();
            }
            else
            {
                if (!CanMove && FirstPersonView)
                    ResetFirstPersonViewRotation();
                else
                    CameraRotation();
            }

            UpdateHeadTargetPos();
        }

        private void UpdateHeadTargetPos()
        {
            if (animator != null && headTarget != null && cinemachineCameraTarget != null)
            {
                headTarget.position = cinemachineCameraTarget.transform.position +
                                      cinemachineCameraTarget.transform.forward * 10f;
            }
        }

        private void SitCameraRotation()
        {
            if (input == null || cinemachineCameraTarget == null) return;

            var lookInput = LookCameraLimitRotationRKM && !input.IsRmbDown ? Vector2.zero : input.Look;

            if (lookInput.sqrMagnitude >= Threshold)
            {
                float multiplier = Time.deltaTime * 60f;
                cinemachineTargetYaw += lookInput.x * multiplier;
                cinemachineTargetPitch += lookInput.y * multiplier;
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
            animIDSpeed = Animator.StringToHash("Speed");
            animIDGrounded = Animator.StringToHash("Grounded");
            animIDJump = Animator.StringToHash("Jump");
            animIDFreeFall = Animator.StringToHash("FreeFall");
            animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            animIDVertical = Animator.StringToHash("Vertical");
            animIDHorizontal = Animator.StringToHash("Horizontal");
            animIDTurn = Animator.StringToHash("TurnAngle");
            animIDFPV = Animator.StringToHash("FirstPerson");
            animIDEmotionActive = Animator.StringToHash("EmotionActive");
            animIDEmotionIndex = Animator.StringToHash("Emotion");
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
            }
        }

        public void ForceSetCameraDistance(float distance) => cameraDistance = distance;

        private void UpdateCameraDistance()
        {
            if (input == null || virtualCamera == null) return;
            var follow = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (follow == null) return;

            bool allowZoomInput = _cursorUsable || LookCameraLimitRotation;
            if (allowZoomInput)
            {
                if (input.CameraSwitchDown)
                {
                    if (FirstPersonView) cameraDistance = savedDistance;
                    else
                    {
                        savedDistance = cameraDistance;
                        cameraDistance = 0f;
                    }
                }

                cameraDistance -= PlayerInput.Instance.Zoom * Time.deltaTime * 100f;
            }

            cameraDistance = Mathf.Clamp01(cameraDistance);

            bool wasFPV = FirstPersonView;
            if (cameraDistance < 0.01f) smoothedFirstPerson = true;
            else if (cameraDistance > 0.02f) smoothedFirstPerson = false;

            bool enteringNow = (!wasFPV && smoothedFirstPerson);
            FirstPersonView = smoothedFirstPerson;

            if (FirstPersonView && (enteringNow || _snapToFpVPending))
            {
                follow.ShoulderOffset = new Vector3(0f, 0f, 0f);
                follow.CameraDistance = 0f;
                virtualCamera.PreviousStateIsValid = false;
                _snapToFpVPending = false;
            }
            else
            {
                follow.ShoulderOffset = new Vector3(0f, FirstPersonView ? 0f : -0.2f, 0f);
                float target = FirstPersonView
                    ? 0f
                    : Mathf.Lerp(minCameraDistance, maxCameraDistance, cameraDistance);
                follow.CameraDistance = FirstPersonView
                    ? 0f
                    : Mathf.Lerp(follow.CameraDistance, target, Time.deltaTime * 3f);
            }

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
            cinemachineTargetYaw = eul.y;
            cinemachineCameraTarget.transform.localRotation = Quaternion.identity;
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = transform.position + Vector3.down * groundedOffset;
            grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers,
                QueryTriggerInteraction.Ignore);
            if (animator != null) animator.SetBool(animIDGrounded, grounded);
        }

        private void Move()
        {
            if (controller == null) return;

            var mv = input != null ? input.Move : Vector2.zero;
            var mvUsed = _cursorUsable ? mv : Vector2.zero;

            var canSprint = !FirstPersonView || (Mathf.Abs(mvUsed.x) < 0.1f && mvUsed.y > 0.1f);
            var targetSpeed = input != null && input.SprintHeld && canSprint ? sprintSpeed : moveSpeed;
            if (mv == Vector2.zero) targetSpeed = 0f;

            var currentSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
            var inputMagnitude = mvUsed.magnitude;

            if (Mathf.Abs(currentSpeed - targetSpeed) > 0.1f)
                speed = Mathf.Round(Mathf.Lerp(currentSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * speedChangeRate) * 1000f) / 1000f;
            else
                speed = targetSpeed;

            animationBlend = Mathf.Lerp(animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);
            if (animationBlend < 0.01f) animationBlend = 0f;

            var inputDir = new Vector3(mvUsed.x, 0f, mvUsed.y).normalized;
            var camYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
            targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camYaw;

            if (mv != Vector2.zero)
            {
                var rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity,
                    rotationSmoothTime);

                if (!FirstPersonView && _cursorUsable)
                    transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            }

            if (FirstPersonView && _cursorUsable)
                transform.rotation = Quaternion.Euler(0f, camYaw, 0f);

            var moveDir = Quaternion.Euler(0f, targetRotation, 0f) * Vector3.forward;

            if (_cursorUsable)
                controller.Move(moveDir.normalized * (speed * Time.deltaTime) +
                                Vector3.up * verticalVelocity * Time.deltaTime);

            UpdateAnimByKinematics();
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

            bool allowLook = _cursorUsable || (LookCameraLimitRotation && !FirstPersonView);
            Vector2 look = allowLook ? input.Look : Vector2.zero;

            if (look.sqrMagnitude >= Threshold && !LockCameraPosition)
            {
                float multiplier = Time.deltaTime * 60f;
                cinemachineTargetYaw += look.x * multiplier;
                cinemachineTargetPitch += look.y * multiplier;
            }

            cinemachineTargetYaw = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
            cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);
            cinemachineCameraTarget.transform.rotation = Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride,
                cinemachineTargetYaw, 0f);
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
                AudioSource.PlayClipAtPoint(landingAudioClip, transform.TransformPoint(controller.center),
                    footstepAudioVolume);
        }

        private float _lastIkSendTime;
        [SerializeField] private float ikSendRate = 1f / 30f;
        private Vector3 _lastSentLookPos;
        private float _lastSentWeight;
        private bool spawned = false;

        [ServerRpc(RunLocally = true)]
        private void SyncIKServerRpc(Vector3 lookPos, float weight)
        {
            networkLookAtPos.Value = lookPos;
            networkIkWeight.Value = weight;
        }

        public void SnapAimToCurrentCamera()
        {
            if (cinemachineCameraTarget == null) return;
            var e = cinemachineCameraTarget.transform.rotation.eulerAngles;
            cinemachineTargetPitch = e.x - cameraAngleOverride;
            cinemachineTargetYaw = e.y;
        }

        private void UpdateAnimByKinematics()
        {
            if (animator == null) return;

            var velocity = controller != null ? controller.velocity : Vector3.zero;
            var horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

            var thisYaw = transform.eulerAngles.y;
            var deltaYaw = thisYaw - _lastYaw;
            deltaYaw = deltaYaw > 180f ? deltaYaw - 360f : deltaYaw <= -180f ? deltaYaw + 360f : deltaYaw;
            var angularSpeedDeg = Time.deltaTime > 0f ? deltaYaw / Time.deltaTime : 0f;
            _lastYaw = thisYaw;

            _animSpeed = Mathf.Lerp(_animSpeed, horizontalSpeed, Time.deltaTime * speedLerp);
            _animTurn = Mathf.Lerp(_animTurn, angularSpeedDeg, Time.deltaTime * angLerp);

            animator.SetFloat(animIDSpeed, _animSpeed);
            var normalized = Mathf.Clamp01(_animSpeed / sprintSpeed);
            animator.SetFloat(animIDMotionSpeed, normalized);
            var localVel = transform.InverseTransformDirection(velocity);
            var localForward = localVel.z;
            var localRight = localVel.x;

            vertical = Mathf.Lerp(vertical, localForward, Time.deltaTime * 5f);
            animator.SetFloat(animIDVertical, vertical);

            horizontal = Mathf.Lerp(horizontal, localRight, Time.deltaTime * 5f);
            animator.SetFloat(animIDHorizontal, horizontal);

            var turnClamped = Mathf.Clamp(_animTurn, -360f, 360f);
            animator.SetFloat(animIDTurn, turnClamped);

            animator.SetFloat(animIDFPV, FirstPersonView ? 1f : 0f);
        }

        public void PlayEmotionAnimation(int index)
        {
            if (!IsOwner) return; // только локальный владелец
            if (animator == null) return; // защитная проверка

            // Перезапуск, если уже играется
            if (_emotionRoutine != null)
            {
                //StopCoroutine(_emotionRoutine);
                //_emotionRoutine = null;
                return;
            }

            _wasFPV = _firstPersonView;
            ForceEnterFPV(false);
            _emotionRoutine = StartCoroutine(PlayEmotionRoutine(index));
        }

        private IEnumerator PlayEmotionRoutine(int index)
        {
            CanMove = false;
            SuppressLookAtIK = true;

            // Включаем параметры эмоции
            animator.SetBool(animIDEmotionActive, true);
            animator.SetFloat(animIDEmotionIndex, index);

            // Дать Animator один апдейт, чтобы применился стейт
            yield return null;

            // Определяем длительность текущего клипа на нужном слое
            float clipLength = 0f;
            var stateInfo = animator.GetCurrentAnimatorStateInfo(emotionAnimatorLayer);
            if (stateInfo.length > 0f)
            {
                clipLength = stateInfo.length / Mathf.Max(0.0001f, Mathf.Abs(stateInfo.speed));
            }

            // Если клипа нет (длительность 0), пробуем подождать смену стейта небольшое время
            if (clipLength <= 0f)
            {
                float waitForStateTimeout = 0.25f;
                float t = 0f;
                while (t < waitForStateTimeout)
                {
                    yield return null;
                    t += Time.deltaTime;

                    stateInfo = animator.GetCurrentAnimatorStateInfo(emotionAnimatorLayer);
                    if (stateInfo.length > 0f)
                    {
                        clipLength = stateInfo.length / Mathf.Max(0.0001f, Mathf.Abs(stateInfo.speed));
                        if (clipLength > 0f) break;
                    }
                }
            }

            // Если так и не нашли длительность — используем дефолт, например 1 секунду
            if (clipLength <= 0f) clipLength = 1f;

            // Ждём завершения клипа
            float timer = 0f;
            while (timer < clipLength)
            {
                // Если объект потерял владение/Animator исчез — выходим
                if (!IsOwner || animator == null) break;

                timer += Time.deltaTime;
                yield return null;
            }

            // Выключаем эмоцию и возвращаем управление
            if (animator != null)
            {
                animator.SetBool(animIDEmotionActive, false);
                //animator.SetFloat(animIDEmotionIndex, 0);
            }

            CanMove = true;
            _emotionRoutine = null;
            SuppressLookAtIK = false;
            FirstPersonView = _wasFPV;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            if (IsOwner)
            {
                if (SuppressLookAtIK || Time.time < _ikSuppressUntil) return;

                currentIkWeight = FirstPersonView ? 1f : 0f;

                if (Time.unscaledTime - _lastIkSendTime >= ikSendRate)
                {
                    var nowPos = headTarget != null
                        ? headTarget.position
                        : transform.position + transform.forward * 10f;
                    if ((Vector3.SqrMagnitude(_lastSentLookPos - nowPos) > 0.0001f) ||
                        (Mathf.Abs(_lastSentWeight - currentIkWeight) > 0.001f))
                    {
                        _lastSentLookPos = nowPos;
                        _lastSentWeight = currentIkWeight;
                        _lastIkSendTime = Time.unscaledTime;
                        SyncIKServerRpc(_lastSentLookPos, _lastSentWeight);
                    }
                }

                animator.SetLookAtWeight(currentIkWeight, 0f, currentIkWeight, currentIkWeight, lookAtClampWeight);
                if (headTarget != null)
                    animator.SetLookAtPosition(headTarget.position);
            }
            else
            {
                _syncWeight = Mathf.Lerp(_syncWeight, networkIkWeight.Value, Time.deltaTime * 5f);
                _lookPos = Vector3.Lerp(_lookPos, networkLookAtPos.Value, Time.deltaTime * 5f);
                animator.SetLookAtWeight(_syncWeight, 0f, _syncWeight, _syncWeight, lookAtClampWeight);
                animator.SetLookAtPosition(_lookPos);
            }
        }

        #region IMigratable

        public void OnMigrateDataReceived_Server(CharacterMigrateData data)
        {
            if (NetworkManager.IsServerStarted)
                SetPlayerState(Owner, data);
        }

        [TargetRpc]
        private void SetPlayerState(NetworkConnection conn, CharacterMigrateData data)
        {
            cinemachineTargetPitch = data.cinemachineTargetPitch;
            cinemachineTargetYaw = data.cinemachineTargetYaw;
            cameraDistance = data.cameraDistance;
            FirstPersonView = data.isFirstPersonView;
        }

        public CharacterMigrateData GetMigrateData_Client()
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