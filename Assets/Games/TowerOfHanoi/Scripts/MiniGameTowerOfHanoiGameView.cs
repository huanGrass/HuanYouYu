using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 汉诺塔小游戏运行体。
    /// </summary>
    public sealed class MiniGameTowerOfHanoiGameView : MiniGameBase
    {
        public const string GameIdConstant = "towerofhanoi";

        private const int ColumnCount = 3;
        private const int MinDiskCount = 3;
        private const int MaxDiskCount = 8;
        private const int LevelCountValue = MaxDiskCount - MinDiskCount + 1;
        private const int TargetColumnIndex = 2;
        private const float BoardWidth = 680f;
        private const float BoardHeight = 690f;
        private const float ColumnSpacing = 220f;
        private const float ColumnHitWidth = 190f;
        private const float MinDiskWidth = 96f;
        private const float MaxDiskWidthValue = 208f;
        private const float DiskHeight = 42f;
        private const float DiskOverlap = 4f;
        private const float SelectedDiskLift = 54f;
        private const float StackBaseY = -230f;
        private const float DiskMoveDuration = 0.16f;
        private const float CompletionSettlementDelay = 0.12f;
        private const float StatusMessageDuration = 1.25f;

        public static int LevelCount
        {
            get { return LevelCountValue; }
        }

        private static readonly Color BoardColor = new Color32(242, 248, 239, 220);
        private static readonly Color BoardShadowColor = new Color(0.21f, 0.31f, 0.23f, 0.16f);
        private static readonly Color BaseColor = new Color32(116, 92, 70, 255);
        private static readonly Color ColumnColor = new Color32(132, 108, 82, 255);
        private static readonly Color TargetColumnColor = new Color32(87, 150, 97, 76);
        private static readonly Color StatusColor = new Color32(66, 86, 72, 255);
        private static readonly Color InvalidStatusColor = new Color32(184, 79, 58, 255);
        private static readonly Color[] DiskColors =
        {
            new Color32(90, 166, 214, 255),
            new Color32(88, 181, 139, 255),
            new Color32(238, 178, 68, 255),
            new Color32(224, 116, 87, 255),
            new Color32(155, 124, 206, 255),
            new Color32(65, 149, 160, 255),
            new Color32(230, 142, 72, 255),
            new Color32(116, 157, 86, 255)
        };

        private readonly List<int>[] columns = new List<int>[ColumnCount];
        private readonly Dictionary<int, RectTransform> diskRects = new Dictionary<int, RectTransform>();
        private readonly Dictionary<int, Coroutine> diskMoveCoroutines = new Dictionary<int, Coroutine>();

        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI statusLabel;
        private Button restartButton;
        private Button levelSelectButton;
        private RectTransform boardRoot;
        private readonly RectTransform[] columnRoots = new RectTransform[ColumnCount];
        private readonly RoundedRectGraphic[] columnHighlights = new RoundedRectGraphic[ColumnCount];
        private int currentLevelIndex;
        private int diskCount;
        private int minimumMoveCount;
        private int moveCount;
        private int score;
        private int selectedColumnIndex = -1;
        private int draggedDiskSize = -1;
        private int dragSourceColumnIndex = -1;
        private int dragReleaseGuardFrames;
        private Vector2 dragStartPosition;
        private float statusMessageTimer;
        private bool statusIsInvalid;
        private bool isCompleted;
        private Coroutine completionSettlementRoutine;

        public MiniGameTowerOfHanoiGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameTowerOfHanoiView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameId, LevelCountValue);

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("TowerOfHanoiHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildBoard();
            BuildBottomActions();

            if (titleLabel == null || scoreLabel == null || restartButton == null || levelSelectButton == null || boardRoot == null)
            {
                throw new InvalidOperationException("TowerOfHanoi prefab structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelect();

            currentLevelIndex = levelProgress != null ? levelProgress.CurrentLevelIndex : 0;
            diskCount = MinDiskCount + currentLevelIndex;
            minimumMoveCount = (1 << diskCount) - 1;
            moveCount = 0;
            score = 0;
            selectedColumnIndex = -1;
            draggedDiskSize = -1;
            dragSourceColumnIndex = -1;
            dragReleaseGuardFrames = 0;
            statusMessageTimer = 0f;
            statusIsInvalid = false;
            isCompleted = false;
            StopCompletionSettlementRoutine();

            for (var i = 0; i < columns.Length; i++)
            {
                if (columns[i] == null)
                {
                    columns[i] = new List<int>();
                }
                else
                {
                    columns[i].Clear();
                }
            }

            for (var size = diskCount; size >= 1; size--)
            {
                columns[0].Add(size);
            }

            RebuildDisks();
            SetStatus(UiTextCatalog.Get("towerofhanoi.status.ready"), false);
            RefreshHud();
            RefreshColumnHighlights();
        }

        public override void Tick(float deltaTime)
        {
            TickDragReleaseFallback();

            if (statusMessageTimer <= 0f)
            {
                return;
            }

            statusMessageTimer -= Mathf.Max(0f, deltaTime);
            if (statusMessageTimer <= 0f && !isCompleted)
            {
                SetStatus(UiTextCatalog.Get("towerofhanoi.status.ready"), false);
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.towerofhanoi.help", null);
        }

        protected override void OnPauseRequested()
        {
            CloseLevelSelect();
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelect();
            StopCompletionSettlementRoutine();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            }

            foreach (var pair in diskMoveCoroutines)
            {
                if (pair.Value != null)
                {
                    HostBehaviour.StopCoroutine(pair.Value);
                }
            }

            diskMoveCoroutines.Clear();
        }

        private void BuildBoard()
        {
            var boardObject = CreateRectObject("TowerOfHanoiBoard", Shell.ContentHost);
            boardRoot = boardObject.GetComponent<RectTransform>();
            boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.sizeDelta = new Vector2(BoardWidth, BoardHeight);
            boardRoot.anchoredPosition = new Vector2(0f, -12f);

            var shadow = CreateRoundedRect("BoardShadow", boardRoot, BoardShadowColor, 36f, false);
            Stretch(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, -14f), new Vector2(10f, -14f));

            var board = CreateRoundedRect("BoardPanel", boardRoot, BoardColor, 36f, false);
            Stretch(board.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var baseBar = CreateRoundedRect("BaseBar", boardRoot, BaseColor, 18f, false);
            var baseRect = baseBar.rectTransform;
            baseRect.anchorMin = new Vector2(0.5f, 0.5f);
            baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.sizeDelta = new Vector2(610f, 36f);
            baseRect.anchoredPosition = new Vector2(0f, StackBaseY - 12f);

            for (var i = 0; i < ColumnCount; i++)
            {
                BuildColumn(i);
            }

            statusLabel = CreateText(boardRoot, "StatusLabel", string.Empty, 25f, FontStyles.Bold, StatusColor);
            statusLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            statusLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            statusLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            statusLabel.rectTransform.sizeDelta = new Vector2(580f, 46f);
            statusLabel.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.enableAutoSizing = true;
            statusLabel.fontSizeMin = 18f;
            statusLabel.fontSizeMax = 25f;
        }

        private void BuildColumn(int columnIndex)
        {
            var columnObject = CreateRectObject("TowerColumn_" + columnIndex, boardRoot);
            var columnRect = columnObject.GetComponent<RectTransform>();
            columnRect.anchorMin = new Vector2(0.5f, 0.5f);
            columnRect.anchorMax = new Vector2(0.5f, 0.5f);
            columnRect.pivot = new Vector2(0.5f, 0.5f);
            columnRect.sizeDelta = new Vector2(190f, 510f);
            columnRect.anchoredPosition = new Vector2(GetColumnX(columnIndex), -2f);
            columnRoots[columnIndex] = columnRect;

            var hitArea = CreateRoundedRect("HitArea", columnRect, new Color(1f, 1f, 1f, 0f), 28f, true);
            Stretch(hitArea.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var target = columnObject.AddComponent<TowerOfHanoiInputTarget>();
            target.Bind(this, columnIndex, 0);

            var highlight = CreateRoundedRect("Highlight", columnRect, columnIndex == TargetColumnIndex ? TargetColumnColor : Color.clear, 28f, false);
            Stretch(highlight.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 16f), new Vector2(-8f, -42f));
            columnHighlights[columnIndex] = highlight;

            var post = CreateRoundedRect("Post", columnRect, ColumnColor, 16f, false);
            var postRect = post.rectTransform;
            postRect.anchorMin = new Vector2(0.5f, 0.5f);
            postRect.anchorMax = new Vector2(0.5f, 0.5f);
            postRect.pivot = new Vector2(0.5f, 0.5f);
            postRect.sizeDelta = new Vector2(32f, 395f);
            postRect.anchoredPosition = new Vector2(0f, -31f);

            var label = CreateText(columnRect, "ColumnLabel", UiTextCatalog.Format("towerofhanoi.column.label", columnIndex + 1), 21f, FontStyles.Bold, StatusColor);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(160f, 32f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            label.alignment = TextAlignmentOptions.Center;
        }

        private void BuildBottomActions()
        {
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("TowerOfHanoiActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;
            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottomContainerRefs.ActionBar, "LevelSelectButton").Button;

            if (restartButton != null)
            {
                restartButton.gameObject.name = "RestartButton";
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveAllListeners();
                levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
                var label = levelSelectButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = UiTextCatalog.Get("common.action.level_select");
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 16f;
                    label.fontSizeMax = 21f;
                }
            }
        }

        private void RebuildDisks()
        {
            foreach (var pair in diskRects)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.gameObject);
                }
            }

            diskRects.Clear();
            for (var size = diskCount; size >= 1; size--)
            {
                var diskObject = CreateRectObject("Disk_" + size, boardRoot);
                var diskRect = diskObject.GetComponent<RectTransform>();
                diskRect.anchorMin = new Vector2(0.5f, 0.5f);
                diskRect.anchorMax = new Vector2(0.5f, 0.5f);
                diskRect.pivot = new Vector2(0.5f, 0.5f);
                diskRect.sizeDelta = new Vector2(GetDiskWidth(size), DiskHeight);

                var diskGraphic = CreateRoundedRect("Body", diskRect, DiskColors[(size - 1) % DiskColors.Length], 18f, true);
                Stretch(diskGraphic.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var shine = CreateRoundedRect("Shine", diskRect, new Color(1f, 1f, 1f, 0.18f), 12f, false);
                Stretch(shine.rectTransform, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);

                var label = CreateText(diskRect, "Label", size.ToString(), 21f, FontStyles.Bold, Color.white);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 1f), new Vector2(-8f, 3f));
                label.alignment = TextAlignmentOptions.Center;

                var target = diskObject.AddComponent<TowerOfHanoiInputTarget>();
                target.Bind(this, 0, size);
                diskRects[size] = diskRect;
            }

            RefreshDiskPositions(-1, Vector2.zero);
        }

        private void RefreshDiskPositions(int animatedDiskSize, Vector2 animatedStartPosition)
        {
            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                var stack = columns[columnIndex];
                for (var stackIndex = 0; stackIndex < stack.Count; stackIndex++)
                {
                    var size = stack[stackIndex];
                    RectTransform diskRect;
                    if (!diskRects.TryGetValue(size, out diskRect) || diskRect == null)
                    {
                        continue;
                    }

                    var position = GetDiskPosition(columnIndex, stackIndex);
                    var target = diskRect.GetComponent<TowerOfHanoiInputTarget>();
                    if (target != null)
                    {
                        target.Bind(this, columnIndex, size);
                    }

                    diskRect.SetAsLastSibling();
                    if (size == animatedDiskSize)
                    {
                        diskRect.anchoredPosition = animatedStartPosition;
                        StartDiskAnimation(size, diskRect, animatedStartPosition, position);
                    }
                    else if (size != draggedDiskSize)
                    {
                        StopDiskAnimation(size);
                        diskRect.anchoredPosition = position;
                    }
                }
            }

            if (statusLabel != null)
            {
                statusLabel.transform.SetAsLastSibling();
            }
        }

        private void StartDiskAnimation(int diskSize, RectTransform diskRect, Vector2 start, Vector2 end)
        {
            StopDiskAnimation(diskSize);
            diskMoveCoroutines[diskSize] = HostBehaviour.StartCoroutine(AnimateDisk(diskSize, diskRect, start, end));
        }

        private void StopDiskAnimation(int diskSize)
        {
            Coroutine coroutine;
            if (!diskMoveCoroutines.TryGetValue(diskSize, out coroutine))
            {
                return;
            }

            if (coroutine != null)
            {
                HostBehaviour.StopCoroutine(coroutine);
            }

            diskMoveCoroutines.Remove(diskSize);
        }

        private IEnumerator AnimateDisk(int diskSize, RectTransform diskRect, Vector2 start, Vector2 end)
        {
            var elapsed = 0f;
            while (elapsed < DiskMoveDuration && diskRect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / DiskMoveDuration);
                t = t * t * (3f - 2f * t);
                diskRect.anchoredPosition = Vector2.Lerp(start, end, t);
                yield return null;
            }

            if (diskRect != null)
            {
                diskRect.anchoredPosition = end;
            }

            diskMoveCoroutines.Remove(diskSize);
        }

        private void HandleColumnClicked(int columnIndex)
        {
            if (isCompleted || draggedDiskSize > 0 || !IsValidColumn(columnIndex))
            {
                return;
            }

            if (selectedColumnIndex < 0)
            {
                TrySelectColumn(columnIndex);
                return;
            }

            if (selectedColumnIndex == columnIndex)
            {
                ClearSelection();
                return;
            }

            if (TryMove(selectedColumnIndex, columnIndex))
            {
                ClearSelection();
            }
            else
            {
                SetStatus(UiTextCatalog.Get("towerofhanoi.status.invalid_move"), true);
            }
        }

        private void HandleDiskPointerClick(int diskSize)
        {
            var columnIndex = FindDiskColumn(diskSize);
            if (columnIndex >= 0)
            {
                HandleColumnClicked(columnIndex);
            }
        }

        private bool HandleBeginDrag(int diskSize, PointerEventData eventData)
        {
            if (isCompleted || diskSize <= 0)
            {
                return false;
            }

            var columnIndex = FindDiskColumn(diskSize);
            if (columnIndex < 0 || GetTopDisk(columnIndex) != diskSize)
            {
                SetStatus(UiTextCatalog.Get("towerofhanoi.status.only_top"), true);
                return false;
            }

            RectTransform diskRect;
            if (!diskRects.TryGetValue(diskSize, out diskRect) || diskRect == null)
            {
                return false;
            }

            ClearSelection();
            draggedDiskSize = diskSize;
            dragSourceColumnIndex = columnIndex;
            dragReleaseGuardFrames = 1;
            dragStartPosition = diskRect.anchoredPosition;
            diskRect.SetAsLastSibling();
            return true;
        }

        private void HandleDrag(int diskSize, PointerEventData eventData)
        {
            if (diskSize != draggedDiskSize || eventData == null || boardRoot == null)
            {
                return;
            }

            RectTransform diskRect;
            if (!diskRects.TryGetValue(diskSize, out diskRect) || diskRect == null)
            {
                return;
            }

            Vector2 localPosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, eventData.position, eventData.pressEventCamera, out localPosition))
            {
                diskRect.anchoredPosition = localPosition;
            }
        }

        private void HandleEndDrag(int diskSize, PointerEventData eventData)
        {
            if (diskSize != draggedDiskSize)
            {
                return;
            }

            CompleteDrag(diskSize, eventData);
        }

        private void TickDragReleaseFallback()
        {
            if (draggedDiskSize <= 0)
            {
                return;
            }

            if (dragReleaseGuardFrames > 0)
            {
                dragReleaseGuardFrames -= 1;
                return;
            }

            if (IsPrimaryPointerPressed())
            {
                return;
            }

            CompleteDrag(draggedDiskSize, null);
        }

        private void CompleteDrag(int diskSize, PointerEventData eventData)
        {
            var sourceColumn = dragSourceColumnIndex;
            RectTransform diskRect;
            var destinationColumn = diskRects.TryGetValue(diskSize, out diskRect) && diskRect != null
                ? FindDropColumn(diskRect.anchoredPosition)
                : FindDropColumn(eventData);
            draggedDiskSize = -1;
            dragSourceColumnIndex = -1;
            dragReleaseGuardFrames = 0;

            if (destinationColumn >= 0 && destinationColumn != sourceColumn && TryMove(sourceColumn, destinationColumn, false))
            {
                SnapDiskToCurrentStack(diskSize);
                HostBehaviour.StartCoroutine(SnapDiskToCurrentStackNextFrame(diskSize));
                return;
            }

            SnapDiskToCurrentStack(diskSize);

            if (destinationColumn >= 0 && destinationColumn != sourceColumn)
            {
                SetStatus(UiTextCatalog.Get("towerofhanoi.status.invalid_move"), true);
            }
        }

        private static bool IsPrimaryPointerPressed()
        {
            try
            {
                return Input.GetMouseButton(0) || Input.touchCount > 0;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private bool TrySelectColumn(int columnIndex)
        {
            if (GetTopDisk(columnIndex) <= 0)
            {
                SetStatus(UiTextCatalog.Get("towerofhanoi.status.empty_column"), true);
                return false;
            }

            selectedColumnIndex = columnIndex;
            SetStatus(UiTextCatalog.Get("towerofhanoi.status.selected"), false);
            RefreshColumnHighlights();
            LiftSelectedDisk();
            return true;
        }

        private bool TryMove(int fromColumn, int toColumn, bool animateMovedDisk = true)
        {
            if (!CanMove(fromColumn, toColumn))
            {
                return false;
            }

            var movingDisk = GetTopDisk(fromColumn);
            RectTransform diskRect;
            var oldPosition = diskRects.TryGetValue(movingDisk, out diskRect) && diskRect != null
                ? diskRect.anchoredPosition
                : GetDiskPosition(fromColumn, columns[fromColumn].Count - 1);

            columns[fromColumn].RemoveAt(columns[fromColumn].Count - 1);
            columns[toColumn].Add(movingDisk);
            moveCount += 1;
            score = CalculateScore();

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.82f);
            SetStatus(UiTextCatalog.Get("towerofhanoi.status.moved"), false);
            RefreshHud();
            RefreshDiskPositions(animateMovedDisk ? movingDisk : -1, oldPosition);

            if (!animateMovedDisk)
            {
                SnapDiskToCurrentStack(movingDisk);
            }

            if (columns[TargetColumnIndex].Count == diskCount)
            {
                CompleteLevel();
            }

            return true;
        }

        private bool CanMove(int fromColumn, int toColumn)
        {
            if (!IsValidColumn(fromColumn) || !IsValidColumn(toColumn) || fromColumn == toColumn || columns[fromColumn].Count == 0)
            {
                return false;
            }

            var movingDisk = GetTopDisk(fromColumn);
            var targetDisk = GetTopDisk(toColumn);
            return targetDisk <= 0 || movingDisk < targetDisk;
        }

        private void CompleteLevel()
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;
            ClearSelection();
            score = CalculateScore();
            SetStatus(UiTextCatalog.Get("towerofhanoi.status.completed"), false);
            RefreshHud();

            if (levelProgress != null)
            {
                levelProgress.UnlockNext();
            }

            var settlement = CreateSettlement();
            var isLastLevel = currentLevelIndex >= LevelCountValue - 1;
            StopCompletionSettlementRoutine();
            completionSettlementRoutine = HostBehaviour.StartCoroutine(ShowCompletionSettlementAfterMotion(settlement, isLastLevel));
        }

        private IEnumerator ShowCompletionSettlementAfterMotion(MiniGameSettlement settlement, bool isLastLevel)
        {
            while (diskMoveCoroutines.Count > 0)
            {
                yield return null;
            }

            yield return WaitForUnscaledSeconds(CompletionSettlementDelay);

            completionSettlementRoutine = null;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "TowerOfHanoiWinSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = isLastLevel ? MiniGameRewardSettlementPrimaryAction.Confirm : MiniGameRewardSettlementPrimaryAction.NextLevel,
                    Title = UiTextCatalog.Get("towerofhanoi.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("towerofhanoi.settlement.moves"), moveCount.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("towerofhanoi.settlement.minimum_moves"), minimumMoveCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate
                {
                    if (isLastLevel)
                    {
                        CompleteGame?.Invoke(settlement);
                        return;
                    }

                    if (levelProgress != null)
                    {
                        levelProgress.GoNext();
                    }

                    ResetGame();
                },
                delegate
                {
                    SaveNextLevelForReturn();
                    GrantSettlementReward(settlement);
                    CompleteGame?.Invoke(settlement);
                },
                true);
        }

        private void SaveNextLevelForReturn()
        {
            if (levelProgress != null)
            {
                levelProgress.SaveNextAsCurrent();
            }
        }

        private void StopCompletionSettlementRoutine()
        {
            if (completionSettlementRoutine == null)
            {
                return;
            }

            HostBehaviour.StopCoroutine(completionSettlementRoutine);
            completionSettlementRoutine = null;
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.towerofhanoi.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format(
                    "towerofhanoi.hud.summary",
                    currentLevelIndex + 1,
                    diskCount,
                    moveCount,
                    minimumMoveCount);
            }
        }

        private void RefreshColumnHighlights()
        {
            for (var i = 0; i < columnHighlights.Length; i++)
            {
                if (columnHighlights[i] == null)
                {
                    continue;
                }

                if (i == TargetColumnIndex)
                {
                    columnHighlights[i].color = TargetColumnColor;
                }
                else
                {
                    columnHighlights[i].color = Color.clear;
                }
            }
        }

        private void ClearSelection()
        {
            SnapSelectedDiskToStack();
            selectedColumnIndex = -1;
            RefreshColumnHighlights();
        }

        private void LiftSelectedDisk()
        {
            var selectedDisk = GetTopDisk(selectedColumnIndex);
            RectTransform diskRect;
            Vector2 position;
            if (selectedDisk <= 0 || !diskRects.TryGetValue(selectedDisk, out diskRect) || diskRect == null || !TryGetDiskStackPosition(selectedDisk, out position))
            {
                return;
            }

            StopDiskAnimation(selectedDisk);
            diskRect.SetAsLastSibling();
            diskRect.anchoredPosition = position + new Vector2(0f, SelectedDiskLift);
        }

        private void SnapSelectedDiskToStack()
        {
            var selectedDisk = GetTopDisk(selectedColumnIndex);
            if (selectedDisk > 0)
            {
                SnapDiskToCurrentStack(selectedDisk);
            }
        }

        private void SetStatus(string message, bool invalid)
        {
            statusMessageTimer = invalid ? StatusMessageDuration : 0f;
            statusIsInvalid = invalid;
            if (statusLabel != null)
            {
                statusLabel.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
                statusLabel.color = statusIsInvalid ? InvalidStatusColor : StatusColor;
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            CloseLevelSelect();
            StopCompletionSettlementRoutine();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "TowerOfHanoiSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("towerofhanoi.settlement.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("towerofhanoi.settlement.exit_label"), UiTextCatalog.Get("towerofhanoi.settlement.exit_value")),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void OnLevelSelectClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            if (levelSelectView != null)
            {
                CloseLevelSelect();
                return;
            }

            levelSelectView = MiniGameLevelSelectView.Create(
                Shell.PopupHost,
                MiniGameFontProvider.DefaultFont,
                LevelCountValue,
                levelProgress != null ? levelProgress.CurrentLevelIndex : currentLevelIndex,
                levelProgress != null ? levelProgress.UnlockedLevelCount : 1,
                "TowerOfHanoiLevelSelectPanel",
                "TowerOfHanoiLevelButton_",
                OnLevelSelected,
                CloseLevelSelect);
        }

        private void OnLevelSelected(int index)
        {
            if (levelProgress == null || !levelProgress.Select(index))
            {
                return;
            }

            CloseLevelSelect();
            ResetGame();
        }

        private void CloseLevelSelect()
        {
            if (levelSelectView == null)
            {
                return;
            }

            levelSelectView.Dispose();
            levelSelectView = null;
        }

        private MiniGameSettlement CreateSettlement()
        {
            var finalScore = CalculateScore();
            var coinCount = isCompleted ? Mathf.Max(30, (diskCount * 10) + (finalScore / 5)) : 0;
            if (isCompleted && moveCount == minimumMoveCount && moveCount > 0)
            {
                coinCount += 20;
            }

            var chestCount = isCompleted ? 1 : 0;
            return new MiniGameSettlement
            {
                Score = finalScore,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = UiTextCatalog.Format("towerofhanoi.settlement.summary", currentLevelIndex + 1, moveCount, finalScore)
            };
        }

        private int CalculateScore()
        {
            if (!isCompleted || minimumMoveCount <= 0)
            {
                return 0;
            }

            return Mathf.Max(0, minimumMoveCount * 10 - Mathf.Max(0, moveCount - minimumMoveCount) * 2);
        }

        private int GetTopDisk(int columnIndex)
        {
            if (!IsValidColumn(columnIndex) || columns[columnIndex].Count == 0)
            {
                return -1;
            }

            return columns[columnIndex][columns[columnIndex].Count - 1];
        }

        private int FindDiskColumn(int diskSize)
        {
            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                if (columns[columnIndex].Contains(diskSize))
                {
                    return columnIndex;
                }
            }

            return -1;
        }

        private bool TryGetDiskStackPosition(int diskSize, out Vector2 position)
        {
            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                var stackIndex = columns[columnIndex].IndexOf(diskSize);
                if (stackIndex >= 0)
                {
                    position = GetDiskPosition(columnIndex, stackIndex);
                    return true;
                }
            }

            position = Vector2.zero;
            return false;
        }

        private void SnapDiskToCurrentStack(int diskSize)
        {
            RectTransform diskRect;
            Vector2 position;
            if (!diskRects.TryGetValue(diskSize, out diskRect) || diskRect == null || !TryGetDiskStackPosition(diskSize, out position))
            {
                return;
            }

            StopDiskAnimation(diskSize);
            diskRect.anchoredPosition = position;
        }

        private IEnumerator SnapDiskToCurrentStackNextFrame(int diskSize)
        {
            yield return null;
            SnapDiskToCurrentStack(diskSize);
            yield return new WaitForEndOfFrame();
            SnapDiskToCurrentStack(diskSize);
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

        private int FindDropColumn(PointerEventData eventData)
        {
            if (eventData == null || boardRoot == null)
            {
                return -1;
            }

            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, eventData.position, eventData.pressEventCamera, out localPosition))
            {
                return -1;
            }

            return FindDropColumn(localPosition);
        }

        private static int FindDropColumn(Vector2 localPosition)
        {
            for (var i = 0; i < ColumnCount; i++)
            {
                if (Mathf.Abs(localPosition.x - GetColumnX(i)) <= ColumnHitWidth * 0.5f)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsValidColumn(int columnIndex)
        {
            return columnIndex >= 0 && columnIndex < ColumnCount;
        }

        private static float GetColumnX(int columnIndex)
        {
            return (columnIndex - 1) * ColumnSpacing;
        }

        private Vector2 GetDiskPosition(int columnIndex, int stackIndex)
        {
            return new Vector2(GetColumnX(columnIndex), StackBaseY + DiskHeight * 0.5f + stackIndex * (DiskHeight - DiskOverlap));
        }

        private static float GetDiskWidth(int diskSize)
        {
            return Mathf.Lerp(MinDiskWidth, MaxDiskWidthValue, (diskSize - 1f) / (MaxDiskCount - 1f));
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var label = textObject.AddComponent<TextMeshProUGUI>();
            var font = MiniGameFontProvider.DefaultFont;
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            return label;
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, float cornerRadius, bool raycastTarget)
        {
            var gameObject = CreateRectObject(name, parent);
            gameObject.AddComponent<CanvasRenderer>();
            var graphic = gameObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class TowerOfHanoiInputTarget : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private MiniGameTowerOfHanoiGameView owner;
            private int columnIndex;
            private int diskSize;
            private int activeDragDiskSize;
            private bool dragging;

            public void Bind(MiniGameTowerOfHanoiGameView view, int targetColumnIndex, int targetDiskSize)
            {
                owner = view;
                columnIndex = targetColumnIndex;
                diskSize = targetDiskSize;
                activeDragDiskSize = 0;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (owner == null || dragging)
                {
                    dragging = false;
                    return;
                }

                if (diskSize > 0)
                {
                    owner.HandleDiskPointerClick(diskSize);
                }
                else
                {
                    owner.HandleColumnClicked(columnIndex);
                }
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                activeDragDiskSize = owner != null && diskSize <= 0 ? owner.GetTopDisk(columnIndex) : diskSize;
                dragging = owner != null && activeDragDiskSize > 0 && owner.HandleBeginDrag(activeDragDiskSize, eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (dragging && owner != null)
                {
                    owner.HandleDrag(activeDragDiskSize, eventData);
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (dragging && owner != null)
                {
                    owner.HandleEndDrag(activeDragDiskSize, eventData);
                }

                activeDragDiskSize = 0;
                dragging = false;
            }
        }
    }
}
