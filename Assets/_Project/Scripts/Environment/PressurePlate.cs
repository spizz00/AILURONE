#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    [Header("解密设置")]
    public int requiredWeight = 2;
    
    [Header("机关连接")]
    public PuzzleDoor linkedDoor;

    [Header("视觉反馈 (选填)")]
    public Transform plateMesh;
    public float pressDepth = 0.15f;
    public float pressSpeed = 8f;
    public AudioClip activateSound;

    [Header("状态展示 (供测试观察)")]
    [SerializeField] private int _currentWeight = 0; 
    private bool _isActivated = false;

    private Vector3 _originalPos;
    private Vector3 _pressedPos;
    private HashSet<Transform> _objectsOnPlate = new HashSet<Transform>();

    void Start()
    {
        if (plateMesh != null)
        {
            _originalPos = plateMesh.localPosition;
            _pressedPos = _originalPos - new Vector3(0, pressDepth, 0);
        }
    }

    void Update()
    {
        if (plateMesh != null)
        {
            Vector3 targetPos = _currentWeight > 0 ? _pressedPos : _originalPos;
            plateMesh.localPosition = Vector3.Lerp(plateMesh.localPosition, targetPos, Time.deltaTime * pressSpeed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 【核心修复】：双重保险检测！不管是 Tag 还是组件，只要有其中之一就认！
        bool isPlayer = other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null;
        bool isEnemy = other.CompareTag("Enemy") || other.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;

        if (isPlayer || isEnemy)
        {
            Transform uniqueID = other.transform.root;
            if (_objectsOnPlate.Add(uniqueID)) 
            {
                _currentWeight = _objectsOnPlate.Count;
                CheckActivation();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        bool isPlayer = other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null;
        bool isEnemy = other.CompareTag("Enemy") || other.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;

        if (isPlayer || isEnemy)
        {
            Transform uniqueID = other.transform.root;
            if (_objectsOnPlate.Remove(uniqueID)) 
            {
                _currentWeight = _objectsOnPlate.Count;
                CheckActivation();
            }
        }
    }

    private void CheckActivation()
    {
        bool shouldBeActivated = (_currentWeight >= requiredWeight);

        if (shouldBeActivated && !_isActivated)
        {
            _isActivated = true;
            if (activateSound != null) AudioSource.PlayClipAtPoint(activateSound, transform.position);
            if (linkedDoor != null) linkedDoor.OpenDoor();
            
      Debug.Log(" [机关] 踏板达标！门开了！总重量：" + _currentWeight);
        }
        else if (!shouldBeActivated && _isActivated)
        {
            _isActivated = false;
            if (linkedDoor != null) linkedDoor.CloseDoor();
            
      Debug.Log(" [机关] 重量不足！门关上了！当前重量：" + _currentWeight);
        }
    }
}
