#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class RotateRing : MonoBehaviour
{
    [Tooltip("沿 Z 轴自转的速度，数值越小转得越沉稳")]
    public float rotationSpeed = 30f;

    void Update()
    {
        // 每一帧让这个六边形门沿着自己的 Z 轴（朝向玩家的方向）缓慢旋转
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime, Space.Self);
    }
}
