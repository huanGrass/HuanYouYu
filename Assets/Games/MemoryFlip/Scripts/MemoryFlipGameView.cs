using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MemoryFlipGameView : MiniGameBase
    {
        public const string GameIdConstant = "memory-flip";

        private const int CoinsPerPair = 5;
        private const float MismatchRevealSeconds = 0.6f;

        private static readonly LevelDefinition[] LevelDefinitions =
        {
            new LevelDefinition(3, 4, 0),
            new LevelDefinition(3, 4, 2),
            new LevelDefinition(4, 4, 4),
            new LevelDefinition(4, 4, 6),
            new LevelDefinition(4, 4, 8),
            new LevelDefinition(5, 4, 1),
            new LevelDefinition(5, 4, 3),
            new LevelDefinition(5, 4, 5),
            new LevelDefinition(5, 4, 7),
            new LevelDefinition(5, 4, 9),
            new LevelDefinition(5, 4, 11),
            new LevelDefinition(5, 4, 13)
        };

        public static int LevelCount
        {
            get { return LevelDefinitions.Length; }
        }

        private static readonly string[] IconResourcePaths =
        {
            "GameIcons/apple",
            "GameIcons/carrot",
            "GameIcons/corn",
            "GameIcons/diamond",
            "GameIcons/eggplant",
            "GameIcons/flower",
            "GameIcons/grapes",
            "GameIcons/leaf",
            "GameIcons/mushroom",
            "GameIcons/orange",
            "GameIcons/peach",
            "GameIcons/pineapple",
            "GameIcons/potion",
            "GameIcons/pumpkin",
            "GameIcons/star",
            "GameIcons/strawberry",
            "GameIcons/tomato",
            "GameIcons/watermelon",
            "GameIcons/wheat",
            "GameIcons/water_drop"
        };

        private readonly List<CardView> cards = new List<CardView>();
        private readonly List<Sprite> loadedIcons = new List<Sprite>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private Button levelSelectButton;
        private RectTransform contentRoot;
        private RectTransform gridRoot;
        private RectTransform bottomRoot;
        private TMP_FontAsset fontAsset;
        private CardView firstOpenCard;
        private CardView secondOpenCard;
        private Coroutine mismatchRoutine;
        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private int currentLevelIndex;
        private int matchedPairCount;
        private bool interactionLocked;
        private bool settlementShown;

        private sealed class LevelDefinition
        {
            public LevelDefinition(int columns, int rows, int iconOffset)
            {
                Columns = columns;
                Rows = rows;
                IconOffset = iconOffset;
            }

            public int Columns { get; }

            public int Rows { get; }

            public int IconOffset { get; }

            public int PairCount
            {
                get { return Columns * Rows / 2; }
            }
        }

        private sealed class CardView
        {
            public int Index;
            public int PairId;
            public RectTransform Root;
            public Button Button;
            public GameObject Front;
            public GameObject Back;
            public Image Icon;
            public bool IsFaceUp;
            public bool IsMatched;
        }

        public MemoryFlipGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MemoryFlipView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
        }

        protected override void BuildOrBindSections()
        {
            fontAsset = MiniGameFontProvider.DefaultFont;

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("MemoryFlipTop"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContentSection();
            BuildBottomSection();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseLevelSelectView();
            CloseRewardSettlementPanel();
            StopMismatchRoutine();
            interactionLocked = false;
            settlementShown = false;
            matchedPairCount = 0;
            firstOpenCard = null;
            secondOpenCard = null;

            EnsureLevelProgress();
            currentLevelIndex = levelProgress.CurrentLevelIndex;
            EnsureIconsLoaded();
            BuildCardsForCurrentDifficulty();
            RefreshHud();
        }

        protected override void OnPauseRequested()
        {
            if (settlementShown)
            {
                return;
            }

            interactionLocked = true;
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            StopMismatchRoutine();
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            CloseLevelSelectView();
            CloseRewardSettlementPanel();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.memory_flip.help", null);
        }

        private void BuildContentSection()
        {
            var rootObject = CreateRectObject("MemoryFlipContent", Shell.ContentHost);
            contentRoot = rootObject.GetComponent<RectTransform>();
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(24f, 16f), new Vector2(-24f, -16f));

            var boardGraphic = EnsureRoundedRectGraphic(rootObject, new Color32(248, 242, 226, 226), 34f, false);
            boardGraphic.raycastTarget = false;

            var gridObject = CreateRectObject("MemoryFlipGrid", contentRoot);
            gridRoot = gridObject.GetComponent<RectTransform>();
            Stretch(gridRoot, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -24f));

            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.spacing = new Vector2(12f, 12f);
        }

        private void BuildBottomSection()
        {
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("MemoryFlipBottom"));
            bottomRoot = bottomContainerRefs.Root;

            var actionBar = bottomContainerRefs.ActionBar;
            actionBar.sizeDelta = new Vector2(540f, 88f);
            var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 14f;
            }

            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(actionBar).Button;
            MiniGameSfxPlayer.Attach(restartButton, MiniGameSfxType.UiTap, 0.95f);
            restartButton.onClick.AddListener(OnRestartClicked);

            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(actionBar).Button;
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
            MiniGameSfxPlayer.Attach(levelSelectButton, MiniGameSfxType.UiTap, 0.88f);
        }

        private void BuildCardsForCurrentDifficulty()
        {
            ClearCards();

            var config = LevelDefinitions[currentLevelIndex];
            var pairIds = CreateShuffledPairIds(config.PairCount);
            ConfigureGrid(config);

            for (var i = 0; i < pairIds.Count; i++)
            {
                var card = CreateCard(i, pairIds[i]);
                cards.Add(card);
            }
        }

        private void ConfigureGrid(LevelDefinition config)
        {
            var grid = gridRoot.GetComponent<GridLayoutGroup>();
            grid.constraintCount = config.Columns;

            var availableWidth = Mathf.Max(1f, contentRoot.rect.width - 48f);
            var availableHeight = Mathf.Max(1f, contentRoot.rect.height - 48f);
            var cellWidth = (availableWidth - (config.Columns - 1) * grid.spacing.x) / config.Columns;
            var cellHeight = (availableHeight - (config.Rows - 1) * grid.spacing.y) / config.Rows;
            var cellSize = Mathf.Floor(Mathf.Min(cellWidth, cellHeight));
            grid.cellSize = new Vector2(cellSize, cellSize);
        }

        private List<int> CreateShuffledPairIds(int pairCount)
        {
            var level = LevelDefinitions[currentLevelIndex];
            var pairIds = new List<int>(pairCount * 2);
            for (var i = 0; i < pairCount; i++)
            {
                var iconIndex = (level.IconOffset + i) % loadedIcons.Count;
                pairIds.Add(iconIndex);
                pairIds.Add(iconIndex);
            }

            ShuffleDeterministic(pairIds, (currentLevelIndex + 1) * 37);
            return pairIds;
        }

        private CardView CreateCard(int index, int pairId)
        {
            var rootObject = CreateRectObject("MemoryFlipCard_" + index, gridRoot);
            rootObject.AddComponent<LayoutElement>();

            var root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(112f, 112f);

            var backObject = CreateRectObject("Back", root);
            Stretch(backObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backGraphic = EnsureRoundedRectGraphic(backObject, new Color32(234, 183, 92, 255), 18f, true);

            var markObject = CreateRectObject("Mark", backObject.transform);
            var markRect = markObject.GetComponent<RectTransform>();
            markRect.anchorMin = new Vector2(0.5f, 0.5f);
            markRect.anchorMax = new Vector2(0.5f, 0.5f);
            markRect.anchoredPosition = Vector2.zero;
            markRect.sizeDelta = new Vector2(36f, 36f);
            var markGraphic = EnsureRoundedRectGraphic(markObject, new Color32(250, 231, 176, 255), 18f, false);
            markGraphic.raycastTarget = false;

            var frontObject = CreateRectObject("Front", root);
            Stretch(frontObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var frontGraphic = EnsureRoundedRectGraphic(frontObject, new Color32(255, 251, 241, 255), 18f, false);
            frontGraphic.raycastTarget = false;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(frontObject.transform, false);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = loadedIcons[pairId];
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var button = rootObject.AddComponent<Button>();
            button.targetGraphic = backGraphic;
            button.transition = Selectable.Transition.ColorTint;
            ConfigureButtonColors(button);

            var card = new CardView
            {
                Index = index,
                PairId = pairId,
                Root = root,
                Button = button,
                Front = frontObject,
                Back = backObject,
                Icon = icon,
                IsFaceUp = false,
                IsMatched = false
            };

            button.onClick.AddListener(delegate { OnCardClicked(card); });
            SetCardFaceUp(card, false);
            return card;
        }

        private void OnCardClicked(CardView card)
        {
            if (card == null || interactionLocked || settlementShown || card.IsMatched || card.IsFaceUp)
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.82f);
            SetCardFaceUp(card, true);

            if (firstOpenCard == null)
            {
                firstOpenCard = card;
                return;
            }

            secondOpenCard = card;
            interactionLocked = true;

            if (firstOpenCard.PairId == secondOpenCard.PairId)
            {
                ResolveMatch();
                return;
            }

            mismatchRoutine = HostBehaviour.StartCoroutine(ResolveMismatchAfterDelay());
        }

        private void ResolveMatch()
        {
            firstOpenCard.IsMatched = true;
            secondOpenCard.IsMatched = true;
            firstOpenCard.Button.interactable = false;
            secondOpenCard.Button.interactable = false;
            matchedPairCount += 1;
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.95f);

            firstOpenCard = null;
            secondOpenCard = null;
            interactionLocked = false;
            RefreshHud();

            if (matchedPairCount >= LevelDefinitions[currentLevelIndex].PairCount)
            {
                CompleteRound();
            }
        }

        private IEnumerator ResolveMismatchAfterDelay()
        {
            yield return new WaitForSecondsRealtime(MismatchRevealSeconds);

            if (firstOpenCard != null)
            {
                SetCardFaceUp(firstOpenCard, false);
            }

            if (secondOpenCard != null)
            {
                SetCardFaceUp(secondOpenCard, false);
            }

            firstOpenCard = null;
            secondOpenCard = null;
            interactionLocked = false;
            mismatchRoutine = null;
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.78f);
        }

        private void CompleteRound()
        {
            interactionLocked = true;
            settlementShown = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            EnsureLevelProgress();
            levelProgress.UnlockNext();
            ShowWinSettlement(CreateSettlement(true));
        }

        private void ConfirmExitToHall()
        {
            StopMismatchRoutine();
            Shell.ClosePopup();
            interactionLocked = true;
            settlementShown = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement(false);
            ShowBackHallRewardSettlementPanel(
                settlement,
                "MemoryFlipSettlementPanel",
                MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("memory_flip.settlement.pairs"), matchedPairCount + "/" + LevelDefinitions[currentLevelIndex].PairCount),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement(bool completed)
        {
            var pairCount = LevelDefinitions[currentLevelIndex].PairCount;
            var coinCount = matchedPairCount * CoinsPerPair;
            var chestCount = completed ? 1 : 0;
            return new MiniGameSettlement
            {
                Score = matchedPairCount,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = completed
                    ? UiTextCatalog.Format("memory_flip.settlement.win", matchedPairCount, coinCount, chestCount)
                    : UiTextCatalog.Format("memory_flip.settlement.exit", matchedPairCount, pairCount, coinCount, chestCount)
            };
        }

        private void OnLevelSelectClicked()
        {
            EnsureLevelProgress();
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            levelSelectView = MiniGameLevelSelectView.Create(
                Shell.PopupHost,
                fontAsset,
                LevelDefinitions.Length,
                levelProgress.CurrentLevelIndex,
                levelProgress.UnlockedLevelCount,
                "MemoryFlipLevelSelectPanel",
                "MemoryFlipLevelButton_",
                SelectLevel,
                CloseLevelSelectView);
        }

        private void SelectLevel(int index)
        {
            EnsureLevelProgress();
            if (!levelProgress.Select(index))
            {
                return;
            }

            CloseLevelSelectView();
            ResetGame();
        }

        private void OnRestartClicked()
        {
            ResetGame();
        }

        private void LoadNextLevel(MiniGameSettlement settlement)
        {
            EnsureLevelProgress();
            if (!levelProgress.GoNext())
            {
                CompleteGame?.Invoke(settlement);
                return;
            }

            GrantSettlementReward(settlement);
            ResetGame();
        }

        private void ShowWinSettlement(MiniGameSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            var level = LevelDefinitions[currentLevelIndex];
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "MemoryFlipSettlementPanel",
                    Title = UiTextCatalog.Get("memory_flip.settlement.title"),
                    PrimaryInfo = MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("memory_flip.settlement.pairs"), level.PairCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.NextLevel,
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate { LoadNextLevel(settlement); },
                delegate
                {
                    SaveNextLevelForReturn();
                    CompleteGame?.Invoke(settlement);
                },
                false);
        }

        private void SaveNextLevelForReturn()
        {
            EnsureLevelProgress();
            levelProgress.SaveNextAsCurrent();
        }

        private void CloseLevelSelectView()
        {
            if (levelSelectView != null)
            {
                levelSelectView.Dispose();
                levelSelectView = null;
            }
        }

        private void EnsureLevelProgress()
        {
            if (levelProgress == null)
            {
                levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, LevelDefinitions.Length);
            }
        }

        private void ResumeFromPause()
        {
            interactionLocked = false;
            Shell.ClosePopup();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.memory_flip.name");
            }

            if (scoreLabel != null)
            {
                var config = LevelDefinitions[currentLevelIndex];
                scoreLabel.text = UiTextCatalog.Format(
                    "memory_flip.hud.score",
                    matchedPairCount,
                    config.PairCount,
                    matchedPairCount * CoinsPerPair);
            }
        }

        private void SetCardFaceUp(CardView card, bool faceUp)
        {
            card.IsFaceUp = faceUp;
            card.Front.SetActive(faceUp || card.IsMatched);
            card.Back.SetActive(!faceUp && !card.IsMatched);
            card.Button.targetGraphic = faceUp ? card.Icon : card.Back.GetComponent<Graphic>();
        }

        private void EnsureIconsLoaded()
        {
            if (loadedIcons.Count > 0)
            {
                return;
            }

            for (var i = 0; i < IconResourcePaths.Length; i++)
            {
                var sprite = Resources.Load<Sprite>(IconResourcePaths[i]);
                if (sprite != null)
                {
                    loadedIcons.Add(sprite);
                }
            }

            if (loadedIcons.Count < LevelDefinitions[LevelDefinitions.Length - 1].PairCount)
            {
                throw new InvalidOperationException("MemoryFlip requires more GameIcons sprites.");
            }
        }

        private void ClearCards()
        {
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].Root != null)
                {
                    UnityEngine.Object.Destroy(cards[i].Root.gameObject);
                }
            }

            cards.Clear();
        }

        private void StopMismatchRoutine()
        {
            if (mismatchRoutine != null && HostBehaviour != null)
            {
                HostBehaviour.StopCoroutine(mismatchRoutine);
                mismatchRoutine = null;
            }
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

        private static void Shuffle<T>(IList<T> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                var temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
        }

        private static void ShuffleDeterministic<T>(IList<T> values, int seed)
        {
            var random = new System.Random(seed);
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
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
