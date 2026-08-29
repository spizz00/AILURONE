#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System.Collections;

public class GlitchDoor : MonoBehaviour
{
    [Header("高级撕裂设置")]
    [Tooltip("门撕裂多久后彻底消失？(推荐 1.2 秒)")]
    public float glitchDuration = 1.2f; 
    
    [Header("音效")]
    [Tooltip("系统报错、碎裂声")]
    public AudioClip glitchSound; 

    private Collider _collider;
    private MeshRenderer _mainRenderer;
    private MeshFilter _mainMeshFilter;

    void Start()
    {
        _collider = GetComponent<Collider>();
        _mainRenderer = GetComponent<MeshRenderer>();
        _mainMeshFilter = GetComponent<MeshFilter>();
    }

    public void OpenDoor()
    {
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(DigitalTearRoutine());
        }
    }

    private IEnumerator DigitalTearRoutine()
    {
        if (glitchSound != null) AudioSource.PlayClipAtPoint(glitchSound, transform.position);

        // 1. 瞬间关掉物理碰撞，防止卡玩家，玩家甚至可以顶着故障冲过去！
        if (_collider != null) _collider.enabled = false;

        // 2. 获取大门原始比例
        Vector3 baseScale = transform.localScale;

        // 3. 生成 3 个“数字切片” (克隆大门的外壳)
        GameObject[] slices = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            slices[i] = new GameObject("GlitchSlice_" + i);
            
            // 复制大门的网格模型和材质
            MeshFilter mf = slices[i].AddComponent<MeshFilter>();
            mf.sharedMesh = _mainMeshFilter.sharedMesh;
            
            MeshRenderer mr = slices[i].AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mainRenderer.sharedMaterial;
        }

        // 4. 彻底隐藏原本笨重的大门本体
        if (_mainRenderer != null) _mainRenderer.enabled = false;

        float elapsed = 0f;

        // ==========================================
        // 核心动画：低频断层撕裂 (解决 A、C、D)
        // ==========================================
        while (elapsed < glitchDuration)
        {
            // 【降频】：每 0.05 ~ 0.15 秒才刷新一次状态，制造冰冷的“卡顿感”
            float tickDuration = Random.Range(0.05f, 0.15f); 
            
            for (int i = 0; i < 3; i++)
            {
                // 30% 概率某个切片不显示，制造镂空感
                slices[i].SetActive(Random.value > 0.3f);

                if (slices[i].activeSelf)
                {
                    // 【解决果冻感】：把切片在 Y 轴压得极扁，X 轴拉长，形成“数据横纹”
                    float squashY = Random.Range(0.05f, 0.4f); 
                    float stretchX = Random.Range(1.0f, 1.3f); 
                    slices[i].transform.localScale = new Vector3(baseScale.x * stretchX, baseScale.y * squashY, baseScale.z);

                    // 【解决穿模出戏】：只在门原本的高度范围内上下跳动，并伴随极其微小的水平错位
                    float offsetY = Random.Range(-baseScale.y / 2.2f, baseScale.y / 2.2f);
                    float offsetX = Random.Range(-0.3f, 0.3f);
                    
                    // 应用新的错位坐标
                    slices[i].transform.position = transform.position + (transform.up * offsetY) + (transform.right * offsetX);
                    // 保持原本大门的角度
                    slices[i].transform.rotation = transform.rotation;
                }
            }

            elapsed += tickDuration;
            yield return new WaitForSeconds(tickDuration); 
        }

        // ==========================================
        // 结束：将切片与大门彻底从内存删除
        // ==========================================
        for (int i = 0; i < 3; i++)
        {
            if (slices[i] != null) Destroy(slices[i]);
        }
        
        Destroy(gameObject); // 大门彻底灰飞烟灭
    }
}
