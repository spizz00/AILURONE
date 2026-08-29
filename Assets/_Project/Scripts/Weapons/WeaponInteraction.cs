#pragma warning disable 0618
#pragma warning disable 0414
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponInteraction : MonoBehaviour
{
    [Header("拾取设置")]
    public float pickupRange = 3f;           
    [Tooltip("射线只会检测这个层级的物体（建议设为 Interactable）")]
    public LayerMask weaponLayer;            
    public Transform weaponHolder;           

    [Header("丢掷设置")]
    public float throwForce = 20f;           
    public float throwSpin = 10f;            

    [Header("层级动态切换 (Layer Management)")]
    [Tooltip("拿在手里时的专属渲染层 (防穿模)")]
    public string handsLayerName = "Weapon";
    [Tooltip("丢在地上时的物理交互层 (正常渲染)")]
    public string droppedLayerName = "Interactable"; 

    [Header("状态 (Debug观察用)")]
    [SerializeField] private GameObject equippedWeapon = null; 
    private Rigidbody _equippedRb;

    private Camera _mainCamera;
    private Collider _playerCollider;
    
    private int _handsLayerIndex;
    private int _droppedLayerIndex;

    void Start()
    {
        _mainCamera = Camera.main;
        
        _playerCollider = GetComponent<Collider>();
        if (_playerCollider == null) 
        {
            _playerCollider = GetComponentInChildren<CharacterController>();
        }

        // 缓存层级索引，节省性能
        _handsLayerIndex = LayerMask.NameToLayer(handsLayerName);
        _droppedLayerIndex = LayerMask.NameToLayer(droppedLayerName);
    }

    void Update()
    {
        if (!AILURONEGameplayActionGate.AllowsGameplayActions)
        {
            return;
        }

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteractWithWeapon();
        }

        if (Keyboard.current != null &&
            Keyboard.current.gKey.wasPressedThisFrame &&
            equippedWeapon != null)
        {
            ThrowWeapon(throwForce);
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            equippedWeapon != null)
        {
            PlayerWeapon weaponScript = equippedWeapon.GetComponent<PlayerWeapon>();
            if (weaponScript != null)
            {
                weaponScript.TryShoot(); 
            }
        }

        if (equippedWeapon != null && weaponHolder != null)
        {
            equippedWeapon.transform.localPosition = Vector3.zero;
            equippedWeapon.transform.localRotation = Quaternion.identity;
        }
    }

    public bool HasWeapon()
    {
        return equippedWeapon != null;
    }

    public string GetCurrentWeaponName()
    {
        if (equippedWeapon != null)
            return equippedWeapon.name;
        return "";
    }

    private void TryInteractWithWeapon()
    {
        if (weaponHolder == null) return; 

        Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // 向前方发射射线，只检测 weaponLayer 指定的层级！
        if (Physics.Raycast(ray, out hit, pickupRange, weaponLayer))
        {
            if (equippedWeapon != null)
            {
                ThrowWeapon(5f); 
            }
            PickupTarget(hit);
        }
    }

    private void PickupTarget(RaycastHit hit)
    {
        _equippedRb = hit.collider.attachedRigidbody;
        
        if (_equippedRb != null)
        {
            equippedWeapon = _equippedRb.gameObject;
            _equippedRb.isKinematic = true;
            _equippedRb.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            equippedWeapon = hit.collider.gameObject;
        }

        Collider[] allColliders = equippedWeapon.GetComponentsInChildren<Collider>();
        foreach (Collider col in allColliders)
        {
            col.enabled = false;
        }

        equippedWeapon.transform.SetParent(weaponHolder);
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;

        // 💡 核心修复：拾取时，将武器及所有子部件强行拉入 Weapon 渲染层！
        SetLayerRecursively(equippedWeapon, _handsLayerIndex);
    }

    private void ThrowWeapon(float force)
    {
        if (equippedWeapon == null) return;

        equippedWeapon.transform.SetParent(null);

        Collider[] allColliders = equippedWeapon.GetComponentsInChildren<Collider>();
        foreach (Collider col in allColliders)
        {
            col.enabled = true;
            if (_playerCollider != null)
            {
                Physics.IgnoreCollision(_playerCollider, col, true);
            }
        }

        // 💡 核心修复：丢弃时，将武器强行踢回 Interactable 层，让主摄像机正常渲染它！
        SetLayerRecursively(equippedWeapon, _droppedLayerIndex);

        if (_equippedRb != null)
        {
            _equippedRb.isKinematic = false;
            _equippedRb.interpolation = RigidbodyInterpolation.Interpolate;
            
            Vector3 throwDirection = _mainCamera.transform.forward;
            _equippedRb.AddForce(throwDirection * force, ForceMode.VelocityChange); 
            
            Vector3 randomSpin = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            _equippedRb.AddTorque(randomSpin * throwSpin, ForceMode.VelocityChange);
        }

        equippedWeapon = null;
        _equippedRb = null;
    }

    // ==========================================
    // 💡 递归修改层级工具
    // ==========================================
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null || newLayer == -1) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
