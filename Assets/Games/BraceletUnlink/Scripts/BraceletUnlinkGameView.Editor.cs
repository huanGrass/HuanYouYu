using System.IO;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class BraceletUnlinkGameView
    {
        private const int EditorCatalogFormatVersion = 8;
        private const int CampaignCatalogFormatVersion = 3;
        private const string LegacyEditorSaveKey = "bracelet-unlink.editor.level.v1";
        private const string EditorSaveDirectoryName = "BraceletUnlink";
        private const string EditorSaveFileName = "bracelet-unlink-level.json";
        private const float EditorDefaultBoardScale = 0.68f;
        private const float EditorMinBoardScale = 0.50f;
        private const float EditorMaxBoardScale = 2.35f;
        private const float EditorPanViewportPaddingRatio = 0.50f;

        private static readonly int[][] EditorBoardLayouts =
        {
            new[] { 2 },
            new[] { 2, 1 },
            new[] { 3, 2, 3 },
            new[] { 4, 3, 4, 3 },
            new[] { 5, 4, 5, 4, 5 },
            new[] { 6, 5, 6, 5, 6 }
        };

        private RectTransform editorOverlay;
        private RectTransform editorBoardViewport;
        private RectTransform editorHelpers;
        private RectTransform editorContextBar;
        private TextMeshProUGUI editorStatus;
        private TextMeshProUGUI editorLevelLabel;
        private Button editorEntryButton;
        private Button returnEditorButton;
        private Button editorZoomOutButton;
        private Button editorZoomInButton;
        private Slider editorZoomSlider;
        private readonly Button[] editorContextButtons = new Button[4];
        private bool isEditing;
        private int selectedSlot = -1;
        private int selectedEdge = -1;
        private bool isChoosingAttachedOwner;
        private bool hasAttemptedEditorAutoLoad;
        private readonly List<BraceletUnlinkLevelData> editorLevels = new List<BraceletUnlinkLevelData>();
        private int editorLevelIndex;
        private float editorBoardScale = EditorDefaultBoardScale;
        private Vector2 editorBoardPan;
        private Vector2 editorBoardBasePosition;
        private Transform editorBoardOriginalParent;
        private int editorBoardOriginalSiblingIndex;
        private static string editorSavePathOverride;

        private void BuildEditorEntry(Transform actionBar)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            editorEntryButton = MiniGameShellBottomBarBuilder.CreateTextActionButton(
                actionBar, "BraceletLevelEditorButton", EditorText("bracelet-unlink.editor.entry"), 100f, 72f, 22f);
            editorEntryButton.onClick.AddListener(OpenLevelEditor);

            // 暂时保留开发编辑器及其自动化入口，但不让它占用正常游戏底栏。
            var layout = editorEntryButton.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = editorEntryButton.gameObject.AddComponent<LayoutElement>();
            }
            layout.ignoreLayout = true;
            editorEntryButton.interactable = false;
            editorEntryButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var canvasGroup = editorEntryButton.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            var rect = editorEntryButton.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.zero;
#endif
        }

        private void OpenLevelEditor()
        {
            isEditing = true;
            isCompleted = false;
            isChoosingAttachedOwner = false;
            if (!hasAttemptedEditorAutoLoad)
            {
                hasAttemptedEditorAutoLoad = true;
                BraceletUnlinkEditorCatalog savedCatalog;
                if (TryReadSavedEditorCatalog(out savedCatalog))
                {
                    LoadEditorCatalog(savedCatalog);
                    RebuildLevelObjects();
                    ResetGame();
                }
                else
                {
                    InitializeEditorLevelsFromRuntime();
                }
            }
            if (editorOverlay == null)
            {
                BuildEditorOverlay();
            }

            AttachBoardToEditorViewport();
            SetEditorPreviewScale(true);
            hintLabel.gameObject.SetActive(false);
            if (returnEditorButton != null)
            {
                returnEditorButton.gameObject.SetActive(false);
            }
            editorOverlay.gameObject.SetActive(true);
            ClearEditorSelection();
        }

        private void CloseLevelEditorForTrial()
        {
            StoreCurrentEditorLevel();
            CloseEditorOverlay();
            if (returnEditorButton != null)
            {
                returnEditorButton.gameObject.SetActive(true);
            }
        }

        private void CloseLevelEditor()
        {
            CloseEditorOverlay();
        }

        private void CloseEditorOverlay()
        {
            isEditing = false;
            isChoosingAttachedOwner = false;
            RestoreBoardParent();
            SetEditorPreviewScale(false);
            hintLabel.gameObject.SetActive(true);
            if (editorOverlay != null)
            {
                editorOverlay.gameObject.SetActive(false);
            }
            RebuildLevelObjects();
            ResetGame();
        }

        private void ReturnToLevelEditor()
        {
            OpenLevelEditor();
        }

        private void BuildEditorOverlay()
        {
            editorOverlay = CreateLayer("BraceletLevelEditorOverlay", boardRoot.parent);
            Stretch(editorOverlay);
            editorOverlay.SetAsLastSibling();

            var title = CreateText("EditorTitle", editorOverlay, 25f, new Color32(61, 82, 52, 255));
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.text = EditorText("bracelet-unlink.editor.title");
            SetEditorTextRect(title.rectTransform, new Vector2(0f, 320f), new Vector2(620f, 38f));

            editorLevelLabel = CreateText("EditorLevel", editorOverlay, 18f, new Color32(61, 82, 52, 255));
            editorLevelLabel.fontStyle = FontStyles.Bold;
            editorLevelLabel.alignment = TextAlignmentOptions.Center;
            SetEditorTextRect(editorLevelLabel.rectTransform, new Vector2(0f, 290f), new Vector2(300f, 22f));

            editorStatus = CreateText("EditorStatus", editorOverlay, 19f, new Color32(103, 80, 66, 255));
            editorStatus.alignment = TextAlignmentOptions.Center;
            SetEditorTextRect(editorStatus.rectTransform, new Vector2(0f, 266f), new Vector2(640f, 22f));

            var viewActions = CreateEditorToolbar("EditorViewActions", editorOverlay, new Vector2(0f, 225f), 8f);
            CreateEditorButton(viewActions, "BoardSmaller", "bracelet-unlink.editor.action.board_smaller", 92f).onClick.AddListener(() => ChangeEditorBoardSize(-1));
            CreateEditorButton(viewActions, "BoardLarger", "bracelet-unlink.editor.action.board_larger", 92f).onClick.AddListener(() => ChangeEditorBoardSize(1));
            CreateEditorButton(viewActions, "Clear", "bracelet-unlink.editor.action.clear", 88f).onClick.AddListener(ClearEditedLevel);
            CreateEditorZoomControl(viewActions);

            editorContextBar = CreateEditorToolbar("EditorContextActions", editorOverlay, new Vector2(0f, -286f), 8f);
            for (var i = 0; i < editorContextButtons.Length; i++)
            {
                editorContextButtons[i] = CreateEditorButton(editorContextBar, "ContextAction" + i, string.Empty, 138f);
            }

            var globalActions = CreateEditorToolbar("EditorGlobalActions", editorOverlay, new Vector2(0f, -342f), 6f);
            CreateEditorButton(globalActions, "PreviousLevel", "bracelet-unlink.editor.action.previous_level", 82f).onClick.AddListener(() => ChangeEditorLevel(-1));
            CreateEditorButton(globalActions, "NewLevel", "bracelet-unlink.editor.action.new_level", 82f).onClick.AddListener(AddEditorLevel);
            CreateEditorButton(globalActions, "DeleteLevel", "bracelet-unlink.editor.action.delete_level", 82f).onClick.AddListener(DeleteEditorLevel);
            CreateEditorButton(globalActions, "NextLevel", "bracelet-unlink.editor.action.next_level", 82f).onClick.AddListener(() => ChangeEditorLevel(1));
            CreateEditorButton(globalActions, "Save", "bracelet-unlink.editor.action.save", 82f).onClick.AddListener(SaveEditedLevel);
            CreateEditorButton(globalActions, "Trial", "bracelet-unlink.editor.action.trial", 82f).onClick.AddListener(CloseLevelEditorForTrial);
            CreateEditorButton(globalActions, "Close", "bracelet-unlink.editor.action.close", 82f).onClick.AddListener(CloseLevelEditor);

            editorBoardBasePosition = boardRoot.anchoredPosition;
            editorBoardOriginalParent = boardRoot.parent;
            editorBoardOriginalSiblingIndex = boardRoot.GetSiblingIndex();
            var viewportObject = new GameObject(
                "BraceletEditorBoardViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(RectMask2D),
                typeof(BraceletEditorBoardDragHandler));
            editorBoardViewport = viewportObject.GetComponent<RectTransform>();
            editorBoardViewport.SetParent(editorOverlay, false);
            editorBoardViewport.anchorMin = editorBoardViewport.anchorMax = editorBoardViewport.pivot = new Vector2(0.5f, 0.5f);
            editorBoardViewport.sizeDelta = new Vector2(660f, 480f);
            editorBoardViewport.anchoredPosition = editorBoardBasePosition;
            var viewportGraphic = viewportObject.GetComponent<RoundedRectGraphic>();
            viewportGraphic.CornerRadius = 8f;
            viewportGraphic.color = new Color(1f, 1f, 1f, 0.001f);
            viewportGraphic.raycastTarget = true;
            viewportObject.GetComponent<BraceletEditorBoardDragHandler>().OnDragDelta = OnEditorBoardDragged;
            editorBoardViewport.SetAsFirstSibling();

            editorHelpers = CreateLayer("EditorHelpers", editorBoardViewport);
            editorHelpers.anchorMin = editorHelpers.anchorMax = editorHelpers.pivot = new Vector2(0.5f, 0.5f);
            editorHelpers.sizeDelta = boardRoot.sizeDelta;
            editorHelpers.anchoredPosition = Vector2.zero;
            editorHelpers.localScale = Vector3.one * editorBoardScale;

            returnEditorButton = MiniGameShellBottomBarBuilder.CreateTextActionButton(
                boardRoot.parent,
                "BraceletReturnEditorButton",
                EditorText("bracelet-unlink.editor.action.return"),
                150f,
                48f,
                18f,
                14f);
            var returnRect = returnEditorButton.GetComponent<RectTransform>();
            returnRect.anchorMin = returnRect.anchorMax = returnRect.pivot = new Vector2(1f, 1f);
            returnRect.anchoredPosition = new Vector2(-18f, -18f);
            returnEditorButton.onClick.AddListener(ReturnToLevelEditor);
            returnEditorButton.gameObject.SetActive(false);
        }

        private static void SetEditorTextRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static RectTransform CreateEditorToolbar(string name, Transform parent, Vector2 position, float spacing)
        {
            var root = CreateLayer(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(620f, 48f);
            root.anchoredPosition = position;
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return root;
        }

        private static Button CreateEditorButton(Transform parent, string name, string textKey, float width)
        {
            var label = string.IsNullOrEmpty(textKey) ? string.Empty : EditorText(textKey);
            return MiniGameShellBottomBarBuilder.CreateTextActionButton(
                parent, "BraceletEditor" + name + "Button", label, width, 44f, 17f, 13f);
        }

        private void CreateEditorZoomControl(Transform parent)
        {
            var rootObject = new GameObject(
                "BraceletEditorZoomControl",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(LayoutElement));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(330f, 52f);
            var rootLayout = rootObject.GetComponent<LayoutElement>();
            rootLayout.preferredWidth = 330f;
            rootLayout.preferredHeight = 52f;
            var background = rootObject.GetComponent<RoundedRectGraphic>();
            background.color = new Color32(248, 251, 255, 245);
            background.CornerRadius = 18f;
            background.raycastTarget = false;

            var layout = rootObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            editorZoomOutButton = CreateEditorZoomIconButton("BraceletEditorZoomOutButton", root, false);
            editorZoomSlider = CreateEditorZoomSlider(root);
            editorZoomInButton = CreateEditorZoomIconButton("BraceletEditorZoomInButton", root, true);
            editorZoomSlider.SetValueWithoutNotify(Mathf.InverseLerp(
                EditorMinBoardScale,
                EditorMaxBoardScale,
                editorBoardScale));
            editorZoomSlider.onValueChanged.AddListener(OnEditorZoomSliderChanged);
            editorZoomOutButton.onClick.AddListener(OnEditorZoomOutClicked);
            editorZoomInButton.onClick.AddListener(OnEditorZoomInClicked);
        }

        private static Button CreateEditorZoomIconButton(string name, Transform parent, bool isPlus)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Button),
                typeof(LayoutElement),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(38f, 38f);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 38f;
            layout.preferredHeight = 38f;
            var background = buttonObject.GetComponent<RoundedRectGraphic>();
            background.color = Color.white;
            background.CornerRadius = 19f;
            background.raycastTarget = true;

            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArrowEscapeZoomIconGraphic));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(30f, 30f);
            var icon = iconObject.GetComponent<ArrowEscapeZoomIconGraphic>();
            icon.IsPlus = isPlus;
            icon.color = new Color32(73, 99, 138, 255);
            icon.raycastTarget = false;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.85f);
            return button;
        }

        private static Slider CreateEditorZoomSlider(Transform parent)
        {
            var sliderObject = new GameObject(
                "BraceletEditorZoomSlider",
                typeof(RectTransform),
                typeof(Slider),
                typeof(LayoutElement));
            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.SetParent(parent, false);
            sliderRect.sizeDelta = new Vector2(214f, 38f);
            var layout = sliderObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 214f;
            layout.preferredHeight = 38f;

            var backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.SetParent(sliderRect, false);
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.offsetMin = new Vector2(0f, -4f);
            backgroundRect.offsetMax = new Vector2(0f, 4f);
            var sliderBackground = backgroundObject.GetComponent<RoundedRectGraphic>();
            sliderBackground.color = new Color32(148, 148, 148, 255);
            sliderBackground.CornerRadius = 4f;
            sliderBackground.raycastTarget = false;

            var fillArea = CreateLayer("Fill Area", sliderRect);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(0f, 15f);
            fillArea.offsetMax = new Vector2(0f, -15f);
            var fillObject = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(fillArea, false);
            Stretch(fillRect);
            var fill = fillObject.GetComponent<RoundedRectGraphic>();
            fill.color = new Color32(28, 219, 99, 255);
            fill.CornerRadius = 4f;
            fill.raycastTarget = false;

            var handleArea = CreateLayer("Handle Slide Area", sliderRect);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.offsetMin = new Vector2(0f, -16f);
            handleArea.offsetMax = new Vector2(0f, 16f);
            var handleObject = new GameObject(
                "Handle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.SetParent(handleArea, false);
            handleRect.anchorMin = handleRect.anchorMax = handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(32f, 0f);
            var handle = handleObject.GetComponent<RoundedRectGraphic>();
            handle.color = new Color32(64, 166, 230, 255);
            handle.CornerRadius = 16f;
            handle.raycastTarget = true;

            var slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            return slider;
        }

        private void ClearEditorSelection()
        {
            selectedSlot = -1;
            selectedEdge = -1;
            isChoosingAttachedOwner = false;
            SetEditorStatus("bracelet-unlink.editor.status.select");
            HideContextActions();
            RefreshEditorHelpers();
        }

        private void RefreshEditorHelpers()
        {
            if (editorHelpers == null)
            {
                return;
            }

            ClearLayer(editorHelpers);
            RefreshEditorLevelLabel();
            for (var i = 0; i < levelData.Edges.Length; i++)
            {
                CreateEdgeEditorTarget(i, levelData.Edges[i]);
            }
            for (var slot = 0; slot < levelData.SlotCount; slot++)
            {
                CreateRingEditorTarget(slot);
            }
        }

        private void CreateRingEditorTarget(int slot)
        {
            var data = levelData.Rings[slot];
            var buttonObject = new GameObject(
                "RingSlot_" + slot, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(editorHelpers, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(108f, 108f);
            rect.anchoredPosition = levelData.GetSlotPosition(slot);

            var isOwnerChoice = isChoosingAttachedOwner
                && selectedEdge >= 0
                && (levelData.Edges[selectedEdge].SlotA == slot || levelData.Edges[selectedEdge].SlotB == slot);
            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.CornerRadius = 54f;
            var isSelected = slot == selectedSlot || isOwnerChoice;
            if (data.Kind == BraceletRingKind.Empty)
            {
                graphic.color = new Color(0.28f, 0.48f, 0.29f, isSelected ? 0.24f : 0.12f);
            }
            else if (isSelected)
            {
                graphic.color = new Color(1f, 0.66f, 0.12f, isOwnerChoice ? 0.46f : 0.28f);
            }
            else
            {
                graphic.color = new Color(1f, 1f, 1f, 0.01f);
            }
            AddRingEditorMarker(
                rect,
                isSelected ? new Color32(244, 154, 39, 245) : new Color32(73, 127, 70, 205),
                isSelected);

            var capturedSlot = slot;
            buttonObject.GetComponent<Button>().onClick.AddListener(() => SelectRingSlot(capturedSlot));
            if (data.Kind == BraceletRingKind.Empty)
            {
                var label = CreateText("EmptySlotPlus", rect, 38f, new Color32(83, 116, 73, 180));
                label.alignment = TextAlignmentOptions.Center;
                Stretch(label.rectTransform);
                label.text = "+";
            }
        }

        private void CreateEdgeEditorTarget(int edgeIndex, BraceletEdgeSlotData edge)
        {
            var first = levelData.GetSlotPosition(edge.SlotA);
            var second = levelData.GetSlotPosition(edge.SlotB);
            var buttonObject = new GameObject(
                "EdgeSlot_" + edge.SlotA + "_" + edge.SlotB,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(editorHelpers, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var hasBothRings = levelData.Rings[edge.SlotA].Kind != BraceletRingKind.Empty
                && levelData.Rings[edge.SlotB].Kind != BraceletRingKind.Empty;
            rect.sizeDelta = edge.Kind == BraceletEdgeKind.Empty ? new Vector2(52f, 28f) : new Vector2(76f, 48f);
            rect.anchoredPosition = (first + second) * 0.5f;

            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.CornerRadius = rect.sizeDelta.y * 0.5f;
            var isSelected = edgeIndex == selectedEdge;
            graphic.color = isSelected
                ? new Color(1f, 0.65f, 0.12f, 0.40f)
                : edge.Kind == BraceletEdgeKind.Empty && hasBothRings
                    ? new Color(0.27f, 0.43f, 0.25f, 0.18f)
                    : new Color(1f, 1f, 1f, 0.01f);

            var capturedEdge = edgeIndex;
            var button = buttonObject.GetComponent<Button>();
            button.interactable = hasBothRings;
            button.onClick.AddListener(() => SelectEdgeSlot(capturedEdge));
            if (hasBothRings)
            {
                AddEdgeEditorMarker(
                    rect,
                    isSelected ? new Color32(244, 154, 39, 245) : new Color32(73, 127, 70, 220),
                    isSelected);
            }
            if (edge.Kind == BraceletEdgeKind.Empty && hasBothRings)
            {
                var plus = CreateText("EmptyEdgePlus", rect, 22f, new Color32(65, 107, 61, 230));
                plus.alignment = TextAlignmentOptions.Center;
                Stretch(plus.rectTransform);
                plus.text = "+";
            }
        }

        private static void AddRingEditorMarker(RectTransform target, Color color, bool isSelected)
        {
            CreateEditorMarker(target, color, target.sizeDelta.y * 0.5f, isSelected ? 3.5f : 2f);
        }

        private static void AddEdgeEditorMarker(RectTransform target, Color color, bool isSelected)
        {
            CreateEditorMarker(target, color, target.sizeDelta.y * 0.5f, isSelected ? 3f : 2f);
        }

        private static void CreateEditorMarker(RectTransform parent, Color color, float cornerRadius, float strokeWidth)
        {
            var marker = new GameObject(
                "EditableMarker",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(BraceletEditorTargetMarkerGraphic));
            var rect = marker.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            var graphic = marker.GetComponent<BraceletEditorTargetMarkerGraphic>();
            graphic.CornerRadius = cornerRadius;
            graphic.StrokeWidth = strokeWidth;
            graphic.color = color;
            graphic.raycastTarget = false;
        }

        private void SelectRingSlot(int slot)
        {
            if (isChoosingAttachedOwner)
            {
                ApplyAttachedOwner(slot);
                return;
            }

            selectedSlot = slot;
            selectedEdge = -1;
            var ring = levelData.Rings[slot];
            if (ring.Kind == BraceletRingKind.Empty)
            {
                SetEditorStatus("bracelet-unlink.editor.status.empty_ring");
                ShowContextActions(
                    new ContextAction("bracelet-unlink.editor.action.add_open", () => SetRingKind(slot, BraceletRingKind.Open)),
                    new ContextAction("bracelet-unlink.editor.action.add_closed", () => SetRingKind(slot, BraceletRingKind.Closed)),
                    ContextAction.None,
                    new ContextAction("bracelet-unlink.editor.action.cancel", ClearEditorSelection));
            }
            else if (ring.Kind == BraceletRingKind.Open)
            {
                SetEditorStatus("bracelet-unlink.editor.status.open_ring");
                ShowContextActions(
                    new ContextAction("bracelet-unlink.editor.action.rotate_left", () => RotateRing(slot, -15f)),
                    new ContextAction("bracelet-unlink.editor.action.rotate_right", () => RotateRing(slot, 15f)),
                    new ContextAction("bracelet-unlink.editor.action.make_closed", () => SetRingKind(slot, BraceletRingKind.Closed)),
                    new ContextAction("bracelet-unlink.editor.action.delete", () => SetRingKind(slot, BraceletRingKind.Empty)));
            }
            else
            {
                SetEditorStatus("bracelet-unlink.editor.status.closed_ring");
                ShowContextActions(
                    new ContextAction("bracelet-unlink.editor.action.make_open", () => SetRingKind(slot, BraceletRingKind.Open)),
                    ContextAction.None,
                    ContextAction.None,
                    new ContextAction("bracelet-unlink.editor.action.delete", () => SetRingKind(slot, BraceletRingKind.Empty)));
            }
            RefreshEditorHelpers();
        }

        private void SelectEdgeSlot(int edgeIndex)
        {
            if (isChoosingAttachedOwner)
            {
                return;
            }

            var edge = levelData.Edges[edgeIndex];
            if (levelData.Rings[edge.SlotA].Kind == BraceletRingKind.Empty
                || levelData.Rings[edge.SlotB].Kind == BraceletRingKind.Empty)
            {
                SetEditorStatus("bracelet-unlink.editor.status.edge_needs_rings");
                return;
            }

            selectedSlot = -1;
            selectedEdge = edgeIndex;
            SetEditorStatus(edge.Kind == BraceletEdgeKind.Empty
                ? "bracelet-unlink.editor.status.empty_edge"
                : "bracelet-unlink.editor.status.existing_edge");
            ShowContextActions(
                new ContextAction("bracelet-unlink.editor.action.map_loop", () => SetEdgeKind(edgeIndex, BraceletEdgeKind.Map)),
                new ContextAction("bracelet-unlink.editor.action.attached_loop", BeginChooseAttachedOwner),
                edge.Kind == BraceletEdgeKind.Empty
                    ? ContextAction.None
                    : new ContextAction("bracelet-unlink.editor.action.delete", () => SetEdgeKind(edgeIndex, BraceletEdgeKind.Empty)),
                new ContextAction("bracelet-unlink.editor.action.cancel", ClearEditorSelection));
            RefreshEditorHelpers();
        }

        private void BeginChooseAttachedOwner()
        {
            if (selectedEdge < 0)
            {
                return;
            }
            isChoosingAttachedOwner = true;
            HideContextActions();
            SetEditorStatus("bracelet-unlink.editor.status.choose_owner");
            RefreshEditorHelpers();
        }

        private void ApplyAttachedOwner(int slot)
        {
            if (selectedEdge < 0)
            {
                ClearEditorSelection();
                return;
            }

            var edge = levelData.Edges[selectedEdge];
            if (slot != edge.SlotA && slot != edge.SlotB)
            {
                SetEditorStatus("bracelet-unlink.editor.status.choose_owner");
                return;
            }
            levelData.SetAttachedEdge(slot, slot == edge.SlotA ? edge.SlotB : edge.SlotA);
            RebuildEditorPreview("bracelet-unlink.editor.status.attached_done");
        }

        private void SetRingKind(int slot, BraceletRingKind kind)
        {
            levelData.SetRing(slot, kind, levelData.Rings[slot].GapAngle, levelData.Rings[slot].ColorIndex);
            RebuildEditorPreview(kind == BraceletRingKind.Empty
                ? "bracelet-unlink.editor.status.ring_deleted"
                : "bracelet-unlink.editor.status.ring_changed");
        }

        private void RotateRing(int slot, float delta)
        {
            levelData.Rings[slot].GapAngle = NormalizeAngle(levelData.Rings[slot].GapAngle + delta);
            RebuildLevelObjects();
            ResetGame();
            SelectRingSlot(slot);
        }

        private void SetEdgeKind(int edgeIndex, BraceletEdgeKind kind)
        {
            var edge = levelData.Edges[edgeIndex];
            levelData.SetEdge(edge.SlotA, edge.SlotB, kind);
            RebuildEditorPreview(kind == BraceletEdgeKind.Empty
                ? "bracelet-unlink.editor.status.edge_deleted"
                : "bracelet-unlink.editor.status.edge_changed");
        }

        private void RebuildEditorPreview(string statusKey)
        {
            RebuildLevelObjects();
            ResetGame();
            selectedSlot = -1;
            selectedEdge = -1;
            isChoosingAttachedOwner = false;
            HideContextActions();
            SetEditorStatus(statusKey);
            RefreshEditorHelpers();
        }

        private void SetEditorPreviewScale(bool editing)
        {
            if (boardRoot != null)
            {
                boardRoot.localScale = editing ? Vector3.one * editorBoardScale : Vector3.one * gameplayBoardScale;
                boardRoot.anchoredPosition = editing
                    ? editorBoardPan
                    : editorBoardBasePosition;
            }
            if (editorHelpers != null)
            {
                editorHelpers.localScale = Vector3.one * editorBoardScale;
                editorHelpers.anchoredPosition = editorBoardPan;
            }
        }

        private void AttachBoardToEditorViewport()
        {
            if (boardRoot == null || editorBoardViewport == null || boardRoot.parent == editorBoardViewport)
            {
                return;
            }

            boardRoot.SetParent(editorBoardViewport, false);
            boardRoot.anchorMin = boardRoot.anchorMax = boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.anchoredPosition = editorBoardPan;
            boardRoot.SetAsFirstSibling();
        }

        private void RestoreBoardParent()
        {
            if (boardRoot == null || editorBoardOriginalParent == null || boardRoot.parent == editorBoardOriginalParent)
            {
                return;
            }

            boardRoot.SetParent(editorBoardOriginalParent, false);
            boardRoot.SetSiblingIndex(editorBoardOriginalSiblingIndex);
            boardRoot.anchorMin = boardRoot.anchorMax = boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.anchoredPosition = editorBoardBasePosition;
        }

        private void OnEditorZoomSliderChanged(float value)
        {
            editorBoardScale = Mathf.Lerp(EditorMinBoardScale, EditorMaxBoardScale, Mathf.Clamp01(value));
            ClampEditorBoardPan();
            SetEditorPreviewScale(true);
        }

        private void OnEditorZoomOutClicked()
        {
            if (editorZoomSlider != null)
            {
                editorZoomSlider.value = Mathf.Max(0f, editorZoomSlider.value - 0.16f);
            }
        }

        private void OnEditorZoomInClicked()
        {
            if (editorZoomSlider != null)
            {
                editorZoomSlider.value = Mathf.Min(1f, editorZoomSlider.value + 0.16f);
            }
        }

        private void OnEditorBoardDragged(Vector2 screenDelta)
        {
            if (!isEditing || editorBoardScale <= EditorMinBoardScale + 0.01f)
            {
                return;
            }

            var canvas = editorBoardViewport != null ? editorBoardViewport.GetComponentInParent<Canvas>() : null;
            var scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            editorBoardPan += screenDelta / scaleFactor;
            ClampEditorBoardPan();
            SetEditorPreviewScale(true);
        }

        private void ClampEditorBoardPan()
        {
            if (boardRoot == null || editorBoardViewport == null)
            {
                editorBoardPan = Vector2.zero;
                return;
            }

            var viewportSize = editorBoardViewport.rect.size;
            var scaledBoardSize = boardRoot.sizeDelta * editorBoardScale;
            var zoomProgress = Mathf.InverseLerp(EditorMinBoardScale, EditorMaxBoardScale, editorBoardScale);
            var extraPan = viewportSize * (EditorPanViewportPaddingRatio * zoomProgress);
            var maxX = Mathf.Max(0f, (scaledBoardSize.x - viewportSize.x) * 0.5f + extraPan.x);
            var maxY = Mathf.Max(0f, (scaledBoardSize.y - viewportSize.y) * 0.5f + extraPan.y);
            editorBoardPan = new Vector2(
                Mathf.Clamp(editorBoardPan.x, -maxX, maxX),
                Mathf.Clamp(editorBoardPan.y, -maxY, maxY));
        }

        private void ClearEditedLevel()
        {
            levelData = BraceletUnlinkLevelData.CreateEmpty(levelData.RowLengths);
            RebuildEditorPreview("bracelet-unlink.editor.status.cleared");
        }

        private void ChangeEditorBoardSize(int direction)
        {
            var current = FindEditorBoardLayoutIndex(levelData.RowLengths);
            var target = Mathf.Clamp(current + direction, 0, EditorBoardLayouts.Length - 1);
            if (target == current)
            {
                return;
            }
            levelData = BraceletUnlinkLevelData.CreateEmpty(EditorBoardLayouts[target]);
            editorBoardPan = Vector2.zero;
            RebuildEditorPreview("bracelet-unlink.editor.status.board_changed");
        }

        private static int FindEditorBoardLayoutIndex(int[] rows)
        {
            for (var i = 0; i < EditorBoardLayouts.Length; i++)
            {
                var candidate = EditorBoardLayouts[i];
                if (rows == null || rows.Length != candidate.Length)
                {
                    continue;
                }
                var matches = true;
                for (var row = 0; row < rows.Length; row++)
                {
                    matches &= rows[row] == candidate[row];
                }
                if (matches)
                {
                    return i;
                }
            }
            return 3;
        }

        private void SaveEditedLevel()
        {
            hasAttemptedEditorAutoLoad = true;
            StoreCurrentEditorLevel();
            var catalog = CreateEditorCatalog();
            SetEditorStatus(TryWriteEditorCatalog(catalog)
                ? "bracelet-unlink.editor.status.saved"
                : "bracelet-unlink.editor.status.save_failed");
        }

        private static BraceletUnlinkLevelData[] ResolveInitialLevelDefinitions()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BraceletUnlinkEditorCatalog catalog;
            if (TryReadSavedEditorCatalog(out catalog)
                && catalog.FormatVersion >= EditorCatalogFormatVersion)
            {
                var savedLevels = new BraceletUnlinkLevelData[catalog.Levels.Length];
                for (var i = 0; i < savedLevels.Length; i++)
                {
                    savedLevels[i] = catalog.Levels[i].Clone();
                }
                return savedLevels;
            }
#endif
            return string.IsNullOrEmpty(editorSavePathOverride)
                ? BraceletUnlinkLevelData.CreateBuiltInLevels()
                : new[] { BraceletUnlinkLevelData.LoadSavedFourthLevel() };
        }

        private void AddEditorLevel()
        {
            StoreCurrentEditorLevel();
            var level = BraceletUnlinkLevelData.CreateEmpty(2);
            level.LevelId = GetNextEditorLevelId();
            editorLevels.Add(level);
            editorLevelIndex = editorLevels.Count - 1;
            levelData = editorLevels[editorLevelIndex].Clone();
            RebuildEditorPreview("bracelet-unlink.editor.status.level_added");
        }

        private void ChangeEditorLevel(int direction)
        {
            if (editorLevels.Count == 0)
            {
                return;
            }
            var target = Mathf.Clamp(editorLevelIndex + direction, 0, editorLevels.Count - 1);
            if (target == editorLevelIndex)
            {
                return;
            }
            StoreCurrentEditorLevel();
            editorLevelIndex = target;
            levelData = editorLevels[editorLevelIndex].Clone();
            RebuildEditorPreview("bracelet-unlink.editor.status.level_changed");
        }

        private void DeleteEditorLevel()
        {
            if (editorLevels.Count <= 1)
            {
                SetEditorStatus("bracelet-unlink.editor.status.cannot_delete_only_level");
                return;
            }
            editorLevels.RemoveAt(editorLevelIndex);
            editorLevelIndex = Mathf.Clamp(editorLevelIndex, 0, editorLevels.Count - 1);
            levelData = editorLevels[editorLevelIndex].Clone();
            RebuildEditorPreview("bracelet-unlink.editor.status.level_deleted");
        }

        private static string GetEditorSavePath()
        {
            return string.IsNullOrEmpty(editorSavePathOverride)
                ? Path.Combine(Application.persistentDataPath, EditorSaveDirectoryName, EditorSaveFileName)
                : editorSavePathOverride;
        }

        private static bool TryWriteEditorCatalog(BraceletUnlinkEditorCatalog catalog)
        {
            if (!IsValidEditorCatalog(catalog))
            {
                return false;
            }

            try
            {
                var path = GetEditorSavePath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(path, JsonUtility.ToJson(catalog, true), new UTF8Encoding(true));
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Bracelet editor JSON save failed: " + exception.Message);
                return false;
            }
        }

        private static bool TryReadSavedEditorCatalog(out BraceletUnlinkEditorCatalog catalog)
        {
            catalog = null;
            var path = GetEditorSavePath();
            try
            {
                if (File.Exists(path) && TryParseEditorCatalog(File.ReadAllText(path, Encoding.UTF8), out catalog))
                {
                    var catalogChanged = RemoveObsoleteTutorialLevels(catalog);
                    if (string.IsNullOrEmpty(editorSavePathOverride)
                        && catalog.FormatVersion < CampaignCatalogFormatVersion)
                    {
                        catalog = MigrateEditorCatalog(catalog);
                        catalogChanged = true;
                    }
                    if (catalog.FormatVersion < EditorCatalogFormatVersion)
                    {
                        if (catalog.FormatVersion >= CampaignCatalogFormatVersion
                            && catalog.Levels.Length == 8)
                        {
                            var upgradedLevels = BraceletUnlinkLevelData.CreateBuiltInLevels();
                            upgradedLevels[upgradedLevels.Length - 1] = catalog.Levels[catalog.Levels.Length - 1].Clone();
                            upgradedLevels[upgradedLevels.Length - 1].LevelId = 8;
                            catalog.Levels = upgradedLevels;
                            catalog.CurrentLevelIndex = Mathf.Clamp(
                                catalog.CurrentLevelIndex,
                                0,
                                catalog.Levels.Length - 1);
                        }
                        for (var levelIndex = 0; levelIndex < catalog.Levels.Length; levelIndex++)
                        {
                            catalogChanged |= catalog.Levels[levelIndex].EnsureInitialGoldLoopGapClearance();
                        }
                        catalog.FormatVersion = EditorCatalogFormatVersion;
                        catalogChanged = true;
                    }
                    catalogChanged |= EnsureStableLevelIds(catalog.Levels);
                    if (catalogChanged && string.IsNullOrEmpty(editorSavePathOverride))
                    {
                        TryWriteEditorCatalog(catalog);
                    }
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Bracelet editor JSON read failed: " + exception.Message);
            }

            if (!string.IsNullOrEmpty(editorSavePathOverride) || !PlayerPrefs.HasKey(LegacyEditorSaveKey))
            {
                return false;
            }

            BraceletUnlinkLevelData legacyLevel;
            if (!TryParseEditorLevel(PlayerPrefs.GetString(LegacyEditorSaveKey, string.Empty), out legacyLevel))
            {
                return false;
            }

            catalog = new BraceletUnlinkEditorCatalog
            {
                FormatVersion = 1,
                CurrentLevelIndex = 0,
                Levels = new[] { legacyLevel }
            };
            if (string.IsNullOrEmpty(editorSavePathOverride))
            {
                catalog = MigrateLegacyEditorLevel(legacyLevel);
            }
            TryWriteEditorCatalog(catalog);
            return true;
        }

        private static bool RemoveObsoleteTutorialLevels(BraceletUnlinkEditorCatalog catalog)
        {
            if (catalog == null
                || catalog.Levels == null
                || catalog.Levels.Length < 3
                || !IsObsoleteTwoRingTutorial(catalog.Levels[0], BraceletEdgeKind.Map)
                || !IsObsoleteTwoRingTutorial(catalog.Levels[1], BraceletEdgeKind.AttachedToA))
            {
                return false;
            }

            var retained = new BraceletUnlinkLevelData[catalog.Levels.Length - 2];
            System.Array.Copy(catalog.Levels, 2, retained, 0, retained.Length);
            catalog.Levels = retained;
            catalog.CurrentLevelIndex = Mathf.Clamp(catalog.CurrentLevelIndex - 2, 0, retained.Length - 1);
            return true;
        }

        private static bool IsObsoleteTwoRingTutorial(BraceletUnlinkLevelData data, BraceletEdgeKind expectedEdgeKind)
        {
            if (data == null
                || data.RowLengths == null
                || data.RowLengths.Length != 1
                || data.RowLengths[0] != 2
                || data.Rings == null
                || data.Rings.Length != 2
                || data.Rings[0].Kind != BraceletRingKind.Open
                || data.Rings[1].Kind != BraceletRingKind.Open
                || data.Edges == null
                || data.Edges.Length != 1)
            {
                return false;
            }

            var actualKind = data.Edges[0].Kind;
            return expectedEdgeKind == BraceletEdgeKind.AttachedToA
                ? actualKind == BraceletEdgeKind.AttachedToA || actualKind == BraceletEdgeKind.AttachedToB
                : actualKind == expectedEdgeKind;
        }

        private static BraceletUnlinkEditorCatalog MigrateLegacyEditorLevel(BraceletUnlinkLevelData savedLevel)
        {
            var levels = BraceletUnlinkLevelData.CreateBuiltInLevels();
            levels[levels.Length - 1] = savedLevel.Clone();
            levels[levels.Length - 1].LevelId = 8;
            return new BraceletUnlinkEditorCatalog
            {
                FormatVersion = EditorCatalogFormatVersion,
                CurrentLevelIndex = levels.Length - 1,
                Levels = levels
            };
        }

        private static BraceletUnlinkEditorCatalog MigrateEditorCatalog(BraceletUnlinkEditorCatalog catalog)
        {
            if (catalog != null && catalog.Levels != null && catalog.Levels.Length == 2)
            {
                var levels = BraceletUnlinkLevelData.CreateBuiltInLevels();
                levels[levels.Length - 1] = catalog.Levels[1].Clone();
                levels[levels.Length - 1].LevelId = 8;
                return new BraceletUnlinkEditorCatalog
                {
                    FormatVersion = EditorCatalogFormatVersion,
                    CurrentLevelIndex = levels.Length - 1,
                    Levels = levels
                };
            }
            return MigrateLegacyEditorLevel(catalog.Levels[0]);
        }

        private static bool TryParseEditorCatalog(string json, out BraceletUnlinkEditorCatalog catalog)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }
            try
            {
                catalog = JsonUtility.FromJson<BraceletUnlinkEditorCatalog>(json);
                if (IsValidEditorCatalog(catalog))
                {
                    return true;
                }
                BraceletUnlinkLevelData legacyLevel;
                if (!TryParseEditorLevel(json, out legacyLevel))
                {
                    catalog = null;
                    return false;
                }
                catalog = new BraceletUnlinkEditorCatalog
                {
                    FormatVersion = 1,
                    CurrentLevelIndex = 0,
                    Levels = new[] { legacyLevel }
                };
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Bracelet editor catalog load failed: " + exception.Message);
                catalog = null;
                return false;
            }
        }

        private static bool TryParseEditorLevel(string json, out BraceletUnlinkLevelData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<BraceletUnlinkLevelData>(json);
                return IsValidEditorLevel(data);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Bracelet editor JSON load failed: " + exception.Message);
                data = null;
                return false;
            }
        }

        private static bool IsValidEditorLevel(BraceletUnlinkLevelData data)
        {
            if (data == null)
            {
                return false;
            }
            data.ApplyLegacyLayoutIfMissing();
            if (data.Rings == null || data.Edges == null)
            {
                return false;
            }

            var expected = BraceletUnlinkLevelData.CreateEmpty(data.RowLengths);
            if (data.Rings.Length != expected.Rings.Length)
            {
                return false;
            }
            if (data.Edges.Length != expected.Edges.Length)
            {
                return false;
            }
            for (var slot = 0; slot < data.Rings.Length; slot++)
            {
                if (data.Rings[slot] == null)
                {
                    return false;
                }
            }
            for (var edgeIndex = 0; edgeIndex < data.Edges.Length; edgeIndex++)
            {
                var edge = data.Edges[edgeIndex];
                if (edge == null || expected.FindEdge(edge.SlotA, edge.SlotB) == null)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsValidEditorCatalog(BraceletUnlinkEditorCatalog catalog)
        {
            if (catalog == null || catalog.Levels == null || catalog.Levels.Length == 0)
            {
                return false;
            }
            for (var i = 0; i < catalog.Levels.Length; i++)
            {
                if (!IsValidEditorLevel(catalog.Levels[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private void InitializeEditorLevelsFromRuntime()
        {
            editorLevels.Clear();
            var source = levelDefinitions ?? BraceletUnlinkLevelData.CreateBuiltInLevels();
            for (var i = 0; i < source.Length; i++)
            {
                editorLevels.Add(source[i].Clone());
            }
            editorLevelIndex = Mathf.Clamp(currentLevelIndex, 0, editorLevels.Count - 1);
            levelData = editorLevels[editorLevelIndex].Clone();
        }

        private void LoadEditorCatalog(BraceletUnlinkEditorCatalog catalog)
        {
            editorLevels.Clear();
            for (var i = 0; i < catalog.Levels.Length; i++)
            {
                editorLevels.Add(catalog.Levels[i].Clone());
            }
            editorLevelIndex = Mathf.Clamp(catalog.CurrentLevelIndex, 0, editorLevels.Count - 1);
            levelData = editorLevels[editorLevelIndex].Clone();
        }

        private void StoreCurrentEditorLevel()
        {
            if (levelData == null)
            {
                return;
            }
            if (editorLevels.Count == 0)
            {
                editorLevels.Add(levelData.Clone());
                editorLevelIndex = 0;
                return;
            }
            editorLevels[Mathf.Clamp(editorLevelIndex, 0, editorLevels.Count - 1)] = levelData.Clone();
        }

        private BraceletUnlinkEditorCatalog CreateEditorCatalog()
        {
            var levels = new BraceletUnlinkLevelData[editorLevels.Count];
            for (var i = 0; i < levels.Length; i++)
            {
                levels[i] = editorLevels[i].Clone();
            }
            EnsureStableLevelIds(levels);
            return new BraceletUnlinkEditorCatalog
            {
                FormatVersion = EditorCatalogFormatVersion,
                CurrentLevelIndex = editorLevelIndex,
                Levels = levels
            };
        }

        private int GetNextEditorLevelId()
        {
            var nextId = 1;
            for (var index = 0; index < editorLevels.Count; index++)
            {
                if (editorLevels[index] != null)
                {
                    nextId = Mathf.Max(nextId, editorLevels[index].LevelId + 1);
                }
            }
            return nextId;
        }

        private static bool EnsureStableLevelIds(BraceletUnlinkLevelData[] levels)
        {
            if (levels == null || levels.Length == 0)
            {
                return false;
            }

            var changed = false;
            var usedIds = new HashSet<int>();
            var builtInIds = levels.Length == 6 ? new[] { 1, 2, 4, 5, 7, 8 } : null;
            var nextId = 1;
            for (var index = 0; index < levels.Length; index++)
            {
                if (levels[index] == null)
                {
                    continue;
                }

                var levelId = levels[index].LevelId;
                if (levelId <= 0 || !usedIds.Add(levelId))
                {
                    var preferredId = builtInIds != null ? builtInIds[index] : nextId;
                    while (preferredId <= 0 || usedIds.Contains(preferredId))
                    {
                        preferredId += 1;
                    }
                    levels[index].LevelId = preferredId;
                    usedIds.Add(preferredId);
                    levelId = preferredId;
                    changed = true;
                }
                nextId = Mathf.Max(nextId, levelId + 1);
            }
            return changed;
        }

        private void RefreshEditorLevelLabel()
        {
            if (editorLevelLabel != null)
            {
                editorLevelLabel.text = UiTextCatalog.Format(
                    "bracelet-unlink.editor.level",
                    editorLevelIndex + 1,
                    Mathf.Max(1, editorLevels.Count));
            }
        }

        private void ShowContextActions(params ContextAction[] actions)
        {
            editorContextBar.gameObject.SetActive(true);
            for (var i = 0; i < editorContextButtons.Length; i++)
            {
                var button = editorContextButtons[i];
                button.onClick.RemoveAllListeners();
                var action = i < actions.Length ? actions[i] : ContextAction.None;
                button.gameObject.SetActive(action.IsValid);
                if (!action.IsValid)
                {
                    continue;
                }
                button.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = EditorText(action.TextKey);
                button.onClick.AddListener(action.Action);
            }
        }

        private void HideContextActions()
        {
            if (editorContextBar != null)
            {
                editorContextBar.gameObject.SetActive(false);
            }
        }

        private void SetEditorStatus(string key)
        {
            if (editorStatus != null)
            {
                editorStatus.text = EditorText(key);
            }
        }

        private static string EditorText(string key)
        {
            return UiTextCatalog.Get(key);
        }

        private readonly struct ContextAction
        {
            public static readonly ContextAction None = new ContextAction(null, null);

            public ContextAction(string textKey, UnityEngine.Events.UnityAction action)
            {
                TextKey = textKey;
                Action = action;
            }

            public string TextKey { get; }
            public UnityEngine.Events.UnityAction Action { get; }
            public bool IsValid => !string.IsNullOrEmpty(TextKey) && Action != null;
        }
    }

    public sealed class BraceletEditorBoardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public UnityEngine.Events.UnityAction<Vector2> OnDragDelta;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            OnDragDelta?.Invoke(eventData.delta);
        }
    }

    [System.Serializable]
    internal sealed class BraceletUnlinkEditorCatalog
    {
        public int FormatVersion;
        public int CurrentLevelIndex;
        public BraceletUnlinkLevelData[] Levels;
    }

    public sealed class BraceletEditorTargetMarkerGraphic : MaskableGraphic
    {
        private const int CornerSegments = 8;
        private float cornerRadius = 20f;
        private float strokeWidth = 2f;

        public float CornerRadius
        {
            get { return cornerRadius; }
            set
            {
                cornerRadius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public float StrokeWidth
        {
            get { return strokeWidth; }
            set
            {
                strokeWidth = Mathf.Max(1f, value);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var outerRect = GetPixelAdjustedRect();
            var width = Mathf.Min(strokeWidth, outerRect.width * 0.25f, outerRect.height * 0.25f);
            var innerRect = Rect.MinMaxRect(
                outerRect.xMin + width,
                outerRect.yMin + width,
                outerRect.xMax - width,
                outerRect.yMax - width);
            var outerRadius = Mathf.Min(cornerRadius, outerRect.width * 0.5f, outerRect.height * 0.5f);
            var innerRadius = Mathf.Max(0f, outerRadius - width);
            var outer = new System.Collections.Generic.List<Vector2>((CornerSegments + 1) * 4);
            var inner = new System.Collections.Generic.List<Vector2>((CornerSegments + 1) * 4);
            AppendContour(outer, outerRect, outerRadius);
            AppendContour(inner, innerRect, innerRadius);

            var vertexColor = (Color32)color;
            for (var i = 0; i < outer.Count; i++)
            {
                var next = (i + 1) % outer.Count;
                var start = vertexHelper.currentVertCount;
                vertexHelper.AddVert(outer[i], vertexColor, Vector2.zero);
                vertexHelper.AddVert(inner[i], vertexColor, Vector2.zero);
                vertexHelper.AddVert(inner[next], vertexColor, Vector2.zero);
                vertexHelper.AddVert(outer[next], vertexColor, Vector2.zero);
                vertexHelper.AddTriangle(start, start + 1, start + 2);
                vertexHelper.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static void AppendContour(System.Collections.Generic.ICollection<Vector2> contour, Rect rect, float radius)
        {
            AppendCorner(contour, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            AppendCorner(contour, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            AppendCorner(contour, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            AppendCorner(contour, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);
        }

        private static void AppendCorner(
            System.Collections.Generic.ICollection<Vector2> contour,
            Vector2 center,
            float radius,
            float startDegrees,
            float endDegrees)
        {
            for (var i = 0; i <= CornerSegments; i++)
            {
                var degrees = Mathf.Lerp(startDegrees, endDegrees, i / (float)CornerSegments);
                var radians = degrees * Mathf.Deg2Rad;
                contour.Add(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
            }
        }
    }
}
