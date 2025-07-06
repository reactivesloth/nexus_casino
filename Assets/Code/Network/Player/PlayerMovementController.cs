using Cinemachine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : NetworkBehaviour
    {
        public bool CanMove = true;
        
        public float MoveSpeed = 2.0f;
        public float SprintSpeed = 5.335f;
        public float RotationSmoothTime = 0.12f;
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;
        public float JumpTimeout = 0.50f;
        public float FallTimeout = 0.15f;
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;
        
        public GameObject CinemachineCameraTarget;
        public bool FirstPersonView = true;
        public GameObject[] HideForFirstPersonViewLocal;
        public float MinCameraDistance = 1f;
        public float MaxCameraDistance = 4f;
        private float cameraDistance = 3f;
        private float savedDistance = 3f;
        public float MinFOV = 40;
        public float MaxFOV = 65;
        private float fov = 35;
        public float TopClamp = 70.0f;
        public float BottomClamp = -30.0f;
        public float CameraAngleOverride = 0.0f;
        public bool LockCameraPosition = false;

        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private CinemachineVirtualCamera _cinemachineVirtualCamera;
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDVertical;
        private int _animIDHorizontal;
        private int _animIDTurn;

        private Animator _animator;
        private CharacterController _controller;
        private GameObject _mainCamera;
        private PlayerInput _input;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        private Quaternion previousRotation;

        private float _horizontal;
        private float _vertical;
        private float _turning;
        private float _spd;
        
        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }

            if (_input == null)
            {
                _input = gameObject.GetComponent<PlayerInput>();
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();

            AssignAnimationIDs();

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            cameraDistance = 0.5f;
            savedDistance = 0.5f;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            
            if (!IsOwner)
                return;
            
            _cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        private void Update()
        {
            if(!IsOwner) return;
            
            if (_cinemachineVirtualCamera == null)
            {
                _cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            }

            _hasAnimator = TryGetComponent(out _animator);

            if (CanMove)
            {
                JumpAndGravity();
                GroundedCheck();
                Move();
            }
        }
        private void LateUpdate()
        {
            if(!IsOwner) return;

            if (CanMove)
                UpdateCameraDistance();

            if (CanMove || !FirstPersonView)
                CameraRotation();
            else
            {
                if (!FirstPersonView) return;
                _cinemachineTargetPitch = CinemachineCameraTarget.transform.rotation.eulerAngles.x - CameraAngleOverride;
                _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(0, 0, 0);
            }
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDVertical = Animator.StringToHash("Vertical");
            _animIDHorizontal = Animator.StringToHash("Horizontal");
            _animIDTurn = Animator.StringToHash("TurnAngle");
        }

        private void UpdateCameraDistance ()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (FirstPersonView)
                {
                    cameraDistance = savedDistance;
                }
                else
                {
                    savedDistance = cameraDistance;
                    cameraDistance = 0;
                }
            }

            cameraDistance -= Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * 100;
            cameraDistance = Mathf.Clamp(cameraDistance, 0, 1);

            FirstPersonView = cameraDistance switch
            {
                < 0.1f when !FirstPersonView => true,
                > 0.1f when FirstPersonView => false,
                _ => FirstPersonView
            };

            Cinemachine3rdPersonFollow follow = _cinemachineVirtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            _cinemachineVirtualCamera.Follow = CinemachineCameraTarget.transform;
            follow.ShoulderOffset = new Vector3(0, FirstPersonView ? 0 : -0.15f, 0);
            follow.CameraDistance = Mathf.Lerp(follow.CameraDistance, FirstPersonView ? 0 : Mathf.Lerp(MinCameraDistance, MaxCameraDistance, cameraDistance), Time.deltaTime * 3);
            _cinemachineVirtualCamera.m_Lens.FieldOfView = Mathf.Lerp(_cinemachineVirtualCamera.m_Lens.FieldOfView, Mathf.Lerp(MinFOV + (_speed > MoveSpeed ? 15 : 0), MaxFOV + (_speed > MoveSpeed ? 15 : 0), cameraDistance), Time.deltaTime * 3);
            foreach (var o in HideForFirstPersonViewLocal)
            {
                o.SetActive(!FirstPersonView);
            }
        }
        
        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private Vector3 GetAngularVelocity (Quaternion foreLastFrameRotation, Quaternion lastFrameRotation)
        {
            var q = lastFrameRotation * Quaternion.Inverse(foreLastFrameRotation);
            if(Mathf.Abs(q.w) > 1023.5f / 1024.0f)
                return new Vector3(0,0,0);
            float gain;
            if(q.w < 0.0f)
            {
                var angle = Mathf.Acos(-q.w);
                gain = -2.0f * angle / (Mathf.Sin(angle)*Time.deltaTime);
            }
            else
            {
                var angle = Mathf.Acos(q.w);
                gain = 2.0f * angle / (Mathf.Sin(angle)*Time.deltaTime);
            }
            return new Vector3(q.x * gain,q.y * gain,q.z * gain);
        }
        
        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = Input.mousePositionDelta.magnitude > 0 ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            var canSprint = !FirstPersonView || (Mathf.Abs(_input.move.x) < 0.1f && _input.move.y > 0.1f);
            float targetSpeed = _input.sprint && canSprint ? SprintSpeed : MoveSpeed;

            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

                if (FirstPersonView)
                    transform.rotation = Quaternion.Euler(0.0f, _mainCamera.transform.eulerAngles.y, 0.0f);
                else if (_input.move != Vector2.zero)
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);


                Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

                _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                                 new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

                if (_hasAnimator)
            {
                var velocity = transform.InverseTransformDirection(_controller.velocity);
                _vertical = Mathf.Lerp(_vertical, velocity.normalized.z * (_speed > MoveSpeed ? 2 : 1), Time.deltaTime * 5);
                _horizontal = Mathf.Lerp(_horizontal, velocity.normalized.x, Time.deltaTime * 5);

                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
                _animator.SetFloat(_animIDVertical, _vertical); 
                _animator.SetFloat(_animIDHorizontal, _horizontal);
                previousRotation = transform.rotation;
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}