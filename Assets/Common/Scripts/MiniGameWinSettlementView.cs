using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameWinSettlementView : IDisposable
    {
        private const float EnterDuration = 0.36f;
        private const float InputBlockDuration = 0.48f;
        private const float IdlePulseScale = 0.018f;
        private const string PanelSpritePath = "HallTheme/popup_panel";
        private const string PanelTopDecorSpritePath = "HallTheme/popup_panel_top_decor_side";
        private const string PrimaryButtonSpritePath = "HallTheme/hall_tab_unselected";
        private const string SecondaryButtonSpritePath = "HallTheme/hall_tab_unselected";
        private const string CoinSpritePath = "GameIcons/coin";
        private const string ChestSpritePath = "GameIcons/chest";
        private const string StarSpritePath = "GameIcons/star";

        private readonly GameObject root;
        private readonly RectTransform dialog;
        private readonly RectTransform star;
        private readonly Graphic starGlow;
        private readonly Graphic[] sparkles;
        private readonly RectTransform rewardRow;
        private readonly Button nextButton;
        private readonly Button hallButton;
        private readonly GameObject inputBlocker;

        private float elapsed;

        private MiniGameWinSettlementView(
            GameObject rootObject,
            RectTransform dialogRect,
            RectTransform starRect,
            Graphic glowGraphic,
            Graphic[] sparkleGraphics,
            RectTransform rewardRowRect,
            Button nextAction,
            Button hallAction,
            GameObject inputBlockerObject)
        {
            root = rootObject;
            dialog = dialogRect;
            star = starRect;
            starGlow = glowGraphic;
            sparkles = sparkleGraphics;
            rewardRow = rewardRowRect;
            nextButton = nextAction;
            hallButton = hallAction;
            inputBlocker = inputBlockerObject;
        }

        public static MiniGameWinSettlementView Create(
            Transform parent,
            TMP_FontAsset fontAsset,
            MiniGameRewardSettlementPanelParams panelParams,
            Action onNextLevel,
            Action onBackHall)
        {
            if (panelParams == null)
            {
                throw new ArgumentNullException(nameof(panelParams));
            }

            var root = new GameObject(panelParams.RootName, typeof(RectTransform), typeof(CanvasRenderer));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var theme = MiniGameSettlementPanelTheme.Resolve(panelParams.Style);

            var blockerGraphic = root.AddComponent<RoundedRectGraphic>();
            blockerGraphic.color = theme.BlockerColor;
            blockerGraphic.CornerRadius = 0f;
            blockerGraphic.raycastTarget = true;

            var dialog = CreateRectObject("Dialog", rootRect);
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(620f, 548f);
            dialogRect.anchoredPosition = new Vector2(0f, -32f);
            var cardGraphic = CreateSpriteImage(dialog, LoadSprite(PanelSpritePath), Color.white, true, false);
            cardGraphic.type = Image.Type.Sliced;
            cardGraphic.preserveAspect = false;
            cardGraphic.raycastTarget = false;

            PopupPanelTopDecorUtility.CreateMirroredTopDecor(dialogRect, LoadSprite(PanelTopDecorSpritePath), 620f);
            CreateRibbon(dialogRect, theme.RibbonColor);
            CreateHeader(dialogRect, fontAsset, panelParams.Title, theme.TitleColor, theme.DividerColor);
            CreateInfoRows(dialogRect, fontAsset, panelParams, theme.DividerColor);
            var rewardRow = CreateRewardRow(dialogRect, fontAsset, panelParams.RewardLabel, panelParams.CoinCount, panelParams.ChestCount);
            CreateButtons(dialogRect, fontAsset, panelParams.PrimaryAction, theme.PrimaryButtonColor, onNextLevel, onBackHall, out var nextButton, out var hallButton);
            var starRefs = CreateStatusMark(dialogRect, fontAsset, theme);
            var sparkles = CreateSparkles(dialogRect, theme.SparkleColor, theme.ShowSparkles);
            var inputBlocker = CreateInputBlocker(rootRect);

            var view = new MiniGameWinSettlementView(
                root,
                dialogRect,
                starRefs.star,
                starRefs.glow,
                sparkles,
                rewardRow,
                nextButton,
                hallButton,
                inputBlocker);
            view.ApplyInitialState();
            if (panelParams.AutoTick)
            {
                root.AddComponent<MiniGameSettlementViewTicker>().Bind(view);
            }

            return view;
        }

        public void Tick(float deltaTime)
        {
            elapsed += Mathf.Max(0f, deltaTime);
            var enter = SmoothStep01(elapsed / EnterDuration);
            dialog.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, enter);
            dialog.anchoredPosition = new Vector2(0f, Mathf.Lerp(-108f, -32f, enter));
            if (inputBlocker != null && inputBlocker.activeSelf && elapsed >= InputBlockDuration)
            {
                inputBlocker.SetActive(false);
            }

            if (star != null)
            {
                var pulse = Mathf.Sin(elapsed * 5.6f) * 0.04f;
                star.localScale = Vector3.one * (1.0f + pulse);
                star.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(elapsed * 2.1f) * 4.5f);
            }

            if (starGlow != null)
            {
                var color = starGlow.color;
                color.a = 0.34f + Mathf.Sin(elapsed * 3.8f) * 0.11f;
                starGlow.color = color;
                starGlow.rectTransform.localScale = Vector3.one * (1.0f + Mathf.Sin(elapsed * 2.8f) * 0.08f);
            }

            if (rewardRow != null)
            {
                rewardRow.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 4.2f) * IdlePulseScale);
            }

            for (var i = 0; i < sparkles.Length; i++)
            {
                var sparkle = sparkles[i];
                if (sparkle == null)
                {
                    continue;
                }

                var phase = elapsed * 4.5f + i * 1.37f;
                var color = sparkle.color;
                color.a = 0.28f + Mathf.Abs(Mathf.Sin(phase)) * 0.56f;
                sparkle.color = color;
                sparkle.rectTransform.localScale = Vector3.one * (0.72f + Mathf.Abs(Mathf.Sin(phase)) * 0.38f);
            }
        }

        public void Dispose()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
            }

            if (hallButton != null)
            {
                hallButton.onClick.RemoveAllListeners();
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void ApplyInitialState()
        {
            elapsed = 0f;
            if (inputBlocker != null)
            {
                inputBlocker.SetActive(true);
            }

            if (dialog != null)
            {
                dialog.localScale = Vector3.one * 0.72f;
                dialog.anchoredPosition = new Vector2(0f, -108f);
            }
        }

        private static void CreateRibbon(RectTransform parent, Color color)
        {
            var ribbon = CreateRectObject("GoldRibbon", parent);
            var rect = ribbon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(310f, 54f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            var graphic = ribbon.AddComponent<MiniGameSettlementRibbonGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
        }

        private static void CreateHeader(RectTransform parent, TMP_FontAsset fontAsset, string titleText, Color titleColor, Color dividerColor)
        {
            var title = CreateText(parent, "Title", titleText, fontAsset, 42f, FontStyles.Bold, titleColor);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(380f, 62f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);
            title.alignment = TextAlignmentOptions.Center;

            CreateDivider(parent, "HeaderLineLeft", new Vector2(-166f, -102f), 86f, dividerColor);
            CreateDivider(parent, "HeaderLineRight", new Vector2(166f, -102f), 86f, dividerColor);
        }

        private static void CreateInfoRows(RectTransform parent, TMP_FontAsset fontAsset, MiniGameRewardSettlementPanelParams panelParams, Color dividerColor)
        {
            CreateInfoRow(parent, fontAsset, "PrimaryInfoRow", -146f, panelParams.PrimaryInfo.Label, panelParams.PrimaryInfo.Value);
            CreateInfoRow(parent, fontAsset, "SecondaryInfoRow", -216f, panelParams.SecondaryInfo.Label, panelParams.SecondaryInfo.Value);
            CreateDivider(parent, "InfoDividerA", new Vector2(0f, -184f), 404f, dividerColor);
            CreateDivider(parent, "InfoDividerB", new Vector2(0f, -254f), 404f, dividerColor);
        }

        private static RectTransform CreateRewardRow(RectTransform parent, TMP_FontAsset fontAsset, string rewardLabel, int coinCount, int chestCount)
        {
            var row = CreateRectObject("RewardRow", parent);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(456f, 72f);
            rowRect.anchoredPosition = new Vector2(0f, -292f);

            var label = CreateText(rowRect, "RewardLabel", rewardLabel, fontAsset, 26f, FontStyles.Normal, new Color32(87, 72, 54, 255));
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(114f, 48f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var coinIcon = CreateRewardIcon(rowRect, "CoinIcon", LoadSprite(CoinSpritePath), new Vector2(106f, 0f), new Vector2(54f, 51f));
            var coinText = CreateText(rowRect, "CoinText", "+" + coinCount, fontAsset, 40f, FontStyles.Bold, new Color32(246, 190, 38, 255));
            coinText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            coinText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            coinText.rectTransform.pivot = new Vector2(0f, 0.5f);
            coinText.rectTransform.sizeDelta = new Vector2(136f, 54f);
            coinText.rectTransform.anchoredPosition = new Vector2(134f, 0f);
            coinText.alignment = TextAlignmentOptions.MidlineLeft;
            coinText.enableAutoSizing = true;
            coinText.fontSizeMin = 32f;
            coinText.fontSizeMax = 40f;
            AddTextShadow(coinText, new Color(0.47f, 0.31f, 0.02f, 0.45f), new Vector2(2f, -2f));

            CreateRewardIcon(rowRect, "ChestIcon", LoadSprite(ChestSpritePath), new Vector2(314f, 0f), new Vector2(82f, 66f));
            var chestText = CreateText(rowRect, "ChestText", "+" + chestCount, fontAsset, 38f, FontStyles.Bold, new Color32(126, 80, 26, 255));
            chestText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            chestText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            chestText.rectTransform.pivot = new Vector2(0f, 0.5f);
            chestText.rectTransform.sizeDelta = new Vector2(86f, 52f);
            chestText.rectTransform.anchoredPosition = new Vector2(366f, -1f);
            chestText.alignment = TextAlignmentOptions.MidlineLeft;

            coinIcon.SetAsLastSibling();
            return rowRect;
        }

        private static void CreateButtons(RectTransform parent, TMP_FontAsset fontAsset, MiniGameRewardSettlementPrimaryAction primaryAction, Color primaryButtonColor, Action onNextLevel, Action onBackHall, out Button nextButton, out Button hallButton)
        {
            var primaryReturnsToHall = primaryAction == MiniGameRewardSettlementPrimaryAction.BackHall;
            nextButton = CreateActionButton(
                parent,
                "NextButton",
                primaryReturnsToHall ? new Vector2(0f, -428f) : new Vector2(0f, -400f),
                new Vector2(286f, 87f),
                34f,
                primaryButtonColor,
                LoadSprite(PrimaryButtonSpritePath),
                ResolvePrimaryActionText(primaryAction),
                38f,
                Color.white,
                fontAsset,
                primaryReturnsToHall ? onBackHall : onNextLevel);
            var nextLabel = nextButton.transform.Find("Label")?.GetComponent<RectTransform>();
            if (nextLabel != null)
            {
                nextLabel.offsetMin = new Vector2(14f, 5f);
                nextLabel.offsetMax = new Vector2(-14f, 15f);
            }

            hallButton = CreateActionButton(
                parent,
                "BackHallButton",
                new Vector2(0f, -486f),
                new Vector2(224f, 63f),
                24f,
                Color.white,
                LoadSprite(SecondaryButtonSpritePath),
                UiTextCatalog.Get("common.action.back_hall"),
                27f,
                new Color32(92, 82, 70, 255),
                fontAsset,
                onBackHall);
            hallButton.gameObject.SetActive(!primaryReturnsToHall);
            var hallLabel = hallButton.transform.Find("Label")?.GetComponent<RectTransform>();
            if (hallLabel != null)
            {
                hallLabel.offsetMin = new Vector2(14f, 2f);
                hallLabel.offsetMax = new Vector2(-14f, 10f);
            }
        }

        private static (RectTransform star, Graphic glow) CreateStatusMark(RectTransform parent, TMP_FontAsset fontAsset, MiniGameSettlementPanelTheme theme)
        {
            if (theme.UseStarBadge)
            {
                return CreateStar(parent, theme.GlowColor);
            }

            var glow = CreateRectObject("StatusGlow", parent);
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 1f);
            glowRect.anchorMax = new Vector2(0.5f, 1f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(124f, 124f);
            glowRect.anchoredPosition = new Vector2(0f, 0f);
            var glowGraphic = glow.AddComponent<MiniGameSettlementGlowGraphic>();
            glowGraphic.color = theme.GlowColor;
            glowGraphic.raycastTarget = false;

            var badge = CreateRectObject("StatusBadge", parent);
            var badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 1f);
            badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(78f, 78f);
            badgeRect.anchoredPosition = new Vector2(0f, -6f);
            var badgeGraphic = EnsureRoundedRectGraphic(badge, theme.BadgeColor, 39f, false);
            badgeGraphic.raycastTarget = false;

            var symbol = CreateText(badgeRect, "Symbol", theme.BadgeText, fontAsset, 48f, FontStyles.Bold, Color.white);
            Stretch(symbol.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 3f));
            symbol.alignment = TextAlignmentOptions.Center;
            return (badgeRect, glowGraphic);
        }

        private static (RectTransform star, Graphic glow) CreateStar(RectTransform parent, Color glowColor)
        {
            var glow = CreateRectObject("StarGlow", parent);
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 1f);
            glowRect.anchorMax = new Vector2(0.5f, 1f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(132f, 132f);
            glowRect.anchoredPosition = new Vector2(0f, 4f);
            var glowGraphic = glow.AddComponent<MiniGameSettlementGlowGraphic>();
            glowGraphic.color = glowColor;
            glowGraphic.raycastTarget = false;

            var star = CreateRectObject("StarBadge", parent);
            var starRect = star.GetComponent<RectTransform>();
            starRect.anchorMin = new Vector2(0.5f, 1f);
            starRect.anchorMax = new Vector2(0.5f, 1f);
            starRect.pivot = new Vector2(0.5f, 0.5f);
            starRect.sizeDelta = new Vector2(84f, 75f);
            starRect.anchoredPosition = new Vector2(0f, -5f);
            var starGraphic = CreateSpriteImage(star, LoadSprite(StarSpritePath), Color.white, true, false);
            starGraphic.raycastTarget = false;
            return (starRect, glowGraphic);
        }

        private static Graphic[] CreateSparkles(RectTransform parent, Color sparkleColor, bool showSparkles)
        {
            if (!showSparkles)
            {
                return Array.Empty<Graphic>();
            }

            var positions = new[]
            {
                new Vector2(-206f, -314f),
                new Vector2(214f, -304f),
                new Vector2(-202f, -104f),
                new Vector2(202f, -124f)
            };

            var sparkles = new Graphic[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var sparkle = CreateRectObject("Sparkle_" + i, parent);
                var rect = sparkle.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(i < 2 ? 34f : 22f, i < 2 ? 34f : 22f);
                rect.anchoredPosition = positions[i];
                var graphic = sparkle.AddComponent<MiniGameSettlementSparkleGraphic>();
                graphic.color = sparkleColor;
                graphic.raycastTarget = false;
                sparkles[i] = graphic;
            }

            return sparkles;
        }

        private static GameObject CreateInputBlocker(RectTransform parent)
        {
            var blocker = CreateRectObject("InputBlocker", parent);
            var rect = blocker.GetComponent<RectTransform>();
            Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var graphic = EnsureRoundedRectGraphic(blocker, new Color(1f, 1f, 1f, 0f), 0f, true);
            graphic.raycastTarget = true;
            blocker.transform.SetAsLastSibling();
            return blocker;
        }

        private static void CreateInfoRow(RectTransform parent, TMP_FontAsset fontAsset, string name, float y, string labelText, string valueText)
        {
            var row = CreateRectObject(name, parent);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(404f, 52f);
            rowRect.anchoredPosition = new Vector2(0f, y);

            var label = CreateText(rowRect, "Label", labelText, fontAsset, 28f, FontStyles.Normal, new Color32(88, 77, 66, 255));
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(150f, 48f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 28f;

            var value = CreateText(rowRect, "Value", valueText, fontAsset, 30f, FontStyles.Normal, new Color32(68, 58, 48, 255));
            value.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            value.rectTransform.pivot = new Vector2(0f, 0.5f);
            value.rectTransform.offsetMin = new Vector2(166f, -24f);
            value.rectTransform.offsetMax = new Vector2(0f, 24f);
            value.alignment = TextAlignmentOptions.MidlineLeft;
            value.enableAutoSizing = true;
            value.fontSizeMin = 24f;
            value.fontSizeMax = 30f;
        }

        private static void CreateDivider(RectTransform parent, string name, Vector2 anchoredPosition, float width, Color color)
        {
            var line = CreateRectObject(name, parent);
            var lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 1f);
            lineRect.anchorMax = new Vector2(0.5f, 1f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.sizeDelta = new Vector2(width, 2f);
            lineRect.anchoredPosition = anchoredPosition;
            var graphic = EnsureRoundedRectGraphic(line, color, 1f, false);
            graphic.raycastTarget = false;
        }

        private static Button CreateActionButton(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            float radius,
            Color color,
            Sprite sprite,
            string labelText,
            float fontSize,
            Color labelColor,
            TMP_FontAsset fontAsset,
            Action onClick)
        {
            var buttonObject = CreateRectObject(name, parent);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            Graphic graphic;
            if (sprite != null)
            {
                graphic = CreateSpriteImage(buttonObject, sprite, color, true, true);
            }
            else
            {
                graphic = EnsureRoundedRectGraphic(buttonObject, color, radius, true);
            }

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            ConfigureButtonColors(button);
            button.onClick.AddListener(delegate { onClick?.Invoke(); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.95f);

            var label = CreateText(rect, "Label", labelText, fontAsset, fontSize, FontStyles.Bold, labelColor);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 2f), new Vector2(-14f, -2f));
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static RectTransform CreateRewardIcon(RectTransform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
        {
            var icon = CreateRectObject(name, parent);
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var graphic = CreateSpriteImage(icon, sprite, Color.white, true, false);
            graphic.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset fontAsset,
            float fontSize,
            FontStyles style,
            Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var label = textObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        private static void AddTextShadow(TextMeshProUGUI text, Color color, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            return string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
        }

        private static string ResolvePrimaryActionText(MiniGameRewardSettlementPrimaryAction action)
        {
            switch (action)
            {
                case MiniGameRewardSettlementPrimaryAction.Retry:
                    return UiTextCatalog.Get("common.action.retry");
                case MiniGameRewardSettlementPrimaryAction.Continue:
                    return UiTextCatalog.Get("common.action.continue");
                case MiniGameRewardSettlementPrimaryAction.Confirm:
                    return UiTextCatalog.Get("common.action.got_it");
                case MiniGameRewardSettlementPrimaryAction.BackHall:
                    return UiTextCatalog.Get("common.action.back_hall");
                default:
                    return UiTextCatalog.Get("common.action.next_level");
            }
        }

        private static Image CreateSpriteImage(GameObject target, Sprite sprite, Color color, bool preserveAspect, bool raycastTarget)
        {
            var image = target.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = raycastTarget;
            image.type = Image.Type.Simple;
            return image;
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static RoundedRectGraphic EnsureRoundedRectGraphic(GameObject target, Color color, float radius, bool raycastTarget)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
            {
                target.AddComponent<CanvasRenderer>();
            }

            var graphic = target.GetComponent<RoundedRectGraphic>();
            if (graphic == null)
            {
                graphic = target.AddComponent<RoundedRectGraphic>();
            }

            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static int AddVertex(VertexHelper vh, Vector2 position, Color color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vh.AddVert(vertex);
            return vh.currentVertCount - 1;
        }

        private sealed class MiniGameSettlementViewTicker : MonoBehaviour
        {
            private MiniGameWinSettlementView view;

            public void Bind(MiniGameWinSettlementView settlementView)
            {
                view = settlementView;
            }

            private void Update()
            {
                if (view != null)
                {
                    view.Tick(Time.unscaledDeltaTime);
                }
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class MiniGameSettlementRibbonGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var points = new[]
                {
                    new Vector2(rect.xMin, rect.center.y - 2f),
                    new Vector2(rect.xMin + 34f, rect.yMax),
                    new Vector2(rect.xMax - 34f, rect.yMax),
                    new Vector2(rect.xMax, rect.center.y - 2f),
                    new Vector2(rect.xMax - 34f, rect.yMin),
                    new Vector2(rect.xMin + 34f, rect.yMin)
                };

                var center = AddVertex(vh, rect.center, new Color32(255, 203, 45, 255));
                var indices = new int[points.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    indices[i] = AddVertex(vh, points[i], color);
                }

                for (var i = 0; i < indices.Length; i++)
                {
                    vh.AddTriangle(center, indices[i], indices[(i + 1) % indices.Length]);
                }
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class MiniGameSettlementGlowGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var center = AddVertex(vh, rect.center, color);
                var outerColor = color;
                outerColor.a = 0f;
                const int steps = 36;
                var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
                var indices = new int[steps];
                for (var i = 0; i < steps; i++)
                {
                    var angle = Mathf.PI * 2f * i / steps;
                    indices[i] = AddVertex(vh, rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, outerColor);
                }

                for (var i = 0; i < steps; i++)
                {
                    vh.AddTriangle(center, indices[i], indices[(i + 1) % steps]);
                }
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class MiniGameSettlementSparkleGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var center = rect.center;
                var top = AddVertex(vh, new Vector2(center.x, rect.yMax), color);
                var right = AddVertex(vh, new Vector2(rect.xMax, center.y), color);
                var bottom = AddVertex(vh, new Vector2(center.x, rect.yMin), color);
                var left = AddVertex(vh, new Vector2(rect.xMin, center.y), color);
                var middle = AddVertex(vh, center, new Color(1f, 1f, 1f, color.a));
                vh.AddTriangle(middle, top, right);
                vh.AddTriangle(middle, right, bottom);
                vh.AddTriangle(middle, bottom, left);
                vh.AddTriangle(middle, left, top);
            }
        }
    }

    public sealed class MiniGameRewardSettlementPanelParams
    {
        public string RootName = "MiniGameWinSettlementPanel";
        public MiniGameRewardSettlementPanelStyle Style = MiniGameRewardSettlementPanelStyle.Success;
        public MiniGameRewardSettlementPrimaryAction PrimaryAction = MiniGameRewardSettlementPrimaryAction.NextLevel;
        public bool AutoTick;
        public string Title;
        public MiniGameSettlementInfoRow PrimaryInfo;
        public MiniGameSettlementInfoRow SecondaryInfo;
        public string RewardLabel;
        public int CoinCount;
        public int ChestCount;
    }

    public enum MiniGameRewardSettlementPanelStyle
    {
        Success,
        Failure,
        Neutral
    }

    public enum MiniGameRewardSettlementPrimaryAction
    {
        NextLevel,
        Retry,
        Continue,
        Confirm,
        BackHall
    }

    internal sealed class MiniGameSettlementPanelTheme
    {
        public Color BlockerColor;
        public Color RibbonColor;
        public Color TitleColor;
        public Color DividerColor;
        public Color PrimaryButtonColor;
        public Color GlowColor;
        public Color BadgeColor;
        public Color SparkleColor;
        public string BadgeText;
        public bool UseStarBadge;
        public bool ShowSparkles;

        public static MiniGameSettlementPanelTheme Resolve(MiniGameRewardSettlementPanelStyle style)
        {
            switch (style)
            {
                case MiniGameRewardSettlementPanelStyle.Failure:
                    return new MiniGameSettlementPanelTheme
                    {
                        BlockerColor = new Color(0.22f, 0.12f, 0.11f, 0.46f),
                        RibbonColor = new Color32(191, 92, 63, 255),
                        TitleColor = new Color32(176, 70, 55, 255),
                        DividerColor = new Color32(216, 172, 154, 116),
                        PrimaryButtonColor = new Color32(236, 128, 72, 255),
                        GlowColor = new Color32(235, 121, 88, 88),
                        BadgeColor = new Color32(204, 83, 64, 255),
                        SparkleColor = new Color32(235, 139, 94, 160),
                        BadgeText = "!",
                        UseStarBadge = false,
                        ShowSparkles = false
                    };
                case MiniGameRewardSettlementPanelStyle.Neutral:
                    return new MiniGameSettlementPanelTheme
                    {
                        BlockerColor = new Color(0.12f, 0.16f, 0.22f, 0.42f),
                        RibbonColor = new Color32(99, 128, 163, 255),
                        TitleColor = new Color32(74, 103, 140, 255),
                        DividerColor = new Color32(177, 192, 213, 112),
                        PrimaryButtonColor = new Color32(94, 139, 190, 255),
                        GlowColor = new Color32(116, 158, 211, 82),
                        BadgeColor = new Color32(86, 126, 176, 255),
                        SparkleColor = new Color32(140, 174, 218, 150),
                        BadgeText = "i",
                        UseStarBadge = false,
                        ShowSparkles = false
                    };
                default:
                    return new MiniGameSettlementPanelTheme
                    {
                        BlockerColor = new Color(0.13f, 0.28f, 0.16f, 0.42f),
                        RibbonColor = new Color32(231, 165, 24, 255),
                        TitleColor = new Color32(50, 132, 63, 255),
                        DividerColor = new Color32(224, 202, 146, 98),
                        PrimaryButtonColor = new Color32(255, 183, 31, 255),
                        GlowColor = new Color32(255, 221, 64, 112),
                        BadgeColor = new Color32(255, 183, 31, 255),
                        SparkleColor = new Color32(255, 222, 82, 190),
                        BadgeText = string.Empty,
                        UseStarBadge = true,
                        ShowSparkles = true
                    };
            }
        }
    }

    public sealed class MiniGameSettlementInfoRow
    {
        public MiniGameSettlementInfoRow(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }

        public static MiniGameSettlementInfoRow CreateLevel(int levelNumber)
        {
            return new MiniGameSettlementInfoRow(
                UiTextCatalog.GetOrFallback("settlement.level_label", "关卡"),
                UiTextCatalog.Format("settlement.level_value", levelNumber));
        }
    }
}
