using System.Collections.Generic;
using AILURONE.WorldRewrite;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeleportController))]
public sealed class TeleportArrivalEnemyScan : MonoBehaviour
{
    private sealed class Marker
    {
        public EnemyTarget target;
        public RectTransform root;
        public CanvasGroup group;
        public RectTransform[] edges;
    }

    [Header("References")]
    [SerializeField] private TeleportController teleportController;
    [SerializeField] private TeleportWorldRewriteController rewriteController;

    [Header("Arrival Scan")]
    [SerializeField] private bool highlightNearbyEnemies = false;
    [SerializeField, Min(1f)] private float fallbackScanRadius = 120f;
    [SerializeField, Min(0.1f)] private float highlightDuration = 2.4f;
    [SerializeField, Min(1)] private int maximumHighlightedEnemies = 24;
    [SerializeField, Min(1f)] private float minimumFrameSize = 24f;
    [SerializeField, Min(1f)] private float maximumFrameSize = 108f;
    [SerializeField] private Color scanColor =
        new Color(0.72f, 1f, 1f, 0.94f);

    private readonly List<Marker> _markers = new List<Marker>();
    private readonly List<EnemyTarget> _targets = new List<EnemyTarget>();
    private readonly Dictionary<EnemyTarget, Renderer[]> _rendererCache =
        new Dictionary<EnemyTarget, Renderer[]>();

    private Canvas _canvas;
    private RectTransform _layer;
    private float _remaining;
    private float _elapsed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (teleportController != null)
        {
            teleportController.TeleportCompleted += HandleTeleportCompleted;
        }
    }

    private void OnDisable()
    {
        if (teleportController != null)
        {
            teleportController.TeleportCompleted -= HandleTeleportCompleted;
        }

        HideAllMarkers();
    }

    private void Update()
    {
        if (_remaining <= 0f)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        _remaining -= Time.unscaledDeltaTime;
        UpdateMarkers();

        if (_remaining <= 0f)
        {
            HideAllMarkers();
        }
    }

    private void HandleTeleportCompleted(int slotIndex, Vector3 destination)
    {
        if (!highlightNearbyEnemies || !EnsureLayer())
        {
            return;
        }

        float radius = rewriteController != null
            ? rewriteController.ArrivalCoverageRadius
            : Mathf.Max(1f, fallbackScanRadius);

        CollectNearbyEnemies(destination, radius);
        EnsureMarkerCount(_targets.Count);

        for (int index = 0; index < _markers.Count; index++)
        {
            Marker marker = _markers[index];
            marker.target = index < _targets.Count
                ? _targets[index]
                : null;
            marker.root.gameObject.SetActive(marker.target != null);
        }

        _elapsed = 0f;
        _remaining = Mathf.Max(0.1f, highlightDuration);
        UpdateMarkers();
    }

    private void CollectNearbyEnemies(Vector3 centre, float radius)
    {
        _targets.Clear();
        float radiusSquared = radius * radius;

        EnemyTarget[] all = Object.FindObjectsByType<EnemyTarget>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int index = 0; index < all.Length; index++)
        {
            EnemyTarget enemy = all[index];
            if (enemy == null || enemy.IsDead ||
                !enemy.gameObject.scene.IsValid())
            {
                continue;
            }

            if ((enemy.transform.position - centre).sqrMagnitude <=
                radiusSquared)
            {
                _targets.Add(enemy);
            }
        }

        _targets.Sort((left, right) =>
            ((left.transform.position - centre).sqrMagnitude)
            .CompareTo((right.transform.position - centre).sqrMagnitude));

        int maximum = Mathf.Max(1, maximumHighlightedEnemies);
        if (_targets.Count > maximum)
        {
            _targets.RemoveRange(maximum, _targets.Count - maximum);
        }
    }

    private void UpdateMarkers()
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null || _layer == null)
        {
            HideAllMarkers();
            return;
        }

        float duration = Mathf.Max(0.1f, highlightDuration);
        float normalized = Mathf.Clamp01(_elapsed / duration);
        float fadeIn = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0f, 0.12f, normalized));
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0.70f, 1f, normalized));
        float pulse = 0.92f + Mathf.Sin(_elapsed * 8f) * 0.08f;
        float alpha = fadeIn * fadeOut * pulse;

        for (int index = 0; index < _markers.Count; index++)
        {
            Marker marker = _markers[index];
            EnemyTarget enemy = marker.target;
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
            {
                marker.root.gameObject.SetActive(false);
                continue;
            }

            Bounds bounds = GetEnemyBounds(enemy);
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(bounds.center);
            if (screenPoint.z <= 0f ||
                screenPoint.x < 0f || screenPoint.x > Screen.width ||
                screenPoint.y < 0f || screenPoint.y > Screen.height)
            {
                marker.root.gameObject.SetActive(false);
                continue;
            }

            Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : worldCamera;
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _layer,
                    screenPoint,
                    eventCamera,
                    out localPoint))
            {
                marker.root.gameObject.SetActive(false);
                continue;
            }

            marker.root.anchoredPosition = localPoint;
            UpdateMarkerGeometry(
                marker,
                GetProjectedDiameter(worldCamera, bounds));
            marker.group.alpha = alpha;
            marker.root.gameObject.SetActive(true);
        }
    }

    private Bounds GetEnemyBounds(EnemyTarget enemy)
    {
        Renderer[] renderers;
        if (!_rendererCache.TryGetValue(enemy, out renderers))
        {
            renderers = enemy.GetComponentsInChildren<Renderer>(true);
            _rendererCache[enemy] = renderers;
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds(enemy.transform.position, Vector3.one);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private float GetProjectedDiameter(Camera worldCamera, Bounds bounds)
    {
        Vector3 top = worldCamera.WorldToScreenPoint(
            bounds.center + Vector3.up * bounds.extents.y);
        Vector3 bottom = worldCamera.WorldToScreenPoint(
            bounds.center - Vector3.up * bounds.extents.y);
        float diameter = Mathf.Abs(top.y - bottom.y) * 1.15f;
        return Mathf.Clamp(
            diameter,
            Mathf.Max(1f, minimumFrameSize),
            Mathf.Max(minimumFrameSize, maximumFrameSize));
    }

    private bool EnsureLayer()
    {
        if (_layer != null && _canvas != null)
        {
            return true;
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < canvases.Length; index++)
        {
            if (canvases[index].name == "HUD_Canvas_AILURONE")
            {
                _canvas = canvases[index];
                break;
            }
        }

        if (_canvas == null)
        {
            return false;
        }

        RectTransform canvasRect = _canvas.transform as RectTransform;
        Transform existing = canvasRect.Find("TeleportArrivalEnemyScanLayer");
        if (existing != null)
        {
            _layer = existing as RectTransform;
        }
        else
        {
            GameObject layerObject = new GameObject(
                "TeleportArrivalEnemyScanLayer",
                typeof(RectTransform));
            _layer = layerObject.GetComponent<RectTransform>();
            _layer.SetParent(canvasRect, false);
        }

        _layer.anchorMin = Vector2.zero;
        _layer.anchorMax = Vector2.one;
        _layer.offsetMin = Vector2.zero;
        _layer.offsetMax = Vector2.zero;
        _layer.SetAsLastSibling();
        return true;
    }

    private void EnsureMarkerCount(int count)
    {
        while (_markers.Count < count)
        {
            GameObject rootObject = new GameObject(
                "TeleportEnemyMarker_" + (_markers.Count + 1).ToString("00"),
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(_layer, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform[] edges = new RectTransform[4];
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                GameObject edgeObject = new GameObject(
                    "Edge",
                    typeof(RectTransform),
                    typeof(Image));
                edges[edgeIndex] = edgeObject.GetComponent<RectTransform>();
                edges[edgeIndex].SetParent(root, false);
                Image image = edgeObject.GetComponent<Image>();
                image.color = scanColor;
                image.raycastTarget = false;
            }

            Marker marker = new Marker
            {
                root = root,
                group = group,
                edges = edges
            };
            UpdateMarkerGeometry(marker, minimumFrameSize);
            rootObject.SetActive(false);
            _markers.Add(marker);
        }
    }

    private static void UpdateMarkerGeometry(Marker marker, float diameter)
    {
        float radius = Mathf.Max(10f, diameter * 0.5f);
        float halfRadius = radius * 0.5f;
        float edgeLength = radius * 1.41421356f;
        marker.root.sizeDelta = new Vector2(diameter, diameter);

        SetEdge(marker.edges[0], new Vector2( halfRadius,  halfRadius), edgeLength, -45f);
        SetEdge(marker.edges[1], new Vector2( halfRadius, -halfRadius), edgeLength,  45f);
        SetEdge(marker.edges[2], new Vector2(-halfRadius, -halfRadius), edgeLength, -45f);
        SetEdge(marker.edges[3], new Vector2(-halfRadius,  halfRadius), edgeLength,  45f);
    }

    private static void SetEdge(
        RectTransform edge,
        Vector2 position,
        float length,
        float rotation)
    {
        edge.anchorMin = new Vector2(0.5f, 0.5f);
        edge.anchorMax = new Vector2(0.5f, 0.5f);
        edge.pivot = new Vector2(0.5f, 0.5f);
        edge.anchoredPosition = position;
        edge.sizeDelta = new Vector2(length, 2f);
        edge.localEulerAngles = new Vector3(0f, 0f, rotation);
    }

    private void HideAllMarkers()
    {
        _remaining = 0f;
        for (int index = 0; index < _markers.Count; index++)
        {
            _markers[index].target = null;
            if (_markers[index].root != null)
            {
                _markers[index].root.gameObject.SetActive(false);
            }
        }
    }

    private void ResolveReferences()
    {
        if (teleportController == null)
        {
            teleportController = GetComponent<TeleportController>();
        }

        if (rewriteController == null)
        {
            rewriteController = GetComponent<TeleportWorldRewriteController>();
        }
    }
}
