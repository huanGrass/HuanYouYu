using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameSudokuView : MiniGameBase
    {
        public const string GameIdConstant = "sudoku";

        private static readonly Color ModeTabActiveColor = new Color32(238, 196, 110, 255);
        private static readonly Color ModeTabInactiveColor = new Color32(242, 233, 214, 255);
        private static readonly Color ModeTabActiveTextColor = new Color32(69, 84, 61, 255);
        private static readonly Color ModeTabInactiveTextColor = new Color32(112, 107, 89, 255);
        private static readonly Color ActionButtonTextColor = new Color32(66, 80, 63, 255);
        private static readonly Color HintTextColor = new Color32(38, 143, 116, 255);
        private static readonly Color PrimaryActionButtonColor = new Color32(239, 205, 123, 255);
        private static readonly Color SecondaryActionButtonColor = new Color32(244, 236, 222, 255);
        private static readonly Color ActionPanelColor = new Color32(249, 243, 230, 245);
        private const float TopLayoutInset = 224f;
        private const float BottomLayoutInset = 324f;
        private const float BottomPreferredHeight = 284f;
        private const float HintRevealDuration = 0.42f;

        private RectTransform topRoot;
        private RectTransform bottomRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI summaryLabel;
        private Button restartButton;
        private Button hintButton;
        private Button clearButton;
        private Button fillModeButton;
        private Button notesButton;
        private Button autoCandidatesButton;
        private Button resetRoundButton;
        private Button easyDifficultyButton;
        private Button normalDifficultyButton;
        private Button hardDifficultyButton;
        private RectTransform keypadPanelHost;
        private SudokuRuntimeView runtimeView;
        private SudokuBoardState boardState;
        private SudokuPuzzle currentPuzzle;
        private SudokuDifficulty selectedDifficulty = SudokuDifficulty.Normal;
        private float elapsedSeconds;
        private int displayedSeconds = -1;
        private int selectedCellIndex = -1;
        private bool gameCompleted;
        private bool isNotesModeEnabled;
        private bool isHintRevealPlaying;
        private Coroutine hintRevealCoroutine;
        private RectTransform hintRevealDigit;

        public GameSudokuView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameSudokuView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (gameCompleted)
            {
                return;
            }

            elapsedSeconds += Mathf.Max(0f, deltaTime);
            var wholeSeconds = Mathf.FloorToInt(elapsedSeconds);
            if (wholeSeconds != displayedSeconds)
            {
                displayedSeconds = wholeSeconds;
                RefreshHud();
            }
        }

        protected override MiniGameShellLayout CreateShellLayout()
        {
            return new MiniGameShellLayout(TopLayoutInset, BottomLayoutInset, MiniGameShellBottomMode.DefaultSlot);
        }

        protected override void BuildOrBindSections()
        {
            BuildTopSection();
            var actionBar = BuildBottomSection();

            fillModeButton = CreateActionButton("FillModeButton", actionBar, ModeTabInactiveColor, 22f);
            notesButton = CreateActionButton("NotesButton", actionBar, ModeTabInactiveColor, 22f);
            autoCandidatesButton = CreateActionButton("AutoCandidatesButton", actionBar, SecondaryActionButtonColor, 18f);
            hintButton = CreateActionButton("HintButton", actionBar, SecondaryActionButtonColor, 18f);
            clearButton = CreateActionButton("ClearButton", actionBar, SecondaryActionButtonColor, 18f);
            resetRoundButton = CreateActionButton("ResetRoundButton", actionBar, SecondaryActionButtonColor, 18f);
            restartButton = CreateActionButton("RestartButton", actionBar, PrimaryActionButtonColor, 22f);
            RepositionActionButtons(actionBar);

            fillModeButton.onClick.RemoveAllListeners();
            fillModeButton.onClick.AddListener(OnFillModeClicked);
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
            hintButton.onClick.RemoveAllListeners();
            hintButton.onClick.AddListener(OnHintClicked);
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(OnClearClicked);
            notesButton.onClick.RemoveAllListeners();
            notesButton.onClick.AddListener(OnNotesClicked);
            autoCandidatesButton.onClick.RemoveAllListeners();
            autoCandidatesButton.onClick.AddListener(OnAutoCandidatesClicked);
            resetRoundButton.onClick.RemoveAllListeners();
            resetRoundButton.onClick.AddListener(OnResetRoundClicked);

            EnsureModeButtonLabel(fillModeButton.transform, "sudoku.input.fill", "Fill");
            EnsureModeButtonLabel(notesButton.transform, "sudoku.action.notes", "Notes");
            EnsureActionButtonLabel(restartButton.transform, "sudoku.action.restart", "新开一局", 22f);
            EnsureActionButtonLabel(resetRoundButton.transform, "sudoku.action.reset_round", "Reset", 20f);
            EnsureActionButtonLabel(hintButton.transform, "common.action.hint", "Hint", 20f);
            EnsureActionButtonLabel(clearButton.transform, "sudoku.action.clear", "Clear", 20f);
            EnsureActionButtonLabel(autoCandidatesButton.transform, "sudoku.action.auto_candidates", "Auto", 20f);

            runtimeView = new SudokuRuntimeView(
                Shell.ContentHost,
                keypadPanelHost,
                titleLabel.font,
                titleLabel.fontSharedMaterial,
                OnCellSelected,
                OnDigitInput);
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("sudoku.help.gameplay", null);
        }

        protected override void ResetGame()
        {
            StartPuzzle(SudokuPuzzleGenerator.GeneratePuzzle(selectedDifficulty));
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            StopHintRevealAnimation();

            if (fillModeButton != null)
            {
                fillModeButton.onClick.RemoveListener(OnFillModeClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (hintButton != null)
            {
                hintButton.onClick.RemoveListener(OnHintClicked);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(OnClearClicked);
            }

            if (notesButton != null)
            {
                notesButton.onClick.RemoveListener(OnNotesClicked);
            }

            if (autoCandidatesButton != null)
            {
                autoCandidatesButton.onClick.RemoveListener(OnAutoCandidatesClicked);
            }

            if (easyDifficultyButton != null)
            {
                easyDifficultyButton.onClick.RemoveListener(OnEasyDifficultyClicked);
            }

            if (normalDifficultyButton != null)
            {
                normalDifficultyButton.onClick.RemoveListener(OnNormalDifficultyClicked);
            }

            if (hardDifficultyButton != null)
            {
                hardDifficultyButton.onClick.RemoveListener(OnHardDifficultyClicked);
            }

            if (resetRoundButton != null)
            {
                resetRoundButton.onClick.RemoveListener(OnResetRoundClicked);
            }

            if (runtimeView != null)
            {
                runtimeView.Dispose();
                runtimeView = null;
            }
        }

        private void BuildTopSection()
        {
            var topConfig = MiniGameShellTopBarBuilder.CreateDefaultConfig("SudokuTop");
            topConfig.ShadowAnchorMin = new Vector2(0.21f, 0.12f);
            topConfig.ShadowAnchorMax = new Vector2(0.79f, 0.91f);
            topConfig.HeaderAnchorMin = new Vector2(0.20f, 0.14f);
            topConfig.HeaderAnchorMax = new Vector2(0.80f, 0.93f);
            topConfig.HeaderPadding = new RectOffset(22, 22, 12, 12);
            topConfig.HeaderSpacing = 4f;
            topConfig.PreferredHeight = 142f;
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(Shell.TopHost, topConfig);
            topRoot = topBarRefs.Root;
            titleLabel = topBarRefs.TitleText;
            summaryLabel = topBarRefs.ScoreText;

            if (titleLabel == null || summaryLabel == null)
            {
                throw new InvalidOperationException("Sudoku top section is incomplete.");
            }

            BuildDifficultyButtons();
        }

        private void BuildDifficultyButtons()
        {
            var header = topRoot == null ? null : topRoot.Find("Header") as RectTransform;
            if (header == null)
            {
                throw new InvalidOperationException("Sudoku top section is incomplete.");
            }

            var difficultyBar = CreateRect(
                "DifficultyBar",
                header,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, 34f),
                Vector2.zero);
            difficultyBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            var layout = difficultyBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            easyDifficultyButton = CreateDifficultyButton(
                "EasyDifficultyButton",
                difficultyBar,
                "sudoku.hud.difficulty.easy",
                "Easy",
                OnEasyDifficultyClicked);
            normalDifficultyButton = CreateDifficultyButton(
                "NormalDifficultyButton",
                difficultyBar,
                "sudoku.hud.difficulty.normal",
                "Normal",
                OnNormalDifficultyClicked);
            hardDifficultyButton = CreateDifficultyButton(
                "HardDifficultyButton",
                difficultyBar,
                "sudoku.hud.difficulty.hard",
                "Hard",
                OnHardDifficultyClicked);
        }

        private RectTransform BuildBottomSection()
        {
            bottomRoot = CreateRect("SudokuBottom", Shell.BottomHost, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bottomRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = BottomPreferredHeight;

            keypadPanelHost = CreateRect(
                "KeypadPanelHost",
                bottomRoot,
                new Vector2(0.04f, 0.07f),
                new Vector2(0.52f, 0.94f),
                Vector2.zero,
                Vector2.zero);

            var actionPanel = CreateRect(
                "ActionPanel",
                bottomRoot,
                new Vector2(0.57f, 0.07f),
                new Vector2(0.96f, 0.94f),
                Vector2.zero,
                Vector2.zero);
            AddRoundedGraphic(actionPanel.gameObject, ActionPanelColor, 28f).raycastTarget = false;

            var actionBar = CreateRect(
                "ActionBar",
                actionPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(240f, 252f),
                new Vector2(0f, -16f));
            actionBar.pivot = new Vector2(0.5f, 1f);

            if (keypadPanelHost == null)
            {
                throw new InvalidOperationException("Sudoku bottom section is incomplete.");
            }

            return actionBar;
        }

        private void RefreshAll()
        {
            RefreshHud();
            RefreshModeButtons();

            if (runtimeView != null && boardState != null)
            {
                runtimeView.Render(boardState, selectedCellIndex);
            }
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.GetOrFallback("game.sudoku.name", "Sudoku");
            }

            if (summaryLabel != null)
            {
                summaryLabel.text = UiTextCatalog.Format(
                    "sudoku.hud.summary",
                    GetDifficultyLabel(selectedDifficulty),
                    BuildElapsedTimeText(),
                    BuildProgressText());
            }
        }

        private string BuildElapsedTimeText()
        {
            var wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            var minutes = wholeSeconds / 60;
            var seconds = wholeSeconds % 60;
            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        private string BuildProgressText()
        {
            return UiTextCatalog.Format("sudoku.hud.progress", CountFilledCells(), SudokuBoardState.CellCount);
        }

        private int CountFilledCells()
        {
            if (boardState == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < SudokuBoardState.CellCount; i++)
            {
                if (boardState.GetValue(i) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);
            var settlement = BuildExitSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "SudokuSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("sudoku.settlement.time"), BuildElapsedTimeText()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("sudoku.settlement.difficulty"), GetDifficultyLabel(selectedDifficulty)),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnFillModeClicked()
        {
            if (isNotesModeEnabled)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            }

            isNotesModeEnabled = false;
            RefreshAll();
        }

        private void OnRestartClicked()
        {
            if (isHintRevealPlaying)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.92f);
            ResetGame();
        }

        private void OnResetRoundClicked()
        {
            if (isHintRevealPlaying || currentPuzzle == null)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            StartPuzzle(currentPuzzle);
        }

        private void OnHintClicked()
        {
            if (gameCompleted || isHintRevealPlaying || boardState == null)
            {
                return;
            }

            int hintCellIndex;
            int hintValue;
            if (!boardState.TryFindHint(out hintCellIndex, out hintValue))
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            selectedCellIndex = hintCellIndex;
            RefreshAll();

            if (HostBehaviour == null)
            {
                ApplyHintReveal(hintCellIndex);
                return;
            }

            hintRevealCoroutine = HostBehaviour.StartCoroutine(PlayHintRevealRoutine(hintCellIndex, hintValue));
        }

        private void OnNotesClicked()
        {
            if (!isNotesModeEnabled)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            }

            isNotesModeEnabled = true;
            RefreshAll();
        }

        private void OnAutoCandidatesClicked()
        {
            if (gameCompleted || isHintRevealPlaying || boardState == null)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            boardState.RebuildAllCandidates();
            RefreshAll();
        }

        private void OnEasyDifficultyClicked()
        {
            SelectDifficulty(SudokuDifficulty.Easy);
        }

        private void OnNormalDifficultyClicked()
        {
            SelectDifficulty(SudokuDifficulty.Normal);
        }

        private void OnHardDifficultyClicked()
        {
            SelectDifficulty(SudokuDifficulty.Hard);
        }

        private void SelectDifficulty(SudokuDifficulty difficulty)
        {
            if (isHintRevealPlaying || selectedDifficulty == difficulty)
            {
                return;
            }

            selectedDifficulty = difficulty;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Shuffle, 0.92f);
            ResetGame();
        }

        private void OnCellSelected(int cellIndex)
        {
            if (isHintRevealPlaying || boardState == null || cellIndex < 0 || cellIndex >= SudokuBoardState.CellCount)
            {
                return;
            }

            selectedCellIndex = cellIndex;
            RefreshAll();
        }

        private void OnDigitInput(int digit)
        {
            if (gameCompleted || isHintRevealPlaying || boardState == null || digit < 1 || digit > 9 || !boardState.CanEdit(selectedCellIndex))
            {
                return;
            }

            if (isNotesModeEnabled)
            {
                boardState.ToggleCandidate(selectedCellIndex, digit);
            }
            else
            {
                boardState.SetPlayerValue(selectedCellIndex, digit);
                TryCompleteGame();
            }

            RefreshAll();
        }

        private void OnClearClicked()
        {
            if (isHintRevealPlaying)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.88f);
            OnClearInput();
        }

        private void OnClearInput()
        {
            if (gameCompleted || isHintRevealPlaying || boardState == null || !boardState.CanEdit(selectedCellIndex))
            {
                return;
            }

            if (isNotesModeEnabled)
            {
                boardState.ClearCandidates(selectedCellIndex);
            }
            else
            {
                boardState.ClearPlayerValue(selectedCellIndex);
            }

            RefreshAll();
        }

        private void TryCompleteGame()
        {
            if (gameCompleted || boardState == null || !boardState.IsSolved())
            {
                return;
            }

            gameCompleted = true;
            selectedCellIndex = -1;
            RefreshAll();
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.96f);

            var settlement = BuildSolvedSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "SudokuSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("sudoku.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("sudoku.settlement.time"), BuildElapsedTimeText()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("sudoku.settlement.difficulty"), GetDifficultyLabel(selectedDifficulty)),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private IEnumerator PlayHintRevealRoutine(int cellIndex, int value)
        {
            isHintRevealPlaying = true;
            RefreshModeButtons();

            var targetPosition = Vector2.zero;
            if (runtimeView == null || !runtimeView.TryGetCellCenterInRoot(cellIndex, out targetPosition))
            {
                ApplyHintReveal(cellIndex);
                isHintRevealPlaying = false;
                hintRevealCoroutine = null;
                RefreshModeButtons();
                yield break;
            }

            hintRevealDigit = CreateHintRevealDigit(value);
            var canvasGroup = hintRevealDigit.GetComponent<CanvasGroup>();
            var startPosition = Vector2.zero;
            var elapsed = 0f;
            while (elapsed < HintRevealDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / HintRevealDuration);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                hintRevealDigit.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
                hintRevealDigit.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.46f, eased);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0.72f, progress);
                }

                yield return null;
            }

            ApplyHintReveal(cellIndex);
            DestroyHintRevealDigit();
            isHintRevealPlaying = false;
            hintRevealCoroutine = null;
            RefreshModeButtons();
        }

        private void ApplyHintReveal(int cellIndex)
        {
            if (boardState == null || !boardState.ApplyHint(cellIndex))
            {
                return;
            }

            selectedCellIndex = cellIndex;
            RefreshAll();
            TryCompleteGame();
        }

        private RectTransform CreateHintRevealDigit(int value)
        {
            DestroyHintRevealDigit();

            var parent = runtimeView == null ? Shell.ContentHost : runtimeView.Root;
            var digitObject = new GameObject(
                "SudokuHintFlyDigit",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(TextMeshProUGUI));
            var rect = digitObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 150f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * 1.35f;

            var label = digitObject.GetComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.text = value.ToString();
            label.fontSize = 92f;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HintTextColor;
            if (titleLabel != null)
            {
                label.font = titleLabel.font;
                label.fontSharedMaterial = titleLabel.fontSharedMaterial;
            }

            return rect;
        }

        private void StopHintRevealAnimation()
        {
            if (hintRevealCoroutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(hintRevealCoroutine);
                hintRevealCoroutine = null;
            }

            isHintRevealPlaying = false;
            DestroyHintRevealDigit();
        }

        private void DestroyHintRevealDigit()
        {
            if (hintRevealDigit == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(hintRevealDigit.gameObject);
            hintRevealDigit = null;
        }

        private Button CreateActionButton(string name, RectTransform parent, Color backgroundColor, float radius)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            var background = buttonObject.GetComponent<RoundedRectGraphic>();
            background.CornerRadius = radius;
            background.color = backgroundColor;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.88f);
            return button;
        }

        private Button CreateDifficultyButton(
            string name,
            RectTransform parent,
            string textKey,
            string fallback,
            UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateActionButton(name, parent, SecondaryActionButtonColor, 17f);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);

            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(106f, 34f);
            }

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 106f;
            layout.preferredHeight = 34f;

            EnsureButtonLabel(button.transform, textKey, fallback, 18f);
            return button;
        }

        private void RepositionActionButtons(RectTransform actionBar)
        {
            SetButtonLayout(actionBar.Find("FillModeButton") as RectTransform, -62f, -14f, 116f, 46f);
            SetButtonLayout(actionBar.Find("NotesButton") as RectTransform, 62f, -14f, 116f, 46f);
            SetButtonLayout(actionBar.Find("AutoCandidatesButton") as RectTransform, -62f, -72f, 116f, 48f);
            SetButtonLayout(actionBar.Find("HintButton") as RectTransform, 62f, -72f, 116f, 48f);
            SetButtonLayout(actionBar.Find("ClearButton") as RectTransform, -62f, -132f, 116f, 48f);
            SetButtonLayout(actionBar.Find("ResetRoundButton") as RectTransform, 62f, -132f, 116f, 48f);
            SetButtonLayout(actionBar.Find("RestartButton") as RectTransform, 0f, -194f, 240f, 52f);
        }

        private static void SetButtonLayout(RectTransform rectTransform, float anchoredX, float anchoredY, float width, float height)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(anchoredX, anchoredY);
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private void RefreshModeButtons()
        {
            RefreshModeButton(fillModeButton, !isNotesModeEnabled, "sudoku.input.fill", "Fill");
            RefreshModeButton(notesButton, isNotesModeEnabled, "sudoku.action.notes", "Notes");
            RefreshActionButtonVisual(autoCandidatesButton, false);
            RefreshActionButtonVisual(hintButton, false);
            RefreshActionButtonVisual(clearButton, false);
            RefreshActionButtonVisual(resetRoundButton, false);
            RefreshActionButtonVisual(restartButton, true);
            if (hintButton != null)
            {
                hintButton.interactable = !isHintRevealPlaying;
            }

            RefreshDifficultyButton(easyDifficultyButton, SudokuDifficulty.Easy);
            RefreshDifficultyButton(normalDifficultyButton, SudokuDifficulty.Normal);
            RefreshDifficultyButton(hardDifficultyButton, SudokuDifficulty.Hard);
        }

        private void RefreshModeButton(Button button, bool isActive, string textKey, string fallback)
        {
            if (button == null)
            {
                return;
            }

            var background = button.targetGraphic as RoundedRectGraphic;
            if (background != null)
            {
                background.color = isActive ? ModeTabActiveColor : ModeTabInactiveColor;
            }

            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = UiTextCatalog.GetOrFallback(textKey, fallback);
                label.color = isActive ? ModeTabActiveTextColor : ModeTabInactiveTextColor;
            }
        }

        private void RefreshActionButtonVisual(Button button, bool isPrimary)
        {
            if (button == null)
            {
                return;
            }

            var background = button.targetGraphic as RoundedRectGraphic;
            if (background != null)
            {
                background.color = isPrimary ? PrimaryActionButtonColor : SecondaryActionButtonColor;
            }
        }

        private void RefreshDifficultyButton(Button button, SudokuDifficulty difficulty)
        {
            if (button == null)
            {
                return;
            }

            var isActive = selectedDifficulty == difficulty;
            var background = button.targetGraphic as RoundedRectGraphic;
            if (background != null)
            {
                background.color = isActive ? ModeTabActiveColor : SecondaryActionButtonColor;
            }

            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = GetDifficultyLabel(difficulty);
                label.color = isActive ? ModeTabActiveTextColor : ActionButtonTextColor;
            }
        }

        private static string GetDifficultyLabel(SudokuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case SudokuDifficulty.Easy:
                    return UiTextCatalog.GetOrFallback("sudoku.hud.difficulty.easy", "Easy");
                case SudokuDifficulty.Hard:
                    return UiTextCatalog.GetOrFallback("sudoku.hud.difficulty.hard", "Hard");
                default:
                    return UiTextCatalog.GetOrFallback("sudoku.hud.difficulty.normal", "Normal");
            }
        }

        private MiniGameSettlement BuildSolvedSettlement()
        {
            return new MiniGameSettlement
            {
                Score = Mathf.Max(0, 3600 - Mathf.FloorToInt(elapsedSeconds)),
                CoinCount = 60,
                ChestCount = 1,
                Summary = UiTextCatalog.Format(
                    "sudoku.settlement.win",
                    BuildElapsedTimeText(),
                    60,
                    1)
            };
        }

        private MiniGameSettlement BuildExitSettlement()
        {
            var manualFilledCount = CountManualFilledCells();
            return new MiniGameSettlement
            {
                Score = 0,
                CoinCount = manualFilledCount,
                ChestCount = 0,
                Summary = UiTextCatalog.Format(
                    "sudoku.settlement.exit",
                    manualFilledCount,
                    manualFilledCount,
                    0)
            };
        }

        private int CountManualFilledCells()
        {
            if (boardState == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < SudokuBoardState.CellCount; i++)
            {
                if (boardState.IsGiven(i) || boardState.IsHintRevealed(i))
                {
                    continue;
                }

                if (boardState.GetValue(i) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureModeButtonLabel(Transform buttonTransform, string textKey, string fallback)
        {
            EnsureButtonLabel(buttonTransform, textKey, fallback, 21f);
        }

        private void EnsureActionButtonLabel(Transform buttonTransform, string textKey, string fallback, float fontSize)
        {
            EnsureButtonLabel(buttonTransform, textKey, fallback, fontSize);
        }

        private void EnsureButtonLabel(Transform buttonTransform, string textKey, string fallback, float fontSize)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var label = buttonTransform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(buttonTransform, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);
                label = labelObject.GetComponent<TextMeshProUGUI>();
            }

            label.raycastTarget = false;
            label.text = UiTextCatalog.GetOrFallback(textKey, fallback);
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.Center;
            label.color = ActionButtonTextColor;

            if (titleLabel != null)
            {
                label.font = titleLabel.font;
                label.fontSharedMaterial = titleLabel.fontSharedMaterial;
            }
        }

        private void StartPuzzle(SudokuPuzzle puzzle)
        {
            Shell.ClosePopup();
            StopHintRevealAnimation();
            currentPuzzle = puzzle;
            boardState = puzzle == null ? null : new SudokuBoardState(puzzle);
            elapsedSeconds = 0f;
            displayedSeconds = 0;
            selectedCellIndex = boardState == null ? -1 : boardState.FindFirstEditableCell();
            gameCompleted = false;
            isNotesModeEnabled = false;
            RefreshAll();
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;
            return rectTransform;
        }

        private static RoundedRectGraphic AddRoundedGraphic(GameObject gameObject, Color color, float cornerRadius)
        {
            EnsureCanvasRenderer(gameObject);
            var graphic = gameObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            return graphic;
        }

        private static void EnsureCanvasRenderer(GameObject gameObject)
        {
            if (gameObject.GetComponent<CanvasRenderer>() == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }
        }
    }
}
