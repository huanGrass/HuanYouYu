using HuanYouYu.MiniGameHall;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmPrototype
{
    internal enum FarmInventoryContainerType
    {
        Backpack,
        ShippingBin
    }

    internal sealed class FarmInventorySlotBinding
    {
        public RectTransform Rect = null!;
        public Image Background = null!;
        public Image Fill = null!;
        public Image Icon = null!;
        public TextMeshProUGUI Name = null!;
        public TextMeshProUGUI Count = null!;
        public FarmInventoryContainerType ContainerType;
        public int SlotIndex;
        public Color EmptyAccentColor;
        public Sprite EmptySprite = null!;
    }

    internal sealed class FarmHudView
    {
        private const int BackpackSlotCount = 8;
        private const int MerchantShopPageSize = 10;
        private const float DialogueSpeakerFontSize = 20f;
        private const float DialogueBodyFontSize = 22f;
        private const float PortraitDialogueSpeakerFontSize = 17f;
        private const float PortraitDialogueBodyFontSize = 17f;
        private const float PortraitHorizontalMargin = 12f;
        private const float PortraitMaxPanelWidth = 560f;
        private const float PortraitMinPanelWidth = 280f;
        private const float LandscapeAdvanceDayButtonWidth = 116f;
        private const float PortraitAdvanceDayButtonMinWidth = 104f;
        private const float PortraitAdvanceDayButtonMaxWidth = 128f;
        private const float AdvanceDayButtonHeight = 36f;
        private const float LandscapeBottomPanelHeight = 108f;

        private enum FarmHudLayoutMode
        {
            Landscape,
            Portrait
        }

        private readonly TMP_FontAsset fontAsset;
        private FarmHudLayoutMode currentLayoutMode = FarmHudLayoutMode.Landscape;
        private Rect currentSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int currentScreenSize = Vector2Int.zero;
        private RectTransform infoTabRow = null!;
        private RectTransform backpackLabelRect = null!;
        private RectTransform backpackGrid = null!;
        private RectTransform merchantShopList = null!;
        private RectTransform toolbar = null!;

        private FarmHudView(TMP_FontAsset runtimeFontAsset)
        {
            fontAsset = runtimeFontAsset;
        }

        public RectTransform TopRoot { get; private set; } = null!;
        public RectTransform BottomRoot { get; private set; } = null!;
        public RectTransform OverlayRoot { get; private set; } = null!;
        public RectTransform InfoCardPanel { get; private set; } = null!;
        public RectTransform InventoryPanel { get; private set; } = null!;
        public RectTransform MerchantShopPanel { get; private set; } = null!;
        public RectTransform InfoCloseButtonRect { get; private set; } = null!;
        public RectTransform InventoryCloseButtonRect { get; private set; } = null!;
        public RectTransform MerchantShopCloseButtonRect { get; private set; } = null!;
        public RectTransform MerchantShopPrevButtonRect { get; private set; } = null!;
        public RectTransform MerchantShopNextButtonRect { get; private set; } = null!;
        public RectTransform AdvanceDayButtonRect { get; private set; } = null!;
        public RectTransform DialoguePanel { get; private set; } = null!;
        public RectTransform DragGhostRect { get; private set; } = null!;
        public RectTransform InventoryTooltipPanel { get; private set; } = null!;
        public Image DragGhostBackground { get; private set; } = null!;
        public Image DragGhostIcon { get; private set; } = null!;
        public TextMeshProUGUI StatusText { get; private set; } = null!;
        public TextMeshProUGUI InfoCardTitleText { get; private set; } = null!;
        public TextMeshProUGUI MessageText { get; private set; } = null!;
        public TextMeshProUGUI ControlsText { get; private set; } = null!;
        public TextMeshProUGUI InventoryTitleText { get; private set; } = null!;
        public TextMeshProUGUI MerchantShopTitleText { get; private set; } = null!;
        public TextMeshProUGUI MerchantShopPageText { get; private set; } = null!;
        public TextMeshProUGUI MerchantShopHintText { get; private set; } = null!;
        public TextMeshProUGUI DialogueSpeakerText { get; private set; } = null!;
        public TextMeshProUGUI DialogueBodyText { get; private set; } = null!;
        public TextMeshProUGUI DragGhostCountText { get; private set; } = null!;
        public TextMeshProUGUI InventoryTooltipTitleText { get; private set; } = null!;
        public TextMeshProUGUI InventoryTooltipBodyText { get; private set; } = null!;
        public RectTransform[] ToolButtonRects { get; } = new RectTransform[4];
        public Image[] ToolButtonImages { get; } = new Image[4];
        public TextMeshProUGUI[] ToolButtonTexts { get; } = new TextMeshProUGUI[4];
        public RectTransform[] InfoTabButtonRects { get; } = new RectTransform[5];
        public Image[] InfoTabButtonImages { get; } = new Image[5];
        public TextMeshProUGUI[] InfoTabButtonTexts { get; } = new TextMeshProUGUI[5];
        public RectTransform[] MerchantShopItemButtonRects { get; } = new RectTransform[MerchantShopPageSize];
        public Image[] MerchantShopItemButtonImages { get; } = new Image[MerchantShopPageSize];
        public TextMeshProUGUI[] MerchantShopItemButtonTexts { get; } = new TextMeshProUGUI[MerchantShopPageSize];
        public FarmInventorySlotBinding[] InventorySlotBindings { get; } = new FarmInventorySlotBinding[BackpackSlotCount];

        public static FarmHudView Create(Transform topHost, Transform bottomHost, Transform overlayHost, TMP_FontAsset runtimeFontAsset)
        {
            var view = new FarmHudView(runtimeFontAsset);
            view.Build(topHost, bottomHost, overlayHost);
            return view;
        }

        public void Dispose()
        {
            if (TopRoot != null)
            {
                Object.Destroy(TopRoot.gameObject);
            }

            if (BottomRoot != null)
            {
                Object.Destroy(BottomRoot.gameObject);
            }
        }

        public void ApplyLayout(Rect safeArea, Vector2Int screenSize)
        {
            if (TopRoot == null || BottomRoot == null || OverlayRoot == null)
            {
                return;
            }

            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                screenSize = new Vector2Int(Screen.width, Screen.height);
            }

            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                screenSize = new Vector2Int(1280, 720);
            }

            var layoutMode = screenSize.y > screenSize.x ? FarmHudLayoutMode.Portrait : FarmHudLayoutMode.Landscape;
            if (layoutMode == currentLayoutMode && currentSafeArea == safeArea && currentScreenSize == screenSize)
            {
                return;
            }

            currentLayoutMode = layoutMode;
            currentSafeArea = safeArea;
            currentScreenSize = screenSize;

            ApplySafeArea(TopRoot, safeArea, screenSize);
            ApplySafeArea(BottomRoot, safeArea, screenSize);
            ApplySafeArea(OverlayRoot, safeArea, screenSize);

            if (layoutMode == FarmHudLayoutMode.Portrait)
            {
                ApplyPortraitLayout(safeArea, screenSize);
            }
            else
            {
                ApplyLandscapeLayout();
            }
        }

        private void Build(Transform topHost, Transform bottomHost, Transform overlayHost)
        {
            TopRoot = CreateStretchRoot("FarmTopHudRoot", topHost);
            BottomRoot = CreateStretchRoot("FarmBottomHudRoot", bottomHost);
            OverlayRoot = CreateStretchRoot("FarmOverlayRoot", overlayHost);

            RectTransform topPanel = CreatePanel(
                "TopPanel",
                TopRoot,
                new Vector2(24f, -100f),
                new Vector2(328f, 60f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.25f, 0.34f, 0.24f, 0.72f));

            RectTransform infoButtonPanel = CreatePanel(
                "InfoButtonPanel",
                OverlayRoot,
                new Vector2(-18f, -18f),
                new Vector2(336f, 52f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Color(0.3f, 0.22f, 0.12f, 0.76f));

            InfoCardPanel = CreatePanel(
                "RightPanel",
                OverlayRoot,
                new Vector2(-18f, -76f),
                new Vector2(336f, 236f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Color(0.3f, 0.22f, 0.12f, 0.88f));

            InventoryPanel = CreatePanel(
                "InventoryPanel",
                OverlayRoot,
                new Vector2(0f, -110f),
                new Vector2(620f, 214f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Color(0.84f, 0.72f, 0.5f, 0.97f));

            MerchantShopPanel = CreatePanel(
                "MerchantShopPanel",
                OverlayRoot,
                new Vector2(0f, -38f),
                new Vector2(460f, 560f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Color(0.22f, 0.18f, 0.11f, 0.94f));

            RectTransform bottomPanel = CreatePanel(
                "BottomPanel",
                BottomRoot,
                new Vector2(0f, 18f),
                new Vector2(478f, 90f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(0.18f, 0.24f, 0.18f, 0.72f));

            DialoguePanel = CreatePanel(
                "DialoguePanel",
                OverlayRoot,
                new Vector2(0f, 182f),
                new Vector2(760f, 124f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(0.24f, 0.18f, 0.11f, 0.92f));

            StatusText = CreateText(topPanel, "StatusText", 20f, 14f, TextAlignmentOptions.TopLeft);
            InfoCardTitleText = CreateText(InfoCardPanel, "InfoCardTitle", 20f, 14f, TextAlignmentOptions.TopLeft);
            MessageText = CreateText(InfoCardPanel, "MessageText", 20f, 15f, TextAlignmentOptions.TopLeft);
            ControlsText = CreateText(bottomPanel, "ControlsText", 18f, 14f, TextAlignmentOptions.TopLeft);
            InventoryTitleText = CreateText(InventoryPanel, "InventoryTitle", 22f, 16f, TextAlignmentOptions.TopLeft);
            MerchantShopTitleText = CreateText(MerchantShopPanel, "MerchantShopTitle", 24f, 18f, TextAlignmentOptions.TopLeft);
            MerchantShopPageText = CreateText(MerchantShopPanel, "MerchantShopPage", 16f, 12f, TextAlignmentOptions.TopRight);
            MerchantShopHintText = CreateText(MerchantShopPanel, "MerchantShopHint", 14f, 11f, TextAlignmentOptions.BottomLeft);
            DialogueSpeakerText = CreateText(DialoguePanel, "DialogueSpeaker", 20f, 14f, TextAlignmentOptions.TopLeft);
            DialogueBodyText = CreateText(DialoguePanel, "DialogueBody", 22f, 16f, TextAlignmentOptions.TopLeft);
            DialogueSpeakerText.enableAutoSizing = false;
            DialogueSpeakerText.fontSize = DialogueSpeakerFontSize;
            DialogueBodyText.enableAutoSizing = false;
            DialogueBodyText.fontSize = DialogueBodyFontSize;

            ConfigureTextLayout(false);

            DialoguePanel.gameObject.SetActive(false);

            InfoCloseButtonRect = CreateCloseButton(InfoCardPanel, "InfoCloseButton");
            InventoryCloseButtonRect = CreateCloseButton(InventoryPanel, "InventoryCloseButton");
            MerchantShopCloseButtonRect = CreateCloseButton(MerchantShopPanel, "MerchantShopCloseButton");

            MerchantShopPrevButtonRect = CreateMerchantShopActionButton(
                MerchantShopPanel,
                "MerchantShopPrevButton",
                new Vector2(16f, -508f),
                new Vector2(96f, 34f),
                "stardewai.ui.prev", UiTextCatalog.Get("stardewai.ui.prev"));
            MerchantShopNextButtonRect = CreateMerchantShopActionButton(
                MerchantShopPanel,
                "MerchantShopNextButton",
                new Vector2(126f, -508f),
                new Vector2(96f, 34f),
                "stardewai.ui.next", UiTextCatalog.Get("stardewai.ui.next"));

            merchantShopList = CreateRectOnly("MerchantShopList", MerchantShopPanel);
            merchantShopList.anchorMin = new Vector2(0f, 1f);
            merchantShopList.anchorMax = new Vector2(0f, 1f);
            merchantShopList.pivot = new Vector2(0f, 1f);
            merchantShopList.anchoredPosition = new Vector2(16f, -56f);
            merchantShopList.sizeDelta = new Vector2(428f, 444f);

            for (int i = 0; i < MerchantShopPageSize; i++)
            {
                CreateMerchantShopItemButton(merchantShopList, i);
            }

            infoTabRow = CreateRectOnly("InfoTabRow", infoButtonPanel);
            infoTabRow.anchorMin = new Vector2(0f, 1f);
            infoTabRow.anchorMax = new Vector2(0f, 1f);
            infoTabRow.pivot = new Vector2(0f, 1f);
            infoTabRow.anchoredPosition = new Vector2(12f, -10f);
            infoTabRow.sizeDelta = new Vector2(318f, 34f);

            CreateInfoTabButton(infoTabRow, 0, "stardewai.tab.overview", UiTextCatalog.Get("stardewai.tab.overview"));
            CreateInfoTabButton(infoTabRow, 1, "stardewai.tab.event", UiTextCatalog.Get("stardewai.tab.event"));
            CreateInfoTabButton(infoTabRow, 2, "stardewai.tab.calendar", UiTextCatalog.Get("stardewai.tab.calendar"));
            CreateInfoTabButton(infoTabRow, 3, "stardewai.tab.backpack", UiTextCatalog.Get("stardewai.tab.backpack"));
            CreateInfoTabButton(infoTabRow, 4, "stardewai.tab.controls", UiTextCatalog.Get("stardewai.tab.controls"));

            TextMeshProUGUI backpackLabel = CreateSectionLabel(
                InventoryPanel,
                "BackpackLabel",
                UiTextCatalog.Get("stardewai.backpack.label"),
                new Vector2(16f, -48f));
            backpackLabelRect = backpackLabel.rectTransform.parent as RectTransform;
            backpackGrid = CreateRectOnly("BackpackGrid", InventoryPanel);
            backpackGrid.anchorMin = new Vector2(0f, 1f);
            backpackGrid.anchorMax = new Vector2(0f, 1f);
            backpackGrid.pivot = new Vector2(0f, 1f);
            backpackGrid.anchoredPosition = new Vector2(16f, -74f);
            backpackGrid.sizeDelta = new Vector2(584f, 62f);

            for (int i = 0; i < BackpackSlotCount; i++)
            {
                CreateInventorySlot(
                    backpackGrid,
                    i,
                    FarmInventoryContainerType.Backpack,
                    i,
                    new Vector2(i * 72f, 0f),
                    new Color(0.95f, 0.84f, 0.52f, 0.95f),
                    FarmPixelArtFactory.GetSprite(FarmSpriteArt.SeedChest));
            }

            BuildInventoryDragGhost(OverlayRoot);
            BuildInventoryTooltip(OverlayRoot);

            toolbar = CreateRectOnly("Toolbar", bottomPanel);
            toolbar.anchorMin = new Vector2(0.5f, 1f);
            toolbar.anchorMax = new Vector2(0.5f, 1f);
            toolbar.pivot = new Vector2(0.5f, 1f);
            toolbar.anchoredPosition = new Vector2(0f, -12f);
            toolbar.sizeDelta = new Vector2(428f, 48f);

            CreateToolButton(toolbar, 0, "stardewai.tool.hoe", "1 " + UiTextCatalog.Get("stardewai.tool.hoe"));
            CreateToolButton(toolbar, 1, "stardewai.tool.watering", "2 " + UiTextCatalog.Get("stardewai.tool.watering"));
            CreateToolButton(toolbar, 2, "stardewai.tool.seeds", "3 " + UiTextCatalog.Get("stardewai.tool.seeds"));
            CreateToolButton(toolbar, 3, "stardewai.tool.harvest", "4 " + UiTextCatalog.Get("stardewai.tool.harvest"));
            AdvanceDayButtonRect = CreateHudActionButton(
                bottomPanel,
                "AdvanceDayButton",
                new Vector2(-12f, 8f),
                new Vector2(LandscapeAdvanceDayButtonWidth, AdvanceDayButtonHeight),
                "stardewai.action.next_day",
                UiTextCatalog.Get("stardewai.action.next_day"));

            RectTransform controlsRect = ControlsText.rectTransform;
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(1f, 0f);
            controlsRect.pivot = new Vector2(0.5f, 0f);
            controlsRect.anchoredPosition = new Vector2(-50f, 8f);
            controlsRect.sizeDelta = new Vector2(-132f, 20f);
            ControlsText.alignment = TextAlignmentOptions.BottomLeft;
            ControlsText.color = new Color(0.94f, 0.95f, 0.87f);

            InfoCardPanel.gameObject.SetActive(false);
            InventoryPanel.gameObject.SetActive(false);
            MerchantShopPanel.gameObject.SetActive(false);
            InventoryTooltipPanel.gameObject.SetActive(false);

            ApplyLayout(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        private void ApplyLandscapeLayout()
        {
            ConfigureTextLayout(false);

            SetPanel(
                TopRoot,
                "TopPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, -100f),
                new Vector2(328f, 60f));

            SetPanel(
                OverlayRoot,
                "InfoButtonPanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -18f),
                new Vector2(336f, 52f));

            SetPanel(
                OverlayRoot,
                "RightPanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -76f),
                new Vector2(336f, 236f));

            SetPanel(
                OverlayRoot,
                "InventoryPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -110f),
                new Vector2(620f, 214f));

            SetPanel(
                OverlayRoot,
                "MerchantShopPanel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -38f),
                new Vector2(460f, 560f));

            SetPanel(
                BottomRoot,
                "BottomPanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(478f, LandscapeBottomPanelHeight));

            SetPanel(
                OverlayRoot,
                "DialoguePanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 182f),
                new Vector2(760f, 124f));

            ReflowLandscapeLayout();
        }

        private void ApplyPortraitLayout(Rect safeArea, Vector2Int screenSize)
        {
            ConfigureTextLayout(true);

            float canvasScale = ResolveCanvasScaleFactor(OverlayRoot, screenSize);
            float safeWidth = Mathf.Max(320f, safeArea.width / canvasScale);
            float safeHeight = Mathf.Max(480f, safeArea.height / canvasScale);
            float horizontalInset = Mathf.Min(PortraitHorizontalMargin, safeWidth * 0.04f);
            float panelWidth = Mathf.Clamp(safeWidth - (horizontalInset * 2f), PortraitMinPanelWidth, PortraitMaxPanelWidth);
            float topHeight = Mathf.Clamp(safeHeight * 0.045f, 50f, 62f);
            float infoBandHeight = Mathf.Clamp(safeHeight * 0.034f, 38f, 46f);
            float infoCardHeight = Mathf.Clamp(safeHeight * 0.115f, 118f, 148f);
            float inventoryHeight = Mathf.Clamp(safeHeight * 0.22f, 178f, 218f);
            float merchantHeight = Mathf.Min(Mathf.Clamp(safeHeight * 0.42f, 360f, 520f), Mathf.Max(360f, safeHeight - 96f));
            float bottomHeight = Mathf.Clamp(safeHeight * 0.08f, 108f, 128f);
            float dialogueHeight = Mathf.Clamp(safeHeight * 0.09f, 104f, 132f);

            SetPanel(
                TopRoot,
                "TopPanel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -100f),
                new Vector2(panelWidth, topHeight));

            SetPanel(
                OverlayRoot,
                "InfoButtonPanel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -(topHeight + infoBandHeight + 12f)),
                new Vector2(panelWidth, infoBandHeight));

            SetPanel(
                OverlayRoot,
                "RightPanel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -(topHeight + infoBandHeight + infoCardHeight + 20f)),
                new Vector2(panelWidth, infoCardHeight));

            SetPanel(
                OverlayRoot,
                "InventoryPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f),
                new Vector2(panelWidth, inventoryHeight));

            SetPanel(
                OverlayRoot,
                "MerchantShopPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f),
                new Vector2(panelWidth, merchantHeight));

            SetPanel(
                BottomRoot,
                "BottomPanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 12f),
                new Vector2(panelWidth, bottomHeight));

            SetPanel(
                OverlayRoot,
                "DialoguePanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, Mathf.Clamp(safeHeight * 0.08f, 112f, 150f)),
                new Vector2(panelWidth, dialogueHeight));

            ReflowPortraitLayout(panelWidth, infoBandHeight, inventoryHeight, merchantHeight, bottomHeight);
        }

        private void ReflowLandscapeLayout()
        {
            InfoCloseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            InventoryCloseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            MerchantShopCloseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            MerchantShopPrevButtonRect.anchoredPosition = new Vector2(16f, -508f);
            MerchantShopNextButtonRect.anchoredPosition = new Vector2(126f, -508f);
            InfoCardTitleText.rectTransform.sizeDelta = new Vector2(-68f, 22f);
            MessageText.rectTransform.offsetMin = new Vector2(18f, 16f);
            MessageText.rectTransform.offsetMax = new Vector2(-18f, -44f);
            InventoryTitleText.rectTransform.sizeDelta = new Vector2(-120f, 32f);
            MerchantShopTitleText.rectTransform.sizeDelta = new Vector2(-120f, 34f);
            MerchantShopPageText.rectTransform.sizeDelta = new Vector2(120f, 24f);
            MerchantShopHintText.rectTransform.sizeDelta = new Vector2(-120f, 48f);
            DialogueSpeakerText.rectTransform.sizeDelta = new Vector2(-28f, 32f);
            DialogueBodyText.rectTransform.offsetMin = new Vector2(16f, 16f);
            DialogueBodyText.rectTransform.offsetMax = new Vector2(-16f, -42f);
            LayoutLandscapeChildren();
        }

        private void ReflowPortraitLayout(float panelWidth, float infoBandHeight, float inventoryHeight, float merchantHeight, float bottomHeight)
        {
            InfoCloseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            InventoryCloseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            MerchantShopCloseButtonRect.anchoredPosition = new Vector2(-10f, -10f);

            InfoCardTitleText.rectTransform.sizeDelta = new Vector2(-44f, 22f);
            MessageText.rectTransform.offsetMin = new Vector2(14f, 14f);
            MessageText.rectTransform.offsetMax = new Vector2(-14f, -36f);
            InventoryTitleText.rectTransform.sizeDelta = new Vector2(-88f, 32f);
            MerchantShopTitleText.rectTransform.sizeDelta = new Vector2(-88f, 34f);
            MerchantShopPageText.rectTransform.sizeDelta = new Vector2(104f, 24f);
            MerchantShopHintText.rectTransform.sizeDelta = new Vector2(-88f, 56f);
            DialogueSpeakerText.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            DialogueSpeakerText.rectTransform.sizeDelta = new Vector2(-20f, 32f);
            DialogueBodyText.rectTransform.offsetMin = new Vector2(12f, 12f);
            DialogueBodyText.rectTransform.offsetMax = new Vector2(-12f, -28f);
            StatusText.fontSize = 13f;
            StatusText.fontSizeMin = 8f;
            DialogueSpeakerText.enableAutoSizing = false;
            DialogueSpeakerText.fontSize = PortraitDialogueSpeakerFontSize;
            DialogueBodyText.enableAutoSizing = false;
            DialogueBodyText.fontSize = PortraitDialogueBodyFontSize;

            LayoutPortraitChildren(panelWidth, infoBandHeight, inventoryHeight, merchantHeight, bottomHeight);
        }

        private void LayoutLandscapeChildren()
        {
            SetTopLeft(infoTabRow, new Vector2(12f, -10f), new Vector2(318f, 34f));
            for (int i = 0; i < InfoTabButtonRects.Length; i++)
            {
                SetTopLeft(InfoTabButtonRects[i], new Vector2(i * 64f, 0f), new Vector2(58f, 30f));
                SetTextFont(InfoTabButtonTexts[i], 15f, 11f);
            }

            if (backpackLabelRect != null)
            {
                SetTopLeft(backpackLabelRect, new Vector2(16f, -48f), new Vector2(272f, 18f));
            }

            SetTopLeft(backpackGrid, new Vector2(16f, -74f), new Vector2(584f, 62f));
            for (int i = 0; i < InventorySlotBindings.Length; i++)
            {
                LayoutInventorySlot(i, new Vector2(i * 72f, 0f), 66f);
            }

            SetTopLeft(merchantShopList, new Vector2(16f, -56f), new Vector2(428f, 444f));
            for (int i = 0; i < MerchantShopPageSize; i++)
            {
                SetTopLeft(MerchantShopItemButtonRects[i], new Vector2(0f, -(i * 42f)), new Vector2(428f, 36f));
                SetTextInsets(MerchantShopItemButtonTexts[i], new Vector2(12f, 0f), new Vector2(-12f, 0f));
                SetTextFont(MerchantShopItemButtonTexts[i], 16f, 12f);
            }

            MerchantShopPrevButtonRect.sizeDelta = new Vector2(96f, 34f);
            MerchantShopNextButtonRect.sizeDelta = new Vector2(96f, 34f);
            SetButtonLabelFont(MerchantShopPrevButtonRect, 16f, 12f);
            SetButtonLabelFont(MerchantShopNextButtonRect, 16f, 12f);

            SetTopCenter(toolbar, new Vector2(0f, -12f), new Vector2(428f, 48f));
            for (int i = 0; i < ToolButtonRects.Length; i++)
            {
                SetTopLeft(ToolButtonRects[i], new Vector2(i * 108f, 0f), new Vector2(98f, 46f));
                SetTextFont(ToolButtonTexts[i], 16f, 12f);
            }

            SetBottomRight(AdvanceDayButtonRect, new Vector2(-12f, 8f), new Vector2(LandscapeAdvanceDayButtonWidth, AdvanceDayButtonHeight));
            SetButtonLabelFont(AdvanceDayButtonRect, 16f, 11f);
            LayoutControlsTextForActionButton(LandscapeAdvanceDayButtonWidth);
        }

        private void LayoutPortraitChildren(float panelWidth, float infoBandHeight, float inventoryHeight, float merchantHeight, float bottomHeight)
        {
            float contentWidth = Mathf.Max(0f, panelWidth - 24f);
            float infoRowHeight = Mathf.Max(28f, infoBandHeight - 12f);
            SetTopLeft(infoTabRow, new Vector2(12f, -6f), new Vector2(contentWidth, infoRowHeight));
            LayoutEvenRow(InfoTabButtonRects, InfoTabButtonTexts, contentWidth, infoRowHeight, 4f, 12f, 8f);

            float toolbarWidth = Mathf.Max(0f, panelWidth - 24f);
            float toolbarHeight = Mathf.Clamp(bottomHeight - 40f, 40f, 50f);
            SetTopCenter(toolbar, new Vector2(0f, -10f), new Vector2(toolbarWidth, toolbarHeight));
            LayoutEvenRow(ToolButtonRects, ToolButtonTexts, toolbarWidth, toolbarHeight, 6f, 13f, 9f);
            float actionButtonWidth = Mathf.Clamp(panelWidth * 0.32f, PortraitAdvanceDayButtonMinWidth, PortraitAdvanceDayButtonMaxWidth);
            SetBottomRight(AdvanceDayButtonRect, new Vector2(-12f, 8f), new Vector2(actionButtonWidth, AdvanceDayButtonHeight));
            SetButtonLabelFont(AdvanceDayButtonRect, 15f, 10f);
            LayoutControlsTextForActionButton(actionButtonWidth);

            LayoutPortraitInventory(panelWidth, inventoryHeight);
            LayoutPortraitMerchantShop(panelWidth, merchantHeight);
        }

        private void LayoutPortraitInventory(float panelWidth, float inventoryHeight)
        {
            float contentWidth = Mathf.Max(0f, panelWidth - 32f);
            if (backpackLabelRect != null)
            {
                SetTopLeft(backpackLabelRect, new Vector2(16f, -48f), new Vector2(contentWidth, 18f));
            }

            float gap = 8f;
            float maxSlotFromWidth = (contentWidth - (gap * 3f)) / 4f;
            float maxSlotFromHeight = (inventoryHeight - 88f - gap) / 2f;
            float slotSize = Mathf.Floor(Mathf.Clamp(Mathf.Min(maxSlotFromWidth, maxSlotFromHeight), 48f, 66f));
            float gridWidth = (slotSize * 4f) + (gap * 3f);
            float gridHeight = (slotSize * 2f) + gap;
            float gridX = Mathf.Max(16f, (panelWidth - gridWidth) * 0.5f);

            SetTopLeft(backpackGrid, new Vector2(gridX, -74f), new Vector2(gridWidth, gridHeight));
            for (int i = 0; i < InventorySlotBindings.Length; i++)
            {
                int column = i % 4;
                int row = i / 4;
                LayoutInventorySlot(i, new Vector2(column * (slotSize + gap), -(row * (slotSize + gap))), slotSize);
            }
        }

        private void LayoutPortraitMerchantShop(float panelWidth, float merchantHeight)
        {
            float contentWidth = Mathf.Max(0f, panelWidth - 32f);
            float listTop = 54f;
            float bottomReserved = 100f;
            float listHeight = Mathf.Max(180f, merchantHeight - listTop - bottomReserved);
            float rowGap = 4f;
            float itemHeight = Mathf.Floor(Mathf.Clamp((listHeight - (rowGap * (MerchantShopPageSize - 1))) / MerchantShopPageSize, 20f, 34f));
            float actualListHeight = (itemHeight * MerchantShopPageSize) + (rowGap * (MerchantShopPageSize - 1));
            SetTopLeft(merchantShopList, new Vector2(16f, -listTop), new Vector2(contentWidth, actualListHeight));

            for (int i = 0; i < MerchantShopPageSize; i++)
            {
                SetTopLeft(MerchantShopItemButtonRects[i], new Vector2(0f, -(i * (itemHeight + rowGap))), new Vector2(contentWidth, itemHeight));
                SetTextInsets(MerchantShopItemButtonTexts[i], new Vector2(10f, 0f), new Vector2(-10f, 0f));
                SetTextFont(MerchantShopItemButtonTexts[i], 12f, 8f);
            }

            float buttonWidth = Mathf.Min(96f, (contentWidth - 12f) * 0.5f);
            float actionY = -(merchantHeight - 88f);
            SetTopLeft(MerchantShopPrevButtonRect, new Vector2(16f, actionY), new Vector2(buttonWidth, 34f));
            SetTopLeft(MerchantShopNextButtonRect, new Vector2(16f + buttonWidth + 12f, actionY), new Vector2(buttonWidth, 34f));
            SetButtonLabelFont(MerchantShopPrevButtonRect, 14f, 10f);
            SetButtonLabelFont(MerchantShopNextButtonRect, 14f, 10f);
        }

        private void LayoutEvenRow(RectTransform[] rects, TextMeshProUGUI[] texts, float rowWidth, float rowHeight, float gap, float maxFontSize, float minFontSize)
        {
            if (rects == null || rects.Length == 0)
            {
                return;
            }

            float itemWidth = Mathf.Max(1f, (rowWidth - (gap * (rects.Length - 1))) / rects.Length);
            for (int i = 0; i < rects.Length; i++)
            {
                SetTopLeft(rects[i], new Vector2(i * (itemWidth + gap), 0f), new Vector2(itemWidth, rowHeight));
                if (texts != null && i < texts.Length)
                {
                    SetTextFont(texts[i], maxFontSize, minFontSize);
                }
            }
        }

        private void LayoutInventorySlot(int index, Vector2 anchoredPosition, float slotSize)
        {
            if (index < 0 || index >= InventorySlotBindings.Length)
            {
                return;
            }

            FarmInventorySlotBinding binding = InventorySlotBindings[index];
            if (binding == null || binding.Rect == null)
            {
                return;
            }

            SetTopLeft(binding.Rect, anchoredPosition, new Vector2(slotSize, slotSize));

            RectTransform border = binding.Rect.Find("Border") as RectTransform;
            RectTransform inner = border != null ? border.Find("Inner") as RectTransform : null;
            if (border != null)
            {
                SetCentered(border, new Vector2(Mathf.Max(0f, slotSize - 4f), Mathf.Max(0f, slotSize - 4f)));
            }

            if (inner != null)
            {
                SetCentered(inner, new Vector2(Mathf.Max(0f, slotSize - 10f), Mathf.Max(0f, slotSize - 10f)));
            }

            if (binding.Icon != null)
            {
                float iconSize = Mathf.Clamp(slotSize * 0.44f, 22f, 28f);
                SetCentered(binding.Icon.rectTransform, new Vector2(iconSize, iconSize));
                binding.Icon.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            }

            SetTextFont(binding.Name, 10f, 7f);
            SetTextFont(binding.Count, 11f, 8f);
            if (binding.Name != null)
            {
                binding.Name.rectTransform.sizeDelta = new Vector2(-10f, 12f);
            }
        }

        private static void SetTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetTopCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetBottomRight(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void LayoutControlsTextForActionButton(float actionButtonWidth)
        {
            if (ControlsText == null)
            {
                return;
            }

            RectTransform controlsRect = ControlsText.rectTransform;
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(1f, 0f);
            controlsRect.pivot = new Vector2(0.5f, 0f);
            controlsRect.anchoredPosition = new Vector2(-(actionButtonWidth * 0.5f + 10f), 8f);
            controlsRect.sizeDelta = new Vector2(-(actionButtonWidth + 40f), 20f);
            ControlsText.alignment = TextAlignmentOptions.BottomLeft;
        }

        private static void SetCentered(RectTransform rect, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void SetTextInsets(TextMeshProUGUI text, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (text == null)
            {
                return;
            }

            text.rectTransform.offsetMin = offsetMin;
            text.rectTransform.offsetMax = offsetMax;
        }

        private static void SetTextFont(TextMeshProUGUI text, float maxFontSize, float minFontSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = maxFontSize;
            text.fontSizeMin = minFontSize;
        }

        private static void SetButtonLabelFont(RectTransform button, float maxFontSize, float minFontSize)
        {
            if (button == null)
            {
                return;
            }

            var label = button.Find("Label")?.GetComponent<TextMeshProUGUI>();
            SetTextFont(label, maxFontSize, minFontSize);
        }

        private void ConfigureTextLayout(bool portrait)
        {
            StatusText.rectTransform.anchorMin = new Vector2(0f, 1f);
            StatusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            StatusText.rectTransform.pivot = new Vector2(0.5f, 1f);
            StatusText.rectTransform.anchoredPosition = portrait ? new Vector2(0f, -10f) : new Vector2(0f, -12f);
            StatusText.rectTransform.sizeDelta = portrait ? new Vector2(-28f, 50f) : new Vector2(-32f, 50f);
            StatusText.enableWordWrapping = false;
            StatusText.overflowMode = TextOverflowModes.Ellipsis;
            StatusText.fontSize = portrait ? 10f : 20f;
            StatusText.fontSizeMin = portrait ? 8f : 14f;
            StatusText.color = new Color(0.96f, 0.96f, 0.92f);

            InfoCardTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            InfoCardTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            InfoCardTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            InfoCardTitleText.rectTransform.anchoredPosition = portrait ? new Vector2(0f, -14f) : new Vector2(0f, -18f);
            InfoCardTitleText.rectTransform.sizeDelta = portrait ? new Vector2(-44f, 22f) : new Vector2(-68f, 22f);
            InfoCardTitleText.fontSize = portrait ? 10f : 20f;
            InfoCardTitleText.fontSizeMin = portrait ? 8f : 14f;
            InfoCardTitleText.color = new Color(0.99f, 0.94f, 0.77f);

            MessageText.rectTransform.offsetMin = portrait ? new Vector2(12f, 12f) : new Vector2(18f, 16f);
            MessageText.rectTransform.offsetMax = portrait ? new Vector2(-12f, -24f) : new Vector2(-18f, -44f);
            MessageText.fontSize = portrait ? 9f : 20f;
            MessageText.fontSizeMin = portrait ? 8f : 15f;

            InventoryTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            InventoryTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            InventoryTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            InventoryTitleText.rectTransform.anchoredPosition = portrait ? new Vector2(14f, -10f) : new Vector2(14f, -12f);
            InventoryTitleText.rectTransform.sizeDelta = portrait ? new Vector2(-88f, 32f) : new Vector2(-120f, 32f);
            InventoryTitleText.fontSize = portrait ? 10f : 22f;
            InventoryTitleText.fontSizeMin = portrait ? 8f : 16f;
            InventoryTitleText.color = new Color(0.24f, 0.15f, 0.06f);

            MerchantShopTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            MerchantShopTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            MerchantShopTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            MerchantShopTitleText.rectTransform.anchoredPosition = portrait ? new Vector2(16f, -12f) : new Vector2(16f, -14f);
            MerchantShopTitleText.rectTransform.sizeDelta = portrait ? new Vector2(-88f, 34f) : new Vector2(-120f, 34f);
            MerchantShopTitleText.fontSize = portrait ? 11f : 24f;
            MerchantShopTitleText.fontSizeMin = portrait ? 8f : 18f;
            MerchantShopTitleText.color = new Color(0.99f, 0.93f, 0.72f);

            MerchantShopPageText.rectTransform.anchorMin = new Vector2(1f, 1f);
            MerchantShopPageText.rectTransform.anchorMax = new Vector2(1f, 1f);
            MerchantShopPageText.rectTransform.pivot = new Vector2(1f, 1f);
            MerchantShopPageText.rectTransform.anchoredPosition = portrait ? new Vector2(-42f, -14f) : new Vector2(-54f, -18f);
            MerchantShopPageText.rectTransform.sizeDelta = portrait ? new Vector2(104f, 24f) : new Vector2(120f, 24f);
            MerchantShopPageText.fontSize = portrait ? 9f : 16f;
            MerchantShopPageText.fontSizeMin = portrait ? 7f : 12f;
            MerchantShopPageText.color = new Color(0.95f, 0.9f, 0.8f);

            MerchantShopHintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            MerchantShopHintText.rectTransform.anchorMax = new Vector2(1f, 0f);
            MerchantShopHintText.rectTransform.pivot = new Vector2(0f, 0f);
            MerchantShopHintText.rectTransform.anchoredPosition = portrait ? new Vector2(16f, 12f) : new Vector2(16f, 12f);
            MerchantShopHintText.rectTransform.sizeDelta = portrait ? new Vector2(-88f, 56f) : new Vector2(-120f, 48f);
            MerchantShopHintText.fontSize = portrait ? 9f : 14f;
            MerchantShopHintText.fontSizeMin = portrait ? 7f : 11f;
            MerchantShopHintText.color = new Color(0.88f, 0.9f, 0.86f);

            DialogueSpeakerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            DialogueSpeakerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            DialogueSpeakerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            DialogueSpeakerText.rectTransform.anchoredPosition = portrait ? new Vector2(0f, -12f) : new Vector2(0f, -14f);
            DialogueSpeakerText.rectTransform.sizeDelta = portrait ? new Vector2(-24f, 32f) : new Vector2(-28f, 32f);
            DialogueSpeakerText.enableAutoSizing = false;
            DialogueSpeakerText.fontSize = DialogueSpeakerFontSize;
            DialogueSpeakerText.color = new Color(0.99f, 0.9f, 0.63f);

            DialogueBodyText.rectTransform.offsetMin = portrait ? new Vector2(12f, 12f) : new Vector2(16f, 16f);
            DialogueBodyText.rectTransform.offsetMax = portrait ? new Vector2(-12f, -16f) : new Vector2(-16f, -42f);
            DialogueBodyText.enableAutoSizing = false;
            DialogueBodyText.fontSize = DialogueBodyFontSize;
        }

        private void CreateToolButton(Transform parent, int index, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject("ToolButton_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(index * 108f, 0f);
            buttonRect.sizeDelta = new Vector2(98f, 46f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.31f, 0.35f, 0.28f, 0.94f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 16f, 12f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.94f, 0.95f, 0.9f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            ToolButtonRects[index] = buttonRect;
            ToolButtonImages[index] = image;
            ToolButtonTexts[index] = text;
        }

        private RectTransform CreateHudActionButton(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.62f, 0.5f, 0.28f, 0.96f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 14f, 10f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.98f, 0.95f, 0.86f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return buttonRect;
        }

        private void CreateInfoTabButton(Transform parent, int index, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject("InfoTabButton_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(index * 64f, 0f);
            buttonRect.sizeDelta = new Vector2(58f, 30f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.33f, 0.24f, 0.14f, 0.94f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 15f, 11f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.93f, 0.9f, 0.82f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            InfoTabButtonRects[index] = buttonRect;
            InfoTabButtonImages[index] = image;
            InfoTabButtonTexts[index] = text;
        }

        private RectTransform CreateCloseButton(Transform parent, string objectName)
        {
            RectTransform buttonRect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-10f, -10f);
            buttonRect.sizeDelta = new Vector2(30f, 30f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.56f, 0.24f, 0.18f, 0.95f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 20f, 14f, TextAlignmentOptions.Center);
            text.text = "×";
            text.color = new Color(0.98f, 0.95f, 0.91f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return buttonRect;
        }

        private RectTransform CreateMerchantShopActionButton(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, string textKey, string fallbackLabel)
        {
            RectTransform buttonRect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.35f, 0.28f, 0.16f, 0.96f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 16f, 12f, TextAlignmentOptions.Center);
            text.text = UiTextCatalog.GetOrFallback(textKey, fallbackLabel);
            text.color = new Color(0.95f, 0.92f, 0.86f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            return buttonRect;
        }

        private void CreateMerchantShopItemButton(Transform parent, int index)
        {
            RectTransform buttonRect = new GameObject("MerchantShopItem_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -(index * 42f));
            buttonRect.sizeDelta = new Vector2(428f, 36f);

            Image image = buttonRect.GetComponent<Image>();
            image.color = (index % 2 == 0)
                ? new Color(0.2f, 0.16f, 0.1f, 0.82f)
                : new Color(0.24f, 0.18f, 0.12f, 0.82f);

            TextMeshProUGUI text = CreateText(buttonRect, "Label", 16f, 12f, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            text.color = new Color(0.95f, 0.93f, 0.89f);

            MerchantShopItemButtonRects[index] = buttonRect;
            MerchantShopItemButtonImages[index] = image;
            MerchantShopItemButtonTexts[index] = text;
        }

        private TextMeshProUGUI CreateSectionLabel(Transform parent, string objectName, string label, Vector2 anchoredPosition)
        {
            RectTransform rect = CreateRectOnly(objectName, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(272f, 18f);

            TextMeshProUGUI text = CreateText(rect, "Label", 15f, 11f, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            text.color = new Color(0.34f, 0.22f, 0.08f);
            return text;
        }

        private void BuildInventoryDragGhost(Transform parent)
        {
            DragGhostRect = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            DragGhostRect.SetParent(parent, false);
            DragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
            DragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
            DragGhostRect.pivot = new Vector2(0.5f, 0.5f);
            DragGhostRect.sizeDelta = new Vector2(66f, 66f);
            DragGhostRect.gameObject.SetActive(false);

            DragGhostBackground = DragGhostRect.GetComponent<Image>();
            DragGhostBackground.color = new Color(0.1f, 0.12f, 0.12f, 0.94f);

            RectTransform iconRect = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconRect.SetParent(DragGhostRect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 2f);
            iconRect.sizeDelta = new Vector2(28f, 28f);

            DragGhostIcon = iconRect.GetComponent<Image>();
            DragGhostIcon.preserveAspect = true;

            DragGhostCountText = CreateText(DragGhostRect, "Count", 13f, 10f, TextAlignmentOptions.BottomRight);
            DragGhostCountText.rectTransform.offsetMin = new Vector2(6f, 6f);
            DragGhostCountText.rectTransform.offsetMax = new Vector2(-6f, -6f);
            DragGhostCountText.color = new Color(1f, 0.98f, 0.92f);
        }

        private void BuildInventoryTooltip(Transform parent)
        {
            InventoryTooltipPanel = new GameObject("InventoryTooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            InventoryTooltipPanel.SetParent(parent, false);
            InventoryTooltipPanel.anchorMin = new Vector2(0.5f, 0.5f);
            InventoryTooltipPanel.anchorMax = new Vector2(0.5f, 0.5f);
            InventoryTooltipPanel.pivot = new Vector2(0f, 1f);
            InventoryTooltipPanel.sizeDelta = new Vector2(240f, 90f);
            InventoryTooltipPanel.gameObject.SetActive(false);

            Image panelImage = InventoryTooltipPanel.GetComponent<Image>();
            panelImage.color = new Color(0.97f, 0.91f, 0.75f, 0.98f);

            InventoryTooltipTitleText = CreateText(InventoryTooltipPanel, "Title", 16f, 12f, TextAlignmentOptions.TopLeft);
            InventoryTooltipTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            InventoryTooltipTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            InventoryTooltipTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            InventoryTooltipTitleText.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            InventoryTooltipTitleText.rectTransform.sizeDelta = new Vector2(-20f, 24f);
            InventoryTooltipTitleText.color = new Color(0.2f, 0.12f, 0.04f);
            InventoryTooltipTitleText.enableWordWrapping = false;
            InventoryTooltipTitleText.overflowMode = TextOverflowModes.Ellipsis;

            InventoryTooltipBodyText = CreateText(InventoryTooltipPanel, "Body", 13f, 10f, TextAlignmentOptions.TopLeft);
            InventoryTooltipBodyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            InventoryTooltipBodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            InventoryTooltipBodyText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            InventoryTooltipBodyText.rectTransform.offsetMin = new Vector2(10f, 8f);
            InventoryTooltipBodyText.rectTransform.offsetMax = new Vector2(-10f, -34f);
            InventoryTooltipBodyText.enableWordWrapping = true;
            InventoryTooltipBodyText.color = new Color(0.28f, 0.18f, 0.08f);
        }

        private void CreateInventorySlot(
            Transform parent,
            int index,
            FarmInventoryContainerType containerType,
            int slotIndex,
            Vector2 anchoredPosition,
            Color accentColor,
            Sprite emptySprite)
        {
            RectTransform slotRect = new GameObject("InventorySlot_" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0f, 1f);
            slotRect.anchorMax = new Vector2(0f, 1f);
            slotRect.pivot = new Vector2(0f, 1f);
            slotRect.anchoredPosition = anchoredPosition;
            slotRect.sizeDelta = new Vector2(66f, 66f);

            Image slotBackground = slotRect.GetComponent<Image>();
            slotBackground.color = new Color(0.9f, 0.78f, 0.58f, 0.98f);

            RectTransform borderRect = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            borderRect.SetParent(slotRect, false);
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(62f, 62f);
            borderRect.GetComponent<Image>().color = new Color(0.45f, 0.3f, 0.14f, 0.95f);

            RectTransform innerRect = new GameObject("Inner", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            innerRect.SetParent(borderRect, false);
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(56f, 56f);
            innerRect.GetComponent<Image>().color = new Color(0.96f, 0.88f, 0.72f, 0.98f);

            Image fillImage = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.SetParent(innerRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 0f);
            fillRect.pivot = new Vector2(0f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            fillImage.color = new Color(0f, 0f, 0f, 0f);

            RectTransform accentRect = new GameObject("Accent", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            accentRect.SetParent(innerRect, false);
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 3f);
            accentRect.GetComponent<Image>().color = accentColor;

            TextMeshProUGUI nameText = CreateText(innerRect, "Name", 10f, 8f, TextAlignmentOptions.TopLeft);
            nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
            nameText.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            nameText.rectTransform.sizeDelta = new Vector2(-10f, 12f);

            RectTransform iconRect = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconRect.SetParent(innerRect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, -2f);
            iconRect.sizeDelta = new Vector2(28f, 28f);

            Image iconImage = iconRect.GetComponent<Image>();
            iconImage.sprite = emptySprite;
            iconImage.preserveAspect = true;
            iconImage.color = new Color(1f, 1f, 1f, 0f);

            TextMeshProUGUI countText = CreateText(innerRect, "Count", 11f, 9f, TextAlignmentOptions.BottomRight);
            countText.rectTransform.offsetMin = new Vector2(6f, 4f);
            countText.rectTransform.offsetMax = new Vector2(-4f, -22f);

            InventorySlotBindings[index] = new FarmInventorySlotBinding
            {
                Rect = slotRect,
                Background = slotBackground,
                Fill = fillImage,
                Icon = iconImage,
                Name = nameText,
                Count = countText,
                ContainerType = containerType,
                SlotIndex = slotIndex,
                EmptyAccentColor = accentColor,
                EmptySprite = emptySprite
            };
        }

        private static RectTransform CreateStretchRoot(string objectName, Transform parent)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void ApplySafeArea(RectTransform root, Rect safeArea, Vector2Int screenSize)
        {
            if (root == null || screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            float width = Mathf.Max(1f, screenSize.x);
            float height = Mathf.Max(1f, screenSize.y);
            root.anchorMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
            root.anchorMax = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        private static float ResolveCanvasScaleFactor(RectTransform root, Vector2Int screenSize)
        {
            var scaler = root != null ? root.GetComponentInParent<CanvasScaler>() : null;
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                return 1f;
            }

            Vector2 reference = scaler.referenceResolution;
            if (reference.x <= 0f || reference.y <= 0f || screenSize.x <= 0 || screenSize.y <= 0)
            {
                return 1f;
            }

            float widthScale = Mathf.Max(0.0001f, screenSize.x / reference.x);
            float heightScale = Mathf.Max(0.0001f, screenSize.y / reference.y);
            if (scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand)
            {
                return Mathf.Min(widthScale, heightScale);
            }

            if (scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink)
            {
                return Mathf.Max(widthScale, heightScale);
            }

            float logWidth = Mathf.Log(widthScale, 2f);
            float logHeight = Mathf.Log(heightScale, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
        }

        private static void SetPanel(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform target = parent.Find(objectName) as RectTransform;
            if (target == null)
            {
                return;
            }

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = sizeDelta;
        }

        private static RectTransform CreateRectOnly(string objectName, Transform parent)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = rect.GetComponent<Image>();
            image.color = color;
            AddPanelTrim(rect);
            return rect;
        }

        private static void AddPanelTrim(RectTransform panel)
        {
            CreatePanelEdge(panel, "TopTrim", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 6f), new Color(0.95f, 0.85f, 0.56f, 0.72f));
            CreatePanelEdge(panel, "BottomTrim", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 2f), new Color(0.12f, 0.08f, 0.04f, 0.56f));
            CreatePanelEdge(panel, "LeftTrim", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(2f, 0f), new Color(0.97f, 0.9f, 0.7f, 0.28f));
            CreatePanelEdge(panel, "RightTrim", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(2f, 0f), new Color(0.1f, 0.07f, 0.03f, 0.42f));
        }

        private static void CreatePanelEdge(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.GetComponent<Image>().color = color;
        }

        private TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            float maxFontSize,
            float minFontSize,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, -16f);

            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.font = fontAsset != null ? fontAsset : MiniGameFontProvider.DefaultFont;
            text.fontSize = maxFontSize;
            text.fontSizeMin = minFontSize;
            text.enableAutoSizing = true;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
            text.color = new Color(0.95f, 0.94f, 0.91f);
            return text;
        }
    }
}
