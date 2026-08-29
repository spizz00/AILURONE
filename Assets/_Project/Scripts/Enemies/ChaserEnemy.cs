#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.AI; 

public class ChaserEnemy : MonoBehaviour
{
    private NavMeshAgent _agent;
    private Transform _player;

    [Header("🔪 攻击与伤害设置")]
    [Tooltip("丧尸的攻击距离，进入此距离开始输出伤害")]
    public float attackRange = 1.5f; 
    [Tooltip("每次攻击扣除玩家多少完整度？(玩家满血100)")]
    public float attackDamage = 35f;
    [Tooltip("攻击冷却时间（秒）。防止一秒内抓玩家 60 下")]
    public float attackCooldown = 1.2f;

    private float _lastAttackTime = 0f;

    [Header("⚙️ 性能优化")]
    [Tooltip("每隔多少秒重新计算一次寻路路径？(0.2秒是性能与反应的完美平衡点)")]
    public float pathUpdateInterval = 0.2f;
    private float _pathUpdateTimer = 0f;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }
    }

    void Update()
    {
        if (_player != null && _agent != null && _agent.isActiveAndEnabled)
        {
            // ==========================================
            // 【核心优化】：寻路节流阀 (不再每帧计算路径)
            // ==========================================
            _pathUpdateTimer -= Time.deltaTime;
            if (_pathUpdateTimer <= 0f)
            {
                _agent.SetDestination(_player.position);
                _pathUpdateTimer = pathUpdateInterval; // 重置计时器
            }

            // ==========================================
            // 💥 攻击判定与伤害分发
            // ==========================================
            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
            
            // 如果玩家进入了攻击范围，且丧尸的攻击冷却完毕
            if (distanceToPlayer <= attackRange)
            {
                if (Time.time - _lastAttackTime >= attackCooldown)
                {
                    PerformAttack();
                }
            }
        }
    }

    private void PerformAttack()
    {
        _lastAttackTime = Time.time;
        
        // 呼叫玩家身上的生命系统，执行扣血
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.TakeDamage(attackDamage);
      Debug.Log($"️ [追击者] 撕咬了玩家！造成 {attackDamage} 点系统损伤！");
        }
    }
}
