#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class AlwaysEquippedWeaponController : MonoBehaviour
{
    [Header("永久持枪设置")]
    [Tooltip("把 WeaponHolder 下面的 Shotgun final 拖到这里。")]
    public PlayerWeapon equippedWeapon;

    [Tooltip("把 MainCamera 下面的 WeaponHolder 拖到这里。")]
    public Transform weaponHolder;

    [Header("持枪姿势微调")]
    public Vector3 weaponLocalPosition =
        Vector3.zero;

    public Vector3 weaponLocalEulerAngles =
        Vector3.zero;

    public Vector3 weaponLocalScale =
        new Vector3(
            0.3f,
            0.3f,
            0.3f
        );

    [Header("输入设置")]
    public bool allowShooting = true;

    [Header("外部视觉偏移（运行时）")]
    [SerializeField]
    private Vector3 currentExternalPositionOffset;

    [SerializeField]
    private Vector3 currentExternalEulerOffset;

    [SerializeField]
    private Vector3 targetExternalPositionOffset;

    [SerializeField]
    private Vector3 targetExternalEulerOffset;

    [SerializeField]
    private float externalOffsetResponseSpeed = 20f;

    [Header("🏃 Locomotion Viewmodel Motion")]
    [Tooltip("Adds restrained running bob, strafe weight and look sway to the complete first-person weapon/hands rig.")]
    public bool enableLocomotionMotion = true;

    [Min(0.5f)]
    [Tooltip("Full-speed bob cycles per second. Vertical motion produces two footfall beats per cycle.")]
    public float runBobFrequency = 1.40f;

    [Range(0f, 0.08f)]
    public float runBobSideAmplitude = 0.018f;

    [Range(0f, 0.08f)]
    public float runBobVerticalAmplitude = 0.0155f;

    [Range(0f, 0.08f)]
    public float runBobDepthAmplitude = 0.009f;

    [Range(0f, 5f)]
    public float runBobPitchDegrees = 0.72f;

    [Range(0f, 5f)]
    public float runBobYawDegrees = 0.48f;

    [Range(0f, 5f)]
    public float runBobRollDegrees = 0.92f;

    [Range(0f, 0.05f)]
    public float strafePositionAmplitude = 0.0095f;

    [Range(0f, 5f)]
    public float strafeRollDegrees = 0.72f;

    [Range(0f, 5f)]
    [Tooltip("Maximum weapon lag when turning the camera quickly.")]
    public float lookSwayYawDegrees = 1.75f;

    [Range(0f, 5f)]
    public float lookSwayPitchDegrees = 1.35f;

    [Range(0f, 0.04f)]
    public float lookSwayPositionX = 0.010f;

    [Range(0f, 0.04f)]
    public float lookSwayPositionY = 0.005f;

    [Min(1f)]
    public float locomotionBlendResponse = 12f;

    [Min(1f)]
    [Tooltip("How quickly periodic bob settles once real planar movement has stopped.")]
    public float locomotionStopResponse = 30f;

    [Range(0.02f, 1f)]
    [Tooltip("Real CharacterController planar speed below this value is treated as fully stopped for bobbing.")]
    public float actualMovementThreshold = 0.12f;

    [Min(1f)]
    public float lookSwayResponse = 16f;

    [HideInInspector]
    public float adsMotionMultiplier = 0.16f;

    [Header("🎯 ADS Motion Suppression")]
    [Range(0f, 0.25f)]
    [Tooltip("Periodic running bob is almost completely removed while ADS is active.")]
    public float adsBobMultiplier = 0.025f;

    [Range(0f, 0.25f)]
    [Tooltip("Keep only a tiny amount of mouse-look lag in ADS so the viewmodel remains alive without drifting.")]
    public float adsLookSwayMultiplier = 0.08f;

    [Header("⚡ Acceleration / Braking Inertia")]
    [Range(0f, 0.05f)]
    [Tooltip("How far the weapon settles downward during a strong acceleration impulse.")]
    public float accelerationDropY = 0.014f;

    [Range(0f, 0.05f)]
    [Tooltip("How far the weapon lags toward the player while acceleration is building.")]
    public float accelerationLagZ = 0.018f;

    [Range(0f, 5f)]
    public float accelerationPitchDegrees = 1.10f;

    [Range(0f, 0.05f)]
    [Tooltip("Small opposite overshoot when releasing movement.")]
    public float brakingLiftY = 0.007f;

    [Range(0f, 0.05f)]
    public float brakingPushZ = 0.010f;

    [Range(0f, 5f)]
    public float brakingPitchDegrees = 0.55f;

    [Range(0f, 0.04f)]
    [Tooltip("Small high-speed ready stance offset. Positive X moves the weapon slightly outward.")]
    public float highSpeedStanceOutwardX = 0.006f;

    [Range(0f, 0.04f)]
    public float highSpeedStanceDownY = 0.008f;

    [Range(0f, 5f)]
    public float highSpeedStanceRollDegrees = 0.42f;

    [Min(1f)]
    public float inertiaImpulseResponse = 13f;

    [Min(1f)]
    public float inertiaSettleResponse = 16f;

    public Vector3 CurrentExternalPositionOffset =>
        currentExternalPositionOffset;

    public Vector3 CurrentExternalEulerOffset =>
        currentExternalEulerOffset;

    private int _weaponLayer;

    private StarterAssets.FirstPersonController _firstPersonController;
    private CharacterController _characterController;
    private Transform _viewTransform;

    private float _locomotionPhase;
    private float _locomotionBlend;
    private Vector3 _locomotionPositionOffset;
    private Vector3 _locomotionEulerOffset;
    private Vector3 _lookSwayPosition;
    private Vector3 _lookSwayEuler;
    private Quaternion _previousViewRotation;
    private bool _hasPreviousViewRotation;

    private float _previousMoveSpeed;
    private bool _hasPreviousMoveSpeed;
    private float _accelerationImpulse;

    public Vector3 CurrentLocomotionPositionOffset =>
        _locomotionPositionOffset;

    public Vector3 CurrentLocomotionEulerOffset =>
        _locomotionEulerOffset;

    private void Start()
    {
        _weaponLayer =
            LayerMask.NameToLayer(
                "Weapon"
            );

        ResolveReferences();

        if (equippedWeapon == null)
        {
            Debug.LogWarning(
                "⚠️ [AlwaysEquippedWeaponController] " +
                "没有找到 PlayerWeapon。请把 WeaponHolder 下的武器拖到 Equipped Weapon。"
            );

            return;
        }

        if (weaponHolder == null)
        {
            Debug.LogWarning(
                "⚠️ [AlwaysEquippedWeaponController] " +
                "没有找到 WeaponHolder。请手动拖拽 WeaponHolder。"
            );

            return;
        }

        SetupPermanentWeapon();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused) return;

        if (equippedWeapon == null)
        {
            return;
        }

        if (!allowShooting)
        {
            equippedWeapon.CancelAdsCharge();
            return;
        }

        if (Mouse.current == null)
        {
            equippedWeapon.CancelAdsCharge();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            equippedWeapon.HandleFirePressed();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            equippedWeapon.HandleFireReleased();
        }
    }

    private void OnDisable()
    {
        if (equippedWeapon != null)
        {
            equippedWeapon.CancelAdsCharge();
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (equippedWeapon == null ||
            weaponHolder == null)
        {
            return;
        }

        UpdateExternalVisualOffset();
        UpdateLocomotionVisualOffset();

        equippedWeapon.transform.SetParent(
            weaponHolder
        );

        equippedWeapon.transform.localPosition =
            weaponLocalPosition +
            currentExternalPositionOffset +
            _locomotionPositionOffset;

        equippedWeapon.transform.localRotation =
            Quaternion.Euler(
                weaponLocalEulerAngles +
                currentExternalEulerOffset +
                _locomotionEulerOffset
            );

        equippedWeapon.transform.localScale =
            weaponLocalScale;
    }

    public void SetExternalVisualOffset(
        Vector3 localPositionOffset,
        Vector3 localEulerOffset,
        float responseSpeed = 20f
    )
    {
        targetExternalPositionOffset =
            localPositionOffset;

        targetExternalEulerOffset =
            localEulerOffset;

        externalOffsetResponseSpeed =
            Mathf.Max(
                1f,
                responseSpeed
            );
    }

    public void ClearExternalVisualOffset(
        float recoverySpeed = 20f
    )
    {
        targetExternalPositionOffset =
            Vector3.zero;

        targetExternalEulerOffset =
            Vector3.zero;

        externalOffsetResponseSpeed =
            Mathf.Max(
                1f,
                recoverySpeed
            );
    }

    public void SnapExternalVisualOffset(
        Vector3 localPositionOffset,
        Vector3 localEulerOffset
    )
    {
        targetExternalPositionOffset =
            localPositionOffset;

        targetExternalEulerOffset =
            localEulerOffset;

        currentExternalPositionOffset =
            localPositionOffset;

        currentExternalEulerOffset =
            localEulerOffset;
    }

    public bool HasWeapon()
    {
        return equippedWeapon != null;
    }

    public PlayerWeapon GetWeapon()
    {
        return equippedWeapon;
    }

    private void ResolveReferences()
    {
        if (weaponHolder == null)
        {
            Transform foundHolder =
                transform.Find(
                    "MainCamera/WeaponHolder"
                );

            if (foundHolder == null &&
                Camera.main != null)
            {
                foundHolder =
                    Camera.main.transform.Find(
                        "WeaponHolder"
                    );
            }

            weaponHolder =
                foundHolder;
        }

        if (equippedWeapon == null)
        {
            equippedWeapon =
                GetComponentInChildren<PlayerWeapon>(
                    true
                );

            if (equippedWeapon == null &&
                weaponHolder != null)
            {
                equippedWeapon =
                    weaponHolder.GetComponentInChildren<PlayerWeapon>(
                        true
                    );
            }
        }

        if (_firstPersonController == null)
        {
            _firstPersonController =
                GetComponent<StarterAssets.FirstPersonController>();
        }

        if (_characterController == null)
        {
            _characterController =
                GetComponent<CharacterController>();
        }

        if (_viewTransform == null)
        {
            if (Camera.main != null)
            {
                _viewTransform = Camera.main.transform;
            }
            else if (weaponHolder != null)
            {
                _viewTransform = weaponHolder;
            }
        }
    }

    private void UpdateLocomotionVisualOffset()
    {
        float unscaledDeltaTime =
            Mathf.Max(0f, Time.unscaledDeltaTime);

        if (unscaledDeltaTime <= 0f)
        {
            return;
        }

        bool rewinding =
            _firstPersonController != null &&
            _firstPersonController.isRewinding;

        bool aiming =
            equippedWeapon != null &&
            equippedWeapon.IsAiming;

        bool grounded =
            _firstPersonController != null &&
            _firstPersonController.Grounded;

        bool sliding =
            _firstPersonController != null &&
            _firstPersonController.IsSliding;

        float maximumSpeed =
            _firstPersonController != null
                ? Mathf.Max(0.01f, _firstPersonController.MaxSprintSpeed)
                : 12f;

        // V3 deliberately measures REAL planar motion from CharacterController.
        // The movement controller's authored current-speed value can continue
        // decaying for a short time after the player has visually stopped.
        // That was one reason V2 kept bobbing after the character was stationary.
        Vector3 localVelocity =
            Vector3.zero;

        float actualPlanarSpeed =
            0f;

        if (_characterController != null &&
            _firstPersonController != null)
        {
            Vector3 planarVelocity =
                _characterController.velocity;

            planarVelocity.y = 0f;

            actualPlanarSpeed =
                planarVelocity.magnitude;

            localVelocity =
                _firstPersonController.transform.InverseTransformDirection(
                    planarVelocity
                );
        }
        else if (_firstPersonController != null)
        {
            actualPlanarSpeed =
                Mathf.Max(
                    0f,
                    _firstPersonController.CurrentMoveSpeed
                );
        }

        float actualSpeed01 =
            Mathf.Clamp01(
                actualPlanarSpeed /
                maximumSpeed
            );

        float shapedActualSpeed =
            Mathf.SmoothStep(
                0f,
                1f,
                actualSpeed01
            );

        bool isActuallyMoving =
            actualPlanarSpeed >
            Mathf.Max(
                0.02f,
                actualMovementThreshold
            );

        float targetLocomotionBlend =
            enableLocomotionMotion &&
            !rewinding &&
            grounded &&
            !sliding &&
            isActuallyMoving
                ? shapedActualSpeed
                : 0f;

        if (aiming)
        {
            targetLocomotionBlend *=
                Mathf.Clamp01(
                    adsBobMultiplier
                );
        }

        // Starting is smooth; stopping is intentionally much faster.
        // This is NOT a hard snap: the weapon still settles to neutral,
        // but it no longer completes extra sine-wave cycles after movement ends.
        float blendResponse =
            targetLocomotionBlend <
            _locomotionBlend
                ? locomotionStopResponse
                : locomotionBlendResponse;

        float locomotionT =
            1f -
            Mathf.Exp(
                -Mathf.Max(
                    1f,
                    blendResponse
                ) *
                unscaledDeltaTime
            );

        _locomotionBlend =
            Mathf.Lerp(
                _locomotionBlend,
                targetLocomotionBlend,
                locomotionT
            );

        // Crucial V3 change:
        // phase advances ONLY while the controller is physically moving.
        // When the player stops, the phase freezes immediately and only the
        // current offset decays back to neutral. No residual "shaking cycle".
        bool advanceBobPhase =
            enableLocomotionMotion &&
            !rewinding &&
            grounded &&
            !sliding &&
            isActuallyMoving;

        if (advanceBobPhase)
        {
            float scaledDeltaTime =
                Mathf.Max(
                    0f,
                    Time.deltaTime
                );

            float frequency =
                Mathf.Lerp(
                    runBobFrequency * 0.72f,
                    runBobFrequency,
                    shapedActualSpeed
                );

            _locomotionPhase +=
                scaledDeltaTime *
                Mathf.Max(
                    0.5f,
                    frequency
                ) *
                Mathf.PI *
                2f;

            if (_locomotionPhase >
                Mathf.PI * 4096f)
            {
                _locomotionPhase =
                    Mathf.Repeat(
                        _locomotionPhase,
                        Mathf.PI * 2f
                    );
            }
        }

        float sideWave =
            Mathf.Sin(
                _locomotionPhase
            );

        // Two vertical footfall beats per side-to-side cycle.
        // V2 used 2.15 cycles/sec => about 4.3 vertical beats/sec.
        // V3 uses 1.40 => about 2.8 beats/sec at full speed.
        float stepWave =
            Mathf.Cos(
                _locomotionPhase * 2f
            );

        float strafe01 =
            Mathf.Clamp(
                localVelocity.x /
                maximumSpeed,
                -1f,
                1f
            );

        Vector3 bobPosition =
            new Vector3(
                sideWave *
                runBobSideAmplitude,

                stepWave *
                runBobVerticalAmplitude,

                -Mathf.Cos(
                    _locomotionPhase
                ) *
                runBobDepthAmplitude
            ) *
            _locomotionBlend;

        bobPosition.x +=
            -strafe01 *
            strafePositionAmplitude *
            _locomotionBlend;

        Vector3 bobEuler =
            new Vector3(
                -stepWave *
                runBobPitchDegrees,

                -sideWave *
                runBobYawDegrees,

                -sideWave *
                runBobRollDegrees -
                strafe01 *
                strafeRollDegrees
            ) *
            _locomotionBlend;

        UpdateAccelerationInertia(
            actualPlanarSpeed,
            grounded,
            sliding,
            rewinding,
            aiming,
            unscaledDeltaTime
        );

        float stanceWeight =
            !aiming &&
            !rewinding &&
            grounded &&
            !sliding &&
            isActuallyMoving
                ? Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0.55f,
                        1f,
                        actualSpeed01
                    )
                )
                : 0f;

        Vector3 stancePosition =
            new Vector3(
                highSpeedStanceOutwardX,
                -highSpeedStanceDownY,
                0f
            ) *
            stanceWeight;

        Vector3 stanceEuler =
            new Vector3(
                0f,
                0f,
                -highSpeedStanceRollDegrees
            ) *
            stanceWeight;

        float accelerationWeight =
            Mathf.Max(
                0f,
                _accelerationImpulse
            );

        float brakingWeight =
            Mathf.Max(
                0f,
                -_accelerationImpulse
            );

        Vector3 inertiaPosition =
            new Vector3(
                0f,

                -accelerationDropY *
                accelerationWeight +
                brakingLiftY *
                brakingWeight,

                -accelerationLagZ *
                accelerationWeight +
                brakingPushZ *
                brakingWeight
            );

        Vector3 inertiaEuler =
            new Vector3(
                accelerationPitchDegrees *
                accelerationWeight -
                brakingPitchDegrees *
                brakingWeight,

                0f,
                0f
            );

        UpdateLookSway(
            unscaledDeltaTime,
            rewinding,
            aiming
        );

        _locomotionPositionOffset =
            bobPosition +
            stancePosition +
            inertiaPosition +
            _lookSwayPosition;

        _locomotionEulerOffset =
            bobEuler +
            stanceEuler +
            inertiaEuler +
            _lookSwayEuler;
    }

    private void UpdateAccelerationInertia(
        float currentSpeed,
        bool grounded,
        bool sliding,
        bool rewinding,
        bool aiming,
        float unscaledDeltaTime
    )
    {
        if (!_hasPreviousMoveSpeed)
        {
            _previousMoveSpeed =
                currentSpeed;

            _hasPreviousMoveSpeed =
                true;
        }

        float speedDelta =
            currentSpeed -
            _previousMoveSpeed;

        _previousMoveSpeed =
            currentSpeed;

        float rawAcceleration =
            speedDelta /
            Mathf.Max(
                0.0001f,
                unscaledDeltaTime
            );

        float positiveReference =
            _firstPersonController != null
                ? Mathf.Max(
                    0.01f,
                    _firstPersonController.AccelerationRate
                )
                : 8f;

        float negativeReference =
            _firstPersonController != null
                ? Mathf.Max(
                    0.01f,
                    _firstPersonController.DecelerationRate
                )
                : 12f;

        float targetImpulse =
            rawAcceleration >= 0f
                ? Mathf.Clamp(
                    rawAcceleration /
                    positiveReference,
                    0f,
                    1f
                )
                : Mathf.Clamp(
                    rawAcceleration /
                    negativeReference,
                    -1f,
                    0f
                );

        // ADS needs a stable shooting platform. The existing ADS pose remains,
        // but running bob and start/stop inertia are removed almost entirely.
        if (!enableLocomotionMotion ||
            rewinding ||
            aiming ||
            !grounded ||
            sliding)
        {
            targetImpulse = 0f;
        }

        float response =
            Mathf.Abs(targetImpulse) >
            Mathf.Abs(_accelerationImpulse)
                ? inertiaImpulseResponse
                : inertiaSettleResponse;

        float inertiaT =
            1f -
            Mathf.Exp(
                -Mathf.Max(1f, response) *
                unscaledDeltaTime
            );

        _accelerationImpulse =
            Mathf.Lerp(
                _accelerationImpulse,
                targetImpulse,
                inertiaT
            );

        if (Mathf.Abs(_accelerationImpulse) < 0.0001f &&
            Mathf.Abs(targetImpulse) < 0.0001f)
        {
            _accelerationImpulse = 0f;
        }
    }

    private void UpdateLookSway(
        float unscaledDeltaTime,
        bool rewinding,
        bool aiming
    )
    {
        if (_viewTransform == null)
        {
            float settleT =
                1f -
                Mathf.Exp(
                    -Mathf.Max(1f, lookSwayResponse) *
                    unscaledDeltaTime
                );

            _lookSwayPosition =
                Vector3.Lerp(
                    _lookSwayPosition,
                    Vector3.zero,
                    settleT
                );

            _lookSwayEuler =
                Vector3.Lerp(
                    _lookSwayEuler,
                    Vector3.zero,
                    settleT
                );

            return;
        }

        Quaternion currentViewRotation =
            _viewTransform.rotation;

        if (!_hasPreviousViewRotation)
        {
            _previousViewRotation =
                currentViewRotation;

            _hasPreviousViewRotation =
                true;
        }

        Quaternion deltaRotation =
            Quaternion.Inverse(
                _previousViewRotation
            ) *
            currentViewRotation;

        Vector3 deltaEuler =
            deltaRotation.eulerAngles;

        float pitchDelta =
            Mathf.DeltaAngle(
                0f,
                deltaEuler.x
            );

        float yawDelta =
            Mathf.DeltaAngle(
                0f,
                deltaEuler.y
            );

        _previousViewRotation =
            currentViewRotation;

        float pitchVelocity =
            Mathf.Clamp(
                pitchDelta / Mathf.Max(0.0001f, unscaledDeltaTime),
                -720f,
                720f
            );

        float yawVelocity =
            Mathf.Clamp(
                yawDelta / Mathf.Max(0.0001f, unscaledDeltaTime),
                -720f,
                720f
            );

        float pitch01 =
            Mathf.Clamp(
                pitchVelocity / 360f,
                -1f,
                1f
            );

        float yaw01 =
            Mathf.Clamp(
                yawVelocity / 360f,
                -1f,
                1f
            );

        float motionScale =
            rewinding || !enableLocomotionMotion
                ? 0f
                : (aiming
                    ? Mathf.Clamp01(adsLookSwayMultiplier)
                    : 1f);

        Vector3 targetPosition =
            new Vector3(
                -yaw01 * lookSwayPositionX,
                pitch01 * lookSwayPositionY,
                0f
            ) *
            motionScale;

        Vector3 targetEuler =
            new Vector3(
                -pitch01 * lookSwayPitchDegrees,
                -yaw01 * lookSwayYawDegrees,
                -yaw01 * lookSwayYawDegrees * 0.18f
            ) *
            motionScale;

        float swayT =
            1f -
            Mathf.Exp(
                -Mathf.Max(1f, lookSwayResponse) *
                unscaledDeltaTime
            );

        _lookSwayPosition =
            Vector3.Lerp(
                _lookSwayPosition,
                targetPosition,
                swayT
            );

        _lookSwayEuler =
            Vector3.Lerp(
                _lookSwayEuler,
                targetEuler,
                swayT
            );
    }

    private void UpdateExternalVisualOffset()
    {
        float interpolation =
            1f -
            Mathf.Exp(
                -Mathf.Max(
                    1f,
                    externalOffsetResponseSpeed
                ) *
                Time.unscaledDeltaTime
            );

        currentExternalPositionOffset =
            Vector3.Lerp(
                currentExternalPositionOffset,
                targetExternalPositionOffset,
                interpolation
            );

        currentExternalEulerOffset =
            Vector3.Lerp(
                currentExternalEulerOffset,
                targetExternalEulerOffset,
                interpolation
            );

        if (
            (
                currentExternalPositionOffset -
                targetExternalPositionOffset
            ).sqrMagnitude <
            0.0000001f
        )
        {
            currentExternalPositionOffset =
                targetExternalPositionOffset;
        }

        if (
            (
                currentExternalEulerOffset -
                targetExternalEulerOffset
            ).sqrMagnitude <
            0.000001f
        )
        {
            currentExternalEulerOffset =
                targetExternalEulerOffset;
        }
    }

    private void SetupPermanentWeapon()
    {
        equippedWeapon.gameObject.SetActive(
            true
        );

        equippedWeapon.transform.SetParent(
            weaponHolder
        );

        equippedWeapon.transform.localPosition =
            weaponLocalPosition;

        equippedWeapon.transform.localRotation =
            Quaternion.Euler(
                weaponLocalEulerAngles
            );

        equippedWeapon.transform.localScale =
            weaponLocalScale;

        Rigidbody rigidbodyComponent =
            equippedWeapon.GetComponent<Rigidbody>();

        if (rigidbodyComponent != null)
        {
            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.useGravity = false;
            rigidbodyComponent.linearVelocity =
                Vector3.zero;

            rigidbodyComponent.angularVelocity =
                Vector3.zero;
        }

        Collider[] colliders =
            equippedWeapon.GetComponentsInChildren<Collider>(
                true
            );

        foreach (Collider collider in colliders)
        {
            collider.enabled =
                false;
        }

        if (_weaponLayer >= 0)
        {
            SetLayerRecursively(
                equippedWeapon.gameObject,
                _weaponLayer
            );
        }

        Debug.Log(
            "✅ [AlwaysEquippedWeaponController] " +
            "永久持枪系统已启动，枪械已锁定到 WeaponHolder。"
        );
    }

    private void SetLayerRecursively(
        GameObject targetObject,
        int layer
    )
    {
        if (targetObject == null ||
            layer < 0)
        {
            return;
        }

        targetObject.layer =
            layer;

        foreach (Transform child in targetObject.transform)
        {
            SetLayerRecursively(
                child.gameObject,
                layer
            );
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        externalOffsetResponseSpeed =
            Mathf.Max(
                1f,
                externalOffsetResponseSpeed
            );

        runBobFrequency =
            Mathf.Max(
                0.5f,
                runBobFrequency
            );

        locomotionBlendResponse =
            Mathf.Max(
                1f,
                locomotionBlendResponse
            );

        locomotionStopResponse =
            Mathf.Max(
                1f,
                locomotionStopResponse
            );

        actualMovementThreshold =
            Mathf.Clamp(
                actualMovementThreshold,
                0.02f,
                1f
            );

        lookSwayResponse =
            Mathf.Max(
                1f,
                lookSwayResponse
            );

        adsMotionMultiplier =
            Mathf.Clamp01(
                adsMotionMultiplier
            );

        adsBobMultiplier =
            Mathf.Clamp(
                adsBobMultiplier,
                0f,
                0.25f
            );

        adsLookSwayMultiplier =
            Mathf.Clamp(
                adsLookSwayMultiplier,
                0f,
                0.25f
            );

        inertiaImpulseResponse =
            Mathf.Max(
                1f,
                inertiaImpulseResponse
            );

        inertiaSettleResponse =
            Mathf.Max(
                1f,
                inertiaSettleResponse
            );
    }
#endif
}
