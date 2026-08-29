#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System.Collections; // 引入协程，用来做倒计时

public class PuzzleDoor : MonoBehaviour
{
    [Header("门基础设置")]
    [Tooltip("门开启时需要移动的偏移量 (比如想让门往上升 3 米，就填 X:0 Y:3 Z:0)")]
    public Vector3 openOffset = new Vector3(0, 3f, 0);
    [Tooltip("门开启和关闭的物理速度")]
    public float speed = 5f;

    [Header("🎮 关卡设计：关门逻辑")]
    [Tooltip("如果勾选，门只要被打开一次，就再也不会关上了！(推荐当前谜题使用)")]
    public bool stayOpenPermanently = true; 
    
    [Tooltip("如果上面没勾选，门会在失去重量后，等待几秒才开始关闭？(给玩家冲刺的时间)")]
    public float closeDelay = 3f;

    private Vector3 _closedPos;
    private Vector3 _openPos;
    private Vector3 _targetPos;

    private Coroutine _closeCoroutine; // 记录正在倒计时的关门任务

    void Start()
    {
        _closedPos = transform.position;
        _openPos = _closedPos + openOffset; 
        _targetPos = _closedPos; // 默认关着
    }

    void Update()
    {
        // 每一帧平滑向目标点移动
        if (Vector3.Distance(transform.position, _targetPos) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * speed);
        }
    }

    // 暴露给踏板调用的公开方法
    public void OpenDoor()
    {
        // 如果门正在倒计时准备关，立刻打断关门动作！重新全开！
        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }
        
        _targetPos = _openPos;
    }

    public void CloseDoor()
    {
        // 1. 如果是永久开启模式，直接无视关门指令！
        if (stayOpenPermanently) return;

        // 2. 如果不是永久开启，就启动倒计时关门程序
        if (_closeCoroutine == null)
        {
            _closeCoroutine = StartCoroutine(CloseDoorRoutine());
        }
    }

    // ==========================================
    // 【核心新增】：倒计时关门协程
    // ==========================================
    private IEnumerator CloseDoorRoutine()
    {
        // 挂起等待指定的秒数
        yield return new WaitForSeconds(closeDelay);
        
        // 时间到，无情关门
        _targetPos = _closedPos;
        _closeCoroutine = null;
    }
}
