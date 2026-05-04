using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class BullsCowsGameView : MiniGameBase
    {
        public const string GameIdConstant = "bulls-cows";

        private const int GuessLength = 4;
        private const int RewardTargetAttempts = 8;
        private static readonly Color PanelColor = new Color32(247, 250, 244, 238);
        private static readonly Color SlotColor = new Color32(255, 255, 255, 255);
        private static readonly Color KeyColor = new Color32(232, 244, 255, 255);
        private static readonly Color UsedKeyColor = new Color32(212, 222, 226, 255);

        private readonly System.Random random = new System.Random();
        private readonly Button[] digitButtons = new Button[10];
        private readonly TextMeshProUGUI[] guessSlots = new TextMeshProUGUI[GuessLength];
        private readonly List<TextMeshProUGUI> historyRows = new List<TextMeshProUGUI>();
        private readonly List<string> historyEntries = new List<string>();
        private readonly HashSet<int> usedDigits = new HashSet<int>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private RectTransform historyRoot;
        private ScrollRect historyScrollRect;
        private Button backspaceButton;
        private Button restartButton;
        private string answer;
        private string currentGuess = string.Empty;
        private int attemptsUsed;
        private bool isFinished;

        public BullsCowsGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "BullsCowsView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public static bool IsValidGuess(string guess)
        {
            if (string.IsNullOrWhiteSpace(guess) || guess.Length != GuessLength)
            {
                return false;
            }

            var seen = new HashSet<char>();
            for (var i = 0; i < guess.Length; i++)
            {
                if (guess[i] < '0' || guess[i] > '9' || !seen.Add(guess[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static void EvaluateGuess(string answer, string guess, out int bulls, out int cows)
        {
            bulls = 0;
            cows = 0;
            if (!IsValidGuess(answer) || !IsValidGuess(guess))
            {
                return;
            }

            for (var i = 0; i < GuessLength; i++)
            {
                if (guess[i] == answer[i])
                {
                    bulls++;
                }
                else if (answer.IndexOf(guess[i]) >= 0)
                {
                    cows++;
                }
            }
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("BullsCowsHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();
            BuildBottom();
        }

        protected override MiniGameShellLayout CreateShellLayout()
        {
            return new MiniGameShellLayout(MiniGameShellLayout.DefaultTopInset, 430f, MiniGameShellBottomMode.DefaultSlot);
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            answer = GenerateAnswer();
            currentGuess = string.Empty;
            attemptsUsed = 0;
            isFinished = false;
            usedDigits.Clear();
            historyEntries.Clear();
            ClearHistoryRows();

            RefreshAll();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.bulls-cows.help", null);
        }

        protected override void OnPauseRequested()
        {
            if (!isFinished)
            {
                Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
            }
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            for (var i = 0; i < digitButtons.Length; i++)
            {
                if (digitButtons[i] != null)
                {
                    digitButtons[i].onClick.RemoveAllListeners();
                }
            }

            if (backspaceButton != null)
            {
                backspaceButton.onClick.RemoveListener(OnBackspaceClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
        }

        private void BuildContent()
        {
            var rootObject = CreateRectObject("BullsCowsContent", Shell.ContentHost);
            var root = rootObject.GetComponent<RectTransform>();
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(32f, 24f), new Vector2(-32f, -18f));
            EnsureRoundedRectGraphic(rootObject, PanelColor, 30f, false);

            var layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var guessRow = CreateRectObject("GuessRow", root);
            guessRow.AddComponent<LayoutElement>().preferredHeight = 154f;
            var guessLayout = guessRow.AddComponent<HorizontalLayoutGroup>();
            guessLayout.spacing = 16f;
            guessLayout.childAlignment = TextAnchor.MiddleCenter;
            guessLayout.childControlWidth = false;
            guessLayout.childControlHeight = false;
            guessLayout.childForceExpandWidth = false;
            guessLayout.childForceExpandHeight = false;

            for (var i = 0; i < GuessLength; i++)
            {
                guessSlots[i] = CreateGuessSlot(guessRow.transform, i);
            }

            var historyObject = CreateRectObject("BullsCowsHistory", root);
            var historyViewport = historyObject.GetComponent<RectTransform>();
            historyObject.AddComponent<LayoutElement>().preferredHeight = 462f;
            var historyImage = historyObject.AddComponent<Image>();
            historyImage.color = new Color32(255, 255, 255, 1);
            var historyMask = historyObject.AddComponent<Mask>();
            historyMask.showMaskGraphic = false;
            historyScrollRect = historyObject.AddComponent<ScrollRect>();
            historyScrollRect.horizontal = false;
            historyScrollRect.vertical = true;
            historyScrollRect.movementType = ScrollRect.MovementType.Clamped;
            historyScrollRect.viewport = historyViewport;

            var historyContentObject = CreateRectObject("BullsCowsHistoryContent", historyViewport);
            historyRoot = historyContentObject.GetComponent<RectTransform>();
            historyRoot.anchorMin = new Vector2(0f, 1f);
            historyRoot.anchorMax = new Vector2(1f, 1f);
            historyRoot.pivot = new Vector2(0.5f, 1f);
            historyRoot.offsetMin = Vector2.zero;
            historyRoot.offsetMax = Vector2.zero;
            var historyContentSize = historyContentObject.AddComponent<ContentSizeFitter>();
            historyContentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScrollRect.content = historyRoot;

            var historyLayout = historyContentObject.AddComponent<VerticalLayoutGroup>();
            historyLayout.spacing = 7f;
            historyLayout.childAlignment = TextAnchor.UpperCenter;
            historyLayout.childControlWidth = true;
            historyLayout.childControlHeight = true;
            historyLayout.childForceExpandWidth = true;
            historyLayout.childForceExpandHeight = false;

        }

        private void BuildBottom()
        {
            var rootObject = CreateRectObject("BullsCowsControls", Shell.BottomHost);
            var root = rootObject.GetComponent<RectTransform>();
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(24f, 14f), new Vector2(-24f, -14f));

            var layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var keypadObject = CreateRectObject("Keypad", root);
            keypadObject.AddComponent<LayoutElement>().preferredHeight = 294f;
            var keypad = keypadObject.AddComponent<GridLayoutGroup>();
            keypad.cellSize = new Vector2(150f, 62f);
            keypad.spacing = new Vector2(12f, 12f);
            keypad.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            keypad.constraintCount = 3;
            keypad.childAlignment = TextAnchor.MiddleCenter;

            var digitOrder = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
            for (var i = 0; i < digitOrder.Length; i++)
            {
                var digit = digitOrder[i];
                digitButtons[digit] = CreateDigitButton(keypadObject.transform, digit);
            }

            var actionsObject = CreateRectObject("BullsCowsActionRow", root);
            actionsObject.AddComponent<LayoutElement>().preferredHeight = 80f;
            var actions = actionsObject.AddComponent<HorizontalLayoutGroup>();
            actions.spacing = 16f;
            actions.childAlignment = TextAnchor.MiddleCenter;
            actions.childControlWidth = false;
            actions.childControlHeight = false;
            actions.childForceExpandWidth = false;
            actions.childForceExpandHeight = false;

            backspaceButton = CreateTextButton(actionsObject.transform, "BackspaceButton", UiTextCatalog.Get("bulls-cows.action.backspace"));
            restartButton = CreateTextButton(actionsObject.transform, "RestartButton", UiTextCatalog.Get("bulls-cows.action.restart"));
            backspaceButton.onClick.AddListener(OnBackspaceClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        private TextMeshProUGUI CreateGuessSlot(Transform parent, int index)
        {
            var slotObject = new GameObject("GuessSlot_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(LayoutElement));
            var rect = slotObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(132f, 132f);
            var layout = slotObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 132f;
            layout.preferredHeight = 132f;
            var graphic = slotObject.GetComponent<RoundedRectGraphic>();
            graphic.CornerRadius = 24f;
            graphic.color = SlotColor;
            graphic.raycastTarget = false;

            var label = CreateText("Value", rect, 62f, FontStyles.Bold, new Color32(38, 57, 66, 255));
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
            return label;
        }

        private TextMeshProUGUI CreateHistoryRow(Transform parent, int index)
        {
            var rowObject = CreateRectObject("HistoryRow_" + index, parent);
            rowObject.AddComponent<LayoutElement>().preferredHeight = 47f;
            var label = rowObject.AddComponent<TextMeshProUGUI>();
            label.font = MiniGameFontProvider.DefaultFont;
            label.fontSize = 24f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 24f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color32(55, 76, 82, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateDigitButton(Transform parent, int digit)
        {
            var button = CreateTextButton(parent, "DigitButton_" + digit, digit.ToString());
            var capturedDigit = digit;
            button.onClick.AddListener(delegate { OnDigitClicked(capturedDigit); });
            return button;
        }

        private void OnDigitClicked(int digit)
        {
            if (isFinished || currentGuess.Length >= GuessLength || currentGuess.IndexOf((char)('0' + digit)) >= 0)
            {
                return;
            }

            currentGuess += digit.ToString();
            usedDigits.Add(digit);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.72f);
            RefreshAll();
            if (currentGuess.Length == GuessLength)
            {
                TrySubmitCurrentGuess();
            }
        }

        private void OnBackspaceClicked()
        {
            if (isFinished || currentGuess.Length <= 0)
            {
                return;
            }

            var last = currentGuess[currentGuess.Length - 1] - '0';
            currentGuess = currentGuess.Substring(0, currentGuess.Length - 1);
            usedDigits.Remove(last);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.72f);
            RefreshAll();
        }

        private void TrySubmitCurrentGuess()
        {
            if (isFinished || !IsValidGuess(currentGuess))
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.7f);
                return;
            }

            EvaluateGuess(answer, currentGuess, out var bulls, out var cows);
            attemptsUsed++;
            AddHistoryEntry(UiTextCatalog.Format("bulls-cows.history.row", attemptsUsed, currentGuess, bulls, cows));
            MiniGameSfxPlayer.Play(bulls == GuessLength ? MiniGameSfxType.MatchSuccess : MiniGameSfxType.UiTap, 0.85f);

            if (bulls == GuessLength)
            {
                Finish(true);
                return;
            }

            currentGuess = string.Empty;
            usedDigits.Clear();

            RefreshAll();
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            ResetGame();
        }

        private void Finish(bool won)
        {
            isFinished = true;
            RefreshAll();
            var settlement = CreateSettlement(won);
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "BullsCowsSettlementPanel",
                    Style = won ? MiniGameRewardSettlementPanelStyle.Success : MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get(won ? "bulls-cows.settlement.win_title" : "bulls-cows.settlement.end_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("bulls-cows.settlement.answer"), answer),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("bulls-cows.settlement.attempts"), attemptsUsed.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            isFinished = true;
            var settlement = CreateSettlement(false);
            ShowBackHallRewardSettlementPanel(
                settlement,
                "BullsCowsSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("bulls-cows.settlement.answer"), answer),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("bulls-cows.settlement.attempts"), attemptsUsed.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement(bool won)
        {
            var score = won ? Mathf.Max(20, (RewardTargetAttempts - attemptsUsed + 1) * 25) : Mathf.Max(1, attemptsUsed * 4);
            var chestCount = won && attemptsUsed <= RewardTargetAttempts ? 1 : 0;
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = Mathf.Max(1, score / 8),
                ChestCount = chestCount,
                Summary = UiTextCatalog.Format("bulls-cows.settlement.summary", answer, attemptsUsed, score)
            };
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void RefreshAll()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.bulls-cows.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("bulls-cows.hud.attempts", attemptsUsed, RewardTargetAttempts);
            }

            for (var i = 0; i < guessSlots.Length; i++)
            {
                guessSlots[i].text = i < currentGuess.Length ? currentGuess[i].ToString() : string.Empty;
            }

            for (var i = 0; i < digitButtons.Length; i++)
            {
                digitButtons[i].interactable = !isFinished && currentGuess.Length < GuessLength && !usedDigits.Contains(i);
                var graphic = digitButtons[i].targetGraphic as RoundedRectGraphic;
                if (graphic != null)
                {
                    graphic.color = usedDigits.Contains(i) ? UsedKeyColor : KeyColor;
                }
            }

            if (backspaceButton != null)
            {
                backspaceButton.interactable = !isFinished && currentGuess.Length > 0;
            }
        }

        private void AddHistoryEntry(string text)
        {
            historyEntries.Add(text);
            if (historyRoot == null)
            {
                return;
            }

            var row = CreateHistoryRow(historyRoot, historyRows.Count);
            row.text = text;
            historyRows.Add(row);
            Canvas.ForceUpdateCanvases();
            if (historyScrollRect != null)
            {
                historyScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void ClearHistoryRows()
        {
            for (var i = 0; i < historyRows.Count; i++)
            {
                if (historyRows[i] != null)
                {
                    UnityEngine.Object.Destroy(historyRows[i].gameObject);
                }
            }

            historyRows.Clear();
        }

        private string GenerateAnswer()
        {
            var digits = new List<int>();
            for (var i = 0; i <= 9; i++)
            {
                digits.Add(i);
            }

            for (var i = digits.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = digits[i];
                digits[i] = digits[swapIndex];
                digits[swapIndex] = temp;
            }

            return digits[0].ToString() + digits[1] + digits[2] + digits[3];
        }

        private static Button CreateTextButton(Transform parent, string name, string labelText)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(152f, 74f);
            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.CornerRadius = 18f;
            graphic.color = KeyColor;
            graphic.raycastTarget = true;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            var label = CreateText("Label", rect, 25f, FontStyles.Bold, new Color32(47, 63, 77, 255));
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            label.text = labelText;
            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var label = textObject.AddComponent<TextMeshProUGUI>();
            label.font = MiniGameFontProvider.DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
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
    }
}
