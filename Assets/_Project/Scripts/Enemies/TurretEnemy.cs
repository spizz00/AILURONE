#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System.Collections.Generic;

public class TurretEnemy : MonoBehaviour
{
    [Header("感知设置 (AI Perception)")]
    public float visionRange = 20f;        
    public float visionAngle = 90f;        
    public float proximityRadius = 3f;
    public LayerMask obstacleMask; 
    
    [Tooltip("多久检测一次视线？0.1秒足以保证反应速度且极大节省性能")]
    public float visionCheckInterval = 0.1f; 

    [Header("战斗设置")]
    public GameObject bulletPrefab;        
    public Transform firePoint;            
    public float fireRate = 1.5f;          
    public float rotationSpeed = 120f;     

    [Header("AI 状态")]
    [SerializeField] private bool _hasSpottedPlayer = false;
    private Vector3 _lastKnownPosition;

    private Transform _player;
    private float _fireTimer;
    private float _visionTimer;
    private bool _currentLineOfSight = false;

    // ==========================================
    // 【核心新增】：敌方子弹对象池
    // ==========================================
    private List<GameObject> _bulletPool = new List<GameObject>();
    private int _poolSize = 15; // 预备15发子弹循环使用

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }

        // 预热子弹池
        if (bulletPrefab != null)
        {
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject bullet = Instantiate(bulletPrefab);
                bullet.SetActive(false);
                _bulletPool.Add(bullet);
            }
        }
    }

    void Update()
    {
        if (_player == null) return;

        // ==========================================
        // 【核心优化】：视觉节流阀，不再每帧发射射线检测
        // ==========================================
        _visionTimer -= Time.deltaTime;
        if (_visionTimer <= 0f)
        {
            _currentLineOfSight = CanSeeOrSensePlayer();
            _visionTimer = visionCheckInterval;
        }

        if (_currentLineOfSight)
        {
            _hasSpottedPlayer = true;
            _lastKnownPosition = _player.position;
            EngagePlayer(true); 
        }
        else if (_hasSpottedPlayer)
        {
            EngagePlayer(false); 
        }
    }

    private bool CanSeeOrSensePlayer()
    {
        Vector3 dirToPlayer = (_player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        bool inRange = false;

        if (distanceToPlayer <= proximityRadius) 
        {
            inRange = true;
        }
        else if (distanceToPlayer <= visionRange)
        {
            float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);
            if (angleToPlayer <= visionAngle / 2f)
            {
                inRange = true;
            }
        }

        if (inRange)
        {
            return CheckLineOfSight();
        }

        return false;
    }

    private bool CheckLineOfSight()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 rayEnd = _player.position + Vector3.up * 0.5f;
        return !Physics.Linecast(rayStart, rayEnd, obstacleMask);
    }

    private void EngagePlayer(bool hasLineOfSight)
    {
        Vector3 targetPos = hasLineOfSight ? _player.position : _lastKnownPosition;
        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0; 
        
        if (dirToTarget.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            float step = rotationSpeed * Time.deltaTime;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, step);
            transform.rotation = Quaternion.Euler(0, newAngle, 0);
        }

        if (hasLineOfSight)
        {
            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                Shoot();
                _fireTimer = fireRate;
            }
        }
        else
        {
            _fireTimer = fireRate; 
        }
    }

    private void Shoot()
    {
        // ==========================================
        // 【核心修改】：从池子里拿子弹，不再 Instantiate
        // ==========================================
        if (firePoint != null)
        {
            GameObject bullet = GetPooledBullet();
            if (bullet != null)
            {
                bullet.transform.position = firePoint.position;
                bullet.transform.rotation = firePoint.rotation;
                bullet.SetActive(true);
            }
        }
    }

    private GameObject GetPooledBullet()
    {
        for (int i = 0; i < _bulletPool.Count; i++)
        {
            if (!_bulletPool[i].activeInHierarchy)
            {
                return _bulletPool[i];
            }
        }
        
        // 如果池子不够用，动态扩容
        GameObject newBullet = Instantiate(bulletPrefab);
        newBullet.SetActive(false);
        _bulletPool.Add(newBullet);
        return newBullet;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
        Gizmos.color = Color.yellow;
        Vector3 leftRayRotation = Quaternion.Euler(0, -visionAngle / 2f, 0) * transform.forward;
        Vector3 rightRayRotation = Quaternion.Euler(0, visionAngle / 2f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, leftRayRotation * visionRange);
        Gizmos.DrawRay(transform.position, rightRayRotation * visionRange);
    }
}
