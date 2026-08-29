#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class WindForceZone : MonoBehaviour
{
    [Header("🌪 风洞基础设置")]
    [Tooltip("是否启用风洞。")]
    public bool windEnabled = true;

    [Tooltip("风向是否使用这个物体自身的 Forward，也就是蓝色 Z 轴方向。")]
    public bool useTransformForward = true;

    [Tooltip("如果不使用 Transform Forward，就使用这个世界方向。")]
    public Vector3 customWindDirection = Vector3.forward;

    [Tooltip("风的加速度。数值越大，玩家被推得越快。")]
    public float windAcceleration = 38f;

    [Tooltip("风洞允许叠加到玩家身上的最大外部速度。")]
    public float maxWindSpeed = 28f;

    [Header("⬆ 垂直托举")]
    [Tooltip("额外向上的托举力。注意：如果 Z 轴本来就朝上，这个可以设为 0。")]
    public float upwardLiftAcceleration = 0f;

    [Tooltip("在风洞里是否强制视为离地。建议开启，否则地面摩擦会抵消很多风力。")]
    public bool forceAirborneInWind = true;

    [Header("🎯 中心线吸附")]
    [Tooltip("是否把玩家轻微拉向风洞中心线。第一版建议先关闭，等风向稳定后再打开。")]
    public bool useCenterPull = false;

    [Tooltip("把玩家拉向风洞中心线的力度。")]
    public float centerPullAcceleration = 10f;

    [Tooltip("玩家距离中心线多远时中心吸附达到最大效果。")]
    public float centerPullRadius = 3f;

    [Header("💥 进入 / 离开冲击")]
    [Tooltip("进入风洞瞬间给一点初速度。垂直测试时建议先设为 0。")]
    public float entryBoost = 0f;

    [Tooltip("离开风洞瞬间向风向补一脚。垂直测试时建议先设为 0。")]
    public float exitBoost = 0f;

    [Header("🎥 可选 FOV 效果")]
    [Tooltip("第一版建议先关闭。开启后进入风洞会有短暂 FOV 拉伸。")]
    public bool useFOVPulse = false;

    public float windFOV = 108f;
    public float fovLerpSpeed = 18f;
    public float fovPulseDuration = 0.25f;

    [Header("🧱 碰撞 / 射线设置")]
    [Tooltip("自动把风洞物体设置到 Ignore Raycast Layer，避免挡子弹。建议开启。")]
    public bool autoSetIgnoreRaycastLayer = true;

    [Tooltip("自动隐藏 MeshRenderer，避免用 Cube 做风洞时出现白色方块。")]
    public bool autoHideMeshRenderer = true;

    [Tooltip("检测玩家是否在风洞内时额外扩一点范围，避免边缘误判。")]
    public float insideCheckPadding = 0.35f;

    [Header("🧪 Debug")]
    public bool showDebugLogs = false;
    public bool drawGizmos = true;

    private BoxCollider _boxCollider;

    private StarterAssets.FirstPersonController _playerController;
    private CharacterController _playerCharacterController;
    private Transform _playerTransform;

    private bool _wasPlayerInside = false;

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();

        if (_boxCollider != null)
        {
            _boxCollider.isTrigger = true;
        }

        if (autoSetIgnoreRaycastLayer)
        {
            SetLayerToIgnoreRaycast();
        }

        if (autoHideMeshRenderer)
        {
            HideAccidentalRenderers();
        }
    }

    private void Start()
    {
        CachePlayerIfNeeded();
    }

    private void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = true;
        box.size = new Vector3(4f, 4f, 12f);
        box.center = Vector3.zero;
    }

    private void Update()
    {
        if (!windEnabled)
        {
            HandlePlayerExitIfNeeded();
            return;
        }

        CachePlayerIfNeeded();

        if (_playerController == null || _playerTransform == null || _boxCollider == null)
        {
            HandlePlayerExitIfNeeded();
            return;
        }

        bool isInside = IsPlayerInsideWindBox();

        if (isInside)
        {
            if (!_wasPlayerInside)
            {
                HandlePlayerEnter();
            }

            ApplyWind(Time.deltaTime);
        }
        else
        {
            if (_wasPlayerInside)
            {
                HandlePlayerExit();
            }
        }

        _wasPlayerInside = isInside;
    }

    private void CachePlayerIfNeeded()
    {
        if (_playerController != null && _playerTransform != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null) return;

        _playerController = player.GetComponent<StarterAssets.FirstPersonController>();
        _playerCharacterController = player.GetComponent<CharacterController>();
        _playerTransform = player.transform;
    }

    private bool IsPlayerInsideWindBox()
    {
        if (_playerTransform == null || _boxCollider == null) return false;

        // 检测多个点，而不是只检测脚底。
        // 这样玩家落地、跳跃、半个身体在风洞里时都更稳定。
        Vector3 basePos = _playerTransform.position;

        if (_playerCharacterController != null)
        {
            Vector3 center = _playerCharacterController.bounds.center;
            Vector3 feet = basePos + Vector3.up * 0.15f;
            Vector3 chest = basePos + Vector3.up * (_playerCharacterController.height * 0.55f);
            Vector3 head = basePos + Vector3.up * (_playerCharacterController.height * 0.9f);

            return IsWorldPointInsideBox(feet) ||
                   IsWorldPointInsideBox(center) ||
                   IsWorldPointInsideBox(chest) ||
                   IsWorldPointInsideBox(head);
        }

        return IsWorldPointInsideBox(basePos);
    }

    private bool IsWorldPointInsideBox(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        Vector3 center = _boxCollider.center;
        Vector3 halfSize = _boxCollider.size * 0.5f;

        halfSize += Vector3.one * Mathf.Max(0f, insideCheckPadding);

        Vector3 delta = localPoint - center;

        return Mathf.Abs(delta.x) <= halfSize.x &&
               Mathf.Abs(delta.y) <= halfSize.y &&
               Mathf.Abs(delta.z) <= halfSize.z;
    }

    private void ApplyWind(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        if (_playerController == null) return;

        Vector3 windDirection = GetWindDirection();

        Vector3 velocityDelta = windDirection * windAcceleration * deltaTime;

        if (upwardLiftAcceleration != 0f)
        {
            velocityDelta += Vector3.up * upwardLiftAcceleration * deltaTime;
        }

        if (useCenterPull)
        {
            velocityDelta += CalculateCenterPullVelocityDelta(deltaTime);
        }

        _playerController.AddExternalVelocity(
            velocityDelta,
            maxWindSpeed,
            forceAirborneInWind
        );

        if (useFOVPulse)
        {
            _playerController.RequestExternalFOV(
                windFOV,
                fovLerpSpeed,
                fovPulseDuration
            );
        }
    }

    private Vector3 GetWindDirection()
    {
        Vector3 direction;

        if (useTransformForward)
        {
            direction = transform.forward;
        }
        else
        {
            direction = customWindDirection;
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
    }

    private Vector3 CalculateCenterPullVelocityDelta(float deltaTime)
    {
        if (_playerTransform == null) return Vector3.zero;

        Vector3 localPlayerPos = transform.InverseTransformPoint(_playerTransform.position);

        // 默认风洞沿 local Z 轴前进。
        // 所以 X/Y 是偏离中心线的距离，Z 不参与吸附。
        Vector3 localOffsetFromCenterLine = new Vector3(
            localPlayerPos.x,
            localPlayerPos.y,
            0f
        );

        float distanceFromCenter = localOffsetFromCenterLine.magnitude;

        if (distanceFromCenter <= 0.001f)
        {
            return Vector3.zero;
        }

        float pullRatio = Mathf.Clamp01(
            distanceFromCenter / Mathf.Max(0.01f, centerPullRadius)
        );

        Vector3 localPullDirection = -localOffsetFromCenterLine.normalized;
        Vector3 worldPullDirection = transform.TransformDirection(localPullDirection).normalized;

        return worldPullDirection * centerPullAcceleration * pullRatio * deltaTime;
    }

    private void HandlePlayerEnter()
    {
        Vector3 windDirection = GetWindDirection();

        if (entryBoost > 0f && _playerController != null)
        {
            _playerController.AddExternalVelocity(
                windDirection * entryBoost,
                maxWindSpeed,
                forceAirborneInWind
            );
        }

        if (useFOVPulse && _playerController != null)
        {
            _playerController.RequestExternalFOV(
                windFOV,
                fovLerpSpeed,
                fovPulseDuration
            );
        }

        if (showDebugLogs)
        {
      Debug.Log(" [WindForceZone] Player entered: " + gameObject.name);
      Debug.Log(" [WindForceZone] Wind Direction: " + windDirection);
        }
    }

    private void HandlePlayerExit()
    {
        Vector3 windDirection = GetWindDirection();

        if (exitBoost > 0f && _playerController != null)
        {
            _playerController.AddExternalVelocity(
                windDirection * exitBoost,
                maxWindSpeed,
                true
            );
        }

        if (useFOVPulse && _playerController != null)
        {
            _playerController.ReleaseExternalFOV(8f);
        }

        if (showDebugLogs)
        {
      Debug.Log(" [WindForceZone] Player exited: " + gameObject.name);
        }
    }

    private void HandlePlayerExitIfNeeded()
    {
        if (_wasPlayerInside)
        {
            HandlePlayerExit();
            _wasPlayerInside = false;
        }
    }

    private void OnDisable()
    {
        if (_playerController != null && useFOVPulse)
        {
            _playerController.ReleaseExternalFOV(8f);
        }

        _wasPlayerInside = false;
    }

    private void SetLayerToIgnoreRaycast()
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer < 0)
        {
      Debug.LogWarning("️ [WindForceZone] 找不到 Ignore Raycast Layer。请检查 Unity 默认 Layer 是否存在。");
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

    private void HideAccidentalRenderers()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 windDirection;

        if (useTransformForward)
        {
            windDirection = transform.forward;
        }
        else
        {
            windDirection = customWindDirection;
        }

        if (windDirection.sqrMagnitude < 0.001f)
        {
            windDirection = Vector3.forward;
        }

        windDirection.Normalize();

        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.18f);
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(box.center, box.size);

            Gizmos.matrix = oldMatrix;
        }

        Gizmos.color = Color.cyan;

        Vector3 start = transform.position;
        Vector3 end = start + windDirection * 4f;

        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.18f);

        Vector3 arrowRight = Quaternion.LookRotation(windDirection) *
                             Quaternion.Euler(0f, 150f, 0f) *
                             Vector3.forward;

        Vector3 arrowLeft = Quaternion.LookRotation(windDirection) *
                            Quaternion.Euler(0f, -150f, 0f) *
                            Vector3.forward;

        Gizmos.DrawLine(end, end + arrowRight * 0.8f);
        Gizmos.DrawLine(end, end + arrowLeft * 0.8f);
    }
}
