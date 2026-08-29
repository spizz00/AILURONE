#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using StarterAssets; 

public class JumpPad : MonoBehaviour
{
    [Header("跳板设置 (Apex 手感)")]
    public float upwardForce = 20f;
    public float forwardForce = 35f; 
    
    [Header("音效反馈")]
    public AudioClip bounceSound;

    [Header("管道专用设置 (仅限管道前的跳板使用)")]
    [Tooltip("勾选此项，踩下跳板的瞬间就会触发顶级 AAA 视听反馈！")]
    public bool isTunnelStarter = false;
    public float tunnelFOV = 110f;
    public float fovExpandSpeed = 30f;
    
    private Rigidbody _rb;
    private bool _hasLanded = false; 

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // 落地静止逻辑
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasLanded) return;
        if (collision.transform.root.CompareTag("Player") || collision.gameObject.CompareTag("Player")) return; 

        if (collision.gameObject.CompareTag("Environment") || collision.gameObject.CompareTag("Untagged"))
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true; 
                _hasLanded = true;
            }
        }
    }

    // 弹射玩家逻辑
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            FirstPersonController fpc = other.GetComponent<FirstPersonController>();
            
            if (fpc != null)
            {
                // 1. 施加物理推力
                Vector3 launchVelocity = (other.transform.forward * forwardForce) + (Vector3.up * upwardForce);
                fpc.ApplyJumpPadForce(launchVelocity);
                
                // ==========================================
                // 2. 触发史诗级起跳反馈 (FOV撑爆 + 屏幕过曝 + 镜头震动)
                // ==========================================
                if (isTunnelStarter)
                {
                    // 呼叫冲刺脚本撑爆 FOV，并挂上 1.5 秒的定时炸弹！
                    if (DashController.Instance != null)
                    {
                        DashController.Instance.HoldFOV(tunnelFOV, fovExpandSpeed, 1.5f);
                    }
                    
                    // 呼叫视觉总管，瞬间爆亮屏幕、震动镜头并切入重影
                    if (VisualFeedbackController.Instance != null)
                    {
                        VisualFeedbackController.Instance.TriggerJumpPadFeedback();
                    }
                }
                
                if (bounceSound != null)
                {
                    AudioSource.PlayClipAtPoint(bounceSound, transform.position);
                }
            }
        }
    }
}
