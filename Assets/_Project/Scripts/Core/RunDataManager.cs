#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class RunDataManager : MonoBehaviour
{
    public static RunDataManager Instance;

    [Header("全局跨关卡数据 (Global Run Data)")]
    [Tooltip("玩家是否在第一关拿到了高危数据彩蛋，并成功带到了通关？")]
    public bool hasGlitchCore = false; 

    // 如果以后需要，甚至可以在这里记录总分：public float totalScore = 0f;

    void Awake()
    {
        // 经典的单例模式 + 不死之身判定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 💡 核心魔法：跨越场景永不销毁！
      Debug.Log(" [系统] 全局数据中心 (RunDataManager) 已上线。");
        }
        else
        {
            // 如果切到新场景发现已经有一个了，就把自己销毁，保证全宇宙只有一个数据中心
            Destroy(gameObject);
        }
    }

    // 当玩家彻底死透或者选择“重新开始游戏”时调用，清空所有跨关卡优势
    public void ResetRunData()
    {
        hasGlitchCore = false;
    Debug.Log(" [系统] 全局数据已重置。");
    }
}
