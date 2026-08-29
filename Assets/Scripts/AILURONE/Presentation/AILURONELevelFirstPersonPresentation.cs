using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AILURONE.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class AILURONELevelFirstPersonPresentation : MonoBehaviour
    {
        [Serializable]
        public struct HandPose
        {
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            [Range(0f, 1f)] public float fingerCurl;
            public bool useIndividualFingerCurls;
            [Range(0f, 1f)] public float thumbCurl;
            [Range(0f, 1f)] public float indexCurl;
            [Range(0f, 1f)] public float middleCurl;
            [Range(0f, 1f)] public float ringCurl;
            [Range(0f, 1f)] public float littleCurl;

            public HandPose(
                Vector3 position,
                Vector3 eulerAngles,
                float curl)
            {
                localPosition = position;
                localEulerAngles = eulerAngles;
                fingerCurl = Mathf.Clamp01(curl);
                useIndividualFingerCurls = false;
                thumbCurl = fingerCurl;
                indexCurl = fingerCurl;
                middleCurl = fingerCurl;
                ringCurl = fingerCurl;
                littleCurl = fingerCurl;
            }

            public float GetFingerCurl(int finger)
            {
                if (!useIndividualFingerCurls)
                {
                    return Mathf.Clamp01(fingerCurl);
                }

                switch (finger)
                {
                    case 0:
                        return Mathf.Clamp01(thumbCurl);
                    case 1:
                        return Mathf.Clamp01(indexCurl);
                    case 2:
                        return Mathf.Clamp01(middleCurl);
                    case 3:
                        return Mathf.Clamp01(ringCurl);
                    default:
                        return Mathf.Clamp01(littleCurl);
                }
            }

            public void EnableIndividualFingerCurls()
            {
                float curl = Mathf.Clamp01(fingerCurl);
                useIndividualFingerCurls = true;
                thumbCurl = curl;
                indexCurl = curl;
                middleCurl = curl;
                ringCurl = curl;
                littleCurl = curl;
            }

            public void BlendFingerCurlsToward(float target, float weight)
            {
                float safeTarget = Mathf.Clamp01(target);
                float safeWeight = Mathf.Clamp01(weight);
                fingerCurl = Mathf.Lerp(fingerCurl, safeTarget, safeWeight);
                if (!useIndividualFingerCurls)
                {
                    return;
                }

                thumbCurl = Mathf.Lerp(thumbCurl, safeTarget, safeWeight);
                indexCurl = Mathf.Lerp(indexCurl, safeTarget, safeWeight);
                middleCurl = Mathf.Lerp(middleCurl, safeTarget, safeWeight);
                ringCurl = Mathf.Lerp(ringCurl, safeTarget, safeWeight);
                littleCurl = Mathf.Lerp(littleCurl, safeTarget, safeWeight);
            }

            public static HandPose Lerp(HandPose from, HandPose to, float t)
            {
                float safeT = Mathf.Clamp01(t);
                HandPose result = new HandPose(
                    Vector3.Lerp(from.localPosition, to.localPosition, safeT),
                    Quaternion.Slerp(
                        Quaternion.Euler(from.localEulerAngles),
                        Quaternion.Euler(to.localEulerAngles),
                        safeT).eulerAngles,
                    Mathf.Lerp(from.fingerCurl, to.fingerCurl, safeT));
                if (from.useIndividualFingerCurls ||
                    to.useIndividualFingerCurls)
                {
                    result.useIndividualFingerCurls = true;
                    result.thumbCurl = Mathf.Lerp(
                        from.GetFingerCurl(0), to.GetFingerCurl(0), safeT);
                    result.indexCurl = Mathf.Lerp(
                        from.GetFingerCurl(1), to.GetFingerCurl(1), safeT);
                    result.middleCurl = Mathf.Lerp(
                        from.GetFingerCurl(2), to.GetFingerCurl(2), safeT);
                    result.ringCurl = Mathf.Lerp(
                        from.GetFingerCurl(3), to.GetFingerCurl(3), safeT);
                    result.littleCurl = Mathf.Lerp(
                        from.GetFingerCurl(4), to.GetFingerCurl(4), safeT);
                }
                return result;
            }
        }

        [Header("Required References")]
        public PlayerWeapon weapon;
        public FirstPersonController firstPersonController;
        public CharacterController characterController;
        public DashController dashController;
        public TimeManager timeManager;
        public FinalGateManager finalGateManager;
        public AlwaysEquippedWeaponController alwaysEquippedWeaponController;
        public Transform weaponPresentationPivot;
        public Transform leftHandRoot;
        public Transform rightHandRoot;
        public Transform cameraTarget;

        [Header("Focus Presentation")]
        public Volume adsFocusVolume;
        public ParticleSystem timeSlowMotes;
        public Transform timeSlowEffectRoot;
        public LineRenderer timeSlowRingA;
        public LineRenderer timeSlowRingB;

        [Header("Authored Left-Hand Poses")]
        public HandPose idlePose;
        public HandPose adsReadyPose;
        public HandPose adsChargedPose;
        public HandPose dashPose;
        public HandPose doubleJumpPose;
        public HandPose fallingPose;
        public HandPose timeSlowPose;
        public HandPose timeStopPose;

        [Header("Authored Multi-Stage Gesture Poses")]
        public HandPose dashAnticipationPose;
        public HandPose dashRecoveryPose;
        public HandPose doubleJumpAnticipationPose;
        public HandPose doubleJumpRecoveryPose;
        public HandPose timeSlowAnticipationPose;
        public HandPose timeStopRecoveryPose;
        public HandPose linkAnticipationPose;
        public HandPose linkPlacementPose;
        public HandPose linkRecoveryPose;

        [Header("Pose Timing")]
        [Min(1f)] public float poseResponse = 13f;
        [Min(0.05f)] public float dashGestureDuration = 0.22f;
        [Min(0.05f)] public float doubleJumpGestureDuration = 0.34f;
        [Min(0.05f)] public float timeStartGestureDuration = 0.62f;
        [Min(0.05f)] public float timeStopGestureDuration = 0.28f;
        [Min(0.15f)] public float linkGestureDuration = 0.72f;
        [Min(0.05f)] public float hipfireReactionDuration = 0.16f;
        public float fallVelocityThreshold = -1.5f;

        [Header("Viewmodel Feedback")]
        public Vector3 adsPivotOffset = new Vector3(0f, -0.005f, -0.025f);
        public Vector3 fullChargePivotOffset = new Vector3(0f, -0.018f, -0.115f);
        public Vector3 shotPivotKick = new Vector3(0f, 0.015f, -0.105f);
        public Vector3 shotPivotEuler = new Vector3(-4.5f, 0f, 0.7f);
        public Vector3 rewindPivotOffset = new Vector3(0f, -1.25f, -0.22f);
        [Min(1f)] public float pivotResponse = 24f;
        [Min(1f)] public float rewindHideResponse = 24f;
        [Min(1f)] public float rewindReturnResponse = 12f;
        [Range(0f, 3f)] public float adsCameraPitchKick = 1.15f;
        [Range(0f, 2f)] public float adsCameraRollKick = 0.35f;
        [Range(0f, 0.5f)] public float adsVignetteIntensity = 0.12f;
        [Range(0f, 0.5f)] public float chargeVignetteIntensity = 0.12f;
        [Range(45f, 75f)] public float levelAdsFov = 65f;
        [Tooltip("When disabled outside the Level scene, this presentation leaves FirstPersonController.adsFOV unchanged.")]
        public bool overrideAdsFov = true;

        [Header("High-Impact Landing Camera")]
        [Min(1f)] public float landingMinVerticalSpeed = 8.5f;
        [Min(1f)] public float landingStrongVerticalSpeed = 22f;
        [Min(1f)] public float landingMinTotalSpeed = 13.5f;
        [Min(1f)] public float landingStrongTotalSpeed = 30f;
        [Min(0.05f)] public float landingImpactDuration = 0.36f;
        [Range(0f, 12f)] public float landingPitchDegrees = 6.2f;
        [Range(0f, 5f)] public float landingRollDegrees = 1.4f;

        private readonly List<Transform> _fingerBones = new List<Transform>(15);
        private readonly List<Quaternion> _fingerRestRotations =
            new List<Quaternion>(15);
        private readonly List<float> _fingerCurlWeights = new List<float>(15);
        private readonly List<int> _fingerCurlGroups = new List<int>(15);

        private Vignette _vignette;
        private Vector3 _currentLeftPosition;
        private Quaternion _currentLeftRotation;
        private float _currentThumbCurl;
        private float _currentIndexCurl;
        private float _currentMiddleCurl;
        private float _currentRingCurl;
        private float _currentLittleCurl;
        private Vector3 _currentPivotPosition;
        private Quaternion _currentPivotRotation = Quaternion.identity;
        private float _adsBlend;
        private float _shotKick01;
        private float _shotRollDirection = 1f;
        private float _dashRemaining;
        private float _doubleJumpRemaining;
        private float _timeStartRemaining;
        private float _timeStopRemaining;
        private float _linkRemaining;
        private float _hipfireRemaining;
        private float _landingImpactRemaining;
        private float _landingImpactStrength;
        private float _landingRollDirection = 1f;
        private float _maxAirborneDownSpeed;
        private float _maxAirborneTotalSpeed;
        private bool _wasGrounded;
        private int _lastInstalledSocketCount;
        private bool _wasTimeSlowActive;
        private bool _wasRewinding;
        private bool _initialized;
        private float _originalAdsFov;
        private bool _adsFovOverridden;

#if UNITY_EDITOR
        [NonSerialized] public bool editorPosePreviewActive;
        [NonSerialized] public HandPose editorPosePreviewPose;

        public void ApplyEditorPosePreviewImmediately()
        {
            if (!editorPosePreviewActive || leftHandRoot == null)
            {
                return;
            }

            leftHandRoot.localPosition = editorPosePreviewPose.localPosition;
            leftHandRoot.localRotation =
                Quaternion.Euler(editorPosePreviewPose.localEulerAngles);
            _currentLeftPosition = editorPosePreviewPose.localPosition;
            _currentLeftRotation =
                Quaternion.Euler(editorPosePreviewPose.localEulerAngles);
            SetCurrentFingerCurls(editorPosePreviewPose);
            ApplyFingerCurl(
                _currentThumbCurl,
                _currentIndexCurl,
                _currentMiddleCurl,
                _currentRingCurl,
                _currentLittleCurl);
        }
#endif

        public bool IsInitialized => _initialized;
        public int CachedFingerBoneCount => _fingerBones.Count;

        private void Awake()
        {
            MigrateLandingImpactDefaults();
            RefreshConfigurationCache();

            _currentLeftPosition = leftHandRoot != null
                ? leftHandRoot.localPosition
                : idlePose.localPosition;
            _currentLeftRotation = leftHandRoot != null
                ? leftHandRoot.localRotation
                : Quaternion.Euler(idlePose.localEulerAngles);
            SetCurrentFingerCurls(idlePose);
            _currentPivotPosition = weaponPresentationPivot != null
                ? weaponPresentationPivot.localPosition
                : Vector3.zero;
            _currentPivotRotation = weaponPresentationPivot != null
                ? weaponPresentationPivot.localRotation
                : Quaternion.identity;
            _wasTimeSlowActive =
                timeManager != null && timeManager.IsAbilityActive;
            _wasRewinding =
                firstPersonController != null && firstPersonController.isRewinding;
            _wasGrounded = firstPersonController == null ||
                firstPersonController.Grounded;
            _lastInstalledSocketCount = finalGateManager != null
                ? finalGateManager.CurrentFilledSockets
                : 0;
            _initialized = ValidateConfiguration(out _);
        }

        private void OnEnable()
        {
            ResolveRuntimeReferences();
            ApplyLevelAdsFov();
            SubscribeSignals();
        }

        private void OnDisable()
        {
            UnsubscribeSignals();
            RestoreLevelAdsFov();
            SetTimeEffectVisible(false, true);
            if (_vignette != null)
            {
                _vignette.intensity.value = 0f;
            }
        }

        private void ApplyLevelAdsFov()
        {
            if (firstPersonController == null || _adsFovOverridden)
            {
                return;
            }

            // Level keeps the original presentation behavior regardless of how
            // Unity initializes the newly-added serialized flag on old scenes.
            // Tutorial can explicitly opt out so its authored ADS FOV (40 in
            // the current project) is not clamped to the Level-only 45-75 range.
            if (!overrideAdsFov &&
                gameObject.scene.name != "Level")
            {
                return;
            }

            _originalAdsFov = firstPersonController.adsFOV;
            // Migrate the previous Level presentation default without touching
            // FirstPersonController's project-wide ADS tuning.
            float requestedFov = Mathf.Approximately(levelAdsFov, 55f)
                ? 65f
                : levelAdsFov;
            levelAdsFov = requestedFov;
            firstPersonController.adsFOV = Mathf.Clamp(requestedFov, 45f, 75f);
            _adsFovOverridden = true;
        }

        private void RestoreLevelAdsFov()
        {
            if (!_adsFovOverridden || firstPersonController == null)
            {
                return;
            }

            firstPersonController.adsFOV = _originalAdsFov;
            _adsFovOverridden = false;
        }

        private void Update()
        {
            if (!_initialized)
            {
                _initialized = ValidateConfiguration(out _);
                if (!_initialized)
                {
                    return;
                }
            }

            if (Mathf.Approximately(Time.timeScale, 0f) &&
                (firstPersonController == null || !firstPersonController.isRewinding))
            {
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            UpdateRewindTransition();
            UpdateTimeSlowTransition();
            UpdateLandingImpactObservation();
            UpdateTimers(deltaTime);
            UpdatePresentation(deltaTime);
            UpdateTimeEffect(deltaTime);
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

#if UNITY_EDITOR
            // The hand belongs to an animated prefab hierarchy. During runtime
            // authoring, reinforce the preview after animation has written its
            // transforms so Scene handles and all three numeric axes remain
            // visible in the real Game view.
            if (editorPosePreviewActive)
            {
                ApplyEditorPosePreviewImmediately();
            }
#endif

            if (weapon != null)
            {
                weapon.transform.localPosition += _currentPivotPosition;
                weapon.transform.localRotation *= _currentPivotRotation;
            }

            if (cameraTarget == null)
            {
                return;
            }

            bool rewinding =
                firstPersonController != null && firstPersonController.isRewinding;
            if (rewinding)
            {
                return;
            }

            float curvedKick = _shotKick01 * _shotKick01;
            float landingPitch = 0f;
            float landingRoll = 0f;
            if (_landingImpactRemaining > 0f)
            {
                float progress = 1f - _landingImpactRemaining /
                    Mathf.Max(0.01f, landingImpactDuration);
                progress = Mathf.Clamp01(progress);
                float envelope = 1f - progress;
                envelope *= envelope;
                landingPitch = Mathf.Sin(progress * Mathf.PI * 3f) *
                    envelope * landingPitchDegrees * _landingImpactStrength;
                landingRoll = Mathf.Sin(progress * Mathf.PI * 4f) *
                    envelope * landingRollDegrees * _landingImpactStrength *
                    _landingRollDirection;
            }

            if (curvedKick <= 0.0001f &&
                Mathf.Abs(landingPitch) <= 0.0001f &&
                Mathf.Abs(landingRoll) <= 0.0001f)
            {
                return;
            }

            cameraTarget.localRotation *= Quaternion.Euler(
                -adsCameraPitchKick * curvedKick + landingPitch,
                0f,
                adsCameraRollKick * _shotRollDirection * curvedKick +
                landingRoll);
        }

        private void ResolveRuntimeReferences()
        {
            if (weapon == null)
            {
                weapon = FindAnyObjectByType<PlayerWeapon>(
                    FindObjectsInactive.Include);
            }

            if (firstPersonController == null)
            {
                firstPersonController = FindAnyObjectByType<FirstPersonController>();
            }

            if (characterController == null && firstPersonController != null)
            {
                characterController =
                    firstPersonController.GetComponent<CharacterController>();
            }

            if (dashController == null && firstPersonController != null)
            {
                dashController = firstPersonController.GetComponent<DashController>();
            }

            if (timeManager == null)
            {
                timeManager = FindAnyObjectByType<TimeManager>();
            }

            if (finalGateManager == null)
            {
                finalGateManager = FindAnyObjectByType<FinalGateManager>();
            }

            if (alwaysEquippedWeaponController == null)
            {
                alwaysEquippedWeaponController =
                    FindAnyObjectByType<AlwaysEquippedWeaponController>();
            }

            if (cameraTarget == null &&
                firstPersonController != null &&
                firstPersonController.CinemachineCameraTarget != null)
            {
                cameraTarget =
                    firstPersonController.CinemachineCameraTarget.transform;
            }
        }

        private void SubscribeSignals()
        {
            UnsubscribeSignals();
            if (weapon != null)
            {
                weapon.ShotFiredSuccessfully += HandleShotFired;
            }

            if (dashController != null)
            {
                dashController.DashPerformed += HandleDashPerformed;
            }

            if (firstPersonController != null)
            {
                firstPersonController.DoubleJumpPerformed += HandleDoubleJumpPerformed;
            }

            if (timeManager != null)
            {
                timeManager.TimeSlowActivated += HandleTimeSlowActivated;
            }

            if (finalGateManager != null)
            {
                _lastInstalledSocketCount =
                    finalGateManager.CurrentFilledSockets;
                finalGateManager.SocketProgressChanged +=
                    HandleSocketProgressChanged;
            }
        }

        private void UnsubscribeSignals()
        {
            if (weapon != null)
            {
                weapon.ShotFiredSuccessfully -= HandleShotFired;
            }

            if (dashController != null)
            {
                dashController.DashPerformed -= HandleDashPerformed;
            }

            if (firstPersonController != null)
            {
                firstPersonController.DoubleJumpPerformed -= HandleDoubleJumpPerformed;
            }

            if (timeManager != null)
            {
                timeManager.TimeSlowActivated -= HandleTimeSlowActivated;
            }

            if (finalGateManager != null)
            {
                finalGateManager.SocketProgressChanged -=
                    HandleSocketProgressChanged;
            }
        }

        private void HandleShotFired()
        {
            if (weapon != null && weapon.IsAiming)
            {
                _shotKick01 = 1f;
                _shotRollDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            }
            else
            {
                _hipfireRemaining = hipfireReactionDuration;
            }
        }

        private void HandleDashPerformed()
        {
            _dashRemaining = dashGestureDuration;
        }

        private void HandleDoubleJumpPerformed()
        {
            _doubleJumpRemaining = doubleJumpGestureDuration;
        }

        private void HandleTimeSlowActivated()
        {
            _timeStartRemaining = timeStartGestureDuration;
            _timeStopRemaining = 0f;
        }

        private void HandleSocketProgressChanged(int current, int required)
        {
            if (current > _lastInstalledSocketCount)
            {
                _linkRemaining = linkGestureDuration;
            }

            _lastInstalledSocketCount = current;
        }

        private void UpdateLandingImpactObservation()
        {
            if (firstPersonController == null || characterController == null)
            {
                return;
            }

            bool grounded = firstPersonController.Grounded;
            Vector3 velocity = characterController.velocity;
            if (!grounded)
            {
                _maxAirborneDownSpeed = Mathf.Max(
                    _maxAirborneDownSpeed,
                    Mathf.Max(0f, -velocity.y));
                _maxAirborneTotalSpeed = Mathf.Max(
                    _maxAirborneTotalSpeed,
                    velocity.magnitude);
            }

            bool rewinding = firstPersonController.isRewinding;
            if (grounded && !_wasGrounded && !rewinding)
            {
                float verticalStrength = Mathf.InverseLerp(
                    landingMinVerticalSpeed,
                    Mathf.Max(landingMinVerticalSpeed + 0.01f,
                        landingStrongVerticalSpeed),
                    _maxAirborneDownSpeed);
                float totalStrength = Mathf.InverseLerp(
                    landingMinTotalSpeed,
                    Mathf.Max(landingMinTotalSpeed + 0.01f,
                        landingStrongTotalSpeed),
                    _maxAirborneTotalSpeed);
                float strength = Mathf.Max(verticalStrength, totalStrength);
                if (strength > 0.001f)
                {
                    _landingImpactStrength = Mathf.Sqrt(strength);
                    _landingImpactRemaining = landingImpactDuration;
                    _landingRollDirection =
                        UnityEngine.Random.value < 0.5f ? -1f : 1f;
                }

                _maxAirborneDownSpeed = 0f;
                _maxAirborneTotalSpeed = 0f;
            }
            else if (!grounded && _wasGrounded)
            {
                _maxAirborneDownSpeed = Mathf.Max(0f, -velocity.y);
                _maxAirborneTotalSpeed = velocity.magnitude;
            }

            _wasGrounded = grounded;
        }

        private void MigrateLandingImpactDefaults()
        {
            bool usesPreviousDefaults =
                Mathf.Approximately(landingMinVerticalSpeed, 14f) &&
                Mathf.Approximately(landingStrongVerticalSpeed, 32f) &&
                Mathf.Approximately(landingMinTotalSpeed, 22f) &&
                Mathf.Approximately(landingStrongTotalSpeed, 46f);
            if (!usesPreviousDefaults)
            {
                return;
            }

            landingMinVerticalSpeed = 8.5f;
            landingStrongVerticalSpeed = 22f;
            landingMinTotalSpeed = 13.5f;
            landingStrongTotalSpeed = 30f;
        }

        private void UpdateTimeSlowTransition()
        {
            bool active = timeManager != null && timeManager.IsAbilityActive;
            if (active && !_wasTimeSlowActive)
            {
                _timeStartRemaining = timeStartGestureDuration;
                _timeStopRemaining = 0f;
            }
            else if (!active && _wasTimeSlowActive)
            {
                bool rewinding =
                    firstPersonController != null && firstPersonController.isRewinding;
                bool endedEarly =
                    timeManager != null && timeManager.CurrentEnergy > 0.025f;
                if (!rewinding && endedEarly)
                {
                    _timeStopRemaining = timeStopGestureDuration;
                    _timeStartRemaining = 0f;
                    if (timeSlowMotes != null)
                    {
                        timeSlowMotes.Emit(12);
                    }
                }
            }

            _wasTimeSlowActive = active;
        }

        private void UpdateRewindTransition()
        {
            bool rewinding =
                firstPersonController != null && firstPersonController.isRewinding;
            if (rewinding && !_wasRewinding)
            {
                _dashRemaining = 0f;
                _doubleJumpRemaining = 0f;
                _timeStartRemaining = 0f;
                _timeStopRemaining = 0f;
                _linkRemaining = 0f;
                _hipfireRemaining = 0f;
                _shotKick01 = 0f;
                _landingImpactRemaining = 0f;
                _landingImpactStrength = 0f;
                _maxAirborneDownSpeed = 0f;
                _maxAirborneTotalSpeed = 0f;
                _adsBlend = 0f;
                SetTimeEffectVisible(false, true);
            }

            _wasRewinding = rewinding;
        }

        private void UpdateTimers(float deltaTime)
        {
            _dashRemaining = Mathf.Max(0f, _dashRemaining - deltaTime);
            _doubleJumpRemaining = Mathf.Max(0f, _doubleJumpRemaining - deltaTime);
            _timeStartRemaining = Mathf.Max(0f, _timeStartRemaining - deltaTime);
            _timeStopRemaining = Mathf.Max(0f, _timeStopRemaining - deltaTime);
            _linkRemaining = Mathf.Max(0f, _linkRemaining - deltaTime);
            _hipfireRemaining = Mathf.Max(0f, _hipfireRemaining - deltaTime);
            _landingImpactRemaining = Mathf.Max(
                0f,
                _landingImpactRemaining - deltaTime);
            _shotKick01 = Mathf.MoveTowards(_shotKick01, 0f, deltaTime * 7.5f);

            bool rewinding =
                firstPersonController != null && firstPersonController.isRewinding;
            float targetAds = !rewinding && weapon != null && weapon.IsAiming
                ? 1f
                : 0f;
            float adsSpeed = targetAds > _adsBlend ? 8.5f : 5.2f;
            _adsBlend = Mathf.MoveTowards(_adsBlend, targetAds, deltaTime * adsSpeed);
        }

        private void UpdatePresentation(float deltaTime)
        {
            bool rewinding =
                firstPersonController != null && firstPersonController.isRewinding;
            float charge01 = weapon != null ? weapon.AdsCharge01 : 0f;
            HandPose basePose = SelectBasePose(charge01);
            HandPose targetPose = SelectPriorityPose(basePose);

            if (_hipfireRemaining > 0f && _timeStartRemaining <= 0f &&
                _timeStopRemaining <= 0f && _dashRemaining <= 0f &&
                _doubleJumpRemaining <= 0f)
            {
                float hipfireWeight = PulseWeight(
                    _hipfireRemaining,
                    hipfireReactionDuration);
                targetPose.localPosition +=
                    new Vector3(-0.035f, -0.025f, -0.10f) * hipfireWeight;
                targetPose.BlendFingerCurlsToward(
                    0.05f,
                    hipfireWeight * 0.35f);
            }

            if (rewinding)
            {
                targetPose = idlePose;
            }

            float poseT = 1f - Mathf.Exp(-poseResponse * deltaTime);
            _currentLeftPosition = Vector3.Lerp(
                _currentLeftPosition,
                targetPose.localPosition,
                poseT);
            _currentLeftRotation = Quaternion.Slerp(
                _currentLeftRotation,
                Quaternion.Euler(targetPose.localEulerAngles),
                poseT);
            _currentThumbCurl = Mathf.Lerp(
                _currentThumbCurl, targetPose.GetFingerCurl(0), poseT);
            _currentIndexCurl = Mathf.Lerp(
                _currentIndexCurl, targetPose.GetFingerCurl(1), poseT);
            _currentMiddleCurl = Mathf.Lerp(
                _currentMiddleCurl, targetPose.GetFingerCurl(2), poseT);
            _currentRingCurl = Mathf.Lerp(
                _currentRingCurl, targetPose.GetFingerCurl(3), poseT);
            _currentLittleCurl = Mathf.Lerp(
                _currentLittleCurl, targetPose.GetFingerCurl(4), poseT);

            leftHandRoot.localPosition = _currentLeftPosition;
            leftHandRoot.localRotation = _currentLeftRotation;
            ApplyFingerCurl(
                _currentThumbCurl,
                _currentIndexCurl,
                _currentMiddleCurl,
                _currentRingCurl,
                _currentLittleCurl);

            Vector3 pivotTarget = Vector3.Lerp(
                Vector3.zero,
                adsPivotOffset,
                _adsBlend);
            pivotTarget += fullChargePivotOffset * charge01 * _adsBlend;
            pivotTarget += shotPivotKick * (_shotKick01 * _shotKick01);
            if (rewinding)
            {
                pivotTarget = rewindPivotOffset;
            }

            Vector3 pivotEuler =
                shotPivotEuler * (_shotKick01 * _shotKick01);
            Quaternion pivotRotationTarget = Quaternion.Euler(pivotEuler);
            float activePivotResponse = rewinding
                ? rewindHideResponse
                : (_currentPivotPosition.y < -0.12f
                    ? rewindReturnResponse
                    : pivotResponse);
            float pivotT = 1f - Mathf.Exp(-activePivotResponse * deltaTime);
            _currentPivotPosition = Vector3.Lerp(
                _currentPivotPosition,
                pivotTarget,
                pivotT);
            _currentPivotRotation = Quaternion.Slerp(
                _currentPivotRotation,
                pivotRotationTarget,
                pivotT);
            if (_vignette != null)
            {
                float focus =
                    adsVignetteIntensity * _adsBlend +
                    chargeVignetteIntensity * charge01 * _adsBlend;
                _vignette.intensity.value = rewinding ? 0f : focus;
            }
        }

        private HandPose SelectBasePose(float charge01)
        {
            bool falling =
                firstPersonController != null &&
                !firstPersonController.Grounded &&
                characterController != null &&
                characterController.velocity.y < fallVelocityThreshold;
            if (falling)
            {
                return fallingPose;
            }

            if (_adsBlend > 0.001f)
            {
                HandPose adsPose = HandPose.Lerp(
                    adsReadyPose,
                    adsChargedPose,
                    charge01);
                return HandPose.Lerp(idlePose, adsPose, _adsBlend);
            }

            return idlePose;
        }

        private HandPose SelectPriorityPose(HandPose basePose)
        {
#if UNITY_EDITOR
            if (editorPosePreviewActive)
            {
                return editorPosePreviewPose;
            }
#endif
            bool timeSlowActive =
                timeManager != null && timeManager.IsAbilityActive;
            if (_timeStartRemaining > 0f)
            {
                float progress = 1f -
                    _timeStartRemaining / Mathf.Max(0.01f, timeStartGestureDuration);
                return EvaluateEntrySequence(
                    basePose,
                    ResolvedTimeSlowAnticipationPose,
                    timeSlowPose,
                    progress);
            }

            if (_timeStopRemaining > 0f)
            {
                float progress = 1f -
                    _timeStopRemaining / Mathf.Max(0.01f, timeStopGestureDuration);
                return EvaluateExitSequence(
                    timeSlowPose,
                    timeStopPose,
                    ResolvedTimeStopRecoveryPose,
                    basePose,
                    progress);
            }

            HandPose sustainedBase = timeSlowActive
                ? timeSlowPose
                : basePose;

            if (_linkRemaining > 0f)
            {
                return EvaluateGestureSequence(
                    sustainedBase,
                    ResolvedLinkAnticipationPose,
                    ResolvedLinkPlacementPose,
                    ResolvedLinkRecoveryPose,
                    _linkRemaining,
                    linkGestureDuration);
            }

            if (_dashRemaining > 0f)
            {
                return EvaluateGestureSequence(
                    sustainedBase,
                    ResolvedDashAnticipationPose,
                    dashPose,
                    ResolvedDashRecoveryPose,
                    _dashRemaining,
                    dashGestureDuration);
            }

            if (_doubleJumpRemaining > 0f)
            {
                return EvaluateGestureSequence(
                    sustainedBase,
                    ResolvedDoubleJumpAnticipationPose,
                    doubleJumpPose,
                    ResolvedDoubleJumpRecoveryPose,
                    _doubleJumpRemaining,
                    doubleJumpGestureDuration);
            }

            return sustainedBase;
        }

        public HandPose ResolvedDashAnticipationPose =>
            IsUnsetStagePose(dashAnticipationPose)
                ? HandPose.Lerp(idlePose, dashPose, 0.48f)
                : dashAnticipationPose;

        public HandPose ResolvedDashRecoveryPose =>
            IsUnsetStagePose(dashRecoveryPose)
                ? HandPose.Lerp(dashPose, idlePose, 0.58f)
                : dashRecoveryPose;

        public HandPose ResolvedDoubleJumpAnticipationPose =>
            IsUnsetStagePose(doubleJumpAnticipationPose)
                ? HandPose.Lerp(idlePose, doubleJumpPose, 0.46f)
                : doubleJumpAnticipationPose;

        public HandPose ResolvedDoubleJumpRecoveryPose =>
            IsUnsetStagePose(doubleJumpRecoveryPose)
                ? HandPose.Lerp(doubleJumpPose, fallingPose, 0.58f)
                : doubleJumpRecoveryPose;

        public HandPose ResolvedTimeSlowAnticipationPose
        {
            get
            {
                if (!IsUnsetStagePose(timeSlowAnticipationPose))
                {
                    return timeSlowAnticipationPose;
                }

                HandPose pose = HandPose.Lerp(idlePose, timeSlowPose, 0.52f);
                pose.BlendFingerCurlsToward(0f, 1f);
                return pose;
            }
        }

        public HandPose ResolvedTimeStopRecoveryPose =>
            IsUnsetStagePose(timeStopRecoveryPose)
                ? HandPose.Lerp(timeStopPose, idlePose, 0.62f)
                : timeStopRecoveryPose;

        public HandPose ResolvedLinkAnticipationPose =>
            IsUnsetStagePose(linkAnticipationPose)
                ? HandPose.Lerp(idlePose, ResolvedTimeSlowAnticipationPose, 0.55f)
                : linkAnticipationPose;

        public HandPose ResolvedLinkPlacementPose =>
            IsUnsetStagePose(linkPlacementPose)
                ? HandPose.Lerp(timeSlowPose, adsChargedPose, 0.42f)
                : linkPlacementPose;

        public HandPose ResolvedLinkRecoveryPose =>
            IsUnsetStagePose(linkRecoveryPose)
                ? HandPose.Lerp(ResolvedLinkPlacementPose, idlePose, 0.62f)
                : linkRecoveryPose;

        public static bool IsUnsetStagePose(HandPose pose)
        {
            return pose.localPosition == Vector3.zero &&
                pose.localEulerAngles == Vector3.zero &&
                Mathf.Approximately(pose.fingerCurl, 0f) &&
                !pose.useIndividualFingerCurls;
        }

        private static HandPose EvaluateGestureSequence(
            HandPose basePose,
            HandPose anticipationPose,
            HandPose peakPose,
            HandPose recoveryPose,
            float remaining,
            float duration)
        {
            float progress = 1f - remaining / Mathf.Max(0.01f, duration);
            progress = Mathf.Clamp01(progress);
            if (progress < 0.18f)
            {
                return HandPose.Lerp(
                    basePose,
                    anticipationPose,
                    Smooth01(progress / 0.18f));
            }
            if (progress < 0.42f)
            {
                return HandPose.Lerp(
                    anticipationPose,
                    peakPose,
                    Smooth01((progress - 0.18f) / 0.24f));
            }
            if (progress < 0.66f)
            {
                return peakPose;
            }
            if (progress < 0.86f)
            {
                return HandPose.Lerp(
                    peakPose,
                    recoveryPose,
                    Smooth01((progress - 0.66f) / 0.20f));
            }
            return HandPose.Lerp(
                recoveryPose,
                basePose,
                Smooth01((progress - 0.86f) / 0.14f));
        }

        private static HandPose EvaluateEntrySequence(
            HandPose basePose,
            HandPose anticipationPose,
            HandPose holdPose,
            float progress)
        {
            float safeProgress = Mathf.Clamp01(progress);
            if (safeProgress < 0.32f)
            {
                return HandPose.Lerp(
                    basePose,
                    anticipationPose,
                    Smooth01(safeProgress / 0.32f));
            }
            return HandPose.Lerp(
                anticipationPose,
                holdPose,
                Smooth01((safeProgress - 0.32f) / 0.68f));
        }

        private static HandPose EvaluateExitSequence(
            HandPose holdPose,
            HandPose stopPose,
            HandPose recoveryPose,
            HandPose basePose,
            float progress)
        {
            float safeProgress = Mathf.Clamp01(progress);
            if (safeProgress < 0.28f)
            {
                return HandPose.Lerp(
                    holdPose,
                    stopPose,
                    Smooth01(safeProgress / 0.28f));
            }
            if (safeProgress < 0.58f)
            {
                return stopPose;
            }
            if (safeProgress < 0.82f)
            {
                return HandPose.Lerp(
                    stopPose,
                    recoveryPose,
                    Smooth01((safeProgress - 0.58f) / 0.24f));
            }
            return HandPose.Lerp(
                recoveryPose,
                basePose,
                Smooth01((safeProgress - 0.82f) / 0.18f));
        }

        private static float Smooth01(float value)
        {
            float safeValue = Mathf.Clamp01(value);
            return safeValue * safeValue * (3f - 2f * safeValue);
        }

        private static float PulseWeight(float remaining, float duration)
        {
            float progress = 1f - remaining / Mathf.Max(0.01f, duration);
            return Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        }

        private void CacheFingerBones()
        {
            _fingerBones.Clear();
            _fingerRestRotations.Clear();
            _fingerCurlWeights.Clear();
            _fingerCurlGroups.Clear();
            if (leftHandRoot == null)
            {
                return;
            }

            Transform[] descendants =
                leftHandRoot.GetComponentsInChildren<Transform>(true);
            int currentFingerGroup = 0;
            for (int index = 0; index < descendants.Length; index++)
            {
                Transform bone = descendants[index];
                string boneName = bone.name;
                if (boneName.EndsWith("_end", StringComparison.OrdinalIgnoreCase) ||
                    (!boneName.Contains("finger") && !boneName.StartsWith("thumb_")))
                {
                    continue;
                }

                _fingerBones.Add(bone);
                _fingerRestRotations.Add(bone.localRotation);
                _fingerCurlWeights.Add(GetCurlWeight(boneName));
                if (boneName.StartsWith("thumb_"))
                {
                    _fingerCurlGroups.Add(0);
                }
                else
                {
                    if (boneName.Contains("01"))
                    {
                        currentFingerGroup = Mathf.Min(
                            4,
                            currentFingerGroup + 1);
                    }
                    _fingerCurlGroups.Add(Mathf.Max(1, currentFingerGroup));
                }
            }
        }

        private static float GetCurlWeight(string boneName)
        {
            if (boneName.StartsWith("thumb_"))
            {
                return 0.48f;
            }

            if (boneName.Contains("01"))
            {
                return 0.64f;
            }

            if (boneName.Contains("02"))
            {
                return 1f;
            }

            return 0.82f;
        }

        private void SetCurrentFingerCurls(HandPose pose)
        {
            _currentThumbCurl = pose.GetFingerCurl(0);
            _currentIndexCurl = pose.GetFingerCurl(1);
            _currentMiddleCurl = pose.GetFingerCurl(2);
            _currentRingCurl = pose.GetFingerCurl(3);
            _currentLittleCurl = pose.GetFingerCurl(4);
        }

        private void ApplyFingerCurl(
            float thumbCurl,
            float indexCurl,
            float middleCurl,
            float ringCurl,
            float littleCurl)
        {
            for (int index = 0; index < _fingerBones.Count; index++)
            {
                Transform bone = _fingerBones[index];
                if (bone == null)
                {
                    continue;
                }

                int fingerGroup = index < _fingerCurlGroups.Count
                    ? _fingerCurlGroups[index]
                    : 4;
                float curl;
                switch (fingerGroup)
                {
                    case 0:
                        curl = thumbCurl;
                        break;
                    case 1:
                        curl = indexCurl;
                        break;
                    case 2:
                        curl = middleCurl;
                        break;
                    case 3:
                        curl = ringCurl;
                        break;
                    default:
                        curl = littleCurl;
                        break;
                }

                float degrees = -78f * Mathf.Clamp01(curl) *
                    _fingerCurlWeights[index];
                bone.localRotation = _fingerRestRotations[index] *
                    Quaternion.Euler(degrees, 0f, 0f);
            }
        }

        private void UpdateTimeEffect(float deltaTime)
        {
            bool active =
                timeManager != null && timeManager.IsAbilityActive &&
                (firstPersonController == null || !firstPersonController.isRewinding);
            SetTimeEffectVisible(active, false);
            if (timeSlowEffectRoot == null || leftHandRoot == null)
            {
                return;
            }

            timeSlowEffectRoot.position = leftHandRoot.position;
            timeSlowEffectRoot.rotation = leftHandRoot.rotation;
            float spin = 42f * deltaTime;
            if (timeSlowRingA != null)
            {
                timeSlowRingA.transform.Rotate(0f, spin, spin * 0.35f, Space.Self);
            }

            if (timeSlowRingB != null)
            {
                timeSlowRingB.transform.Rotate(spin * -0.4f, spin * -0.75f, 0f, Space.Self);
            }
        }

        private void SetTimeEffectVisible(bool visible, bool immediate)
        {
            float targetAlpha = visible ? 0.34f : 0f;
            float blend = immediate
                ? 1f
                : 1f - Mathf.Exp(-10f * Mathf.Max(0f, Time.unscaledDeltaTime));
            SetRingAlpha(timeSlowRingA, targetAlpha, blend);
            SetRingAlpha(timeSlowRingB, targetAlpha * 0.72f, blend);

            if (timeSlowMotes == null)
            {
                return;
            }

            if (visible && !timeSlowMotes.isPlaying)
            {
                timeSlowMotes.Play(true);
            }
            else if (!visible && timeSlowMotes.isPlaying)
            {
                timeSlowMotes.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }

            if (immediate && !visible)
            {
                timeSlowMotes.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void SetRingAlpha(
            LineRenderer ring,
            float targetAlpha,
            float blend)
        {
            if (ring == null)
            {
                return;
            }

            Color start = ring.startColor;
            Color end = ring.endColor;
            start.a = Mathf.Lerp(start.a, targetAlpha, blend);
            end.a = Mathf.Lerp(end.a, targetAlpha * 0.25f, blend);
            ring.startColor = start;
            ring.endColor = end;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (weapon == null || firstPersonController == null ||
                characterController == null || dashController == null ||
                timeManager == null || alwaysEquippedWeaponController == null)
            {
                reason = "A gameplay signal source is missing.";
                return false;
            }

            if (weaponPresentationPivot == null || leftHandRoot == null ||
                rightHandRoot == null || cameraTarget == null)
            {
                reason = "A required hand, weapon pivot, or camera target is missing.";
                return false;
            }

            if (leftHandRoot.name != "Left_Hand" ||
                rightHandRoot.name != "Right_Hand")
            {
                reason = "The imported left/right hand roots are not assigned correctly.";
                return false;
            }

            if (adsFocusVolume == null)
            {
                reason = "ADS focus Volume reference is missing.";
                return false;
            }

            if (_vignette == null)
            {
                reason = "ADS focus VolumeProfile has no persistent Vignette override.";
                return false;
            }

            if (timeSlowMotes == null || timeSlowEffectRoot == null ||
                timeSlowRingA == null || timeSlowRingB == null)
            {
                reason = "Time Slow presentation references are incomplete.";
                return false;
            }

            if (_fingerBones.Count < 15)
            {
                reason = "Fewer than fifteen left-hand finger bones were discovered.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool RefreshAndValidateConfiguration(out string reason)
        {
            RefreshConfigurationCache();
            _initialized = ValidateConfiguration(out reason);
            return _initialized;
        }

        private void RefreshConfigurationCache()
        {
            ResolveRuntimeReferences();
            if (_fingerBones.Count == 0 || !Application.isPlaying)
            {
                CacheFingerBones();
            }
            _vignette = null;
            if (adsFocusVolume != null)
            {
                VolumeProfile profile = Application.isPlaying
                    ? adsFocusVolume.profile
                    : adsFocusVolume.sharedProfile;
                if (profile != null)
                {
                    profile.TryGet(out _vignette);
                }
            }
        }
    }
}
