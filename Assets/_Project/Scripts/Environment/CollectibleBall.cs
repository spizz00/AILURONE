#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System.Collections; // 必须加上这个，因为要用协程

public class CollectibleBall : MonoBehaviour
{
    [Header("收集反馈 Assets")]
    [Tooltip("如果想在吃球瞬间爆点粒子特效，将粒子预制体拖到这里")]
    public GameObject collectEffectPrefab;
    [Tooltip("如果想在吃球瞬间播放音效，将音频剪辑拖到这里")]
    public AudioClip collectSound;

    [Header("淡出动画设置 (Auto)")]
    [Tooltip("球从吃掉到彻底缩小的动画时间")]
    public float shrinkDuration = 0.2f;

    private Collider _collider;
    private bool _isCollected = false; // 防止多重触发的神仙锁

    void Start()
    {
        _collider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 安全锁：如果是玩家碰到，且还没被收集过
        if (other.CompareTag("Player") && !_isCollected)
        {
            _isCollected = true; // 立刻上锁
            
            // 2. 停掉球的物理，防止连吃，防止挡路
            if (_collider != null) _collider.enabled = false;

            // 3. 在吃球位置生成粒子特效预制体 (爆一下)
            if (collectEffectPrefab != null)
            {
                // 生成在球当前的位置
                Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            }

            // 4. 播放清脆音效
            if (collectSound != null)
            {
                // PlayClipAtPoint 是最简单也是最适合收集品的音效播放方式，不受物体销毁影响
                AudioSource.PlayClipAtPoint(collectSound, transform.position, 1f);
            }

            // 5. 通知 GameManager：球数 + 1
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddBall();
            }

            // 6. 开启淡出协程：优雅缩小，然后销毁
            StartCoroutine(ShrinkAndDestroyRoutine());
        }
    }

    // ==========================================
    // 【协程】：让球平滑地缩到0，然后彻底死掉
    // ==========================================
    private IEnumerator ShrinkAndDestroyRoutine()
    {
        Vector3 originalScale = transform.localScale; // 记录球原本的大小
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            // 随着时间推进，大小朝着 (0, 0, 0) 线性插值
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, elapsed / shrinkDuration);
            elapsed += Time.deltaTime; // 累加时间
            yield return null; // 每一帧等一下，直到时间耗尽
        }

        transform.localScale = Vector3.zero; // 确保彻底变成 0

        // 彻底消灭
        Destroy(gameObject);
    }
}
