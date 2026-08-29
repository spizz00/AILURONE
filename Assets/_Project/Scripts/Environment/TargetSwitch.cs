#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class TargetSwitch : MonoBehaviour
{
    [Header("绑定移动平台")]
    [Tooltip("把你要控制的移动平台拖拽到这里！")]
    public MovingPlatform linkedPlatform;

    [Header("视觉/听觉反馈 (选填)")]
    public AudioClip hitSound;
    public GameObject hitEffect;

    // 当被玩家子弹打中时，PlayerBullet.cs 会调用这个函数
    public void OnHit()
    {
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }

        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        if (linkedPlatform != null)
        {
            linkedPlatform.TogglePlatform(); // 通知平台开关
        }
        
    Debug.Log(" 靶心被击中！已发送信号给移动平台！");
    }
}
