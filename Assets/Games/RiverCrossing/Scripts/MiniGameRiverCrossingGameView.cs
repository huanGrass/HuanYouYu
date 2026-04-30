using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameRiverCrossingGameView : MiniGameBase
    {
        public const string GameIdConstant = "rivercrossing";

        private const int OptimalStepCount = 7;
        private const int ItemCount = 3;
        private static readonly Vector2 ItemButtonSize = new Vector2(112f, 96f);
        private const float BankItemSpacing = 72f;
        private const float UpperBankBoatDockExtraOffsetY = 40f;

        private static readonly Color ContentPanelColor = new Color(1f, 0.98f, 0.91f, 0.86f);
        private static readonly Color LeftBankColor = new Color(0.78f, 0.91f, 0.64f, 0.92f);
        private static readonly Color RightBankColor = new Color(0.76f, 0.88f, 0.68f, 0.92f);
        private static readonly Color RiverColor = new Color(0.43f, 0.72f, 0.9f, 0.86f);
        private static readonly Color BoatColor = new Color(0.65f, 0.43f, 0.24f, 1f);
        private static readonly Color ItemColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color ItemSelectedColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color PrimaryButtonColor = new Color(0.23f, 0.56f, 0.42f, 1f);

        private readonly BankSide[] itemSides = new BankSide[ItemCount];
        private readonly Button[] itemButtons = new Button[ItemCount];
        private readonly Image[] itemBackgrounds = new Image[ItemCount];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI statusLabel;
        private TextMeshProUGUI crossButtonLabel;
        private Button restartButton;
        private Button hintButton;
        private Button boatButton;
        private Button crossButton;
        private RectTransform leftItemsHost;
        private RectTransform rightItemsHost;
        private RectTransform riverPanel;
        private RectTransform riverSailingArea;
        private RectTransform boatVisual;
        private RectTransform boatCargoHost;
        private RectTransform draggedItemRect;
        private Transform draggedItemOriginalParent;
        private Vector2 draggedItemOriginalAnchoredPosition;
        private int draggedItemOriginalSiblingIndex;
        private RectTransform failureEffectRoot;
        private Coroutine boatMoveRoutine;
        private Coroutine failureRoutine;
        private int stepCount;
        private BankSide boatSide;
        private Cargo boatCargo;
        private bool completed;

        private enum BankSide
        {
            Left,
            Right
        }

        private enum Cargo
        {
            None,
            Fox,
            Chicken,
            Corn
        }

        private enum UnsafePair
        {
            None,
            FoxChicken,
            ChickenCorn
        }

        public MiniGameRiverCrossingGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameRiverCrossingView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("RiverCrossingHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();

            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("RiverCrossingActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;
            hintButton = MiniGameShellBottomBarBuilder.CreateHintButton(bottomContainerRefs.ActionBar).Button;

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (hintButton != null)
            {
                hintButton.gameObject.name = "HintButton";
                hintButton.onClick.RemoveAllListeners();
                hintButton.onClick.AddListener(OnHintClicked);
            }

            if (titleLabel == null ||
                scoreLabel == null ||
                statusLabel == null ||
                crossButtonLabel == null ||
                restartButton == null ||
                hintButton == null ||
                boatButton == null ||
                crossButton == null ||
                leftItemsHost == null ||
                rightItemsHost == null ||
                riverSailingArea == null ||
                boatVisual == null ||
                boatCargoHost == null)
            {
                throw new InvalidOperationException("RiverCrossing prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            StopFailureRoutine();
            StopBoatMoveRoutine();
            RestoreDraggedItem();
            ClearFailureEffects();

            completed = false;
            stepCount = 0;
            boatSide = BankSide.Left;
            boatCargo = Cargo.None;
            for (var i = 0; i < itemSides.Length; i++)
            {
                itemSides[i] = BankSide.Left;
            }

            RefreshAll();
            SetStatus("rivercrossing.status.ready");
            RefreshLayoutAndBoatPosition();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.rivercrossing.help", null);
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            StopFailureRoutine();
            StopBoatMoveRoutine();
            RestoreDraggedItem();
            ClearFailureEffects();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (hintButton != null)
            {
                hintButton.onClick.RemoveListener(OnHintClicked);
            }

            if (boatButton != null)
            {
                boatButton.onClick.RemoveListener(OnCrossClicked);
            }

            if (crossButton != null)
            {
                crossButton.onClick.RemoveListener(OnCrossClicked);
            }

            for (var i = 0; i < itemButtons.Length; i++)
            {
                if (itemButtons[i] != null)
                {
                    itemButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        private void BuildContent()
        {
            var root = CreateRectObject("RiverCrossingContent", Shell.ContentHost);
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(24f, 18f), new Vector2(-24f, -18f));

            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 14f;
            rootLayout.childAlignment = TextAnchor.MiddleCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            statusLabel = CreateText("StatusText", root, 28f, FontStyles.Bold, new Color(0.25f, 0.34f, 0.22f, 1f));
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.enableWordWrapping = true;
            statusLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 86f;

            var board = CreatePanel("RiverCrossingBoard", root, ContentPanelColor);
            var boardLayoutElement = board.gameObject.AddComponent<LayoutElement>();
            boardLayoutElement.flexibleHeight = 1f;
            boardLayoutElement.minHeight = 520f;
            boardLayoutElement.preferredHeight = 720f;

            var boardLayout = board.gameObject.AddComponent<VerticalLayoutGroup>();
            boardLayout.padding = new RectOffset(18, 18, 18, 18);
            boardLayout.spacing = 14f;
            boardLayout.childAlignment = TextAnchor.MiddleCenter;
            boardLayout.childControlWidth = true;
            boardLayout.childControlHeight = true;
            boardLayout.childForceExpandWidth = true;
            boardLayout.childForceExpandHeight = true;

            rightItemsHost = CreateBank("RightBank", board, "rivercrossing.bank.right", RightBankColor, 1f);
            BuildRiver(board);
            leftItemsHost = CreateBank("LeftBank", board, "rivercrossing.bank.left", LeftBankColor, 1f);

            CreateItemButton(Cargo.Fox, "FoxButton");
            CreateItemButton(Cargo.Chicken, "ChickenButton");
            CreateItemButton(Cargo.Corn, "CornButton");
        }

        private RectTransform CreateBank(string name, RectTransform parent, string titleKey, Color color, float flexibleHeight)
        {
            var bank = CreatePanel(name, parent, color);
            var layoutElement = bank.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = flexibleHeight;
            layoutElement.minHeight = 164f;
            layoutElement.preferredHeight = 190f;

            var layout = bank.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateText(name + "Title", bank, 26f, FontStyles.Bold, new Color(0.25f, 0.36f, 0.2f, 1f));
            title.text = UiTextCatalog.Get(titleKey);
            title.alignment = TextAlignmentOptions.Center;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            var itemsHost = CreateRectObject(name + "Items", bank);
            var hostLayoutElement = itemsHost.gameObject.AddComponent<LayoutElement>();
            hostLayoutElement.flexibleHeight = 1f;
            hostLayoutElement.minHeight = 96f;
            return itemsHost;
        }

        private void BuildRiver(RectTransform parent)
        {
            var river = CreatePanel("RiverPanel", parent, RiverColor);
            riverPanel = river;
            var layoutElement = river.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 0.86f;
            layoutElement.minHeight = 222f;
            layoutElement.preferredHeight = 260f;

            var layout = river.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            riverSailingArea = CreateRectObject("RiverSailingArea", river);
            var sailingAreaLayoutElement = riverSailingArea.gameObject.AddComponent<LayoutElement>();
            sailingAreaLayoutElement.ignoreLayout = true;
            Stretch(riverSailingArea, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-88f, 0f));

            boatButton = CreateBoatButton(riverSailingArea);

            var title = CreateText("RiverTitle", river, 25f, FontStyles.Bold, new Color(0.16f, 0.34f, 0.48f, 1f));
            title.text = UiTextCatalog.Get("rivercrossing.river.title");
            title.alignment = TextAlignmentOptions.Center;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            var riverBody = CreateRectObject("RiverBody", river);
            var bodyLayoutElement = riverBody.gameObject.AddComponent<LayoutElement>();
            bodyLayoutElement.flexibleHeight = 1f;
            bodyLayoutElement.minHeight = 150f;
            bodyLayoutElement.preferredHeight = 166f;

            crossButton = CreateTextButton("CrossRiverButton", riverBody, PrimaryButtonColor, 19f, 54f);
            var crossButtonRect = crossButton.GetComponent<RectTransform>();
            crossButtonRect.anchorMin = new Vector2(1f, 0.5f);
            crossButtonRect.anchorMax = new Vector2(1f, 0.5f);
            crossButtonRect.pivot = new Vector2(1f, 0.5f);
            crossButtonRect.anchoredPosition = new Vector2(-8f, 0f);
            crossButtonRect.sizeDelta = new Vector2(68f, 54f);
            crossButton.onClick.AddListener(OnCrossClicked);
            crossButtonLabel = crossButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        private Button CreateBoatButton(RectTransform parent)
        {
            var boatObject = new GameObject("BoatButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(BoatHullGraphic), typeof(Button));
            boatVisual = boatObject.GetComponent<RectTransform>();
            boatVisual.SetParent(parent, false);
            boatVisual.anchorMin = new Vector2(0.5f, 0.5f);
            boatVisual.anchorMax = new Vector2(0.5f, 0.5f);
            boatVisual.pivot = new Vector2(0.5f, 0.5f);
            boatVisual.sizeDelta = new Vector2(230f, 92f);

            var hull = boatObject.GetComponent<BoatHullGraphic>();
            hull.color = BoatColor;
            hull.raycastTarget = true;

            var rim = CreateRectObject("BoatRim", boatVisual);
            rim.anchorMin = new Vector2(0.5f, 0.16f);
            rim.anchorMax = new Vector2(0.5f, 0.16f);
            rim.pivot = new Vector2(0.5f, 0.5f);
            rim.anchoredPosition = Vector2.zero;
            rim.sizeDelta = new Vector2(174f, 12f);
            var rimImage = rim.gameObject.AddComponent<Image>();
            rimImage.color = new Color(0.9f, 0.66f, 0.36f, 1f);
            rimImage.raycastTarget = false;

            boatCargoHost = CreateRectObject("BoatCargoHost", boatVisual);
            boatCargoHost.anchorMin = new Vector2(0.5f, 0.54f);
            boatCargoHost.anchorMax = new Vector2(0.5f, 0.54f);
            boatCargoHost.pivot = new Vector2(0.5f, 0.5f);
            boatCargoHost.anchoredPosition = Vector2.zero;
            boatCargoHost.sizeDelta = new Vector2(126f, 86f);

            var button = boatObject.GetComponent<Button>();
            button.targetGraphic = hull;
            button.onClick.AddListener(OnCrossClicked);
            return button;
        }

        private void CreateItemButton(Cargo item, string name)
        {
            var button = CreateIconButton(name, leftItemsHost, ItemColor, 86f, GetItemSprite(item));

            var itemCopy = item;
            button.gameObject.AddComponent<ItemDragHandler>().Initialize(this, itemCopy);

            var index = GetItemIndex(item);
            itemButtons[index] = button;
            itemBackgrounds[index] = button.targetGraphic as Image;
        }

        private Button CreateIconButton(string name, RectTransform parent, Color backgroundColor, float preferredHeight, Sprite sprite)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ItemButtonSize;

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredWidth = ItemButtonSize.x;
            layoutElement.minWidth = ItemButtonSize.x;

            var image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var iconRect = CreateRectObject("Icon", rect);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(4f, 3f), new Vector2(-4f, -3f));
            var iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            return button;
        }

        private Button CreateTextButton(string name, RectTransform parent, Color backgroundColor, float fontSize, float preferredHeight)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(160f, preferredHeight);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;

            var image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", rect, fontSize, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return button;
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel.GetComponent<RectTransform>();
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var text = textObject.gameObject.AddComponent<TextMeshProUGUI>();
            var fontAsset = MiniGameFontProvider.DefaultFont;
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private void OnItemClicked(Cargo item)
        {
            if (completed)
            {
                return;
            }

            if (boatCargo == item)
            {
                TryUnloadItem(item);
                return;
            }

            if (boatCargo != Cargo.None)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.74f);
                SetStatus("rivercrossing.status.boat_full");
                return;
            }

            if (itemSides[GetItemIndex(item)] != boatSide)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.74f);
                SetStatus("rivercrossing.status.item_wrong_side");
                return;
            }

            boatCargo = item;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.86f);
            RefreshAll();
            SetStatusFormat("rivercrossing.status.loaded", GetItemName(item));
        }

        private void TryUnloadItem(Cargo item)
        {
            string unsafeStatusKey;
            UnsafePair unsafePair;
            BankSide unsafeSide;
            if (TryGetUnsafeStatusKey(boatSide, Cargo.None, item, boatSide, out unsafeStatusKey, out unsafePair, out unsafeSide))
            {
                BeginFailure(GetFailureReasonKey(unsafePair), unsafePair, unsafeSide);
                return;
            }

            itemSides[GetItemIndex(item)] = boatSide;
            boatCargo = Cargo.None;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.86f);
            RefreshAll();
            SetStatusFormat("rivercrossing.status.unloaded", GetItemName(item));
            TryCompleteGame();
        }

        private bool CanDragItemToBoat(Cargo item)
        {
            return !completed &&
                boatCargo == Cargo.None &&
                item != Cargo.None &&
                itemSides[GetItemIndex(item)] == boatSide;
        }

        private bool CanBeginItemDrag(Cargo item)
        {
            return !completed && item != Cargo.None;
        }

        private bool BeginItemDrag(Cargo item, PointerEventData eventData, RectTransform sourceRect)
        {
            if (!CanBeginItemDrag(item) || sourceRect == null)
            {
                return false;
            }

            RestoreDraggedItem();

            var canvas = sourceRect.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            draggedItemRect = sourceRect;
            draggedItemOriginalParent = sourceRect.parent;
            draggedItemOriginalAnchoredPosition = sourceRect.anchoredPosition;
            draggedItemOriginalSiblingIndex = sourceRect.GetSiblingIndex();
            sourceRect.SetParent(canvas.transform, true);
            sourceRect.SetAsLastSibling();

            SetDraggedItemPosition(eventData);
            return true;
        }

        private void UpdateItemDrag(PointerEventData eventData)
        {
            if (draggedItemRect == null)
            {
                return;
            }

            SetDraggedItemPosition(eventData);
        }

        private void EndItemDrag(Cargo item, PointerEventData eventData)
        {
            var droppedOnRiver = IsPointerOverRiver(eventData);
            var droppedOnCurrentBank = IsPointerOverCurrentBank(eventData);

            if (boatCargo == item && droppedOnCurrentBank)
            {
                ClearDraggedItemState();
                TryUnloadItem(item);
            }
            else if (droppedOnRiver && CanDragItemToBoat(item))
            {
                ClearDraggedItemState();
                OnItemClicked(item);
            }
            else
            {
                RestoreDraggedItem();
            }
        }

        private bool IsPointerOverRiver(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            return riverPanel != null &&
                RectTransformUtility.RectangleContainsScreenPoint(riverPanel, eventData.position, eventData.pressEventCamera);
        }

        private bool IsPointerOverCurrentBank(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            var currentItemsHost = boatSide == BankSide.Left ? leftItemsHost : rightItemsHost;
            var targetBank = currentItemsHost == null ? null : currentItemsHost.parent as RectTransform;
            return targetBank != null &&
                RectTransformUtility.RectangleContainsScreenPoint(targetBank, eventData.position, eventData.pressEventCamera);
        }

        private void SetDraggedItemPosition(PointerEventData eventData)
        {
            if (draggedItemRect == null || eventData == null)
            {
                return;
            }

            var parentRect = draggedItemRect.parent as RectTransform;
            Vector2 localPoint;
            if (parentRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                draggedItemRect.anchoredPosition = localPoint;
            }
        }

        private void RestoreDraggedItem()
        {
            if (draggedItemRect == null)
            {
                return;
            }

            if (draggedItemOriginalParent != null)
            {
                draggedItemRect.SetParent(draggedItemOriginalParent, false);
                draggedItemRect.SetSiblingIndex(Mathf.Min(draggedItemOriginalSiblingIndex, draggedItemOriginalParent.childCount - 1));
                draggedItemRect.anchoredPosition = draggedItemOriginalAnchoredPosition;
                draggedItemRect.localScale = Vector3.one;
            }

            draggedItemRect = null;
            draggedItemOriginalParent = null;
            draggedItemOriginalAnchoredPosition = Vector2.zero;
            draggedItemOriginalSiblingIndex = 0;
        }

        private void ClearDraggedItemState()
        {
            draggedItemRect = null;
            draggedItemOriginalParent = null;
            draggedItemOriginalAnchoredPosition = Vector2.zero;
            draggedItemOriginalSiblingIndex = 0;
        }

        private void OnCrossClicked()
        {
            if (completed)
            {
                return;
            }

            var nextSide = Opposite(boatSide);
            var previousSide = boatSide;
            string unsafeStatusKey;
            UnsafePair unsafePair;
            BankSide unsafeSide;
            if (TryGetUnsafeStatusKey(nextSide, boatCargo, Cargo.None, BankSide.Left, out unsafeStatusKey, out unsafePair, out unsafeSide))
            {
                BeginFailure(GetFailureReasonKey(unsafePair), unsafePair, unsafeSide);
                return;
            }

            boatSide = nextSide;
            stepCount++;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.74f);
            StartBoatMove(previousSide, nextSide);
            RefreshAll();
            SetStatusFormat("rivercrossing.status.crossed", GetBankName(boatSide));
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void OnHintClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            SetStatus("rivercrossing.status.hint");
        }

        private void TryCompleteGame()
        {
            if (completed || boatCargo != Cargo.None)
            {
                return;
            }

            for (var i = 0; i < itemSides.Length; i++)
            {
                if (itemSides[i] != BankSide.Right)
                {
                    return;
                }
            }

            completed = true;
            RefreshAll();
            SetStatus("rivercrossing.status.complete");
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);

            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "RiverCrossingWinSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Confirm,
                    Title = UiTextCatalog.Get("rivercrossing.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(
                        UiTextCatalog.Get("rivercrossing.settlement.steps"),
                        stepCount.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(
                        UiTextCatalog.Get("rivercrossing.settlement.best_steps"),
                        OptimalStepCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate { CompleteGame?.Invoke(settlement); },
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void BeginFailure(string reasonKey, UnsafePair unsafePair, BankSide unsafeSide)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            RestoreDraggedItem();
            StopBoatMoveRoutine();
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.78f);
            RefreshAll();

            if (HostBehaviour != null)
            {
                failureRoutine = HostBehaviour.StartCoroutine(PlayFailureRoutine(reasonKey, unsafePair, unsafeSide));
                return;
            }

            ShowFailureSettlement(reasonKey);
        }

        private IEnumerator PlayFailureRoutine(string reasonKey, UnsafePair unsafePair, BankSide unsafeSide)
        {
            ClearFailureEffects();
            var predator = unsafePair == UnsafePair.FoxChicken ? Cargo.Fox : Cargo.Chicken;
            var prey = unsafePair == UnsafePair.FoxChicken ? Cargo.Chicken : Cargo.Corn;
            var host = GetItemsHost(unsafeSide);
            var predatorButton = itemButtons[GetItemIndex(predator)];
            var preyButton = itemButtons[GetItemIndex(prey)];
            if (host == null || predatorButton == null || preyButton == null)
            {
                yield return WaitForUnscaledSeconds(0.35f);
                ShowFailureSettlement(reasonKey);
                yield break;
            }

            failureEffectRoot = CreateRectObject("RiverCrossingFailureEffectRoot", host);
            Stretch(failureEffectRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            failureEffectRoot.SetAsLastSibling();

            var predatorRect = predatorButton.GetComponent<RectTransform>();
            var preyRect = preyButton.GetComponent<RectTransform>();
            var predatorStart = GetLocalCenterIn(host, predatorRect);
            var preyPosition = GetLocalCenterIn(host, preyRect);
            var predatorClone = CreateFailureIcon("FailurePredator", failureEffectRoot, GetItemSprite(predator), predatorStart, predatorRect.rect.size);
            var preyClone = CreateFailureIcon("FailurePrey", failureEffectRoot, GetItemSprite(prey), preyPosition, preyRect.rect.size);
            SetItemVisible(predator, false);
            SetItemVisible(prey, false);

            const float moveDuration = 0.58f;
            var elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / moveDuration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                predatorClone.anchoredPosition = Vector2.Lerp(predatorStart, preyPosition, easedProgress);
                predatorClone.localScale = Vector3.one * (1f + Mathf.Sin(progress * Mathf.PI) * 0.10f);
                yield return null;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.64f, 0.92f);
            elapsed = 0f;
            const float eatDuration = 0.24f;
            while (elapsed < eatDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / eatDuration);
                var color = preyClone.GetComponent<Image>().color;
                color.a = Mathf.Lerp(1f, 0f, progress);
                preyClone.GetComponent<Image>().color = color;
                preyClone.localScale = Vector3.one * Mathf.Lerp(1f, 0.18f, progress);
                predatorClone.localScale = Vector3.one * Mathf.Lerp(1.08f, 1.18f, Mathf.Sin(progress * Mathf.PI));
                yield return null;
            }

            preyClone.gameObject.SetActive(false);
            yield return WaitForUnscaledSeconds(0.18f);
            ShowFailureSettlement(reasonKey);
            failureRoutine = null;
        }

        private void ShowFailureSettlement(string reasonKey)
        {
            var settlement = CreateFailureSettlement(reasonKey);
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "RiverCrossingFailureSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("rivercrossing.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(
                        UiTextCatalog.Get("rivercrossing.settlement.steps"),
                        stepCount.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(
                        UiTextCatalog.Get("rivercrossing.settlement.failure_reason"),
                        UiTextCatalog.Get(reasonKey)),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                false);
        }

        private MiniGameSettlement CreateFailureSettlement(string reasonKey)
        {
            return new MiniGameSettlement
            {
                Score = 0,
                CoinCount = 0,
                ChestCount = 0,
                Summary = UiTextCatalog.Format("rivercrossing.settlement.failure_summary", stepCount, UiTextCatalog.Get(reasonKey))
            };
        }

        private bool TryGetUnsafeStatusKey(
            BankSide farmerSide,
            Cargo cargoInBoat,
            Cargo overrideItem,
            BankSide overrideSide,
            out string statusKey,
            out UnsafePair unsafePair,
            out BankSide unsafeSide)
        {
            var unattendedSide = Opposite(farmerSide);
            if (HasItemOnBank(Cargo.Fox, unattendedSide, cargoInBoat, overrideItem, overrideSide) &&
                HasItemOnBank(Cargo.Chicken, unattendedSide, cargoInBoat, overrideItem, overrideSide))
            {
                statusKey = "rivercrossing.status.unsafe_fox_chicken";
                unsafePair = UnsafePair.FoxChicken;
                unsafeSide = unattendedSide;
                return true;
            }

            if (HasItemOnBank(Cargo.Chicken, unattendedSide, cargoInBoat, overrideItem, overrideSide) &&
                HasItemOnBank(Cargo.Corn, unattendedSide, cargoInBoat, overrideItem, overrideSide))
            {
                statusKey = "rivercrossing.status.unsafe_chicken_corn";
                unsafePair = UnsafePair.ChickenCorn;
                unsafeSide = unattendedSide;
                return true;
            }

            statusKey = null;
            unsafePair = UnsafePair.None;
            unsafeSide = unattendedSide;
            return false;
        }

        private bool HasItemOnBank(Cargo item, BankSide side, Cargo cargoInBoat, Cargo overrideItem, BankSide overrideSide)
        {
            if (item == cargoInBoat)
            {
                return false;
            }

            var itemSide = itemSides[GetItemIndex(item)];
            if (item == overrideItem)
            {
                itemSide = overrideSide;
            }

            return itemSide == side;
        }

        private void RefreshAll()
        {
            RefreshHud();
            RefreshBoat();
            RefreshItems();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.rivercrossing.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("rivercrossing.hud.steps", stepCount, OptimalStepCount);
            }
        }

        private void RefreshBoat()
        {
            if (boatButton != null)
            {
                boatButton.interactable = !completed;
            }

            if (crossButtonLabel != null)
            {
                crossButtonLabel.text = UiTextCatalog.Get("rivercrossing.action.cross");
            }

            if (crossButton != null)
            {
                crossButton.interactable = !completed;
            }

            if (boatMoveRoutine == null)
            {
                SetBoatPositionForSide(boatSide);
            }
        }

        private void RefreshLayoutAndBoatPosition()
        {
            if (Shell?.RootTransform == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Shell.RootTransform);
            Canvas.ForceUpdateCanvases();
            if (boatMoveRoutine == null)
            {
                SetBoatPositionForSide(boatSide);
            }
        }

        private void RefreshItems()
        {
            RefreshItemParent(Cargo.Fox);
            RefreshItemParent(Cargo.Chicken);
            RefreshItemParent(Cargo.Corn);
            LayoutItems(leftItemsHost);
            LayoutItems(rightItemsHost);
            LayoutItems(boatCargoHost);
        }

        private void RefreshItemParent(Cargo item)
        {
            var index = GetItemIndex(item);
            var button = itemButtons[index];
            if (button == null)
            {
                return;
            }

            var targetParent = boatCargo == item
                ? boatCargoHost
                : itemSides[index] == BankSide.Left
                    ? leftItemsHost
                    : rightItemsHost;
            button.transform.SetParent(targetParent, false);
            button.interactable = !completed;

            if (itemBackgrounds[index] != null)
            {
                itemBackgrounds[index].color = boatCargo == item ? ItemSelectedColor : ItemColor;
            }
        }

        private void LayoutItems(RectTransform host)
        {
            if (host == null)
            {
                return;
            }

            for (var i = 0; i < itemButtons.Length; i++)
            {
                var button = itemButtons[i];
                if (button == null || button.transform.parent != host || !button.gameObject.activeSelf)
                {
                    continue;
                }

                var rect = button.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = ItemButtonSize;
                rect.anchoredPosition = host == boatCargoHost
                    ? Vector2.zero
                    : GetBankItemPosition((Cargo)(i + 1));
                rect.localScale = Vector3.one;
            }
        }

        private static Vector2 GetBankItemPosition(Cargo item)
        {
            switch (item)
            {
                case Cargo.Fox:
                    return new Vector2(-BankItemSpacing, -2f);
                case Cargo.Chicken:
                    return new Vector2(0f, -2f);
                case Cargo.Corn:
                    return new Vector2(BankItemSpacing, -2f);
                default:
                    return Vector2.zero;
            }
        }

        private void SetStatus(string key)
        {
            if (statusLabel != null)
            {
                statusLabel.text = UiTextCatalog.Get(key);
            }
        }

        private void SetStatusFormat(string key, params object[] args)
        {
            if (statusLabel != null)
            {
                statusLabel.text = UiTextCatalog.Format(key, args);
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "RiverCrossingSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("rivercrossing.settlement.steps"), stepCount.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("rivercrossing.settlement.exit_label"), UiTextCatalog.Get("rivercrossing.settlement.exit_value")),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement()
        {
            var score = Mathf.Clamp(100 - (Mathf.Max(0, stepCount - OptimalStepCount) * 8), 0, 100);
            var coinCount = completed ? Mathf.Max(50, score) : CountRightBankItems() * 10;
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = completed ? 1 : 0,
                Summary = UiTextCatalog.Format("rivercrossing.settlement.summary", stepCount, score)
            };
        }

        private int CountRightBankItems()
        {
            var count = 0;
            for (var i = 0; i < itemSides.Length; i++)
            {
                if (itemSides[i] == BankSide.Right)
                {
                    count++;
                }
            }

            return count;
        }

        private RectTransform GetItemsHost(BankSide side)
        {
            return side == BankSide.Left ? leftItemsHost : rightItemsHost;
        }

        private static Vector2 GetLocalCenterIn(RectTransform host, RectTransform item)
        {
            if (host == null || item == null)
            {
                return Vector2.zero;
            }

            return (Vector2)host.InverseTransformPoint(item.TransformPoint(item.rect.center));
        }

        private RectTransform CreateFailureIcon(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var icon = CreateRectObject(name, parent);
            icon.anchorMin = new Vector2(0.5f, 0.5f);
            icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = position;
            icon.sizeDelta = size;
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return icon;
        }

        private void SetItemVisible(Cargo item, bool visible)
        {
            if (item == Cargo.None)
            {
                return;
            }

            var button = itemButtons[GetItemIndex(item)];
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void ClearFailureEffects()
        {
            for (var i = 0; i < itemButtons.Length; i++)
            {
                if (itemButtons[i] != null)
                {
                    itemButtons[i].gameObject.SetActive(true);
                    var rect = itemButtons[i].GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                    }
                }
            }

            if (failureEffectRoot != null)
            {
                UnityEngine.Object.Destroy(failureEffectRoot.gameObject);
                failureEffectRoot = null;
            }
        }

        private void StopFailureRoutine()
        {
            if (failureRoutine == null)
            {
                return;
            }

            if (HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(failureRoutine);
            }

            failureRoutine = null;
        }

        private string GetItemName(Cargo item)
        {
            switch (item)
            {
                case Cargo.Fox:
                    return UiTextCatalog.Get("rivercrossing.item.fox");
                case Cargo.Chicken:
                    return UiTextCatalog.Get("rivercrossing.item.chicken");
                case Cargo.Corn:
                    return UiTextCatalog.Get("rivercrossing.item.corn");
                default:
                    return UiTextCatalog.Get("rivercrossing.item.none");
            }
        }

        private Sprite GetItemSprite(Cargo item)
        {
            return Resources.Load<Sprite>("GameIcons/" + GetItemIconKey(item));
        }

        private string GetItemIconKey(Cargo item)
        {
            switch (item)
            {
                case Cargo.Fox:
                    return "fox";
                case Cargo.Chicken:
                    return "chicken";
                case Cargo.Corn:
                    return "corn";
                default:
                    return string.Empty;
            }
        }

        private string GetFailureReasonKey(UnsafePair unsafePair)
        {
            switch (unsafePair)
            {
                case UnsafePair.FoxChicken:
                    return "rivercrossing.settlement.failure_fox_chicken";
                case UnsafePair.ChickenCorn:
                    return "rivercrossing.settlement.failure_chicken_corn";
                default:
                    return "rivercrossing.settlement.failure_unknown";
            }
        }

        private string GetBankName(BankSide side)
        {
            return UiTextCatalog.Get(side == BankSide.Left ? "rivercrossing.bank.left" : "rivercrossing.bank.right");
        }

        private static int GetItemIndex(Cargo item)
        {
            return ((int)item) - 1;
        }

        private static BankSide Opposite(BankSide side)
        {
            return side == BankSide.Left ? BankSide.Right : BankSide.Left;
        }

        private void StartBoatMove(BankSide fromSide, BankSide toSide)
        {
            if (boatVisual == null)
            {
                return;
            }

            var startPosition = boatMoveRoutine == null
                ? GetBoatPositionForSide(fromSide)
                : boatVisual.anchoredPosition;
            StopBoatMoveRoutine();

            if (HostBehaviour == null)
            {
                boatVisual.anchoredPosition = GetBoatPositionForSide(toSide);
                return;
            }

            boatMoveRoutine = HostBehaviour.StartCoroutine(AnimateBoatMove(startPosition, GetBoatPositionForSide(toSide)));
        }

        private IEnumerator AnimateBoatMove(Vector2 startPosition, Vector2 endPosition)
        {
            const float duration = 0.32f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                if (boatVisual != null)
                {
                    boatVisual.anchoredPosition = Vector2.Lerp(startPosition, endPosition, easedProgress);
                }

                yield return null;
            }

            if (boatVisual != null)
            {
                boatVisual.anchoredPosition = endPosition;
            }

            boatMoveRoutine = null;
        }

        private void StopBoatMoveRoutine()
        {
            if (boatMoveRoutine == null)
            {
                return;
            }

            if (HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(boatMoveRoutine);
            }

            boatMoveRoutine = null;
        }

        private void SetBoatPositionForSide(BankSide side)
        {
            if (boatVisual != null)
            {
                boatVisual.anchoredPosition = GetBoatPositionForSide(side);
            }
        }

        private Vector2 GetBoatPositionForSide(BankSide side)
        {
            var travelY = 42f;
            if (riverSailingArea != null)
            {
                var boatHeight = boatVisual == null ? 92f : Mathf.Max(1f, boatVisual.rect.height);
                travelY = Mathf.Max(56f, (riverSailingArea.rect.height - boatHeight) * 0.5f);
            }

            return new Vector2(0f, side == BankSide.Left ? -travelY : travelY + UpperBankBoatDockExtraOffsetY);
        }

        private static IEnumerator WaitForUnscaledSeconds(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class BoatHullGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();

                var rect = rectTransform.rect;
                var topY = rect.yMin + (rect.height * 0.46f);
                var bottomY = rect.yMin + (rect.height * 0.14f);
                var topInset = rect.width * 0.1f;
                var bottomInset = rect.width * 0.2f;

                var topLeft = AddVertex(vh, new Vector2(rect.xMin + topInset, topY));
                var topRight = AddVertex(vh, new Vector2(rect.xMax - topInset, topY));
                var bottomRight = AddVertex(vh, new Vector2(rect.xMax - bottomInset, bottomY));
                var bottomLeft = AddVertex(vh, new Vector2(rect.xMin + bottomInset, bottomY));

                vh.AddTriangle(topLeft, topRight, bottomRight);
                vh.AddTriangle(topLeft, bottomRight, bottomLeft);
            }

            private int AddVertex(VertexHelper vh, Vector2 position)
            {
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = position;
                vh.AddVert(vertex);
                return vh.currentVertCount - 1;
            }
        }

        private sealed class ItemDragHandler : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private MiniGameRiverCrossingGameView owner;
            private Cargo item;
            private bool dragging;
            private bool suppressClick;

            public void Initialize(MiniGameRiverCrossingGameView owner, Cargo item)
            {
                this.owner = owner;
                this.item = item;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                dragging = owner != null && owner.BeginItemDrag(item, eventData, transform as RectTransform);
                suppressClick = dragging;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (dragging)
                {
                    owner?.UpdateItemDrag(eventData);
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (dragging)
                {
                    owner?.EndItemDrag(item, eventData);
                }

                dragging = false;
                StartCoroutine(ClearClickSuppressionAfterFrame());
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (suppressClick)
                {
                    suppressClick = false;
                    return;
                }

                if (!dragging)
                {
                    owner?.OnItemClicked(item);
                }
            }

            private IEnumerator ClearClickSuppressionAfterFrame()
            {
                yield return null;
                suppressClick = false;
            }
        }
    }
}
