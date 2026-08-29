#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LandingAssistZone : MonoBehaviour
{
    [Header("基础开关")]
    public bool assistEnabled = true;

    [Header("目标点")]
    [Tooltip("玩家会被轻微拉向这个点。建议放在目标平台中心上方一点。")]
    public Transform landingTarget;

    [Header("辅助强度")]
    [Tooltip("把玩家往 Landing Target 拉的力量。")]
    public float pullToCenterStrength = 10f;

    [Tooltip("降低横向飞行速度的力量，用来减少飞过头。")]
    public float horizontalBrakeStrength = 0.45f;

    [Tooltip("向下压的力量，让玩家更容易落到平台上。")]
    public float downwardAssistStrength = 4f;

    [Tooltip("辅助速度上限，防止吸附太强。")]
    public float maxAssistSpeed = 18f;

    [Header("触发条件")]
    [Tooltip("只在玩家空中时辅助。建议勾选。")]
    public bool onlyAssistWhenAirborne = true;

    [Tooltip("玩家离 Landing Target 多近时停止横向吸附，只保留下压。")]
    public float centerStopDistance = 0.7f;

    [Tooltip("玩家落地后是否停止辅助。建议勾选。")]
    public bool stopWhenGrounded = true;

    [Header("视觉 / 碰撞设置")]
    [Tooltip("自动设置为 Trigger。")]
    public bool forceTrigger = true;

    [Tooltip("自动设置到 Ignore Raycast，避免挡子弹。")]
    public bool autoSetIgnoreRaycastLayer = true;

    [Tooltip("隐藏本物体 MeshRenderer，避免白盒挡画面。")]
    public bool autoHideOwnMeshRenderer = true;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool showDebugLine = true;

    private BoxCollider _boxCollider;

    private StarterAssets.FirstPersonController _playerController;
    private CharacterController _characterController;
    private Transform _playerTransform;

    private Vector3 _lastPlayerPosition;
    private bool _hasLastPlayerPosition = false;

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();

        if (_boxCollider != null && forceTrigger)
        {
            _boxCollider.isTrigger = true;
        }

        if (autoSetIgnoreRaycastLayer)
        {
            SetLayerToIgnoreRaycast();
        }

        if (autoHideOwnMeshRenderer)
        {
            HideOwnMeshRenderer();
        }
    }

    private void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = true;
        box.size = new Vector3(6f, 4f, 6f);
        box.center = new Vector3(0f, 2f, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        StarterAssets.FirstPersonController fpc =
            other.GetComponentInParent<StarterAssets.FirstPersonController>();

        if (fpc == null) return;

        _playerController = fpc;
        _characterController = fpc.GetComponent<CharacterController>();
        _playerTransform = fpc.transform;

        _lastPlayerPosition = _playerTransform.position;
        _hasLastPlayerPosition = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!assistEnabled) return;
        if (landingTarget == null) return;

        StarterAssets.FirstPersonController fpc =
            other.GetComponentInParent<StarterAssets.FirstPersonController>();

        if (fpc == null) return;

        _playerController = fpc;
        _characterController = fpc.GetComponent<CharacterController>();
        _playerTransform = fpc.transform;

        ApplyLandingAssist(Time.deltaTime);
    }

    private void OnTriggerExit(Collider other)
    {
        StarterAssets.FirstPersonController fpc =
            other.GetComponentInParent<StarterAssets.FirstPersonController>();

        if (fpc == null) return;

        if (_playerController == fpc)
        {
            _playerController = null;
            _characterController = null;
            _playerTransform = null;
            _hasLastPlayerPosition = false;
        }
    }

    private void ApplyLandingAssist(float deltaTime)
    {
        if (_playerController == null || _playerTransform == null) return;
        if (deltaTime <= 0f) return;

        if (stopWhenGrounded && _playerController.Grounded)
        {
            UpdateLastPlayerPosition();
            return;
        }

        if (onlyAssistWhenAirborne && _playerController.Grounded)
        {
            UpdateLastPlayerPosition();
            return;
        }

        Vector3 currentPosition = _playerTransform.position;

        Vector3 estimatedVelocity = Vector3.zero;

        if (_hasLastPlayerPosition)
        {
            estimatedVelocity = (currentPosition - _lastPlayerPosition) / Mathf.Max(0.0001f, deltaTime);
        }

        Vector3 horizontalEstimatedVelocity = new Vector3(
            estimatedVelocity.x,
            0f,
            estimatedVelocity.z
        );

        Vector3 toTarget = landingTarget.position - currentPosition;
        Vector3 horizontalToTarget = new Vector3(
            toTarget.x,
            0f,
            toTarget.z
        );

        Vector3 assistVelocity = Vector3.zero;

        float horizontalDistance = horizontalToTarget.magnitude;

        if (horizontalDistance > centerStopDistance)
        {
            Vector3 centerPull = horizontalToTarget.normalized * pullToCenterStrength;
            assistVelocity += centerPull;
        }

        if (horizontalEstimatedVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 brakeVelocity = -horizontalEstimatedVelocity * horizontalBrakeStrength;
            assistVelocity += brakeVelocity;
        }

        assistVelocity += Vector3.down * downwardAssistStrength;

        assistVelocity = Vector3.ClampMagnitude(assistVelocity, maxAssistSpeed);

        _playerController.AddExternalVelocity(
            assistVelocity * deltaTime,
            maxAssistSpeed,
            false
        );

        if (showDebugLine)
        {
            Debug.DrawLine(currentPosition, landingTarget.position, Color.green, 0.02f);
        }

        UpdateLastPlayerPosition();
    }

    private void UpdateLastPlayerPosition()
    {
        if (_playerTransform == null) return;

        _lastPlayerPosition = _playerTransform.position;
        _hasLastPlayerPosition = true;
    }

    private void SetLayerToIgnoreRaycast()
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer < 0)
        {
      Debug.LogWarning("️ [LandingAssistZone] 找不到 Ignore Raycast Layer。");
            return;
        }

        SetLayerRecursively(gameObject, ignoreRaycastLayer);
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void HideOwnMeshRenderer()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.0f, 1f, 0.45f, 0.14f);
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = new Color(0.0f, 1f, 0.45f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);

            Gizmos.matrix = oldMatrix;
        }

        if (landingTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(landingTarget.position, 0.25f);
            Gizmos.DrawWireSphere(landingTarget.position, centerStopDistance);
        }
    }
}
