#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class ActionLogItem : MonoBehaviour
{
    [Header("UI 组件绑定")]
    public TextMeshProUGUI scoreText;   // 左侧的黑字：+500
    public TextMeshProUGUI actionText;  // 右上侧的白字：DISPATCHED
    public TextMeshProUGUI subInfoText; // 右下侧的小字：■ CHASER

    [Header("动画生命周期")]
    public float logLifeTime = 1.5f;
    public float fadeOutTime = 0.5f;

    private CanvasGroup _canvasGroup;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f; // 一出生先隐身
    }

    // 接收总管传来的数据并启动生命周期
    public void Initialize(string score, string action, string subInfo)
    {
        if (scoreText != null) scoreText.text = score;
        if (actionText != null) actionText.text = action;
        if (subInfoText != null) subInfoText.text = subInfo;

        StartCoroutine(LifecycleRoutine());
    }

    private IEnumerator LifecycleRoutine()
    {
        // 1. 瞬间出现
        _canvasGroup.alpha = 1f;

        // 2. 悬停展示
        yield return new WaitForSecondsRealtime(logLifeTime);

        // 3. 数据流失 (平滑淡出)
        float elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutTime);
            yield return null;
        }

        // 4. 自我销毁
        Destroy(gameObject);
    }
}
