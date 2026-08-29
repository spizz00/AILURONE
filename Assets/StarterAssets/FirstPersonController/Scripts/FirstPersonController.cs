#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using Unity.Cinemachine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController Instance;

        public event System.Action JumpPerformed;
        public event System.Action DoubleJumpPerformed;

        [Header("🚀 Apex 动量狂奔 (Momentum Sprint)")]
        public float MaxSprintSpeed = 12.0f;
        public float AccelerationRate = 8.0f;
        public float DecelerationRate = 12.0f;

        [Header("🛹 极限滑铲 (Apex Slide)")]
        public float MaxSlideBoost = 4.5f;
        public float MaxSlideSpeed = 16.5f;
        public float SlideDecayRate = 6.0f;
        public float CrouchHeight = 0.8f;
        public float SlideJumpBoost = 12.0f;
        public float SlideCooldown = 0.5f;

        [Header("🦘 喷气背包：二段跳 (Titanfall Double Jump)")]
        public int MaxJumps = 2;
        public float DoubleJumpBoostMultiplier = 1.25f;
        public float DoubleJumpForwardPunch = 8.0f;
        public float MaxDoubleJumpSpeed = 24.0f;
        public AudioClip doubleJumpSound;

        private int _currentJumps;
        private float _doubleJumpJuiceTimer = 0f;
        private AudioSource _audioSource;

        [Header("🎥 Apex 镜头魔法 (Camera Juice)")]
        public CinemachineCamera virtualCamera;
        public float normalFOV = 90f;
        public float sprintFOV = 105f;
        public float slideFOV = 115f;
        public float maxRollAngle = 3.5f;

        private float _authoredNormalFOV = 90f;
        private float _authoredSprintFOV = 105f;
        private float _authoredSlideFOV = 115f;
        private float _authoredRewindFOV = 150f;

        [Header("🎯 武器瞄准系统 (ADS)")]
        public float adsFOV = 40f;

        [Header("💥 外部 FOV 覆盖：Dash / JumpPad / Cannon / Lift")]
        public float externalFOVRecoverSpeed = 8f;

        private bool _externalFOVActive = false;
        private float _externalFOVTarget = 90f;
        private float _externalFOVLerpSpeed = 20f;
        private float _externalFOVTimer = 0f;
        private bool _externalFOVHasAutoRelease = false;

        [Header("💀 空间回溯拉扯 (Rewind Shake)")]
        public float rewindFOV = 150f;
        public float rewindMaxRoll = 12f;
        [HideInInspector] public bool isRewinding = false;

        [Header("🧲 重力大炮支持 (Gravity Cannon Support)")]
        [Tooltip("大炮发射后，短时间降低空中外力阻尼，让轨迹更稳定。")]
        public float launchNoDragAirFriction = 0.15f;

        private bool _isCannonControlled = false;
        private float _launchNoDragTimer = 0f;
        private float _cannonActionLockTimer = 0f;

        public event System.Action CannonControlStarted;

        [Header("🌀 重力上升场支持 (Gravity Lift Support)")]
        [Tooltip("玩家在 Gravity Lift 区域内时是否屏蔽跳跃。现在建议关闭。")]
        public bool blockJumpWhileGravityLifted = false;

        [Tooltip("进入 Gravity Lift 时是否刷新跳跃次数。建议开启，这样玩家可以在上升场内二段跳。")]
        public bool rechargeJumpsOnGravityLiftEnter = true;

        private float _gravityLiftTimer = 0f;
        private float _gravityLiftTargetUpSpeed = 9f;
        private float _gravityLiftAcceleration = 24f;
        private float _gravityLiftGravityMultiplier = 0.15f;

        [Header("🌀 TP 引导支持 (Teleport Channel Support)")]
        [SerializeField, Range(0.1f, 1f)]
        private float _teleportActiveMovementMultiplier = 1f;

        [SerializeField]
        private bool _teleportChannelActive = false;

        private bool _teleportAimBlocked = false;
        private bool _blockJumpRechargeUntilAirborneAfterTeleport = false;

        [Space(10)]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;
        public float RotationSpeed = 1.0f;
        public float JumpTimeout = 0.1f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.5f;
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 89.0f;
        public float BottomClamp = -89.0f;

        private float _cinemachineTargetPitch;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _currentSpeed;
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private Vector3 _externalVelocity = Vector3.zero;

        // Ground Bot 强化弹使用的短促受控击退。
        // 它与玩家输入叠加，不锁定视角或移动，并通过 CharacterController.Move 保持碰撞。
        private bool _controlledKnockbackActive = false;
        private Vector3 _controlledKnockbackDirection = Vector3.zero;
        private float _controlledKnockbackDistance = 0f;
        private float _controlledKnockbackDuration = 0f;
        private float _controlledKnockbackElapsed = 0f;
        private float _controlledKnockbackPreviousTravel = 0f;

        private float _jumpPadGraceTimer = 0f;

        private bool _isSliding = false;

        public float CurrentMoveSpeed =>
            _currentSpeed;

        public bool IsSliding =>
            _isSliding;

        private float _normalHeight;
        private float _normalCameraY;
        private Vector3 _slideDirection;
        private float _slideCooldownTimer = 0f;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private float _cinematicMovementInputScale = 1f;
        private float _cinematicLookInputScale = 1f;
        private float _cinematicPitchOffset;
        private float _cinematicRollOffset;
        private bool _cinematicActionsLocked;

        private AlwaysEquippedWeaponController _alwaysEquippedWeaponController;

        private readonly Collider[] _groundCheckHits = new Collider[16];

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private bool IsMouseAdsActive()
        {
#if ENABLE_INPUT_SYSTEM
            return
                AILURONEGameplayActionGate.AllowsGameplayActions &&
                _alwaysEquippedWeaponController != null &&
                _alwaysEquippedWeaponController.HasWeapon() &&
                !_teleportAimBlocked &&
                Mouse.current != null &&
                Mouse.current.rightButton.isPressed;
#else
            return false;
#endif
        }

        private void Awake()
        {
            Instance = this;

            _authoredNormalFOV = normalFOV;
            _authoredSprintFOV = sprintFOV;
            _authoredSlideFOV = slideFOV;
            _authoredRewindFOV = rewindFOV;

            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _alwaysEquippedWeaponController = GetComponent<AlwaysEquippedWeaponController>();

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            if (_controller != null)
            {
                _normalHeight = _controller.height;
            }

            if (CinemachineCameraTarget != null)
            {
                _normalCameraY = CinemachineCameraTarget.transform.localPosition.y;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _currentJumps = MaxJumps;

            if (virtualCamera != null)
            {
                virtualCamera.Lens.FieldOfView = AILURONEGameSettings.BaseFov;
            }
        }

        private void Update()
        {
            if (_controller != null && !_controller.enabled) return;
            if (_input == null || _controller == null) return;
            if (GameManager.Instance != null && GameManager.Instance.isGamePaused) return;

            if (_slideCooldownTimer > 0f)
            {
                _slideCooldownTimer -= Time.deltaTime;
            }

            if (_doubleJumpJuiceTimer > 0f)
            {
                _doubleJumpJuiceTimer -= Time.unscaledDeltaTime;
            }

            UpdateExternalFOVTimer();
            UpdateCannonActionLockTimer();
            UpdateGravityLiftTimer();

            if (_isCannonControlled)
            {
                ClearMovementInput();
                ApplyCameraJuice();
                return;
            }

            GroundedCheck();
            JumpAndGravity();
            Move();
            ApplyCameraJuice();
        }

        private bool _wasPausedLastFrame;
        private int _ignoreLookFrames;

        private void LateUpdate()
        {
            bool isPaused = GameManager.Instance != null && GameManager.Instance.isGamePaused;

            if (isPaused)
            {
                _wasPausedLastFrame = true;
                if (_input != null) _input.LookInput(Vector2.zero);
                return;
            }

            if (_wasPausedLastFrame)
            {
                _wasPausedLastFrame = false;
                _ignoreLookFrames = 2;
            }

            if (_ignoreLookFrames > 0)
            {
                _ignoreLookFrames--;
                if (_input != null) _input.LookInput(Vector2.zero);
                return;
            }

            CameraRotation();
        }

        private void UpdateExternalFOVTimer()
        {
            if (!_externalFOVActive) return;
            if (!_externalFOVHasAutoRelease) return;

            _externalFOVTimer -= Time.unscaledDeltaTime;

            if (_externalFOVTimer <= 0f)
            {
                ReleaseExternalFOV(externalFOVRecoverSpeed);
            }
        }

        private void UpdateCannonActionLockTimer()
        {
            if (_cannonActionLockTimer > 0f)
            {
                _cannonActionLockTimer -= Time.deltaTime;

                if (_cannonActionLockTimer < 0f)
                {
                    _cannonActionLockTimer = 0f;
                }
            }
        }

        private void UpdateGravityLiftTimer()
        {
            if (_gravityLiftTimer > 0f)
            {
                _gravityLiftTimer -= Time.deltaTime;

                if (_gravityLiftTimer < 0f)
                {
                    _gravityLiftTimer = 0f;
                }
            }
        }

        private void GroundedCheck()
        {
            if (_jumpPadGraceTimer > 0f)
            {
                _jumpPadGraceTimer -= Time.deltaTime;
                Grounded = false;
                return;
            }

            Vector3 spherePosition = new Vector3(
                transform.position.x,
                transform.position.y + GroundedOffset,
                transform.position.z
            );

            int hitCount = Physics.OverlapSphereNonAlloc(
                spherePosition,
                GroundedRadius,
                _groundCheckHits,
                GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            bool foundRealGround = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _groundCheckHits[i];
                if (hit == null) continue;

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                CharacterController hitController = hit.GetComponentInParent<CharacterController>();
                if (hitController != null && hitController == _controller)
                {
                    continue;
                }

                if (hit.isTrigger)
                {
                    continue;
                }

                foundRealGround = true;
                break;
            }

            Grounded = foundRealGround;
        }

        private void CameraRotation()
        {
            if (_input == null) return;
            if (CinemachineCameraTarget == null) return;

            Vector2 lookInput =
                _input.look * _cinematicLookInputScale;

            if (lookInput.sqrMagnitude > 0f)
            {
                bool isMouse = IsCurrentDeviceMouse;
                float deltaTimeMultiplier = isMouse
                    ? 1.0f
                    : Time.unscaledDeltaTime;
                float sensitivityMultiplier = isMouse
                    ? (IsMouseAdsActive()
                        ? MouseSensitivitySettings.AdsSensitivity
                        : MouseSensitivitySettings.HipfireSensitivity)
                    : 1f;
                float verticalLookMultiplier =
                    AILURONEGameSettings.InvertVerticalLook ? -1f : 1f;

                _cinemachineTargetPitch +=
                    lookInput.y *
                    verticalLookMultiplier *
                    RotationSpeed *
                    sensitivityMultiplier *
                    deltaTimeMultiplier;
                _rotationVelocity =
                    lookInput.x *
                    RotationSpeed *
                    sensitivityMultiplier *
                    deltaTimeMultiplier;

                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

                transform.Rotate(Vector3.up * _rotationVelocity);
            }

            float presentedPitch = ClampAngle(
                _cinemachineTargetPitch + _cinematicPitchOffset,
                BottomClamp,
                TopClamp);

            CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(
                presentedPitch,
                0.0f,
                _cinematicRollOffset
            );
        }

        private void Move()
        {
            Vector3 inputDirection = Vector3.zero;
            Vector2 moveInput =
                _input.move * _cinematicMovementInputScale;

            if (moveInput != Vector2.zero)
            {
                inputDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
                inputDirection.Normalize();
            }

            bool wantsToSlide = false;

#if ENABLE_INPUT_SYSTEM
            if (!_cinematicActionsLocked && Keyboard.current != null)
            {
                wantsToSlide = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
            }
#endif

            if (wantsToSlide && Grounded && moveInput.y > 0 && !_isSliding && _slideCooldownTimer <= 0f)
            {
                _isSliding = true;
                _slideDirection = inputDirection;

                float momentumRatio = Mathf.Clamp01(_currentSpeed / MaxSprintSpeed);
                float dynamicBoost = Mathf.Lerp(0.5f, MaxSlideBoost, momentumRatio);

                _currentSpeed += dynamicBoost;
                _currentSpeed = Mathf.Min(_currentSpeed, MaxSlideSpeed);

                _controller.height = CrouchHeight;
                _controller.center = new Vector3(0, CrouchHeight / 2f, 0);
            }

            if (_isSliding)
            {
                if (!wantsToSlide || _currentSpeed < 3.0f || !Grounded)
                {
                    Vector3 rayStart = transform.position + new Vector3(0, CrouchHeight, 0);

                    if (!Physics.Raycast(rayStart, Vector3.up, _normalHeight - CrouchHeight + 0.1f, GroundLayers))
                    {
                        StopSlide();
                    }
                }
            }

            if (CinemachineCameraTarget != null)
            {
                float targetCamY = _isSliding ? _normalCameraY - (_normalHeight - CrouchHeight) : _normalCameraY;
                Vector3 currentCamPos = CinemachineCameraTarget.transform.localPosition;

                CinemachineCameraTarget.transform.localPosition = Vector3.Lerp(
                    currentCamPos,
                    new Vector3(currentCamPos.x, targetCamY, currentCamPos.z),
                    Time.unscaledDeltaTime * 15f
                );
            }

            if (_isSliding)
            {
                _currentSpeed -= SlideDecayRate * Time.deltaTime;
                _currentSpeed = Mathf.Max(_currentSpeed, 0f);
                inputDirection = _slideDirection;
            }
            else
            {
                float targetSpeed = moveInput == Vector2.zero ? 0.0f : MaxSprintSpeed;
                float accelRate = moveInput == Vector2.zero ? DecelerationRate : AccelerationRate;

                _currentSpeed = Mathf.MoveTowards(
                    _currentSpeed,
                    targetSpeed,
                    Time.deltaTime * accelRate
                );
            }

            if (_launchNoDragTimer > 0f)
            {
                _launchNoDragTimer -= Time.deltaTime;

                if (_launchNoDragTimer < 0f)
                {
                    _launchNoDragTimer = 0f;
                }
            }

            _externalVelocity = Vector3.ClampMagnitude(_externalVelocity, 80f);

            float activeMovementMultiplier =
                _teleportChannelActive
                    ? Mathf.Clamp(
                        _teleportActiveMovementMultiplier,
                        0.1f,
                        1f
                    )
                    : 1f;

            Vector3 horizontalMove =
                inputDirection *
                (
                    _currentSpeed *
                    activeMovementMultiplier *
                    Time.deltaTime
                );
            Vector3 verticalMove = new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;
            Vector3 externalMove = _externalVelocity * Time.deltaTime;
            Vector3 controlledKnockbackMove =
                ConsumeControlledKnockbackDisplacement(Time.deltaTime);

            Vector3 moveVector =
                horizontalMove +
                verticalMove +
                externalMove +
                controlledKnockbackMove;

            _controller.Move(moveVector);

            float friction;

            if (_launchNoDragTimer > 0f)
            {
                friction = launchNoDragAirFriction;
            }
            else
            {
                friction = Grounded ? 8f : 1.5f;
            }

            _externalVelocity = Vector3.Lerp(_externalVelocity, Vector3.zero, Time.deltaTime * friction);
        }

        private void JumpAndGravity()
        {
            if (IsGravityLiftActive())
            {
                Grounded = false;
                _fallTimeoutDelta = FallTimeout;

                if (_jumpPadGraceTimer < 0.05f)
                {
                    _jumpPadGraceTimer = 0.05f;
                }

                float acceleration = Mathf.Max(1f, _gravityLiftAcceleration);

                _verticalVelocity = Mathf.MoveTowards(
                    _verticalVelocity,
                    _gravityLiftTargetUpSpeed,
                    acceleration * Time.deltaTime
                );

                _verticalVelocity += Gravity * Mathf.Clamp01(_gravityLiftGravityMultiplier) * Time.deltaTime;

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = Mathf.MoveTowards(
                        _verticalVelocity,
                        0f,
                        acceleration * Time.deltaTime
                    );
                }

                bool jumpBlocked =
                    blockJumpWhileGravityLifted ||
                    IsCannonActionLocked() ||
                    _cinematicActionsLocked;

                if (jumpBlocked && _input.jump)
                {
                    _input.jump = false;
                }

                if (!jumpBlocked && _input.jump && _jumpTimeoutDelta <= 0.0f && _currentJumps > 0)
                {
                    HandleJumpInput();
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }

                _input.jump = false;
                return;
            }

            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity <= 0.0f)
                {
                    if (!_blockJumpRechargeUntilAirborneAfterTeleport)
                    {
                        _currentJumps = MaxJumps;
                    }

                    _verticalVelocity = -2f;
                }
            }
            else
            {
                if (_blockJumpRechargeUntilAirborneAfterTeleport)
                {
                    _blockJumpRechargeUntilAirborneAfterTeleport = false;
                }

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
            }

            bool jumpBlockedByCannon =
                IsCannonActionLocked() ||
                _cinematicActionsLocked;

            if (jumpBlockedByCannon && _input.jump)
            {
                _input.jump = false;
            }

            if (!jumpBlockedByCannon && _input.jump && _jumpTimeoutDelta <= 0.0f && _currentJumps > 0)
            {
                HandleJumpInput();
            }

            if (_jumpTimeoutDelta >= 0.0f)
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }

            _input.jump = false;

            if (!Grounded)
            {
                if (_verticalVelocity > -_terminalVelocity)
                {
                    _verticalVelocity += Gravity * Time.deltaTime;
                }

                if (_verticalVelocity < -_terminalVelocity)
                {
                    _verticalVelocity = -_terminalVelocity;
                }
            }
        }

        private void HandleJumpInput()
        {
            bool isDoubleJump = !Grounded && _currentJumps < MaxJumps;

            float jumpVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

            if (IsGravityLiftActive())
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity, jumpVelocity);
            }
            else
            {
                _verticalVelocity = jumpVelocity;
            }

            if (isDoubleJump)
            {
                _currentSpeed *= DoubleJumpBoostMultiplier;

                Vector3 inputDir = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

                if (inputDir == Vector3.zero)
                {
                    inputDir = Vector3.forward;
                }

                Vector3 punchDirection = transform.right * inputDir.x + transform.forward * inputDir.z;
                _externalVelocity += punchDirection * DoubleJumpForwardPunch;

                _currentSpeed = Mathf.Min(_currentSpeed, MaxDoubleJumpSpeed);

                if (doubleJumpSound != null && _audioSource != null)
                {
                    _audioSource.pitch = Random.Range(1.1f, 1.4f);
                    _audioSource.PlayOneShot(doubleJumpSound, 0.8f);
                }

                _doubleJumpJuiceTimer = 0.2f;
                DoubleJumpPerformed?.Invoke();
            }
            else
            {
                if (_isSliding)
                {
                    Vector3 forwardBoost = transform.forward * SlideJumpBoost;
                    _externalVelocity += forwardBoost;

                    StopSlide();
                }

                JumpPerformed?.Invoke();
            }

            _currentJumps--;
            _jumpTimeoutDelta = JumpTimeout;
        }

        private void ApplyCameraJuice()
        {
            if (virtualCamera == null) return;

            float targetFOV = AILURONEGameSettings.BaseFov;
            float targetRoll = 0f;
            float lerpSpeed = 18f;

            bool isAiming = IsMouseAdsActive();

            if (isRewinding)
            {
                targetFOV = ApplyDynamicFovStrength(_authoredRewindFOV);
                targetRoll = Random.Range(-rewindMaxRoll, rewindMaxRoll);
                lerpSpeed = 40f;
            }
            else if (isAiming)
            {
                targetFOV = adsFOV;
                targetRoll = 0f;
                lerpSpeed = 22f;
            }
            else if (_isSliding)
            {
                targetFOV = ApplyDynamicFovStrength(_authoredSlideFOV);

                float slideRightAmount = Vector3.Dot(_slideDirection, transform.right);
                targetRoll = -slideRightAmount * maxRollAngle * 1.5f;
            }
            else if (Grounded)
            {
                float speedRatio =
                    Mathf.Clamp01(
                        _currentSpeed /
                        Mathf.Max(0.01f, MaxSprintSpeed)
                    );

                // Ordinary acceleration should read through the moving
                // viewmodel first, not through a constant camera zoom.
                float fovProgress =
                    Mathf.InverseLerp(
                        0.42f,
                        0.94f,
                        speedRatio
                    );

                fovProgress =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        fovProgress
                    );

                float forwardWeight =
                    Mathf.Clamp01(_input.move.y);

                float strafeWeight =
                    Mathf.Clamp01(
                        Mathf.Abs(_input.move.x)
                    ) *
                    0.35f;

                float directionWeight =
                    Mathf.Clamp01(
                        forwardWeight +
                        strafeWeight
                    );

                float authoredSprintTarget =
                    ApplyDynamicFovStrength(
                        _authoredSprintFOV
                    );

                float authoredBoost =
                    Mathf.Max(
                        0f,
                        authoredSprintTarget -
                        AILURONEGameSettings.BaseFov
                    );

                // Old grounded movement continuously exposed the full
                // 90 -> 105 style zoom. Keep only 42% for normal running.
                // High-impact actions retain their own existing FOV channels.
                float restrainedSprintTarget =
                    AILURONEGameSettings.BaseFov +
                    authoredBoost * 0.42f;

                targetFOV =
                    Mathf.Lerp(
                        AILURONEGameSettings.BaseFov,
                        restrainedSprintTarget,
                        fovProgress * directionWeight
                    );

                targetRoll =
                    -_input.move.x *
                    maxRollAngle *
                    0.32f;

                lerpSpeed =
                    targetFOV >
                    virtualCamera.Lens.FieldOfView
                        ? 6.5f
                        : 10.5f;
            }

            // 关键修复：
            // 外部 FOV 不能覆盖 ADS。
            // 之前 Lift FOV = 100 会压过 adsFOV = 40，所以你能打独头弹，但镜头不缩放。
            if (_externalFOVActive && !isAiming && !isRewinding)
            {
                targetFOV = Mathf.Max(
                    targetFOV,
                    ApplyDynamicFovStrength(_externalFOVTarget));
                lerpSpeed = Mathf.Max(lerpSpeed, _externalFOVLerpSpeed);
            }

            if (_doubleJumpJuiceTimer > 0f && !isAiming)
            {
                targetFOV += 12f * AILURONEGameSettings.DynamicFovStrength;
            }

            float fovBlend =
                1f -
                Mathf.Exp(
                    -Mathf.Max(1f, lerpSpeed) *
                    Time.unscaledDeltaTime
                );

            virtualCamera.Lens.FieldOfView =
                Mathf.Lerp(
                    virtualCamera.Lens.FieldOfView,
                    targetFOV,
                    fovBlend
                );

            float rollResponse =
                isRewinding
                    ? 50f
                    : lerpSpeed;

            float rollBlend =
                1f -
                Mathf.Exp(
                    -Mathf.Max(1f, rollResponse) *
                    Time.unscaledDeltaTime
                );

            virtualCamera.Lens.Dutch =
                Mathf.Lerp(
                    virtualCamera.Lens.Dutch,
                    targetRoll,
                    rollBlend
                );
        }

        private float ApplyDynamicFovStrength(float authoredTargetFov)
        {
            return AILURONEGameSettings.BaseFov +
                (authoredTargetFov - _authoredNormalFOV) *
                AILURONEGameSettings.DynamicFovStrength;
        }

        private void StopSlide()
        {
            _isSliding = false;
            _slideCooldownTimer = SlideCooldown;

            if (_controller != null)
            {
                _controller.height = _normalHeight;
                _controller.center = new Vector3(0, _normalHeight / 2f, 0);
            }
        }

        private void ClearMovementInput()
        {
            if (_input == null) return;

            _input.move = Vector2.zero;
            _input.jump = false;
            _input.sprint = false;
        }

        public float CinematicMovementInputScale =>
            _cinematicMovementInputScale;

        public float CinematicLookInputScale =>
            _cinematicLookInputScale;

        public bool CinematicActionsLocked =>
            _cinematicActionsLocked;

        /// <summary>
        /// Applies a temporary presentation layer without disabling gravity,
        /// collision, knockback, or the controller component itself.
        /// </summary>
        public void SetCinematicInputControl(
            float movementInputScale,
            float lookInputScale,
            bool lockActions,
            float pitchOffset = 0f,
            float rollOffset = 0f)
        {
            _cinematicMovementInputScale =
                Mathf.Clamp01(movementInputScale);
            _cinematicLookInputScale =
                Mathf.Clamp01(lookInputScale);
            _cinematicActionsLocked = lockActions;
            _cinematicPitchOffset = pitchOffset;
            _cinematicRollOffset = rollOffset;
        }

        public void ClearCinematicInputControl()
        {
            _cinematicMovementInputScale = 1f;
            _cinematicLookInputScale = 1f;
            _cinematicActionsLocked = false;
            _cinematicPitchOffset = 0f;
            _cinematicRollOffset = 0f;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;

            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        public void ApplyJumpPadForce(Vector3 launchVelocity)
        {
            _verticalVelocity = launchVelocity.y;
            _externalVelocity = new Vector3(launchVelocity.x, 0f, launchVelocity.z);

            Grounded = false;
            _jumpPadGraceTimer = 0.2f;

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public void AddExternalVelocity(Vector3 velocity, float maxExternalSpeed = 60f, bool forceAirborne = false)
        {
            _externalVelocity += velocity;
            _externalVelocity = Vector3.ClampMagnitude(_externalVelocity, maxExternalSpeed);

            if (forceAirborne)
            {
                Grounded = false;
                _jumpPadGraceTimer = 0.08f;
            }

            if (_isSliding)
            {
                StopSlide();
            }
        }

        /// <summary>
        /// 在指定时间内叠加一段可碰撞的受控位移。
        /// 玩家始终保留移动与视角控制；新击退会替换尚未完成的旧击退。
        /// </summary>
        public void ApplyControlledKnockback(
            Vector3 worldDirection,
            float distance,
            float duration
        )
        {
            Vector3 horizontalDirection =
                Vector3.ProjectOnPlane(
                    worldDirection,
                    Vector3.up
                );

            if (horizontalDirection.sqrMagnitude <= 0.0001f ||
                distance <= 0f ||
                duration <= 0f)
            {
                return;
            }

            _controlledKnockbackDirection =
                horizontalDirection.normalized;
            _controlledKnockbackDistance =
                Mathf.Max(0f, distance);
            _controlledKnockbackDuration =
                Mathf.Max(0.01f, duration);
            _controlledKnockbackElapsed = 0f;
            _controlledKnockbackPreviousTravel = 0f;
            _controlledKnockbackActive = true;

            if (_isSliding)
            {
                StopSlide();
            }
        }

        private Vector3 ConsumeControlledKnockbackDisplacement(
            float deltaTime
        )
        {
            if (!_controlledKnockbackActive || deltaTime <= 0f)
            {
                return Vector3.zero;
            }

            _controlledKnockbackElapsed += deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    _controlledKnockbackElapsed /
                    Mathf.Max(0.01f, _controlledKnockbackDuration)
                );

            // EaseOutCubic：命中瞬间冲击明显，随后快速收住。
            float easedProgress =
                1f - Mathf.Pow(1f - normalizedTime, 3f);

            float targetTravel =
                _controlledKnockbackDistance * easedProgress;

            float stepDistance =
                Mathf.Max(
                    0f,
                    targetTravel -
                    _controlledKnockbackPreviousTravel
                );

            _controlledKnockbackPreviousTravel = targetTravel;

            Vector3 displacement =
                _controlledKnockbackDirection * stepDistance;

            if (normalizedTime >= 1f)
            {
                _controlledKnockbackActive = false;
                _controlledKnockbackDirection = Vector3.zero;
                _controlledKnockbackDistance = 0f;
                _controlledKnockbackDuration = 0f;
                _controlledKnockbackElapsed = 0f;
                _controlledKnockbackPreviousTravel = 0f;
            }

            return displacement;
        }

        public void ApplyGravityLift(
            float targetUpSpeed,
            float acceleration,
            float gravityMultiplier,
            float keepAliveTime = 0.12f
        )
        {
            bool wasLiftInactive = _gravityLiftTimer <= 0f;

            _gravityLiftTimer = Mathf.Max(_gravityLiftTimer, keepAliveTime);
            _gravityLiftTargetUpSpeed = Mathf.Max(0f, targetUpSpeed);
            _gravityLiftAcceleration = Mathf.Max(1f, acceleration);
            _gravityLiftGravityMultiplier = Mathf.Clamp01(gravityMultiplier);

            if (wasLiftInactive && rechargeJumpsOnGravityLiftEnter)
            {
                _currentJumps = MaxJumps;
                _jumpTimeoutDelta = 0f;
            }

            Grounded = false;

            if (_jumpPadGraceTimer < 0.05f)
            {
                _jumpPadGraceTimer = 0.05f;
            }

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public void ApplyGravityLiftExitBoost(
            Vector3 boostVelocity,
            float launchGraceTime = 0.16f,
            float noDragDuration = 0.3f
        )
        {
            _gravityLiftTimer = 0f;

            Vector3 horizontalBoost = new Vector3(
                boostVelocity.x,
                0f,
                boostVelocity.z
            );

            _externalVelocity += horizontalBoost;
            _externalVelocity = Vector3.ClampMagnitude(_externalVelocity, 80f);

            if (boostVelocity.y > _verticalVelocity)
            {
                _verticalVelocity = boostVelocity.y;
            }

            Grounded = false;
            _jumpPadGraceTimer = Mathf.Max(_jumpPadGraceTimer, launchGraceTime);
            _launchNoDragTimer = Mathf.Max(_launchNoDragTimer, noDragDuration);

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public bool IsGravityLiftActive()
        {
            return _gravityLiftTimer > 0f;
        }

        public void BeginCannonControl(bool resetMomentum = true)
        {
            _isCannonControlled = true;
            CannonControlStarted?.Invoke();

            if (resetMomentum)
            {
                ResetMomentum();
            }

            ClearMovementInput();

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public void CancelCannonControl()
        {
            _isCannonControlled = false;
            ClearMovementInput();
        }

        public void EndCannonControlAndLaunch(
            Vector3 launchVelocity,
            float launchGraceTime = 0.25f,
            float maxHorizontalExternalSpeed = 80f,
            float noDragDuration = 0.75f,
            float actionLockDuration = 0.45f
        )
        {
            _isCannonControlled = false;

            ResetMomentum();

            Vector3 horizontalVelocity = new Vector3(
                launchVelocity.x,
                0f,
                launchVelocity.z
            );

            if (maxHorizontalExternalSpeed > 0f)
            {
                horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxHorizontalExternalSpeed);
            }

            _verticalVelocity = launchVelocity.y;
            _externalVelocity = horizontalVelocity;

            Grounded = false;
            _jumpPadGraceTimer = launchGraceTime;
            _launchNoDragTimer = noDragDuration;
            _cannonActionLockTimer = actionLockDuration;

            _currentJumps = MaxJumps;

            ClearMovementInput();

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public bool IsCannonControlled()
        {
            return _isCannonControlled;
        }

        public bool IsCannonActionLocked()
        {
            return _isCannonControlled || _cannonActionLockTimer > 0f;
        }

        // =========================================================
        // TP 引导 / 到达支持
        // =========================================================

        public void SetTeleportChannelState(
            bool active,
            float activeMovementMultiplier = 0.7f
        )
        {
            _teleportChannelActive = active;
            _teleportAimBlocked = active;

            _teleportActiveMovementMultiplier =
                active
                    ? Mathf.Clamp(
                        activeMovementMultiplier,
                        0.1f,
                        1f
                    )
                    : 1f;
        }

        public bool IsTeleportChannelActive()
        {
            return _teleportChannelActive;
        }

        public Vector3 GetTeleportPreservedVelocity()
        {
            if (_controller != null &&
                _controller.enabled)
            {
                Vector3 controllerVelocity =
                    _controller.velocity;

                if (controllerVelocity.sqrMagnitude >
                    0.0001f)
                {
                    return controllerVelocity;
                }
            }

            return new Vector3(
                _externalVelocity.x,
                _verticalVelocity,
                _externalVelocity.z
            );
        }

        public void ApplyTeleportArrivalVelocity(
            Vector3 preservedVelocity,
            float upwardVelocityRetention = 0.5f,
            bool preventImmediateJumpRecharge = true
        )
        {
            Vector3 horizontalVelocity =
                new Vector3(
                    preservedVelocity.x,
                    0f,
                    preservedVelocity.z
                );

            _currentSpeed = 0f;
            _externalVelocity =
                Vector3.ClampMagnitude(
                    horizontalVelocity,
                    80f
                );

            _verticalVelocity =
                preservedVelocity.y > 0f
                    ? preservedVelocity.y *
                      Mathf.Clamp01(
                          upwardVelocityRetention
                      )
                    : 0f;

            _gravityLiftTimer = 0f;
            _launchNoDragTimer =
                Mathf.Max(
                    _launchNoDragTimer,
                    0.08f
                );

            Grounded = false;
            _jumpPadGraceTimer =
                Mathf.Max(
                    _jumpPadGraceTimer,
                    0.08f
                );

            _blockJumpRechargeUntilAirborneAfterTeleport =
                preventImmediateJumpRecharge;

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public void ResetMomentum()
        {
            _currentSpeed = 0f;
            _verticalVelocity = 0f;
            _externalVelocity = Vector3.zero;
            _controlledKnockbackActive = false;
            _controlledKnockbackDirection = Vector3.zero;
            _controlledKnockbackDistance = 0f;
            _controlledKnockbackDuration = 0f;
            _controlledKnockbackElapsed = 0f;
            _controlledKnockbackPreviousTravel = 0f;
            _launchNoDragTimer = 0f;
            _gravityLiftTimer = 0f;

            if (_isSliding)
            {
                StopSlide();
            }
        }

        public void RequestExternalFOV(float targetFOV, float lerpSpeed, float autoReleaseTime = 0f)
        {
            _externalFOVActive = true;
            _externalFOVTarget = targetFOV;
            _externalFOVLerpSpeed = Mathf.Max(1f, lerpSpeed);

            if (autoReleaseTime > 0f)
            {
                _externalFOVTimer = autoReleaseTime;
                _externalFOVHasAutoRelease = true;
            }
            else
            {
                _externalFOVTimer = 0f;
                _externalFOVHasAutoRelease = false;
            }
        }

        public void ReleaseExternalFOV(float recoverSpeed = 8f)
        {
            _externalFOVActive = false;
            _externalFOVTimer = 0f;
            _externalFOVHasAutoRelease = false;
            externalFOVRecoverSpeed = recoverSpeed;
        }

        public void KeepExternalFOVUntilManualRelease()
        {
            _externalFOVHasAutoRelease = false;
            _externalFOVTimer = 0f;
        }

        public int GetCurrentJumps()
        {
            return _currentJumps;
        }

        public int GetMaxJumps()
        {
            return MaxJumps;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Grounded ? Color.green : Color.red;

            Vector3 spherePosition = new Vector3(
                transform.position.x,
                transform.position.y + GroundedOffset,
                transform.position.z
            );

            Gizmos.DrawWireSphere(spherePosition, GroundedRadius);
        }
    }
}
