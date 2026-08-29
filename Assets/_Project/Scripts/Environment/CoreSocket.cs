#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
public class CoreSocket : MonoBehaviour
{
    [Header("机关关联")]
    [Tooltip("把场景里控制大门的总控组件拖到这里")]
    public FinalGateManager gateManager;
    [Tooltip("在这个插槽模型下建一个默认隐藏的球体模型，拖到这里。塞入成功后它会显形！")]
    public GameObject visualCoreMesh;

    [Header("互动特效")]
    public GameObject insertEffectPrefab;
    public AudioClip insertSound;

    [Header("3D 悬浮提示文本")]
    [Tooltip("挂在插槽上方或附近的 TextMeshPro (3D Text) 物体")]
    public TMP_Text promptText;
    [Tooltip("当身上有核心且插槽为空时显示的提示")]
    public string insertPrompt = "[E] INSERT REWRITE NODE";
    [Tooltip("当身上没有核心且插槽为空时显示的提示")]
    public string missingCorePrompt = "REWRITE NODE REQUIRED";
    public Color readyColor = new Color(0.1f, 1f, 0.9f, 1f);
    public Color warningColor = new Color(1f, 0.25f, 0.25f, 1f);

    private bool _isFilled = false;
    private bool _playerInside = false;

    void Start()
    {
        // 游戏一开始，插槽里应该是空的，提示也默认隐藏
        if (visualCoreMesh != null) visualCoreMesh.SetActive(false);
        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_isFilled)
        {
            return;
        }

        if (_playerInside)
        {
            UpdatePromptVisual();

            if (AILURONEGameplayActionGate.AllowsGameplayActions &&
                Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryPlugCore();
            }
        }
    }

    private void UpdatePromptVisual()
    {
        if (promptText == null) return;

        bool hasCores = GameManager.Instance != null && GameManager.Instance.GetCurrentBallsCount() > 0;

        promptText.text = hasCores ? insertPrompt : missingCorePrompt;
        promptText.color = hasCores ? readyColor : warningColor;

        // 让 3D 文字始终朝向玩家摄像机
        if (Camera.main != null)
        {
            promptText.transform.rotation = Quaternion.LookRotation(
                promptText.transform.position - Camera.main.transform.position
            );
        }
    }

    private void TryPlugCore()
    {
        if (GameManager.Instance == null) return;

        // 检查玩家手里有没有球
        if (GameManager.Instance.GetCurrentBallsCount() > 0)
        {
            // 成功扣除玩家身上的核心
            if (GameManager.Instance.TrySpendBall())
            {
                _isFilled = true;
                _playerInside = false;

                // 隐藏提示
                if (promptText != null) promptText.gameObject.SetActive(false);

                // 核心显形！
                if (visualCoreMesh != null) visualCoreMesh.SetActive(true);

                // 反馈爆浆
                if (insertEffectPrefab != null) Instantiate(insertEffectPrefab, transform.position, insertEffectPrefab.transform.rotation);
                if (insertSound != null) AudioSource.PlayClipAtPoint(insertSound, transform.position);

                // 汇报给大门总控
                if (gateManager != null) gateManager.OnSocketFilled();

                Debug.Log($" [插槽] {gameObject.name} 成功置入能量核！");
            }
        }
        else
        {
            Debug.Log("️ [插槽] 你身上没有多余的核心！快去地图里寻找！");
        }
    }

    // 利用触发器检测玩家是否站在插槽前
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isFilled)
        {
            _playerInside = true;
            if (promptText != null)
            {
                UpdatePromptVisual();
                promptText.gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInside = false;
            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }
        }
    }
}