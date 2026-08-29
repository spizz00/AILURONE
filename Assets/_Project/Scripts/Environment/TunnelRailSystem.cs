#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using StarterAssets; 
using System.Collections;

public class TunnelRailSystem : MonoBehaviour
{
    [Header("📍 轨道定位")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("🌪️ 牵引流体力学")]
    public float initialSpeed = 20f;
    public float acceleration = 50f;
    public float suctionSmoothness = 10f;
    public float exitForce = 40f;

    [Header("✨ 视觉滤镜")]
    public float chromaticDuration = 2f; 

    private bool _isOccupied = false;
    private float _tunnelCooldownTimer = 0f; // 💡 修复2：入口防重复吸附冷却

    void Update()
    {
        // 💡 刷新入口冷却
        if (_tunnelCooldownTimer > 0f)
        {
            _tunnelCooldownTimer -= Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 💡 修复2：如果玩家刚刚用 Dash 撕裂了管道，1.5秒内拒绝二次吸入！
        if (other.CompareTag("Player") && !_isOccupied && _tunnelCooldownTimer <= 0f)
        {
            StartCoroutine(TractorBeamRoutine(other.gameObject));
        }
    }

    private IEnumerator TractorBeamRoutine(GameObject player)
    {
        _isOccupied = true;

        if (DashController.Instance != null)
        {
            DashController.Instance.ConfirmTunnelEntry();
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        FirstPersonController fpc = player.GetComponent<FirstPersonController>();

        if (cc != null) cc.enabled = false;

        if (VisualFeedbackController.Instance != null) 
        {
            VisualFeedbackController.Instance.TriggerTunnelEffect(chromaticDuration);
        }

        Vector3 currentPos = player.transform.position; 
        Vector3 tunnelDir = (endPoint.position - startPoint.position).normalized;
        float currentSpeed = cc != null ? Mathf.Max(cc.velocity.magnitude, initialSpeed) : initialSpeed;

        float totalTunnelLength = Vector3.Distance(startPoint.position, endPoint.position);
        float distanceAlongLine = 0f;

        bool isAbortedByDash = false; 

        while (distanceAlongLine < totalTunnelLength)
        {
            if (DashController.Instance != null && DashController.Instance.isDashing)
            {
                isAbortedByDash = true;
                break; 
            }

            // 💡 修复3：将 unscaledDeltaTime 替换为 deltaTime，让风洞速度向 F 键臣服！
            currentSpeed += acceleration * Time.deltaTime; 

            Vector3 offsetFromStart = currentPos - startPoint.position;
            distanceAlongLine = Vector3.Dot(offsetFromStart, tunnelDir);

            if (distanceAlongLine >= totalTunnelLength)
            {
                break;
            }

            Vector3 pointOnLine = startPoint.position + tunnelDir * distanceAlongLine;
            Vector3 targetPullPoint = pointOnLine + tunnelDir * suctionSmoothness;
            Vector3 moveDir = (targetPullPoint - currentPos).normalized;
            
            // 💡 修复3：移动步长受子弹时间影响
            currentPos += moveDir * currentSpeed * Time.deltaTime; 
            player.transform.position = currentPos;

            if (VisualFeedbackController.Instance != null) 
            {
                VisualFeedbackController.Instance.TriggerTunnelEffect(0.2f);
            }

            yield return null;
        }

        if (isAbortedByDash)
        {
      Debug.Log(" [Tunnel] 玩家使用了 Dash 暴力挣脱了管道牵引！");
            
            // 💡 修复2补充：暴力打断时，强制给管道入口上锁 1.5 秒！FOV 和控制权交给 Dash 自己处理！
            _tunnelCooldownTimer = 1.5f;
        }
        else
        {
            player.transform.position = endPoint.position;
            
            if (cc != null) cc.enabled = true; 
            
            if (fpc != null) fpc.ApplyJumpPadForce(tunnelDir * exitForce);

            if (DashController.Instance != null) 
            {
                DashController.Instance.ReleaseFOV();
            }
        }

        _isOccupied = false;
    }
}
