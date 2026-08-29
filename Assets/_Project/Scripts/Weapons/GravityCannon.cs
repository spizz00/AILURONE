#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class GravityCannon : MonoBehaviour
{
    [Header("🧲 基础开关")]
    public bool cannonEnabled = true;

    [Header("🎯 发射点设置")]
    [Tooltip("黑洞核心点。玩家进入核心后会被吸附到这里，再从这里发射。")]
    public Transform launchPoint;

    [Tooltip("大炮会根据这个目标点计算发射速度。建议放在目标平台上方一点。")]
    public Transform launchTarget;

    [Header("📦 Box 吸力区域")]
    [Tooltip("外圈吸力区域。留空则自动使用本物体的 BoxCollider。")]
    public BoxCollider outerPullBox;

    [Tooltip("内圈吸力盒子的大小比例。基于 Outer Box。X/Z 越小，内圈越窄。")]
    public Vector3 innerBoxScale = new Vector3(0.55f, 0.75f, 0.55f);

    [Tooltip("内圈盒子的本地偏移。一般保持 0。")]
    public Vector3 innerBoxLocalOffset = Vector3.zero;

    [Tooltip("核心锁定半径。只有进入 LaunchPoint 附近这个半径，才会正式捕获并发射。")]
    public float commitRadius = 0.8f;

    [Header("🕳 黑洞吸力")]
    [Tooltip("外圈吸力。玩家应该可以轻松逃出去。")]
    public float outerPullStrength = 8f;

    [Tooltip("内圈吸力。应该明显强于外圈，但玩家仍然可以用移动能力逃逸。")]
    public float innerPullStrength = 36f;

    [Tooltip("靠近核心时额外增加的吸力，让进入核心前有明显吸入感。")]
    public float nearCoreBonusStrength = 10f;

    [Tooltip("靠近核心多少米内开始额外增强吸力。")]
    public float nearCoreBonusDistance = 2.2f;

    [Tooltip("吸力最多能叠加到玩家身上的外部速度。")]
    public float maxPullExternalSpeed = 26f;

    [Tooltip("吸入时是否强制视为离地。第一版建议关闭，否则地面上会有点飘。")]
    public bool forceAirborneWhilePulling = false;

    [Header("⏳ 核心捕获 / 蓄力")]
    [Tooltip("进入核心后，被吸附到 LaunchPoint 并蓄力的时间。")]
    public float captureDuration = 0.3f;

    [Tooltip("吸附移动曲线强度。越高越像快速吸入。")]
    public float captureSmoothPower = 2.0f;

    [Tooltip("捕获期间是否让玩家身体朝向发射目标。第一人称建议关闭。")]
    public bool rotatePlayerTowardsTarget = false;

    [Header("🚀 发射")]
    [Tooltip("预计飞行时间。越小，水平速度越快；越大，弧线越高更慢。")]
    public float flightTime = 1.35f;

    [Tooltip("最大整体发射速度。太低会飞不到，太高可能失控。")]
    public float maxLaunchSpeed = 80f;

    [Tooltip("最大水平外力速度。一般保持 80。")]
    public float maxHorizontalExternalSpeed = 80f;

    [Tooltip("发射后多久降低空中阻尼，让轨迹更稳定。")]
    public float noDragDuration = 0.8f;

    [Tooltip("发射后多长时间强制视为离地，避免刚发射就被地面判定吃掉速度。")]
    public float launchGraceTime = 0.25f;

    [Tooltip("发射后多久锁 Jump / Dash，避免刚起飞就打断轨迹。")]
    public float actionLockDuration = 0.45f;

    [Tooltip("发射后多久内不会再次被同一个大炮捕获。")]
    public float reenterCooldown = 0.6f;

    [Header("🎥 FOV")]
    public bool useFOV = true;
    public float outerPullFOV = 96f;
    public float innerPullFOV = 101f;
    public float pullFOVSpeed = 10f;
    public float chargeFOV = 105f;
    public float chargeFOVSpeed = 18f;
    public float launchFOV = 115f;
    public float launchFOVSpeed = 28f;
    public float launchFOVDuration = 0.45f;

    [Header("🎧 音效，可空")]
    public AudioClip chargeSound;
    public AudioClip launchSound;

    [Header("🧱 碰撞 / 射线设置")]
    [Tooltip("自动把大炮本体和子物体设置到 Ignore Raycast，避免挡子弹。")]
    public bool autoSetIgnoreRaycastLayer = true;

    [Tooltip("只隐藏本物体上的 MeshRenderer，避免 Cube 白块挡画面；不会隐藏子物体视觉效果。")]
    public bool autoHideOwnMeshRenderer = true;

    [Header("🧪 Debug")]
    public bool showDebugLogs = true;
    public bool drawGizmos = true;
    public int trajectoryPreviewSteps = 24;

    private AudioSource _audioSource;

    private StarterAssets.FirstPersonController _playerController;
    private Transform _playerTransform;
    private CharacterController _playerCharacterController;

    private bool _isCommitting = false;
    private float _lastLaunchTime = -999f;

    private void Awake()
    {
        if (outerPullBox == null)
        {
            outerPullBox = GetComponent<BoxCollider>();
        }

        if (outerPullBox != null)
        {
            outerPullBox.isTrigger = true;
        }

        if (autoSetIgnoreRaycastLayer)
        {
            SetLayerToIgnoreRaycast();
        }

        if (autoHideOwnMeshRenderer)
        {
            HideOwnMeshRenderer();
        }

        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
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
        box.size = new Vector3(5f, 4f, 5f);
        box.center = Vector3.zero;

        outerPullBox = box;

        innerBoxScale = new Vector3(0.55f, 0.75f, 0.55f);
        commitRadius = 0.8f;
        outerPullStrength = 8f;
        innerPullStrength = 36f;
        maxPullExternalSpeed = 26f;
    }

    private void Start()
    {
        CachePlayerIfNeeded();
    }

    private void Update()
    {
        if (!cannonEnabled) return;
        if (_isCommitting) return;

        if (Time.time - _lastLaunchTime < reenterCooldown)
        {
            return;
        }

        if (launchPoint == null || launchTarget == null || outerPullBox == null)
        {
            return;
        }

        CachePlayerIfNeeded();

        if (_playerController == null || _playerTransform == null)
        {
            return;
        }

        bool insideOuterBox = IsInsideBox(
            outerPullBox,
            _playerTransform.position,
            Vector3.one,
            Vector3.zero
        );

        if (!insideOuterBox)
        {
            return;
        }

        float distanceToCore = Vector3.Distance(_playerTransform.position, launchPoint.position);

        if (distanceToCore <= commitRadius)
        {
            StartCoroutine(CommitAndLaunchRoutine(_playerController, _playerCharacterController));
            return;
        }

        bool insideInnerBox = IsInsideBox(
            outerPullBox,
            _playerTransform.position,
            innerBoxScale,
            innerBoxLocalOffset
        );

        ApplyBlackHolePull(distanceToCore, insideInnerBox, Time.deltaTime);
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

    private bool IsInsideBox(
        BoxCollider box,
        Vector3 worldPosition,
        Vector3 sizeScale,
        Vector3 localCenterOffset
    )
    {
        if (box == null) return false;

        Vector3 localPoint = box.transform.InverseTransformPoint(worldPosition);
        Vector3 localCenter = box.center + localCenterOffset;

        Vector3 scaledSize = new Vector3(
            box.size.x * Mathf.Abs(sizeScale.x),
            box.size.y * Mathf.Abs(sizeScale.y),
            box.size.z * Mathf.Abs(sizeScale.z)
        );

        Vector3 halfSize = scaledSize * 0.5f;
        Vector3 difference = localPoint - localCenter;

        return Mathf.Abs(difference.x) <= halfSize.x &&
               Mathf.Abs(difference.y) <= halfSize.y &&
               Mathf.Abs(difference.z) <= halfSize.z;
    }

    private void ApplyBlackHolePull(float distanceToCore, bool insideInnerBox, float deltaTime)
    {
        if (_playerController == null || _playerTransform == null) return;
        if (deltaTime <= 0f) return;

        Vector3 directionToCore = launchPoint.position - _playerTransform.position;

        if (directionToCore.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 pullDirection = directionToCore.normalized;

        float pullStrength = insideInnerBox ? innerPullStrength : outerPullStrength;

        if (distanceToCore <= nearCoreBonusDistance)
        {
            float bonusT = Mathf.Clamp01(
                (nearCoreBonusDistance - distanceToCore) /
                Mathf.Max(0.01f, nearCoreBonusDistance - commitRadius)
            );

            bonusT = Mathf.SmoothStep(0f, 1f, bonusT);

            pullStrength += nearCoreBonusStrength * bonusT;
        }

        Vector3 velocityDelta = pullDirection * pullStrength * deltaTime;

        _playerController.AddExternalVelocity(
            velocityDelta,
            maxPullExternalSpeed,
            forceAirborneWhilePulling
        );

        if (useFOV)
        {
            float targetFOV = insideInnerBox ? innerPullFOV : outerPullFOV;
            _playerController.RequestExternalFOV(targetFOV, pullFOVSpeed, 0.12f);
        }

        if (showDebugLogs)
        {
            Color lineColor = insideInnerBox ? Color.red : Color.magenta;
            Debug.DrawLine(_playerTransform.position, launchPoint.position, lineColor, 0.02f);
        }
    }

    private IEnumerator CommitAndLaunchRoutine(
        StarterAssets.FirstPersonController fpc,
        CharacterController controller
    )
    {
        if (fpc == null) yield break;

        if (launchPoint == null || launchTarget == null)
        {
      Debug.LogWarning("️ [GravityCannon] LaunchPoint 或 LaunchTarget 没有设置。");
            yield break;
        }

        _isCommitting = true;

        DashController dashController = fpc.GetComponent<DashController>();
        bool dashWasEnabled = dashController != null && dashController.enabled;

        bool controllerWasEnabled = controller != null && controller.enabled;

        if (dashController != null)
        {
            dashController.enabled = false;
        }

        fpc.BeginCannonControl(true);

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (useFOV)
        {
            fpc.RequestExternalFOV(chargeFOV, chargeFOVSpeed, captureDuration + 0.1f);
        }

        if (chargeSound != null && _audioSource != null)
        {
            _audioSource.pitch = 1f;
            _audioSource.volume = 1f;
            _audioSource.PlayOneShot(chargeSound);
        }

        Vector3 startPosition = fpc.transform.position;
        Quaternion startRotation = fpc.transform.rotation;

        Vector3 targetPosition = launchPoint.position;
        Quaternion targetRotation = startRotation;

        if (rotatePlayerTowardsTarget)
        {
            Vector3 flatDirection = launchTarget.position - launchPoint.position;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, captureDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float rawT = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, rawT);

            if (captureSmoothPower > 0.01f)
            {
                smoothT = Mathf.Pow(smoothT, 1f / captureSmoothPower);
            }

            fpc.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);

            if (rotatePlayerTowardsTarget)
            {
                fpc.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            }

            yield return null;
        }

        fpc.transform.position = launchPoint.position;

        if (rotatePlayerTowardsTarget)
        {
            fpc.transform.rotation = targetRotation;
        }

        if (controller != null)
        {
            controller.enabled = controllerWasEnabled;
        }

        Vector3 launchVelocity = CalculateLaunchVelocity(
            launchPoint.position,
            launchTarget.position,
            flightTime,
            fpc.Gravity
        );

        if (maxLaunchSpeed > 0f && launchVelocity.magnitude > maxLaunchSpeed)
        {
            launchVelocity = launchVelocity.normalized * maxLaunchSpeed;

            if (showDebugLogs)
            {
        Debug.LogWarning("️ [GravityCannon] 发射速度被 Max Launch Speed 限制，可能飞不到目标点。");
            }
        }

        fpc.EndCannonControlAndLaunch(
            launchVelocity,
            launchGraceTime,
            maxHorizontalExternalSpeed,
            noDragDuration,
            actionLockDuration
        );

        if (dashController != null)
        {
            dashController.enabled = dashWasEnabled;
        }

        if (useFOV)
        {
            fpc.RequestExternalFOV(launchFOV, launchFOVSpeed, launchFOVDuration);
        }

        if (launchSound != null && _audioSource != null)
        {
            _audioSource.pitch = 1f;
            _audioSource.volume = 1f;
            _audioSource.PlayOneShot(launchSound);
        }

        _lastLaunchTime = Time.time;

        if (showDebugLogs)
        {
      Debug.Log(" [GravityCannon] Launched player.");
      Debug.Log(" [GravityCannon] Launch Velocity: " + launchVelocity);
        }

        yield return new WaitForSecondsRealtime(reenterCooldown);

        _isCommitting = false;
    }

    private Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float time, float gravity)
    {
        float safeTime = Mathf.Max(0.1f, time);

        Vector3 displacement = target - start;

        Vector3 horizontalDisplacement = new Vector3(
            displacement.x,
            0f,
            displacement.z
        );

        Vector3 horizontalVelocity = horizontalDisplacement / safeTime;

        float verticalVelocity =
            (displacement.y - 0.5f * gravity * safeTime * safeTime) / safeTime;

        return horizontalVelocity + Vector3.up * verticalVelocity;
    }

    private void SetLayerToIgnoreRaycast()
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer < 0)
        {
      Debug.LogWarning("️ [GravityCannon] 找不到 Ignore Raycast Layer。");
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

    private void OnDisable()
    {
        _isCommitting = false;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        BoxCollider box = outerPullBox;

        if (box == null)
        {
            box = GetComponent<BoxCollider>();
        }

        if (box != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;

            Gizmos.color = new Color(0.45f, 0.0f, 1f, 0.12f);
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = new Color(0.9f, 0.25f, 1f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);

            Vector3 innerCenter = box.center + innerBoxLocalOffset;
            Vector3 innerSize = new Vector3(
                box.size.x * Mathf.Abs(innerBoxScale.x),
                box.size.y * Mathf.Abs(innerBoxScale.y),
                box.size.z * Mathf.Abs(innerBoxScale.z)
            );

            Gizmos.color = new Color(1f, 0.0f, 0.7f, 0.18f);
            Gizmos.DrawCube(innerCenter, innerSize);

            Gizmos.color = new Color(1f, 0.0f, 0.7f, 1f);
            Gizmos.DrawWireCube(innerCenter, innerSize);

            Gizmos.matrix = oldMatrix;
        }

        Transform core = launchPoint != null ? launchPoint : transform;

        Gizmos.color = new Color(1f, 0.0f, 1f, 0.28f);
        Gizmos.DrawSphere(core.position, commitRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(core.position, commitRadius);

        if (launchPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(launchPoint.position, 0.25f);
        }

        if (launchTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(launchTarget.position, 0.3f);
        }

        if (launchPoint != null && launchTarget != null)
        {
            Vector3 velocity = CalculatePreviewVelocity();

            Gizmos.color = Color.yellow;

            Vector3 previousPoint = launchPoint.position;

            int steps = Mathf.Max(4, trajectoryPreviewSteps);
            float safeTime = Mathf.Max(0.1f, flightTime);

            for (int i = 1; i <= steps; i++)
            {
                float t = safeTime * i / steps;

                Vector3 point =
                    launchPoint.position +
                    velocity * t +
                    0.5f * Vector3.up * -15f * t * t;

                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }
    }

    private Vector3 CalculatePreviewVelocity()
    {
        if (launchPoint == null || launchTarget == null)
        {
            return Vector3.zero;
        }

        return CalculateLaunchVelocity(
            launchPoint.position,
            launchTarget.position,
            Mathf.Max(0.1f, flightTime),
            -15f
        );
    }
}
