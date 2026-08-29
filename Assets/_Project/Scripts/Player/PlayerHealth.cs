#pragma warning disable 0618
#pragma warning disable 0414
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health Configuration")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Rewind & Reboot Configuration")]
    [Tooltip("Total duration of the historical position buffer in seconds.")]
    public float rewindHistoryDuration = 4f;

    [Tooltip("Interval between recorded breadcrumbs.")]
    public float recordInterval = 0.05f;

    [Tooltip("Duration of the physical rewind replay back to safe ground.")]
    public float rewindPlaybackDuration = 1.2f;

    [Tooltip("Time penalty applied to GameManager when rebooting.")]
    public float rebootTimePenalty = 3f;

    [Tooltip("Invulnerability time granted after completing a reboot.")]
    public float postRebootInvulnerability = 1.5f;

    [Header("Grounded Safety Validation")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckDistance = 2.0f;
    [SerializeField] private float ledgeSafetyPadding = 0.35f;

    public bool IsRewinding => _isRewinding;

    public event Action<float, float> Damaged;
    public event Action RewindStarted;
    public event Action RewindCompleted;

    [System.Serializable]
    public struct Breadcrumb
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool isGroundedSafe;

        public Breadcrumb(Vector3 pos, Quaternion rot, bool safe)
        {
            position = pos;
            rotation = rot;
            isGroundedSafe = safe;
        }
    }

    private readonly List<Breadcrumb> _history = new List<Breadcrumb>();
    private CharacterController _characterController;
    private FirstPersonController _firstPersonController;
    private PlayerInput _playerInput;
    private StarterAssetsInputs _starterInputs;

    private bool _isRewinding;
    private bool _isInvulnerable;
    private float _invulnerabilityTimer;
    private float _recordTimer;
    private Vector3 _lastConfirmedGroundedPos;
    private Quaternion _lastConfirmedGroundedRot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _characterController = GetComponent<CharacterController>();
        _firstPersonController = GetComponent<FirstPersonController>();
        _playerInput = GetComponent<PlayerInput>();
        _starterInputs = GetComponent<StarterAssetsInputs>();

        currentHealth = maxHealth;
        _lastConfirmedGroundedPos = transform.position;
        _lastConfirmedGroundedRot = transform.rotation;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        _lastConfirmedGroundedPos = transform.position;
        _lastConfirmedGroundedRot = transform.rotation;
        SaveSafePosition();
    }

    private void Update()
    {
        if (_isInvulnerable)
        {
            _invulnerabilityTimer -= Time.unscaledDeltaTime;
            if (_invulnerabilityTimer <= 0f)
            {
                _isInvulnerable = false;
            }
        }

        if (_isRewinding) return;

        _recordTimer += Time.unscaledDeltaTime;
        if (_recordTimer >= recordInterval)
        {
            _recordTimer = 0f;
            SaveSafePosition();
        }
    }

    public void SaveSafePosition()
    {
        bool isSolidGround = CheckIsSolidGrounded(out Vector3 safeGroundPoint);

        if (isSolidGround)
        {
            _lastConfirmedGroundedPos = safeGroundPoint;
            _lastConfirmedGroundedRot = transform.rotation;
        }

        _history.Add(new Breadcrumb(transform.position, transform.rotation, isSolidGround));

        int maxCount = Mathf.CeilToInt(rewindHistoryDuration / Mathf.Max(0.01f, recordInterval));
        while (_history.Count > maxCount)
        {
            _history.RemoveAt(0);
        }
    }

    private bool CheckIsSolidGrounded(out Vector3 safePoint)
    {
        safePoint = transform.position;

        if (_firstPersonController != null && !_firstPersonController.Grounded)
        {
            return false;
        }

        Vector3 centerOrigin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(centerOrigin, Vector3.down, out RaycastHit centerHit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float slope = Vector3.Angle(centerHit.normal, Vector3.up);
        float maxSlope = _characterController != null ? _characterController.slopeLimit : 45f;
        if (slope > maxSlope)
        {
            return false;
        }

        Vector3[] probeOffsets = new Vector3[]
        {
            Vector3.forward * ledgeSafetyPadding,
            Vector3.back * ledgeSafetyPadding,
            Vector3.left * ledgeSafetyPadding,
            Vector3.right * ledgeSafetyPadding
        };

        for (int i = 0; i < probeOffsets.Length; i++)
        {
            if (!Physics.Raycast(centerOrigin + probeOffsets[i], Vector3.down, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
        }

        safePoint = centerHit.point + Vector3.up * 0.05f;
        return true;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }

    public void TakeDamage(float amount, bool bypassTemporaryDamageInvulnerability)
    {
        if (_isRewinding) return;
        if (_isInvulnerable && !bypassTemporaryDamageInvulnerability) return;
        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Damaged?.Invoke(amount, currentHealth);

        if (currentHealth <= 0f)
        {
            TriggerSystemReboot();
        }
    }

    public void GrantTemporaryDamageInvulnerability(float duration)
    {
        _isInvulnerable = true;
        _invulnerabilityTimer = Mathf.Max(_invulnerabilityTimer, duration);
    }

    public void ResetRewindHistory()
    {
        _history.Clear();
        _lastConfirmedGroundedPos = transform.position;
        _lastConfirmedGroundedRot = transform.rotation;
        SaveSafePosition();
    }

    public void TriggerSystemReboot()
    {
        if (_isRewinding) return;
        StartCoroutine(PhysicalRewindRoutine());
    }

    private IEnumerator PhysicalRewindRoutine()
    {
        _isRewinding = true;
        RewindStarted?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddBonusScore(0f, -rebootTimePenalty, "REBOOT_PENALTY");
            GameManager.Instance.BreakCombo();
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.TriggerRewindGlitch(rewindPlaybackDuration);
        }

        if (VisualFeedbackController.Instance != null)
        {
            VisualFeedbackController.Instance.TriggerRebootVisuals(rewindPlaybackDuration);
        }

        if (_playerInput != null) _playerInput.enabled = false;
        if (_firstPersonController != null) _firstPersonController.enabled = false;
        if (_characterController != null) _characterController.enabled = false;

        // Find the earliest safe breadcrumb in history, or fall back to last confirmed ground point
        Vector3 targetGroundedPos = _lastConfirmedGroundedPos;
        Quaternion targetGroundedRot = _lastConfirmedGroundedRot;

        for (int i = 0; i < _history.Count; i++)
        {
            if (_history[i].isGroundedSafe)
            {
                targetGroundedPos = _history[i].position;
                targetGroundedRot = _history[i].rotation;
                break;
            }
        }

        // Replay historical positions backward
        if (_history.Count > 1)
        {
            List<Breadcrumb> playbackPoints = new List<Breadcrumb>(_history);
            playbackPoints.Reverse();

            float elapsed = 0f;
            int pointCount = playbackPoints.Count;

            while (elapsed < rewindPlaybackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / rewindPlaybackDuration);

                float floatIndex = t * (pointCount - 1);
                int lowerIndex = Mathf.FloorToInt(floatIndex);
                int upperIndex = Mathf.Min(lowerIndex + 1, pointCount - 1);
                float segmentT = floatIndex - lowerIndex;

                transform.position = Vector3.Lerp(playbackPoints[lowerIndex].position, playbackPoints[upperIndex].position, segmentT);
                transform.rotation = Quaternion.Slerp(playbackPoints[lowerIndex].rotation, playbackPoints[upperIndex].rotation, segmentT);

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(rewindPlaybackDuration);
        }

        // Place firmly onto confirmed solid ground
        transform.position = targetGroundedPos;
        transform.rotation = targetGroundedRot;

        if (_characterController != null) _characterController.enabled = true;
        if (_firstPersonController != null)
        {
            _firstPersonController.enabled = true;
            _firstPersonController.ResetMomentum();
        }
        if (_playerInput != null) _playerInput.enabled = true;

        currentHealth = maxHealth;
        GrantTemporaryDamageInvulnerability(postRebootInvulnerability);
        ResetRewindHistory();

        _isRewinding = false;
        RewindCompleted?.Invoke();
    }
}