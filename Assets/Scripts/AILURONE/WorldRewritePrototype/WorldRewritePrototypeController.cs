#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AILURONE.WorldRewrite
{
    /// <summary>
    /// Standalone Phase 1 prototype driver.
    /// It does not depend on TeleportController and does not modify teleport logic.
    ///
    /// In Play Mode, press T to expand a world-space rewrite sphere around
    /// Centre Transform, hold briefly, and then recover.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldRewritePrototypeController : MonoBehaviour
    {
        private enum PrototypeState
        {
            Idle,
            Expanding,
            Holding,
            Recovering
        }

        private static readonly int RewriteCentreId =
            Shader.PropertyToID("_AILU_RewriteCenterWS");

        private static readonly int RewriteRadiusId =
            Shader.PropertyToID("_AILU_RewriteRadius");

        private static readonly int RewriteAmountId =
            Shader.PropertyToID("_AILU_RewriteAmount");

        [Header("Rewrite Origin")]
        [Tooltip("Usually the player camera, chest, or a child object near the player's centre.")]
        [SerializeField] private Transform centreTransform;

        [Header("Prototype Timing")]
        [Min(0.01f)]
        [SerializeField] private float expandDuration = 0.50f;

        [Min(0f)]
        [SerializeField] private float holdDuration = 0.20f;

        [Min(0.01f)]
        [SerializeField] private float recoverDuration = 0.30f;

        [Header("Prototype Shape")]
        [Min(0.1f)]
        [SerializeField] private float maximumRadius = 8.0f;

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

        [Header("Testing")]
        [Tooltip("Press T in Play Mode. Also available from the component context menu.")]
        [SerializeField] private bool enableKeyboardTest = true;

        [Tooltip("Allow a new T press to restart the prototype immediately.")]
        [SerializeField] private bool restartWhenPressedAgain = true;

        [Header("Debug")]
        [SerializeField] private bool drawRadiusGizmo = true;

        private PrototypeState _state = PrototypeState.Idle;
        private float _stateTime;
        private float _currentRadius;
        private float _currentAmount;

        public float CurrentRadius => _currentRadius;
        public float CurrentAmount => _currentAmount;
        public bool IsPlaying => _state != PrototypeState.Idle;

        private Transform EffectiveCentre =>
            centreTransform != null ? centreTransform : transform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalsBeforeSceneLoad()
        {
            Shader.SetGlobalVector(RewriteCentreId, Vector4.zero);
            Shader.SetGlobalFloat(RewriteRadiusId, 0f);
            Shader.SetGlobalFloat(RewriteAmountId, 0f);
        }

        private void Awake()
        {
            ApplyGlobals(0f, 0f);
        }

        private void OnEnable()
        {
            ApplyGlobals(_currentRadius, _currentAmount);
        }

        private void Update()
        {
            UpdateCentreOnly();

            if (enableKeyboardTest && WasTestKeyPressed())
            {
                if (!IsPlaying || restartWhenPressedAgain)
                {
                    PlayPrototype();
                }
            }

            TickPrototype(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            ResetImmediately();
        }

        private void OnDestroy()
        {
            ResetImmediately();
        }

        [ContextMenu("Play World Rewrite Prototype")]
        public void PlayPrototype()
        {
            _state = PrototypeState.Expanding;
            _stateTime = 0f;
            _currentRadius = 0f;
            _currentAmount = 0f;
            ApplyGlobals(_currentRadius, _currentAmount);
        }

        [ContextMenu("Recover World Rewrite Prototype")]
        public void RecoverPrototype()
        {
            if (_state == PrototypeState.Idle)
            {
                return;
            }

            _state = PrototypeState.Recovering;
            _stateTime = 0f;
        }

        [ContextMenu("Reset World Rewrite Immediately")]
        public void ResetImmediately()
        {
            _state = PrototypeState.Idle;
            _stateTime = 0f;
            _currentRadius = 0f;
            _currentAmount = 0f;
            ApplyGlobals(0f, 0f);
        }

        /// <summary>
        /// Useful for later integration or Inspector-driven tests.
        /// Values are applied immediately without running the prototype timeline.
        /// </summary>
        public void SetManualState(float radius, float amount)
        {
            _state = PrototypeState.Idle;
            _stateTime = 0f;
            _currentRadius = Mathf.Max(0f, radius);
            _currentAmount = Mathf.Clamp01(amount);
            ApplyGlobals(_currentRadius, _currentAmount);
        }

        private void TickPrototype(float unscaledDeltaTime)
        {
            switch (_state)
            {
                case PrototypeState.Idle:
                    return;

                case PrototypeState.Expanding:
                {
                    _stateTime += unscaledDeltaTime;
                    float progress = Mathf.Clamp01(
                        _stateTime / Mathf.Max(expandDuration, 0.0001f)
                    );

                    _currentRadius =
                        maximumRadius * Mathf.Clamp01(radiusCurve.Evaluate(progress));

                    _currentAmount =
                        Mathf.Clamp01(amountCurve.Evaluate(progress));

                    ApplyGlobals(_currentRadius, _currentAmount);

                    if (progress >= 1f)
                    {
                        _state = PrototypeState.Holding;
                        _stateTime = 0f;
                    }

                    break;
                }

                case PrototypeState.Holding:
                {
                    _currentRadius = maximumRadius;
                    _currentAmount = 1f;
                    ApplyGlobals(_currentRadius, _currentAmount);

                    _stateTime += unscaledDeltaTime;
                    if (_stateTime >= holdDuration)
                    {
                        _state = PrototypeState.Recovering;
                        _stateTime = 0f;
                    }

                    break;
                }

                case PrototypeState.Recovering:
                {
                    _stateTime += unscaledDeltaTime;
                    float progress = Mathf.Clamp01(
                        _stateTime / Mathf.Max(recoverDuration, 0.0001f)
                    );

                    // Keep the world-space area stable while its rewritten state
                    // fades out. This avoids an obvious inward "shield bubble".
                    _currentRadius = maximumRadius;
                    _currentAmount = 1f - Mathf.SmoothStep(0f, 1f, progress);

                    ApplyGlobals(_currentRadius, _currentAmount);

                    if (progress >= 1f)
                    {
                        ResetImmediately();
                    }

                    break;
                }
            }
        }

        private void UpdateCentreOnly()
        {
            Vector3 position = EffectiveCentre.position;
            Shader.SetGlobalVector(
                RewriteCentreId,
                new Vector4(position.x, position.y, position.z, 1f)
            );
        }

        private void ApplyGlobals(float radius, float amount)
        {
            Vector3 position = EffectiveCentre.position;

            Shader.SetGlobalVector(
                RewriteCentreId,
                new Vector4(position.x, position.y, position.z, 1f)
            );

            Shader.SetGlobalFloat(RewriteRadiusId, Mathf.Max(0f, radius));
            Shader.SetGlobalFloat(RewriteAmountId, Mathf.Clamp01(amount));
        }

        private static bool WasTestKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null
                && Keyboard.current.tKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.T);
#endif
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawRadiusGizmo)
            {
                return;
            }

            Transform centre = centreTransform != null
                ? centreTransform
                : transform;

            float radius = Application.isPlaying
                ? Mathf.Max(_currentRadius, 0.05f)
                : maximumRadius;

            Gizmos.color = new Color(0.30f, 0.92f, 1.00f, 0.65f);
            Gizmos.DrawWireSphere(centre.position, radius);
        }
    }
}
