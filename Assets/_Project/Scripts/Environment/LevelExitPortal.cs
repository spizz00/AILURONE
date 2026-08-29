#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;

public class LevelExitPortal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 顺藤摸瓜，确保是玩家本体碰到了传送门
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
      Debug.Log(" [终极传送门] 玩家跨入多维空间，关卡结算！");
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerWin(); // 触发胜利！
            }
        }
    }
}
