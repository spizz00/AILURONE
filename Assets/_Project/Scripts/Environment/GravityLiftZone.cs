#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class GravityLiftZone : MonoBehaviour
{
    [Header("基础开关")]
    public bool liftEnabled = true;

    [Header("持续上升设置")]
    [Tooltip("区域内目标上升速度。不是一次性弹力，而是持续把玩家托向这个速度。")]
    public float targetUpSpeed = 9.5f;

    [Tooltip("上升速度变化速度。越大越快进入稳定上升。")]
    public float liftAcceleration = 28f;

    [Tooltip("区域内重力倍率。0.15 表示基本抵消大部分重力。")]
    [Range(0f, 1f)]
    public float liftGravityMultiplier = 0.12f;

    [Tooltip("脚本每次调用后，FirstPersonController 保持 Lift 状态的时间。")]
    public float liftKeepAliveTime = 0.12f;

    [Header("顶部减速")]
    public bool useTopSlowdown = true;

    [Tooltip("从区域高度的多少比例开始减速。0.7 表示顶部 30% 开始减速。")]
    [Range(0.1f, 0.95f)]
    public float topSlowdownStart = 0.68f;

    [Tooltip("到达顶部时的目标上升速度。不要设成 0，否则顶部可能卡住。")]
    public float topTargetUpSpeed = 2.2f;

    [Header("顶部出口推力")]
    public bool useExitBoost = true;

    [Tooltip("玩家高度超过这个比例后离开区域，才会获得出口推力。")]
    [Range(0.1f, 1f)]
    public float exitBoostHeightThreshold = 0.82f;

    [Tooltip("根据玩家当前朝向给的水平推力。")]
    public float exitForwardBoost = 8f;

    [Tooltip("离开顶部时额外向上的推力。")]
    public float exitUpBoost = 4.5f;

    [Tooltip("出口推力后短时间降低外力衰减，让飞出更顺。")]
    public float exitNoDragDuration = 0.35f;

    [Tooltip("出口推力的离地保护时间。")]
    public float exitGraceTime = 0.18f;

    [Tooltip("离开顶部后多久内不会再次触发出口推力。")]
    public float exitBoostCooldown = 0.5f;

    [Header("水平控制")]
    [Tooltip("是否轻微拉向中心。第一版建议关闭，避免玩家感觉被挤出去。")]
    public bool useCenterPull = false;

    [Tooltip("中心吸附力量。只有 Use Center Pull 开启时有效。")]
    public float centerPullStrength = 2f;

    [Tooltip("离中心多近时停止水平吸附。")]
    public float centerStopDistance = 0.8f;

    [Tooltip("中心吸附最大速度。")]
    public float maxCenterPullSpeed = 8f;

    [Header("FOV")]
    public bool useFOV = true;
    public float liftFOV = 100f;
    public float liftFOVSpeed = 10f;

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
    private Transform _playerTransform;

    private float _lastHeight01 = 0f;
    private bool _wasNearTopRecently = false;
    private float _lastExitBoostTime = -999f;

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
        box.center = new Vector3(0f, 4f, 0f);
        box.size = new Vector3(5f, 8f, 5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        StarterAssets.FirstPersonController fpc =
            other.GetComponentInParent<StarterAssets.FirstPersonController>();

        if (fpc == null) return;

        _playerController = fpc;
        _playerTransform = fpc.transform;

        _lastHeight01 = GetPlayerHeight01(_playerTransform.position);
        _wasNearTopRecently = _lastHeight01 >= exitBoostHeightThreshold;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!liftEnabled) return;

        StarterAssets.FirstPersonController fpc =
            other.GetComponentInParent<StarterAssets.FirstPersonController>();

        if (fpc == null) return;

        _playerController = fpc;
        _playerTransform = fpc.transform;

        ApplyLift(Time.deltaTime);
    }

    private void OnTriggerExit(Collider other)
    {
        StarterAssets.FirstPersonController fpc =
            other.GetComponentInParent<StarterAssets.FirstPersonController>();

        if (fpc == null) return;

        if (_playerController == fpc)
        {
            TryApplyExitBoost();

            _playerController = null;
            _playerTransform = null;
            _wasNearTopRecently = false;
            _lastHeight01 = 0f;
        }
    }

    private void ApplyLift(float deltaTime)
    {
        if (_playerController == null || _playerTransform == null) return;
        if (_boxCollider == null) return;
        if (deltaTime <= 0f) return;

        float height01 = GetPlayerHeight01(_playerTransform.position);
        _lastHeight01 = height01;

        if (height01 >= exitBoostHeightThreshold)
        {
            _wasNearTopRecently = true;
        }

        float currentTargetUpSpeed = targetUpSpeed;

        if (useTopSlowdown && height01 >= topSlowdownStart)
        {
            float slowT = Mathf.InverseLerp(topSlowdownStart, 1f, height01);
            slowT = Mathf.SmoothStep(0f, 1f, slowT);

            currentTargetUpSpeed = Mathf.Lerp(
                targetUpSpeed,
                topTargetUpSpeed,
                slowT
            );
        }

        _playerController.ApplyGravityLift(
            currentTargetUpSpeed,
            liftAcceleration,
            liftGravityMultiplier,
            liftKeepAliveTime
        );

        if (useCenterPull)
        {
            ApplyGentleCenterPull(deltaTime);
        }

        if (useFOV)
        {
            _playerController.RequestExternalFOV(liftFOV, liftFOVSpeed, 0.12f);
        }

        if (showDebugLine)
        {
            Debug.DrawLine(
                _playerTransform.position,
                _playerTransform.position + Vector3.up * 2f,
                Color.cyan,
                0.02f
            );
        }
    }

    private void ApplyGentleCenterPull(float deltaTime)
    {
        if (_playerController == null || _playerTransform == null) return;

        Vector3 centerWorld = GetBoxCenterWorld();
        Vector3 toCenter = centerWorld - _playerTransform.position;
        Vector3 horizontalToCenter = new Vector3(toCenter.x, 0f, toCenter.z);

        if (horizontalToCenter.magnitude <= centerStopDistance)
        {
            return;
        }

        Vector3 pullVelocity = horizontalToCenter.normalized * centerPullStrength * deltaTime;

        _playerController.AddExternalVelocity(
            pullVelocity,
            maxCenterPullSpeed,
            false
        );
    }

    private void TryApplyExitBoost()
    {
        if (!useExitBoost) return;
        if (_playerController == null || _playerTransform == null) return;

        if (Time.time - _lastExitBoostTime < exitBoostCooldown)
        {
            return;
        }

        if (!_wasNearTopRecently && _lastHeight01 < exitBoostHeightThreshold)
        {
            return;
        }

        Vector3 forward = _playerTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        Vector3 boostVelocity =
            forward * exitForwardBoost +
            Vector3.up * exitUpBoost;

        _playerController.ApplyGravityLiftExitBoost(
            boostVelocity,
            exitGraceTime,
            exitNoDragDuration
        );

        _lastExitBoostTime = Time.time;
    }

    private float GetPlayerHeight01(Vector3 playerWorldPosition)
    {
        if (_boxCollider == null) return 0f;

        Vector3 localPoint = _boxCollider.transform.InverseTransformPoint(playerWorldPosition);

        float bottom = _boxCollider.center.y - _boxCollider.size.y * 0.5f;
        float top = _boxCollider.center.y + _boxCollider.size.y * 0.5f;

        return Mathf.InverseLerp(bottom, top, localPoint.y);
    }

    private Vector3 GetBoxCenterWorld()
    {
        if (_boxCollider == null)
        {
            return transform.position;
        }

        return _boxCollider.transform.TransformPoint(_boxCollider.center);
    }

    private void SetLayerToIgnoreRaycast()
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer < 0)
        {
      Debug.LogWarning("️ [GravityLiftZone] 找不到 Ignore Raycast Layer。");
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

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.13f);
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = new Color(0f, 0.9f, 1f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);

            float slowdownY = box.center.y - box.size.y * 0.5f + box.size.y * topSlowdownStart;
            float exitY = box.center.y - box.size.y * 0.5f + box.size.y * exitBoostHeightThreshold;

            DrawLocalBoxHorizontalLine(box, slowdownY, new Color(1f, 1f, 0f, 0.85f));
            DrawLocalBoxHorizontalLine(box, exitY, new Color(0f, 1f, 0.35f, 0.85f));

            Gizmos.matrix = oldMatrix;
        }
    }

    private void DrawLocalBoxHorizontalLine(BoxCollider box, float y, Color color)
    {
        Gizmos.color = color;

        Vector3 p1 = new Vector3(box.center.x - box.size.x * 0.5f, y, box.center.z - box.size.z * 0.5f);
        Vector3 p2 = new Vector3(box.center.x + box.size.x * 0.5f, y, box.center.z - box.size.z * 0.5f);
        Vector3 p3 = new Vector3(box.center.x + box.size.x * 0.5f, y, box.center.z + box.size.z * 0.5f);
        Vector3 p4 = new Vector3(box.center.x - box.size.x * 0.5f, y, box.center.z + box.size.z * 0.5f);

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
}
