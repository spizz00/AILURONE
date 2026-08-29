#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System;

public class FinalGateManager : MonoBehaviour
{
    [Header("大门与传送门组件")]
    public GlitchDoor finalGate; 
    [Tooltip("把挂有 GlitchPortal 脚本的传送门物体拖到这里")]
    public GlitchPortal glitchPortal;

    public int totalSocketsRequired = 3;
    private int _currentFilledSockets = 0;
    private bool _gateReleased = false;

    /// <summary>
    /// Fired whenever a Core is successfully installed.
    /// Arguments: current installed count, required installed count.
    /// </summary>
    public event Action<int, int> SocketProgressChanged;

    /// <summary>
    /// Fired once when all required sockets are filled.
    /// </summary>
    public event Action GateReleased;

    public int CurrentFilledSockets => _currentFilledSockets;
    public int TotalSocketsRequired => totalSocketsRequired;
    public bool IsGateReleased => _gateReleased;

    void Start()
    {
        // 【核心修改】：开局不隐藏传送门了，让它处于第 0 阶段（一条隐形的线或极小状态）
        if (glitchPortal != null)
        {
            glitchPortal.gameObject.SetActive(true);
            glitchPortal.SetStage(0); 
        }

        NotifySocketProgress();
    }

    // 每个插槽成功放入球时调用
    public void OnSocketFilled()
    {
        if (_gateReleased)
        {
            return;
        }

        _currentFilledSockets = Mathf.Clamp(
            _currentFilledSockets + 1,
            0,
            Mathf.Max(1, totalSocketsRequired)
        );

        // 【核心驱动】：每塞入一个核心，传送门就前进一步！
        if (glitchPortal != null)
        {
            glitchPortal.SetStage(_currentFilledSockets);
        }

        NotifySocketProgress();

        if (_currentFilledSockets >= totalSocketsRequired)
        {
            TriggerGateRelease();
        }
    }

    private void TriggerGateRelease()
    {
        if (_gateReleased)
        {
            return;
        }

        _gateReleased = true;

    Debug.Log(" [大门总控] 3个核心全部就位！正在执行现实删除！");

        GateReleased?.Invoke();

        if (finalGate != null)
        {
            finalGate.OpenDoor(); // 大门崩溃消失
        }
    }

    private void NotifySocketProgress()
    {
        SocketProgressChanged?.Invoke(
            _currentFilledSockets,
            Mathf.Max(1, totalSocketsRequired)
        );
    }
}
