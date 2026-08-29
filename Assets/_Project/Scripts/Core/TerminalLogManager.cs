#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using TMPro;
using System.Collections;

public class TerminalLogManager : MonoBehaviour
{
    public static TerminalLogManager Instance;

    [Header("核心装配")]
    [Tooltip("把场景里 Canvas 下的 LogContainer 拖到这里")]
    public Transform logContainer;
    [Tooltip("把你在 Project 文件夹里做好的 ActionLog_Prefab 拖到这里！")]
    public GameObject logItemPrefab;

    [Header("🎧 视听通感")]
    [Tooltip("把你准备好的【打字机音效】拖到这里！")]
    public AudioClip typewriterSound;
    private AudioSource _audioSource;

    void Awake() 
    { 
        if(Instance == null) Instance = this; 
    }

    void Start() 
    { 
        _audioSource = gameObject.AddComponent<AudioSource>(); 
        _audioSource.playOnAwake = false;
    }

    // 更新接口：允许传入击杀细节 (subInfoStr)
    public void AddLog(string scoreStr, string actionStr, string subInfoStr)
    {
        if (logContainer == null || logItemPrefab == null) return;

        // 1. 克隆预制体
        GameObject newLog = Instantiate(logItemPrefab, logContainer);
        
        // 2. 把它放到最下面（把旧的往上顶）
        newLog.transform.SetAsLastSibling();

        // 3. 传递数据给预制体内部的组件
        ActionLogItem logItemScript = newLog.GetComponent<ActionLogItem>();
        if (logItemScript != null)
        {
            logItemScript.Initialize(scoreStr, actionStr, subInfoStr);
        }

        // 4. 播放清脆的打字机音效
        if (typewriterSound != null)
        {
            _audioSource.pitch = Random.Range(0.9f, 1.1f);
            _audioSource.PlayOneShot(typewriterSound, 0.7f);
        }
    }
}
