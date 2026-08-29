#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [Header("音效反馈 (选填)")]
    [Tooltip("如果掉进尖刺想播放惨叫或刺穿音效，拖到这里")]
    public AudioClip deathSound;

    private void OnTriggerEnter(Collider other)
    {
        // 只要是玩家掉了进来
        if (other.CompareTag("Player"))
        {
            if (deathSound != null)
            {
                AudioSource.PlayClipAtPoint(deathSound, transform.position);
            }

            // 直接呼叫总控，触发死亡与重启画面
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied();
            }
        }
    }
}
