#pragma warning disable 0618
#pragma warning disable 0414
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AILURONE.HUD
{
    /// <summary>
    /// Applies the approved 1920 x 1080 presentation to the already-bound HUD.
    /// It only touches RectTransforms and graphics; gameplay data remains owned
    /// by the existing controllers.
    /// </summary>
    internal static class AILURONEHUDRuntimeStyle
    {
        internal static readonly Color White =
            new Color(0.94f, 0.98f, 1f, 1f);

        internal static readonly Color MutedWhite =
            new Color(0.78f, 0.86f, 0.92f, 0.56f);

        internal static readonly Color Cyan =
            new Color(0.18f, 0.90f, 1f, 1f);

        internal static readonly Color Yellow =
            new Color(1f, 0.80f, 0.12f, 1f);

        internal static readonly Color Red =
            new Color(1f, 0.16f, 0.26f, 1f);

        internal static readonly Color Ink =
            new Color(0.01f, 0.025f, 0.04f, 0.76f);

        private static TMP_FontAsset _gameplayFont;
        private static Texture2D _portraitTexture;
        private static Sprite _portraitSprite;
        private static bool _portraitLoadAttempted;
        private static Texture2D _integrityBackdropTexture;
        private static Sprite _integrityBackdropSprite;
        private static bool _integrityBackdropLoadAttempted;

internal static void ApplyIntegrity(
    RectTransform root,
    TMP_Text identity,
    TMP_Text value,
    TMP_Text label,
    TMP_Text status)
{
    if (root == null)
    {
        return;
    }

    SetRect(
        root,
        Vector2.zero,
        Vector2.zero,
        Vector2.zero,
        new Vector2(0f, -1f),
        new Vector2(621f, 553f));

    root.localScale = Vector3.one;
    root.localEulerAngles = Vector3.zero;

    // 启用手工制作的新1024比例HUD，并保持在动态文字后面。
    RectTransform finalRoot =
        root.Find("AILURONE_IntegrityHUDVisual") as RectTransform;

    if (finalRoot == null)
    {
        finalRoot =
            root.Find("Approved_IntegrityFinal") as RectTransform;
    }

    if (finalRoot != null)
    {
        SetRect(
            finalRoot,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            new Vector2(621f, 621f));

        finalRoot.localScale = Vector3.one;
        finalRoot.localEulerAngles = Vector3.zero;
        finalRoot.gameObject.SetActive(true);
        finalRoot.SetAsFirstSibling();
    }

    if (identity != null)
    {
        identity.gameObject.SetActive(false);
    }

    // 100
    if (value != null)
    {
        value.gameObject.SetActive(true);

        SetTextRect(
            value,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 0.5f),
            new Vector2(335.4f, 167.4f),
            new Vector2(126f, 67f));

        value.font = ResolveVT323(value.font);
        value.enableAutoSizing = false;
        value.fontSize = 118f;
        value.fontStyle = FontStyles.Normal;
        value.fontWeight = FontWeight.Regular;
        value.characterSpacing = 0f;
        value.alignment = TextAlignmentOptions.Left;
        value.color = White;
        value.outlineColor =
            new Color(0.02f, 0.025f, 0.03f, 0.82f);
        value.outlineWidth = 0.08f;

        SetCounterRotation(value, 0f);
        EnsureReadableShadow(value);
    }

    // FRAME INTEGRITY
    if (label != null)
    {
        label.gameObject.SetActive(true);
        label.text = "FRAME INTEGRITY";

        SetTextRect(
            label,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 0.5f),
            new Vector2(335.4f, 108.9f),
            new Vector2(144f, 22f));

        label.font = ResolveVT323(label.font);
        label.enableAutoSizing = false;
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Normal;
        label.fontWeight = FontWeight.Regular;
        label.characterSpacing = 0f;
        label.alignment = TextAlignmentOptions.Left;
        label.color = White;
        label.outlineColor =
            new Color(0.02f, 0.025f, 0.03f, 0.72f);
        label.outlineWidth = 0.05f;

        SetCounterRotation(label, 0f);
        EnsureReadableShadow(label);
    }

    // NOMINAL / DEGRADED / CRITICAL
    if (status != null)
    {
        status.gameObject.SetActive(true);

        SetTextRect(
            status,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 0.5f),
            new Vector2(335.4f, 77f),
            new Vector2(100f, 24f));

        status.font = ResolveVT323(status.font);
        status.enableAutoSizing = false;
        status.fontSize = 27f;
        status.fontStyle = FontStyles.Normal;
        status.fontWeight = FontWeight.Regular;
        status.characterSpacing = 0f;
        status.alignment = TextAlignmentOptions.Left;
        status.outlineColor =
            new Color(0.02f, 0.025f, 0.03f, 0.72f);
        status.outlineWidth = 0.05f;

        SetCounterRotation(status, 0f);
        EnsureReadableShadow(status);
    }

    // 隐藏旧血条和旧状态标记，但保留其引用及生命逻辑。
    RectTransform oldBar =
        root.Find("IntegrityBar") as RectTransform;

    if (oldBar != null)
    {
        oldBar.gameObject.SetActive(false);
    }

    RectTransform oldStatusMark =
        root.Find("StatusMark") as RectTransform;

    if (oldStatusMark != null)
    {
        oldStatusMark.gameObject.SetActive(false);
    }

    SetChildActive(root, "IdentitySignature", false);
    SetChildActive(root, "Approved_ProxyPortrait", false);

    // 禁止旧运行时视觉残留。
    SetChildActive(root, "Approved_ReferencePortrait", false);
    SetChildActive(root, "Approved_PortraitBackdrop", false);
    SetChildActive(root, "Approved_IntegrityPath", false);
    SetChildActive(root, "Approved_PortraitOuterRail", false);
    SetChildActive(root, "Approved_PortraitInnerRail", false);

    SetChildActive(root, "Approved_HealthLead", false);
    SetChildActive(root, "Approved_FrameTop", false);
    SetChildActive(root, "Approved_FrameUpperCut", false);
    SetChildActive(root, "Approved_FrameLowerCut", false);
    SetChildActive(root, "Approved_FrameBase", false);
    SetChildActive(root, "Approved_FrameAccent", false);

    // 100左右两侧的动态装饰加号。
    EnsurePlus(
        root,
        "Approved_FramePlusLeft",
        new Vector2(322.7f, 201.4f));

    EnsurePlus(
        root,
        "Approved_FramePlusRight",
        new Vector2(469.4f, 201.4f));

    // 下方加号已经使用独立图片，不再生成旧版本。
    SetChildActive(root, "Approved_FramePlusLower", false);
}

        internal static void ApplyTimer(
            RectTransform root,
            TMP_Text timer,
            TMP_Text adjustment,
            Image[] outerLines,
            Image[] innerLines,
            Image[] ticks,
            Image topMark)
        {
            if (root == null)
            {
                return;
            }

            SetRect(
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(460f, 130f));

            root.localScale = Vector3.one;
            root.localEulerAngles = Vector3.zero;

            LayoutTimerArc(outerLines, innerLines, ticks);

            AILURONEContinuousArcGraphic mainArc =
                EnsureContinuousArc(
                    root,
                    "Approved_TimerMainArc",
                    new Vector2(0f, -72f),
                    new Vector2(360f, 18f),
                    14f,
                    1.75f,
                    new Color(White.r, White.g, White.b, 0.74f),
                    new Vector2(0.5f, 1f));

            AILURONEContinuousArcGraphic stateArc =
                EnsureContinuousArc(
                    root,
                    "Approved_TimerStateArc",
                    new Vector2(0f, -80f),
                    new Vector2(250f, 13f),
                    9f,
                    1.45f,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.56f),
                    new Vector2(0.5f, 1f));

            mainArc.rectTransform.SetAsFirstSibling();
            stateArc.rectTransform.SetSiblingIndex(1);

            if (timer != null)
            {
                SetTextRect(
                    timer,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -6f),
                    new Vector2(214f, 46f));

                timer.fontSize = 42f;
                timer.characterSpacing = 3f;
                timer.color = White;
                SetCounterRotation(timer, 0f);
                EnsureReadableShadow(timer);
            }

            if (adjustment != null)
            {
                SetTextRect(
                    adjustment,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -92f),
                    new Vector2(150f, 22f));

                adjustment.fontSize = 18f;
                adjustment.characterSpacing = 1.2f;
                SetCounterRotation(adjustment, 0f);
                EnsureReadableShadow(adjustment);
            }

            if (topMark != null)
            {
                SetRect(
                    topMark.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -1f),
                    new Vector2(12f, 2f));

                topMark.color = Yellow;
                topMark.gameObject.SetActive(true);
            }

            SetChildActive(root, "Approved_TimerLeft", false);
            SetChildActive(root, "Approved_TimerRight", false);
        }

        internal static void ApplyScore(
            RectTransform root,
            TMP_Text label,
            TMP_Text value,
            TMP_Text combo,
            Image track,
            Image[] leftSegments,
            Image[] rightSegments)
        {
            if (root == null)
            {
                return;
            }

            SetRect(
                root,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 38f),
                new Vector2(310f, 92f));

            root.localScale = Vector3.one;
            root.localEulerAngles = Vector3.zero;

            if (label != null)
            {
                SetHeadingFont(label);
                SetTextRect(
                    label,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 72f),
                    new Vector2(150f, 17f));

                label.fontSize = 12f;
                label.characterSpacing = 1.7f;
                SetCounterRotation(label, 0f);
                EnsureReadableShadow(label);
            }

            if (value != null)
            {
                SetTextRect(
                    value,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 24f),
                    new Vector2(224f, 56f));

                value.fontSize = 53f;
                value.characterSpacing = 0.6f;
                SetCounterRotation(value, 0f);
                EnsureReadableShadow(value);
            }

            if (combo != null)
            {
                SetTextRect(
                    combo,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(111f, 66f),
                    new Vector2(78f, 20f));

                combo.fontSize = 15.5f;
                SetCounterRotation(combo, 0f);
                EnsureReadableShadow(combo);
            }

            if (track != null)
            {
                SetRect(
                    track.rectTransform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 7f),
                    new Vector2(24f, 1.75f));

                track.enabled = false;
            }

            LayoutScoreArc(leftSegments, true);
            LayoutScoreArc(rightSegments, false);

            AILURONEContinuousArcGraphic idleArc =
                EnsureContinuousArc(
                    root,
                    "Approved_ScoreIdleArc",
                    new Vector2(0f, 8f),
                    new Vector2(196f, 10f),
                    6f,
                    1.75f,
                    new Color(White.r, White.g, White.b, 0.20f),
                    new Vector2(0.5f, 0f));

            idleArc.rectTransform.SetAsFirstSibling();

            EnsureLine(
                root,
                "Approved_ScoreNotch",
                Yellow,
                new Vector2(-102f, 10f),
                new Vector2(13f, 2.25f),
                0f,
                new Vector2(0.5f, 0f));
        }

        internal static void ApplyObjective(
            RectTransform root,
            TMP_Text header,
            TMP_Text objective,
            TMP_Text progress,
            TMP_Text carry,
            RectTransform[] slots,
            RectTransform gradientRoot,
            Image[] gradientSegments)
        {
            if (root == null)
            {
                return;
            }

            SetRect(
                root,
                Vector2.one,
                Vector2.one,
                Vector2.one,
                new Vector2(-48f, -118f),
                new Vector2(360f, 104f));

            root.localScale = Vector3.one;
            root.localEulerAngles = new Vector3(0f, 0f, -0.35f);

            Image panel = EnsureImage(root, "Approved_ObjectivePanel");
            SetRect(
                panel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(10f, -20f),
                new Vector2(340f, 48f));
            panel.color = new Color(0.018f, 0.022f, 0.024f, 0.78f);
            panel.raycastTarget = false;
            panel.rectTransform.SetAsFirstSibling();

            if (header != null)
            {
                SetHeadingFont(header);
                SetTextRect(
                    header,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(14f, -1f),
                    new Vector2(190f, 16f));

                header.fontSize = 12f;
                header.characterSpacing = 1.6f;
                header.color = Yellow;
                SetCounterRotation(header, 1.25f);
                EnsureReadableShadow(header);
            }

            if (objective != null)
            {
                SetHeadingFont(objective);
                SetTextRect(
                    objective,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(42f, -31f),
                    new Vector2(250f, 25f));

                objective.fontSize = 20f;
                objective.characterSpacing = 0.9f;
                objective.color = White;
                SetCounterRotation(objective, 1.25f);
                EnsureReadableShadow(objective);
            }

            if (progress != null)
            {
                SetTextRect(
                    progress,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-14f, -73f),
                    new Vector2(220f, 20f));

                progress.fontSize = 14f;
                progress.characterSpacing = 0.8f;
                progress.color = White;
                progress.alignment = TextAlignmentOptions.MidlineRight;
                SetCounterRotation(progress, 1.25f);
                EnsureReadableShadow(progress);
            }

            if (carry != null)
            {
                SetTextRect(
                    carry,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(14f, 3f),
                    new Vector2(150f, 16f));

                carry.fontSize = 10f;
                SetCounterRotation(carry, 1.25f);
                EnsureReadableShadow(carry);
            }

            if (slots != null)
            {
                for (int index = 0; index < slots.Length; index++)
                {
                    RectTransform slot = slots[index];

                    if (slot == null)
                    {
                        continue;
                    }

                    slot.gameObject.SetActive(false);
                }
            }

            if (gradientRoot != null)
            {
                SetRect(
                    gradientRoot,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0.5f),
                    new Vector2(14f, 24f),
                    new Vector2(332f, 1.5f));
            }

            if (gradientSegments != null)
            {
                for (int index = 0;
                    index < gradientSegments.Length;
                    index++)
                {
                    Image segment = gradientSegments[index];

                    if (segment == null)
                    {
                        continue;
                    }

                    float t = gradientSegments.Length > 1
                        ? index / (float)(gradientSegments.Length - 1)
                        : 0f;

                    segment.color = new Color(
                        White.r,
                        White.g,
                        White.b,
                        Mathf.Lerp(0.72f, 0.10f, t));

                    const float baselineWidth = 332f;
                    float segmentWidth =
                        baselineWidth / gradientSegments.Length;

                    segment.rectTransform.anchoredPosition =
                        new Vector2(index * segmentWidth, 0f);

                    segment.rectTransform.sizeDelta =
                        new Vector2(segmentWidth + 0.25f, 1.5f);
                }
            }

            SetChildActive(root, "Approved_ObjectiveSpine", false);
            SetChildActive(root, "Approved_ObjectiveElbow", false);

            EnsureLine(root, "Approved_ObjectiveLeft", White,
                new Vector2(10f, -44f), new Vector2(2f, 48f), 0f,
                new Vector2(0f, 1f));
            EnsureLine(root, "Approved_ObjectiveLeftTop", White,
                new Vector2(16f, -20f), new Vector2(12f, 2f), 0f,
                new Vector2(0f, 1f));
            EnsureLine(root, "Approved_ObjectiveLeftBottom", White,
                new Vector2(16f, -68f), new Vector2(12f, 2f), 0f,
                new Vector2(0f, 1f));
            EnsureLine(root, "Approved_ObjectiveRight", White,
                new Vector2(350f, -44f), new Vector2(2f, 48f), 0f,
                new Vector2(0f, 1f));
            EnsureLine(root, "Approved_ObjectiveRightTop", White,
                new Vector2(344f, -20f), new Vector2(12f, 2f), 0f,
                new Vector2(0f, 1f));
            EnsureLine(root, "Approved_ObjectiveRightBottom", White,
                new Vector2(344f, -68f), new Vector2(12f, 2f), 0f,
                new Vector2(0f, 1f));

            EnsureLine(root, "Approved_ObjectiveDot", Yellow,
                new Vector2(28f, -43f), new Vector2(8f, 8f), 0f,
                new Vector2(0f, 1f));
        }

        internal static TMP_FontAsset ResolveVT323(
            TMP_FontAsset fallback)
        {
            return ResolveGameplayFont(fallback);
        }

        internal static TMP_FontAsset ResolveHeadingFont(
            TMP_FontAsset fallback)
        {
            return ResolveGameplayFont(fallback);
        }

        private static TMP_FontAsset ResolveGameplayFont(
            TMP_FontAsset fallback)
        {
            if (fallback != null &&
                fallback.name.IndexOf(
                    "SpaceGrotesk",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _gameplayFont = fallback;
                return _gameplayFont;
            }

            if (_gameplayFont != null)
            {
                return _gameplayFont;
            }

            TMP_FontAsset[] fonts =
                Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

            for (int index = 0; index < fonts.Length; index++)
            {
                TMP_FontAsset font = fonts[index];

                if (font != null &&
                    font.name.IndexOf(
                        "SpaceGrotesk",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _gameplayFont = font;
                    return _gameplayFont;
                }
            }

            return fallback != null
                ? fallback
                : TMP_Settings.defaultFontAsset;
        }

        private static void EnsureReferencePortrait(RectTransform root)
        {
            Sprite portraitSprite = ResolvePortraitSprite();

            RectTransform portraitRoot = EnsureRect(
                root,
                "Approved_ReferencePortrait",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(621f, 621f));

            DisablePortraitMask(portraitRoot);
            portraitRoot.SetAsFirstSibling();

            RectTransform viewport = EnsureMaskedRect(
                portraitRoot,
                "Approved_PortraitViewport",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            Image shadow = EnsureImage(
                viewport,
                "Approved_PortraitShadow");

            ConfigurePortraitImage(
                shadow,
                portraitSprite,
                new Vector2(621f, 621f),
                new Vector2(-137f, -3f),
                0.12f);

            shadow.color =
                new Color(0.003f, 0.008f, 0.012f, 0.16f);
            shadow.rectTransform.SetAsFirstSibling();

            Image main = EnsureImage(
                viewport,
                "Approved_PortraitMain");

            ConfigurePortraitImage(
                main,
                portraitSprite,
                new Vector2(621f, 621f),
                new Vector2(-140f, 0f),
                1f);

            main.rectTransform.SetAsLastSibling();

            ConfigurePortraitBand(
                portraitRoot,
                "Approved_PortraitSliceUpperBand",
                "Approved_PortraitSliceUpper",
                portraitSprite,
                360f,
                14f);

            ConfigurePortraitBand(
                portraitRoot,
                "Approved_PortraitSliceLowerBand",
                "Approved_PortraitSliceLower",
                portraitSprite,
                285f,
                12f);

            portraitRoot.gameObject.SetActive(portraitSprite != null);
        }

        private static void EnsurePortraitBackdrop(RectTransform root)
        {
            Sprite backdropSprite = ResolveIntegrityBackdropSprite();

            RectTransform backdrop = EnsureRect(
                root,
                "Approved_PortraitBackdrop",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(621f, 621f));

            if (backdrop.GetComponent<CanvasRenderer>() == null)
            {
                backdrop.gameObject.AddComponent<CanvasRenderer>();
            }

            AILURONEPortraitMaskGraphic graphic =
                backdrop.GetComponent<AILURONEPortraitMaskGraphic>();

            if (graphic == null)
            {
                graphic = backdrop.gameObject
                    .AddComponent<AILURONEPortraitMaskGraphic>();
            }

            graphic.enabled = backdropSprite == null;

            if (graphic.enabled)
            {
                graphic.Configure(
                    new Color(0.094f, 0.102f, 0.106f, 1f),
                    new Color(0.106f, 0.110f, 0.118f, 1f));
                graphic.color = Color.white;
                graphic.raycastTarget = false;
            }

            Image backdropImage = EnsureImage(
                backdrop,
                "Approved_ReferenceBackdropImage");

            ConfigurePortraitImage(
                backdropImage,
                backdropSprite,
                new Vector2(621f, 621f),
                Vector2.zero,
                1f);

            backdropImage.rectTransform.SetAsFirstSibling();

            Image leftBleed = EnsureImage(
                backdrop,
                "Approved_BackdropLeftBleed");

            SetRect(
                leftBleed.rectTransform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(-12f, -10f),
                new Vector2(16f, 240f));

            leftBleed.sprite = null;
            leftBleed.color = new Color32(25, 27, 30, 255);
            leftBleed.raycastTarget = false;
            leftBleed.rectTransform.SetAsFirstSibling();

            Image bottomBleed = EnsureImage(
                backdrop,
                "Approved_BackdropBottomBleed");

            SetRect(
                bottomBleed.rectTransform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(-12f, -10f),
                new Vector2(455f, 14f));

            bottomBleed.sprite = null;
            bottomBleed.color = new Color32(25, 27, 30, 255);
            bottomBleed.raycastTarget = false;
            bottomBleed.rectTransform.SetAsFirstSibling();

            backdrop.SetAsFirstSibling();
        }

        private static void EnsureIntegrityPath(RectTransform root)
        {
            RectTransform pathRoot = EnsureRect(
                root,
                "Approved_IntegrityPath",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);

            if (pathRoot.GetComponent<CanvasRenderer>() == null)
            {
                pathRoot.gameObject.AddComponent<CanvasRenderer>();
            }

            AILURONEIntegrityPathGraphic graphic =
                pathRoot.GetComponent<AILURONEIntegrityPathGraphic>();

            if (graphic == null)
            {
                graphic = pathRoot.gameObject
                    .AddComponent<AILURONEIntegrityPathGraphic>();
            }

            graphic.Configure(
                new[]
                {
                    new Vector2(16f, 227f),
                    new Vector2(78f, 227f),
                    new Vector2(205f, 114f),
                    new Vector2(304f, 114f),
                    new Vector2(435f, 1f)
                },
                new[]
                {
                    new Vector2(91f, 210f),
                    new Vector2(181f, 128f)
                },
                7.2f,
                4.8f,
                1.05f,
                new Color(White.r, White.g, White.b, 0.20f));

            graphic.raycastTarget = false;
            graphic.enabled = true;
            pathRoot.SetAsLastSibling();
        }

        private static Sprite ResolvePortraitSprite()
        {
            if (_portraitSprite != null)
            {
                return _portraitSprite;
            }

            if (_portraitLoadAttempted)
            {
                return null;
            }

            _portraitLoadAttempted = true;
            _portraitSprite = Resources.Load<Sprite>(
                "AILURONE/HUD/Portraits/AILURONE_PlayerPortrait_Reference_v4");

            if (_portraitSprite != null)
            {
                return _portraitSprite;
            }

            _portraitTexture = Resources.Load<Texture2D>(
                "AILURONE/HUD/Portraits/AILURONE_PlayerPortrait_Reference_v4");

            if (_portraitTexture == null)
            {
                return null;
            }

            _portraitSprite = Sprite.Create(
                _portraitTexture,
                new Rect(
                    0f,
                    0f,
                    _portraitTexture.width,
                    _portraitTexture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);

            _portraitSprite.name =
                "AILURONE Player Portrait Runtime Sprite";

            return _portraitSprite;
        }

        private static Sprite ResolveIntegrityBackdropSprite()
        {
            if (_integrityBackdropSprite != null)
            {
                return _integrityBackdropSprite;
            }

            if (_integrityBackdropLoadAttempted)
            {
                return null;
            }

            _integrityBackdropLoadAttempted = true;
            _integrityBackdropTexture = Resources.Load<Texture2D>(
                "AILURONE/HUD/Portraits/AILURONE_IntegrityBackdrop_Reference_v3");

            if (_integrityBackdropTexture == null)
            {
                return null;
            }

            _integrityBackdropSprite = Sprite.Create(
                _integrityBackdropTexture,
                new Rect(
                    0f,
                    0f,
                    _integrityBackdropTexture.width,
                    _integrityBackdropTexture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);

            _integrityBackdropSprite.name =
                "AILURONE Integrity Backdrop Runtime Sprite";

            return _integrityBackdropSprite;
        }

        private static void ConfigurePortraitBand(
            RectTransform portraitRoot,
            string bandName,
            string imageName,
            Sprite sprite,
            float y,
            float height)
        {
            RectTransform band = EnsureMaskedRect(
                portraitRoot,
                bandName,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, y),
                new Vector2(400f, height));

            Image image = EnsureImage(band, imageName);
            float bandCentreY = y + height * 0.5f;

            ConfigurePortraitImage(
                image,
                sprite,
                new Vector2(621f, 621f),
                new Vector2(0f, 310.5f - bandCentreY),
                0f);
        }

        private static void ConfigurePortraitImage(
            Image image,
            Sprite sprite,
            Vector2 size,
            Vector2 position,
            float alpha)
        {
            if (image == null)
            {
                return;
            }

            SetRect(
                image.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size);

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.enabled = sprite != null;
        }

        private static void LayoutTimerArc(
            Image[] outerLines,
            Image[] innerLines,
            Image[] ticks)
        {
            SetImagesActive(outerLines, false);
            SetImagesActive(innerLines, false);

            if (ticks == null)
            {
                return;
            }

            for (int index = 0; index < ticks.Length; index++)
            {
                Image tick = ticks[index];

                if (tick == null)
                {
                    continue;
                }

                float centre = (ticks.Length - 1) * 0.5f;
                float distance = Mathf.Abs(index - centre);
                Vector2 size = tick.rectTransform.sizeDelta;

                SetRect(
                    tick.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((index - centre) * 8f, -59f - distance),
                    new Vector2(2f, Mathf.Max(4f, size.y)));

                tick.rectTransform.localEulerAngles = Vector3.zero;
                tick.gameObject.SetActive(true);
            }
        }

        private static void LayoutLines(
            Image[] images,
            Vector2[] positions,
            Vector2[] sizes,
            float[] rotations)
        {
            if (images == null)
            {
                return;
            }

            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];

                if (image == null)
                {
                    continue;
                }

                bool hasLayout =
                    index < positions.Length &&
                    index < sizes.Length &&
                    index < rotations.Length;

                image.gameObject.SetActive(hasLayout);

                if (!hasLayout)
                {
                    continue;
                }

                SetRect(
                    image.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    positions[index],
                    sizes[index]);

                image.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, rotations[index]);
            }
        }

        private static void LayoutScoreArc(
            Image[] images,
            bool left)
        {
            if (images == null)
            {
                return;
            }

            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];

                if (image == null)
                {
                    continue;
                }

                float t = (index + 0.5f) /
                    Mathf.Max(1f, images.Length);

                float distance = 7f + index * 11.5f;
                float y = (1f - t * t) * 6f;
                float rotation = Mathf.Lerp(0.8f, 6f, t);

                SetRect(
                    image.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(left ? -distance : distance, y),
                    new Vector2(10f, 2.35f));

                image.rectTransform.localEulerAngles =
                    new Vector3(
                        0f,
                        0f,
                        left ? rotation : -rotation);

                image.gameObject.SetActive(true);
            }
        }

        private static void SetHeadingFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset headingFont =
                ResolveHeadingFont(text.font);

            if (headingFont != null)
            {
                text.font = headingFont;
            }

            text.fontStyle = FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static RectTransform EnsureRect(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect =
                parent.Find(name) as RectTransform;

            if (rect == null)
            {
                rect = CreateRect(
                    name,
                    parent,
                    anchorMin,
                    anchorMax,
                    pivot,
                    position,
                    size);
            }
            else
            {
                SetRect(
                    rect,
                    anchorMin,
                    anchorMax,
                    pivot,
                    position,
                    size);
            }

            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
            rect.gameObject.SetActive(true);
            return rect;
        }

        private static RectTransform EnsureMaskedRect(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect = EnsureRect(
                parent,
                name,
                anchorMin,
                anchorMax,
                pivot,
                position,
                size);

            RectMask2D mask = rect.GetComponent<RectMask2D>();

            if (mask == null)
            {
                mask = rect.gameObject.AddComponent<RectMask2D>();
            }

            mask.padding = Vector4.zero;
            return rect;
        }

        private static Image EnsureImage(
            RectTransform parent,
            string name)
        {
            Transform existing = parent.Find(name);
            Image image = existing != null
                ? existing.GetComponent<Image>()
                : null;

            if (image == null)
            {
                GameObject gameObject = existing != null
                    ? existing.gameObject
                    : new GameObject(
                        name,
                        typeof(RectTransform),
                        typeof(CanvasRenderer));

                if (existing == null)
                {
                    gameObject.transform.SetParent(parent, false);
                }

                image = gameObject.AddComponent<Image>();
            }

            image.raycastTarget = false;
            image.gameObject.SetActive(true);
            return image;
        }

        private static void DisablePortraitMask(RectTransform portraitRoot)
        {
            AILURONEPortraitMaskGraphic graphic =
                portraitRoot.GetComponent<AILURONEPortraitMaskGraphic>();

            if (graphic != null)
            {
                graphic.enabled = false;
            }

            Mask mask = portraitRoot.GetComponent<Mask>();

            if (mask != null)
            {
                mask.enabled = false;
            }
        }

        private static AILURONEAntiAliasedPolylineGraphic
            EnsureAntiAliasedPolyline(
                RectTransform parent,
                string name,
                Color color,
                Vector2[] points,
                float thickness,
                float feather)
        {
            RectTransform rect = EnsureRect(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);

            if (rect.GetComponent<CanvasRenderer>() == null)
            {
                rect.gameObject.AddComponent<CanvasRenderer>();
            }

            AILURONEAntiAliasedPolylineGraphic graphic =
                rect.GetComponent<AILURONEAntiAliasedPolylineGraphic>();

            if (graphic == null)
            {
                graphic = rect.gameObject
                    .AddComponent<AILURONEAntiAliasedPolylineGraphic>();
            }

            graphic.raycastTarget = false;
            graphic.color = color;
            graphic.Configure(points, thickness, feather);
            graphic.enabled = true;
            rect.SetAsLastSibling();
            return graphic;
        }

        private static AILURONEContinuousArcGraphic EnsureContinuousArc(
            RectTransform parent,
            string name,
            Vector2 position,
            Vector2 size,
            float rise,
            float thickness,
            Color color,
            Vector2 anchor)
        {
            RectTransform rect = EnsureRect(
                parent,
                name,
                anchor,
                anchor,
                new Vector2(0.5f, 0.5f),
                position,
                size);

            if (rect.GetComponent<CanvasRenderer>() == null)
            {
                rect.gameObject.AddComponent<CanvasRenderer>();
            }

            AILURONEContinuousArcGraphic graphic =
                rect.GetComponent<AILURONEContinuousArcGraphic>();

            if (graphic == null)
            {
                graphic = rect.gameObject
                    .AddComponent<AILURONEContinuousArcGraphic>();
            }

            graphic.raycastTarget = false;
            graphic.color = color;
            graphic.Configure(rise, thickness, 64);
            graphic.enabled = true;
            return graphic;
        }

        private static void SetImagesActive(
            Image[] images,
            bool active)
        {
            if (images == null)
            {
                return;
            }

            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];

                if (image != null)
                {
                    image.gameObject.SetActive(active);
                }
            }
        }

        private static void EnsureReadableShadow(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = text.GetComponent<Shadow>();

            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor =
                new Color(0.005f, 0.02f, 0.035f, 0.88f);

            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
            shadow.enabled = true;
        }

        private static void SetCounterRotation(
            TMP_Text text,
            float rotation)
        {
            if (text != null)
            {
                text.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, rotation);
            }
        }

        private static void SetChildActive(
            RectTransform parent,
            string childName,
            bool active)
        {
            if (parent == null)
            {
                return;
            }

            Transform child = parent.Find(childName);

            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void EnsurePlus(
            RectTransform parent,
            string name,
            Vector2 position)
        {
            RectTransform root = EnsureRect(
                parent,
                name,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f),
                position,
                new Vector2(10f, 10f));

            EnsureLine(
                root,
                "Horizontal",
                new Color(White.r, White.g, White.b, 0.78f),
                Vector2.zero,
                new Vector2(10f, 1.5f),
                0f,
                new Vector2(0.5f, 0.5f));

            EnsureLine(
                root,
                "Vertical",
                new Color(White.r, White.g, White.b, 0.78f),
                Vector2.zero,
                new Vector2(1.5f, 10f),
                0f,
                new Vector2(0.5f, 0.5f));
        }

        private static void TintChildImages(
            RectTransform root,
            Color color)
        {
            Image[] images =
                root.GetComponentsInChildren<Image>(true);

            for (int index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                Color result = color;
                result.a = image.color.a;
                image.color = result;
            }
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject =
                new GameObject(name, typeof(RectTransform));

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            SetRect(
                rect,
                anchorMin,
                anchorMax,
                pivot,
                anchoredPosition,
                sizeDelta);

            return rect;
        }

        private static Image EnsureLine(
            RectTransform parent,
            string name,
            Color color,
            Vector2 position,
            Vector2 size,
            float rotation,
            Vector2? anchor = null)
        {
            Transform existing = parent.Find(name);
            Image image;

            if (existing != null)
            {
                image = existing.GetComponent<Image>();
            }
            else
            {
                GameObject gameObject = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

                gameObject.transform.SetParent(parent, false);
                image = gameObject.GetComponent<Image>();
                image.raycastTarget = false;
            }

            Vector2 resolvedAnchor = anchor ?? new Vector2(0f, 0.5f);

            SetRect(
                image.rectTransform,
                resolvedAnchor,
                resolvedAnchor,
                new Vector2(0.5f, 0.5f),
                position,
                size);

            image.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, rotation);

            image.color = color;
            image.gameObject.SetActive(true);
            return image;
        }

        private static void SetTextRect(
            TMP_Text text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            SetRect(
                text.rectTransform,
                anchorMin,
                anchorMax,
                pivot,
                position,
                size);

            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AILURONEIntegrityPathGraphic : MaskableGraphic
    {
        private const float SecondaryPathStartRatio = 0.165f;
        private const float SecondaryPathEndRatio = 0.406f;

        private Vector2[] _points = new Vector2[0];
        private Vector2[] _secondaryPoints = new Vector2[0];
        private float _thickness = 5f;
        private float _secondaryThickness = 4f;
        private float _feather = 0.75f;
        private Color _trackColor = Color.clear;
        private Color _fillColor = Color.white;
        private Color _ghostColor = Color.red;
        private float _currentRatio = 1f;
        private float _ghostRatio = 1f;

        internal void Configure(
            Vector2[] points,
            Vector2[] secondaryPoints,
            float thickness,
            float secondaryThickness,
            float feather,
            Color trackColor)
        {
            _points = points != null
                ? (Vector2[])points.Clone()
                : new Vector2[0];

            _secondaryPoints = secondaryPoints != null
                ? (Vector2[])secondaryPoints.Clone()
                : new Vector2[0];

            _thickness = Mathf.Max(1f, thickness);
            _secondaryThickness = Mathf.Max(1f, secondaryThickness);
            _feather = Mathf.Max(0.25f, feather);
            _trackColor = trackColor;
            SetVerticesDirty();
        }

        internal void SetVisualState(
            float currentRatio,
            float ghostRatio,
            Color fillColor,
            Color ghostColor)
        {
            _currentRatio = Mathf.Clamp01(currentRatio);
            _ghostRatio = Mathf.Clamp01(
                Mathf.Max(_currentRatio, ghostRatio));
            _fillColor = fillColor;
            _ghostColor = ghostColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (_points == null || _points.Length < 2)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            Vector2 origin = new Vector2(rect.xMin, rect.yMin);

            DrawRange(
                vertexHelper,
                origin,
                _points,
                0f,
                1f,
                _trackColor,
                _thickness);

            DrawRange(
                vertexHelper,
                origin,
                _points,
                _currentRatio,
                _ghostRatio,
                _ghostColor,
                _thickness + 1.4f);

            DrawRange(
                vertexHelper,
                origin,
                _points,
                0f,
                _currentRatio,
                _fillColor,
                _thickness);

            DrawRange(
                vertexHelper,
                origin,
                _secondaryPoints,
                0f,
                1f,
                _trackColor,
                _secondaryThickness);

            float secondaryCurrentRatio = Mathf.InverseLerp(
                SecondaryPathStartRatio,
                SecondaryPathEndRatio,
                _currentRatio);

            float secondaryGhostRatio = Mathf.InverseLerp(
                SecondaryPathStartRatio,
                SecondaryPathEndRatio,
                _ghostRatio);

            DrawRange(
                vertexHelper,
                origin,
                _secondaryPoints,
                secondaryCurrentRatio,
                secondaryGhostRatio,
                _ghostColor,
                _secondaryThickness + 1f);

            DrawRange(
                vertexHelper,
                origin,
                _secondaryPoints,
                0f,
                secondaryCurrentRatio,
                _fillColor,
                _secondaryThickness);
        }

        private void DrawRange(
            VertexHelper vertexHelper,
            Vector2 origin,
            Vector2[] points,
            float startRatio,
            float endRatio,
            Color lineColor,
            float thickness)
        {
            if (lineColor.a <= 0.001f || points == null || points.Length < 2)
            {
                return;
            }

            float start = Mathf.Clamp01(
                Mathf.Min(startRatio, endRatio));
            float end = Mathf.Clamp01(
                Mathf.Max(startRatio, endRatio));

            if (end - start <= 0.0001f)
            {
                return;
            }

            float totalLength = 0f;

            for (int index = 0; index < points.Length - 1; index++)
            {
                totalLength += Vector2.Distance(
                    points[index],
                    points[index + 1]);
            }

            if (totalLength <= 0.001f)
            {
                return;
            }

            float rangeStart = totalLength * start;
            float rangeEnd = totalLength * end;
            float travelled = 0f;

            for (int index = 0; index < points.Length - 1; index++)
            {
                Vector2 segmentStart = points[index];
                Vector2 segmentEnd = points[index + 1];
                float segmentLength = Vector2.Distance(
                    segmentStart,
                    segmentEnd);
                float segmentLimit = travelled + segmentLength;

                float visibleStart = Mathf.Max(rangeStart, travelled);
                float visibleEnd = Mathf.Min(rangeEnd, segmentLimit);

                if (visibleEnd > visibleStart && segmentLength > 0.001f)
                {
                    float localStart =
                        (visibleStart - travelled) / segmentLength;
                    float localEnd =
                        (visibleEnd - travelled) / segmentLength;

                    AddFeatheredSegment(
                        vertexHelper,
                        origin + Vector2.Lerp(
                            segmentStart,
                            segmentEnd,
                            localStart),
                        origin + Vector2.Lerp(
                            segmentStart,
                            segmentEnd,
                            localEnd),
                        thickness,
                        lineColor);
                }

                travelled = segmentLimit;
            }
        }

        private void AddFeatheredSegment(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color lineColor)
        {
            Vector2 direction = end - start;

            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float halfThickness = thickness * 0.5f;
            float overlap = Mathf.Min(1.2f, halfThickness * 0.35f);
            start -= direction * overlap;
            end += direction * overlap;

            Vector2 inner = normal * halfThickness;
            Vector2 outer = normal * (halfThickness + _feather);
            Color clear = new Color(
                lineColor.r,
                lineColor.g,
                lineColor.b,
                0f);
            int first = vertexHelper.currentVertCount;

            vertexHelper.AddVert(start - outer, clear, Vector2.zero);
            vertexHelper.AddVert(start - inner, lineColor, Vector2.zero);
            vertexHelper.AddVert(start + inner, lineColor, Vector2.zero);
            vertexHelper.AddVert(start + outer, clear, Vector2.zero);
            vertexHelper.AddVert(end - outer, clear, Vector2.zero);
            vertexHelper.AddVert(end - inner, lineColor, Vector2.zero);
            vertexHelper.AddVert(end + inner, lineColor, Vector2.zero);
            vertexHelper.AddVert(end + outer, clear, Vector2.zero);

            for (int band = 0; band < 3; band++)
            {
                vertexHelper.AddTriangle(
                    first + band,
                    first + band + 1,
                    first + 5 + band);

                vertexHelper.AddTriangle(
                    first + band,
                    first + 5 + band,
                    first + 4 + band);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AILURONEAntiAliasedPolylineGraphic : MaskableGraphic
    {
        private Vector2[] _points = new Vector2[0];
        private float _thickness = 3f;
        private float _feather = 0.75f;

        internal void Configure(
            Vector2[] points,
            float thickness,
            float feather)
        {
            _points = points != null
                ? (Vector2[])points.Clone()
                : new Vector2[0];

            _thickness = Mathf.Max(0.5f, thickness);
            _feather = Mathf.Max(0.25f, feather);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (_points == null || _points.Length < 2)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            Vector2 origin = new Vector2(rect.xMin, rect.yMin);
            float halfThickness = _thickness * 0.5f;
            Color32 coreColor = color;
            Color32 clearColor = new Color(
                color.r,
                color.g,
                color.b,
                0f);

            for (int index = 0; index < _points.Length; index++)
            {
                Vector2 previousTangent = index > 0
                    ? (_points[index] - _points[index - 1]).normalized
                    : (_points[1] - _points[0]).normalized;

                Vector2 nextTangent = index < _points.Length - 1
                    ? (_points[index + 1] - _points[index]).normalized
                    : previousTangent;

                Vector2 previousNormal = new Vector2(
                    -previousTangent.y,
                    previousTangent.x);

                Vector2 nextNormal = new Vector2(
                    -nextTangent.y,
                    nextTangent.x);

                Vector2 miter = previousNormal + nextNormal;

                if (miter.sqrMagnitude < 0.001f)
                {
                    miter = nextNormal;
                }

                miter.Normalize();

                float denominator = Mathf.Max(
                    0.45f,
                    Mathf.Abs(Vector2.Dot(miter, nextNormal)));

                float miterScale = Mathf.Min(2f, 1f / denominator);
                Vector2 centre = origin + _points[index];
                Vector2 innerOffset =
                    miter * halfThickness * miterScale;

                Vector2 outerOffset =
                    miter * (halfThickness + _feather) * miterScale;

                vertexHelper.AddVert(
                    centre - outerOffset,
                    clearColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    centre - innerOffset,
                    coreColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    centre + innerOffset,
                    coreColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    centre + outerOffset,
                    clearColor,
                    Vector2.zero);
            }

            for (int index = 0; index < _points.Length - 1; index++)
            {
                int current = index * 4;
                int next = current + 4;

                for (int band = 0; band < 3; band++)
                {
                    vertexHelper.AddTriangle(
                        current + band,
                        current + band + 1,
                        next + band + 1);

                    vertexHelper.AddTriangle(
                        current + band,
                        next + band + 1,
                        next + band);
                }
            }

            AddCap(
                vertexHelper,
                origin + _points[0],
                (_points[1] - _points[0]).normalized,
                halfThickness,
                true,
                coreColor,
                clearColor);

            int last = _points.Length - 1;

            AddCap(
                vertexHelper,
                origin + _points[last],
                (_points[last] - _points[last - 1]).normalized,
                halfThickness,
                false,
                coreColor,
                clearColor);
        }

        private void AddCap(
            VertexHelper vertexHelper,
            Vector2 centre,
            Vector2 tangent,
            float halfThickness,
            bool start,
            Color32 coreColor,
            Color32 clearColor)
        {
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            Vector2 direction = start ? -tangent : tangent;
            Vector2 outerCentre = centre + direction * _feather;
            Vector2 innerOffset = normal * halfThickness;
            Vector2 outerOffset = normal * (halfThickness + _feather);
            int first = vertexHelper.currentVertCount;

            if (start)
            {
                vertexHelper.AddVert(
                    outerCentre - outerOffset,
                    clearColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    centre - innerOffset,
                    coreColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    centre + innerOffset,
                    coreColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    outerCentre + outerOffset,
                    clearColor,
                    Vector2.zero);
            }
            else
            {
                vertexHelper.AddVert(
                    centre - innerOffset,
                    coreColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    outerCentre - outerOffset,
                    clearColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    outerCentre + outerOffset,
                    clearColor,
                    Vector2.zero);

                vertexHelper.AddVert(
                    centre + innerOffset,
                    coreColor,
                    Vector2.zero);
            }

            vertexHelper.AddTriangle(first, first + 1, first + 2);
            vertexHelper.AddTriangle(first, first + 2, first + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AILURONEContinuousArcGraphic : MaskableGraphic
    {
        private float _rise = 8f;
        private float _thickness = 2f;
        private int _segments = 48;

        internal void Configure(
            float rise,
            float thickness,
            int segments)
        {
            _rise = Mathf.Max(0f, rise);
            _thickness = Mathf.Max(0.25f, thickness);
            _segments = Mathf.Clamp(segments, 8, 128);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();

            if (rect.width <= 0.01f || rect.height <= 0.01f)
            {
                return;
            }

            float halfWidth = rect.width * 0.5f;
            float halfThickness = _thickness * 0.5f;

            for (int index = 0; index <= _segments; index++)
            {
                float progress = index / (float)_segments;
                float normalizedX = progress * 2f - 1f;
                float x = rect.center.x + normalizedX * halfWidth;
                float y = rect.center.y - _rise * 0.5f
                    + _rise * (1f - normalizedX * normalizedX);

                float slope = -4f * _rise * normalizedX
                    / Mathf.Max(1f, rect.width);

                Vector2 tangent = new Vector2(1f, slope).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x)
                    * halfThickness;

                Vector2 centre = new Vector2(x, y);
                vertexHelper.AddVert(centre - normal, color, Vector2.zero);
                vertexHelper.AddVert(centre + normal, color, Vector2.one);

                if (index == 0)
                {
                    continue;
                }

                int current = index * 2;
                int previous = current - 2;

                vertexHelper.AddTriangle(
                    previous,
                    previous + 1,
                    current + 1);

                vertexHelper.AddTriangle(
                    previous,
                    current + 1,
                    current);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AILURONEPortraitMaskGraphic : MaskableGraphic
    {
        private Color _bottomColor =
            new Color(0.094f, 0.102f, 0.106f, 1f);

        private Color _topColor =
            new Color(0.106f, 0.110f, 0.118f, 1f);

        internal void Configure(Color bottomColor, Color topColor)
        {
            _bottomColor = bottomColor;
            _topColor = topColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            Vector2[] points =
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMin + 435f, rect.yMin),
                new Vector2(rect.xMin + 304f, rect.yMin + 114f),
                new Vector2(rect.xMin + 205f, rect.yMin + 114f),
                new Vector2(rect.xMin + 78f, rect.yMin + 227f),
                new Vector2(rect.xMin, rect.yMin + 227f)
            };

            for (int index = 0; index < points.Length; index++)
            {
                float height01 = Mathf.InverseLerp(
                    rect.yMin,
                    rect.yMin + 227f,
                    points[index].y);

                Color vertexColor = Color.Lerp(
                    _bottomColor,
                    _topColor,
                    height01);

                vertexHelper.AddVert(
                    points[index],
                    vertexColor,
                    Vector2.zero);
            }

            for (int index = 1; index < points.Length - 1; index++)
            {
                vertexHelper.AddTriangle(0, index, index + 1);
            }
        }
    }

    /// <summary>
    /// Shared, deliberately restrained motion signal for peripheral HUD groups.
    /// The timer and crosshair remain level and do not consume this signal.
    /// </summary>
    internal static class AILURONEHUDMotionSignal
    {
        private static int _lastFrame = -1;
        private static Camera _camera;
        private static CharacterController _controller;
        private static Vector3 _lastCameraEuler;
        private static bool _hasCameraSample;
        private static Vector2 _offset;

        internal static Vector2 GetOffset(float weight = 1f)
        {
            UpdateSignal();
            return _offset * weight;
        }

        private static void UpdateSignal()
        {
            if (_lastFrame == Time.frameCount)
            {
                return;
            }

            _lastFrame = Time.frameCount;

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_controller == null &&
                StarterAssets.FirstPersonController.Instance != null)
            {
                _controller =
                    StarterAssets.FirstPersonController.Instance
                        .GetComponent<CharacterController>();
            }

            Vector2 target = Vector2.zero;

            if (_camera != null)
            {
                Vector3 currentEuler =
                    _camera.transform.eulerAngles;

                if (_hasCameraSample)
                {
                    float yaw = Mathf.DeltaAngle(
                        _lastCameraEuler.y,
                        currentEuler.y);

                    float pitch = Mathf.DeltaAngle(
                        _lastCameraEuler.x,
                        currentEuler.x);

                    target += new Vector2(-yaw, pitch) * 0.32f;
                }

                _lastCameraEuler = currentEuler;
                _hasCameraSample = true;
            }

            if (_controller != null)
            {
                Vector3 velocity = _controller.velocity;
                float planarSpeed =
                    new Vector2(velocity.x, velocity.z).magnitude;

                float walkWeight =
                    Mathf.Clamp01(planarSpeed / 6f);

                target.y +=
                    Mathf.Sin(Time.unscaledTime * 8.5f)
                    * walkWeight
                    * 0.8f;

                target.y +=
                    Mathf.Clamp(velocity.y * 0.08f, -1.2f, 1.2f);
            }

            if (DashController.Instance != null &&
                DashController.Instance.isDashing)
            {
                target.x -= 2.6f;
            }

            target.x = Mathf.Clamp(target.x, -3.2f, 3.2f);
            target.y = Mathf.Clamp(target.y, -2.2f, 2.2f);

            float delta = Mathf.Max(0f, Time.unscaledDeltaTime);
            float blend = 1f - Mathf.Exp(-11f * delta);
            _offset = Vector2.Lerp(_offset, target, blend);
        }
    }
}
