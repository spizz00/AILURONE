#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

namespace AILURONE.WorldRewrite
{
    /// <summary>
    /// Connects the existing TeleportController to the stable Phase 1
    /// world-space rewrite shader.
    ///
    /// This component does not implement teleport movement, anchor selection,
    /// cancellation rules, velocity preservation or safe destination logic.
    /// It only drives the three existing global shader parameters:
    ///
    /// _AILU_RewriteCenterWS
    /// _AILU_RewriteRadius
    /// _AILU_RewriteAmount
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TeleportController))]
    public sealed class TeleportWorldRewriteController : MonoBehaviour
    {
        private enum RewriteState
        {
            Idle,
            Channeling,
            Recovering
        }

        private static readonly int RewriteCentreId =
            Shader.PropertyToID("_AILU_RewriteCenterWS");

        private static readonly int RewriteRadiusId =
            Shader.PropertyToID("_AILU_RewriteRadius");

        private static readonly int RewriteAmountId =
            Shader.PropertyToID("_AILU_RewriteAmount");

        [Header("Core References")]
        [SerializeField] private TeleportController teleportController;

        [Tooltip(
            "Optional explicit rewrite centre. Leave empty to use this " +
            "Player transform plus Player Centre Height."
        )]
        [SerializeField] private Transform rewriteOrigin;

        [Header("Channel Shape")]
        [Min(0.1f)]
        [Tooltip(
            "Minimum departure scan radius. For distant anchors, the actual " +
            "radius grows automatically to cover the arrival area as well."
        )]
        [SerializeField] private float maximumRadius = 80.0f;

        [Min(1f)]
        [Tooltip(
            "How much world around the destination must already be rewritten " +
            "before teleporting, and the starting radius of the arrival pulse."
        )]
        [SerializeField] private float arrivalCoverageRadius = 120.0f;

        public float ArrivalCoverageRadius =>
            Mathf.Max(1f, arrivalCoverageRadius);

        [Min(0f)]
        [Tooltip(
            "Vertical offset from the Player root used as the centre of both " +
            "departure and arrival rewrite spheres."
        )]
        [SerializeField] private float playerCentreHeight = 1.0f;

        [SerializeField] private AnimationCurve radiusCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 1.9f),
                new Keyframe(1f, 1f, 0.25f, 0f)
            );

        [SerializeField] private AnimationCurve amountCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2.2f),
                new Keyframe(1f, 1f, 0.25f, 0f)
            );

        [Header("Recovery")]
        [Min(0.01f)]
        [Tooltip("Fast recovery when the teleport channel is cancelled.")]
        [SerializeField] private float cancelRecoveryDuration = 0.10f;

        [Min(0.01f)]
        [Tooltip("Recovery after arriving at the new position.")]
        [SerializeField] private float arrivalRecoveryDuration = 0.62f;

        [Tooltip(
            "Keep the current radius stable while fading the rewrite amount. " +
            "This avoids an obvious inward shield bubble."
        )]
        [SerializeField] private bool keepRadiusDuringRecovery = true;

        [Tooltip(
            "After a successful teleport, restore the world with a visible " +
            "inward-moving boundary centred on the arrival position."
        )]
        [SerializeField] private bool useInverseArrivalRecovery = true;

        [Range(0f, 0.95f)]
        [Tooltip(
            "The arrival rewrite stays fully readable until this fraction " +
            "of the inverse recovery has elapsed."
        )]
        [SerializeField] private float arrivalAmountFadeStart = 0.78f;

        [Header("Compatibility")]
        [Tooltip(
            "Disables WorldRewritePrototypeController instances so the T-key " +
            "prototype cannot fight for the same global shader parameters."
        )]
        [SerializeField] private bool disablePrototypeControllers = true;

        [Tooltip(
            "The old screen effect is now empty, so the extra visual lead delay " +
            "is unnecessary. This keeps the channel close to 0.5 seconds."
        )]
        [SerializeField] private bool removeOldDepartureLeadDelay = true;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges;

        [SerializeField] private RewriteState state =
            RewriteState.Idle;

        [SerializeField, Range(0f, 1f)]
        private float displayedChannelProgress;

        [SerializeField] private float currentRadius;
        [SerializeField] private float currentAmount;

        private float _recoveryElapsed;
        private float _recoveryDuration;
        private float _recoveryStartRadius;
        private float _recoveryStartAmount;
        private bool _isArrivalRecovery;

        private Vector3 _rewriteCentre;
        private bool _hasRewriteCentre;
        private float _channelMaximumRadius;

        private bool _subscribed;

        public float CurrentRadius => currentRadius;
        public float CurrentAmount => currentAmount;
        public bool IsRewriting => state != RewriteState.Idle;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration
        )]
        private static void ResetGlobalsBeforeSceneLoad()
        {
            Shader.SetGlobalVector(
                RewriteCentreId,
                Vector4.zero
            );

            Shader.SetGlobalFloat(
                RewriteRadiusId,
                0f
            );

            Shader.SetGlobalFloat(
                RewriteAmountId,
                0f
            );
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyCompatibilitySettings();
            ResetImmediately();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyCompatibilitySettings();
            Subscribe();

            if (teleportController != null
                && teleportController.IsChanneling)
            {
                BeginChannelVisual();
            }
        }

        private void Start()
        {
            ResolveReferences();
            ApplyCompatibilitySettings();
            Subscribe();
        }

        private void Update()
        {
            if (teleportController == null)
            {
                ResetImmediately();
                return;
            }

            if (teleportController.IsChanneling)
            {
                if (state != RewriteState.Channeling)
                {
                    BeginChannelVisual();
                }

                UpdateChannelVisual(
                    teleportController.ChannelProgress
                );

                return;
            }

            if (state == RewriteState.Recovering)
            {
                TickRecovery(Time.unscaledDeltaTime);
            }
            else if (state == RewriteState.Channeling)
            {
                // Event-order safety net. A cancellation/completion event should
                // normally set recovery first, but never leave the effect stuck.
                BeginRecovery(
                    cancelRecoveryDuration,
                    false,
                    false
                );
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetImmediately();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ResetImmediately();
        }

        private void ResolveReferences()
        {
            if (teleportController == null)
            {
                teleportController =
                    GetComponent<TeleportController>();
            }

        }

        private void ApplyCompatibilitySettings()
        {
            arrivalRecoveryDuration =
                Mathf.Max(
                    arrivalRecoveryDuration,
                    0.24f
                );

            if (removeOldDepartureLeadDelay
                && teleportController != null)
            {
                teleportController.visualDepartureLeadTime = 0f;
            }

            if (!disablePrototypeControllers)
            {
                return;
            }

            WorldRewritePrototypeController[] prototypes =
                UnityEngine.Object.FindObjectsByType<WorldRewritePrototypeController>(FindObjectsInactive.Include);

            foreach (
                WorldRewritePrototypeController prototype
                in prototypes
            )
            {
                if (prototype == null
                    || !prototype.enabled)
                {
                    continue;
                }

                prototype.enabled = false;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || teleportController == null)
            {
                return;
            }

            teleportController.ChannelStarted +=
                HandleChannelStarted;

            teleportController.ChannelCancelled +=
                HandleChannelCancelled;

            teleportController.TeleportCompleted +=
                HandleTeleportCompleted;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || teleportController == null)
            {
                return;
            }

            teleportController.ChannelStarted -=
                HandleChannelStarted;

            teleportController.ChannelCancelled -=
                HandleChannelCancelled;

            teleportController.TeleportCompleted -=
                HandleTeleportCompleted;

            _subscribed = false;
        }

        private void HandleChannelStarted(
            int slotIndex,
            Vector3 destination
        )
        {
            BeginChannelVisual(destination, true);

            if (logStateChanges)
            {
                Debug.Log(
                    "[TeleportWorldRewrite] Channel started.",
                    this
                );
            }
        }

        private void HandleChannelCancelled(
            TeleportController.TeleportCancelReason reason
        )
        {
            BeginRecovery(
                cancelRecoveryDuration,
                false,
                false
            );

            if (logStateChanges)
            {
                Debug.Log(
                    "[TeleportWorldRewrite] Channel cancelled: "
                    + reason,
                    this
                );
            }
        }

        private void HandleTeleportCompleted(
            int slotIndex,
            Vector3 destination
        )
        {
            // Use the authoritative safe destination supplied by the teleport
            // event. The camera may still be smoothing from the departure point.
            CaptureCentre(
                destination
                + Vector3.up * playerCentreHeight
            );

            currentRadius = arrivalCoverageRadius;
            currentAmount = 1f;
            displayedChannelProgress = 1f;

            ApplyGlobals(
                currentRadius,
                currentAmount
            );

            BeginRecovery(
                arrivalRecoveryDuration,
                false,
                true
            );

            if (logStateChanges)
            {
                Debug.Log(
                    "[TeleportWorldRewrite] Arrival recovery started.",
                    this
                );
            }
        }

        private void BeginChannelVisual()
        {
            BeginChannelVisual(Vector3.zero, false);
        }

        private void BeginChannelVisual(
            Vector3 destination,
            bool hasDestination
        )
        {
            CaptureCurrentCentre();

            _channelMaximumRadius = maximumRadius;

            if (hasDestination)
            {
                Vector3 arrivalCentre =
                    destination
                    + Vector3.up * playerCentreHeight;

                _channelMaximumRadius = Mathf.Max(
                    maximumRadius,
                    Vector3.Distance(
                        _rewriteCentre,
                        arrivalCentre
                    ) + arrivalCoverageRadius
                );
            }

            state = RewriteState.Channeling;
            displayedChannelProgress = 0f;

            _recoveryElapsed = 0f;
            _recoveryDuration = 0f;

            currentRadius = 0f;
            currentAmount = 0f;

            ApplyGlobals(0f, 0f);
        }

        private void UpdateChannelVisual(float progress)
        {
            displayedChannelProgress =
                Mathf.Clamp01(progress);

            float radiusT =
                Mathf.Clamp01(
                    radiusCurve.Evaluate(
                        displayedChannelProgress
                    )
                );

            float amountT =
                Mathf.Clamp01(
                    amountCurve.Evaluate(
                        displayedChannelProgress
                    )
                );

            currentRadius =
                _channelMaximumRadius * radiusT;

            currentAmount = amountT;

            ApplyGlobals(
                currentRadius,
                currentAmount
            );
        }

        private void BeginRecovery(
            float duration,
            bool forceFullRewrite,
            bool isArrivalRecovery
        )
        {
            if (forceFullRewrite)
            {
                currentRadius = _channelMaximumRadius;
                currentAmount = 1f;
            }

            if (currentAmount <= 0.0001f)
            {
                ResetImmediately();
                return;
            }

            state = RewriteState.Recovering;
            _isArrivalRecovery = isArrivalRecovery;

            _recoveryElapsed = 0f;
            _recoveryDuration =
                Mathf.Max(0.01f, duration);

            _recoveryStartRadius =
                Mathf.Max(0f, currentRadius);

            _recoveryStartAmount =
                Mathf.Clamp01(currentAmount);

            ApplyGlobals(
                _recoveryStartRadius,
                _recoveryStartAmount
            );
        }

        private void TickRecovery(float unscaledDeltaTime)
        {
            _recoveryElapsed +=
                Mathf.Max(0f, unscaledDeltaTime);

            float progress =
                Mathf.Clamp01(
                    _recoveryElapsed
                    / Mathf.Max(
                        0.01f,
                        _recoveryDuration
                    )
                );

            float eased =
                Mathf.SmoothStep(0f, 1f, progress);

            if (_isArrivalRecovery
                && useInverseArrivalRecovery)
            {
                currentRadius = Mathf.Lerp(
                    _recoveryStartRadius,
                    0f,
                    eased
                );

                float fadeProgress = Mathf.InverseLerp(
                    arrivalAmountFadeStart,
                    1f,
                    progress
                );

                currentAmount = Mathf.Lerp(
                    _recoveryStartAmount,
                    0f,
                    Mathf.SmoothStep(0f, 1f, fadeProgress)
                );
            }
            else
            {
                currentAmount = Mathf.Lerp(
                    _recoveryStartAmount,
                    0f,
                    eased
                );

                currentRadius = keepRadiusDuringRecovery
                    ? _recoveryStartRadius
                    : Mathf.Lerp(
                        _recoveryStartRadius,
                        0f,
                        eased
                    );
            }

            ApplyGlobals(
                currentRadius,
                currentAmount
            );

            if (progress >= 1f)
            {
                ResetImmediately();
            }
        }

        [ContextMenu("Reset World Rewrite Immediately")]
        public void ResetImmediately()
        {
            state = RewriteState.Idle;
            displayedChannelProgress = 0f;

            _recoveryElapsed = 0f;
            _recoveryDuration = 0f;
            _recoveryStartRadius = 0f;
            _recoveryStartAmount = 0f;
            _isArrivalRecovery = false;
            _channelMaximumRadius = maximumRadius;

            currentRadius = 0f;
            currentAmount = 0f;

            ApplyGlobals(0f, 0f);
        }

        private void CaptureCurrentCentre()
        {
            Vector3 centre = rewriteOrigin != null
                ? rewriteOrigin.position
                : transform.position
                    + Vector3.up * playerCentreHeight;

            CaptureCentre(centre);
        }

        private void CaptureCentre(Vector3 centre)
        {
            _rewriteCentre = centre;
            _hasRewriteCentre = true;

            Shader.SetGlobalVector(
                RewriteCentreId,
                new Vector4(
                    _rewriteCentre.x,
                    _rewriteCentre.y,
                    _rewriteCentre.z,
                    1f
                )
            );
        }

        private void ApplyGlobals(
            float radius,
            float amount
        )
        {
            Vector3 position = _hasRewriteCentre
                ? _rewriteCentre
                : rewriteOrigin != null
                    ? rewriteOrigin.position
                    : transform.position
                        + Vector3.up * playerCentreHeight;

            Shader.SetGlobalVector(
                RewriteCentreId,
                new Vector4(
                    position.x,
                    position.y,
                    position.z,
                    1f
                )
            );

            Shader.SetGlobalFloat(
                RewriteRadiusId,
                Mathf.Max(0f, radius)
            );

            Shader.SetGlobalFloat(
                RewriteAmountId,
                Mathf.Clamp01(amount)
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumRadius =
                Mathf.Max(0.1f, maximumRadius);

            arrivalCoverageRadius =
                Mathf.Max(1f, arrivalCoverageRadius);

            playerCentreHeight =
                Mathf.Max(0f, playerCentreHeight);

            cancelRecoveryDuration =
                Mathf.Max(
                    0.01f,
                    cancelRecoveryDuration
                );

            arrivalRecoveryDuration =
                Mathf.Max(
                    0.01f,
                    arrivalRecoveryDuration
                );

            arrivalAmountFadeStart =
                Mathf.Clamp(
                    arrivalAmountFadeStart,
                    0f,
                    0.95f
                );
        }
#endif
    }
}
