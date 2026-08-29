#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class SuperhotBullet : MonoBehaviour
{
    public float speed = 20f;       
    public float lifeTime = 5f;     

    private float _timer;

    // ==========================================
    // 【核心修复】：不用 Start 设置 Destroy，改用计时器隐身
    // ==========================================
    void OnEnable()
    {
        _timer = 0f; // 每次从对象池拿出来激活时，寿命清零
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        // 寿命耗尽，隐身回收到池子里
        _timer += Time.deltaTime;
        if (_timer >= lifeTime)
        {
            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied();
            }
            
            // 打中玩家后隐身回收
            gameObject.SetActive(false);
        }
        else if (other.CompareTag("Environment"))
        {
            // 打中墙壁后隐身回收
            gameObject.SetActive(false);
        }
    }
}
