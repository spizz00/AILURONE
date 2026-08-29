#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    [Header("物理与动画参数")]
    public float moveSpeed = 1.5f;      // 减慢上浮速度，更易阅读
    public float lifetime = 2.5f;       // 延长总寿命到 2.5 秒
    public float fadeDelay = 0.8f;      // 💡 新增：爆出后保持完全清晰的时间
    public float fadeSpeed = 1.0f;      // 减缓褪色速度
    
    public float scatterRadius = 0.5f;

    private TextMeshPro _textMesh; 
    private Color _textColor;
    private Vector3 _moveDirection;
    private float _age = 0f;

    public void Setup(string text, Color baseColor)
    {
        _textMesh = GetComponent<TextMeshPro>();
        if (_textMesh == null) return;

        _textMesh.text = text;
        _textMesh.color = baseColor;
        _textColor = baseColor;

        Vector3 randomScatter = new Vector3(
            Random.Range(-scatterRadius, scatterRadius), 
            Random.Range(0.2f, 0.5f), 
            Random.Range(-scatterRadius, scatterRadius)
        );
        transform.position += randomScatter;
        
        _moveDirection = Vector3.up * moveSpeed;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        _age += Time.deltaTime;

        // 向上飘浮运动 
        _moveDirection = Vector3.Lerp(_moveDirection, Vector3.zero, Time.deltaTime * 2.5f);
        transform.position += _moveDirection * Time.deltaTime;

        // 💡 延迟褪色：超过 fadeDelay 时间后，才开始慢慢变透明
        if (_age > fadeDelay && _textMesh != null)
        {
            _textColor.a -= fadeSpeed * Time.deltaTime;
            _textMesh.color = _textColor;
        }

        // 广告牌技术：永远面朝主摄像机
        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
