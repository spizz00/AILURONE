#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("移动设置")]
    public float speed = 5f;
    public Transform targetPoint; 
    public bool isMoving = false;

    private Vector3 _startPos;
    private Vector3 _endPos;
    private Vector3 _currentTarget;
    
    private CharacterController _playerController;

    void Start()
    {
        _startPos = transform.position; 
        if (targetPoint != null)
        {
            _endPos = targetPoint.position; 
            targetPoint.SetParent(null); 
        }
        _currentTarget = _endPos;
    }

    void Update()
    {
        if (!isMoving || targetPoint == null) return;

        Vector3 previousPosition = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, _currentTarget, speed * Time.deltaTime);
        Vector3 movementDelta = transform.position - previousPosition;

        // 如果成功抓取到了玩家，就强制推着他走
        if (_playerController != null && _playerController.enabled)
        {
            _playerController.Move(movementDelta);
        }

        if (Vector3.Distance(transform.position, _currentTarget) < 0.05f)
        {
            _currentTarget = _currentTarget == _endPos ? _startPos : _endPos;
        }
    }

    public void TogglePlatform()
    {
        isMoving = !isMoving; 
    }

    // ==========================================
    // 【终极物理探测】：顺藤摸瓜找玩家
    // ==========================================
    private void OnTriggerEnter(Collider other)
    {
        // 无论碰到的是玩家的头、脚还是武器，都往上找 CharacterController 组件
        CharacterController cc = other.GetComponentInParent<CharacterController>();
        
        // 只要找到了控制器，并且这个物体（或它的根节点）叫 Player
        if (cc != null && (other.CompareTag("Player") || other.transform.root.CompareTag("Player")))
        {
            _playerController = cc;
      Debug.Log(" [平台测试] 完美抓取到玩家控制器！玩家已锁定！");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController cc = other.GetComponentInParent<CharacterController>();
        if (cc != null && cc == _playerController)
        {
            _playerController = null;
      Debug.Log(" [平台测试] 玩家离开了移动平台！解锁！");
        }
    }
}
