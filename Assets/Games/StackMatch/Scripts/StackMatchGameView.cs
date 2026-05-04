using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class StackMatchGameView : MiniGameBase
    {
        public const string GameIdConstant = "stack-match";

        private const int TrayCapacity = 7;
        private const int MatchCount = 3;
        private const int CoinsPerMatch = 6;
        private const float CardWidth = 82f;
        private const float CardHeight = 96f;
        private const int CardGridSize = 2;
        private const int BoardGridColumns = 16;
        private const int BoardGridRows = 20;
        private const float GridCellWidth = CardWidth * 0.5f;
        private const float GridCellHeight = CardHeight * 0.5f;
        private const float BlindBoxStepScale = 0.25f;
        private const float TrayCardWidth = CardWidth;
        private const float TrayCardHeight = CardHeight;
        private const float TrayCardSpacing = 4f;
        private const int MoveOutCount = 4;
        private const float MoveToTrayDuration = 0.30f;
        private const float MatchClearDuration = 0.18f;
        private static readonly Color CardColor = new Color32(252, 253, 248, 255);
        private static readonly Color CardShadowColor = new Color32(180, 187, 177, 120);
        private static readonly Color CardHighlightColor = new Color32(255, 255, 255, 95);
        private static readonly Color CoveredMaskColor = new Color(0f, 0f, 0f, 0.30f);
        private static readonly Color TrayFrameColor = new Color32(150, 96, 48, 245);

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
            "GameIcons/watermelon"
        };

        private static readonly LevelDefinition[] LevelDefinitions =
        {
            LevelDefinition.CreateEasy(),
            LevelDefinition.CreateHard()
        };

        private readonly List<CardView> cards = new List<CardView>();
        private readonly List<CardView> trayCards = new List<CardView>();
        private readonly List<CardView> movedOutCards = new List<CardView>();
        private readonly List<CardView> undoStack = new List<CardView>();
        private readonly List<Sprite> loadedIcons = new List<Sprite>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI statusLabel;
        private RectTransform contentRoot;
        private RectTransform boardRoot;
        private RectTransform movedOutRoot;
        private RectTransform trayRoot;
        private RectTransform bottomRoot;
        private Button moveOutButton;
        private Button shuffleButton;
        private Button undoButton;
        private MiniGameLevelProgressController levelProgress;
        private int currentLevelIndex;
        private int currentRunSeed;
        private int score;
        private int clearedSetCount;
        private bool moveOutUsed;
        private bool shuffleUsed;
        private bool undoUsed;
        private bool settlementShown;
        private bool shouldStartFromFirstLevel = true;

        private enum CardState
        {
            Board,
            Tray,
            MovedOut,
            Removed
        }

        private enum BlindBoxDirection
        {
            Horizontal,
            Vertical
        }

        private sealed class CardView
        {
            public int Index;
            public int TypeId;
            public int Layer;
            public int GridX;
            public int GridY;
            public bool IsBlindBox;
            public int BlindBoxGroup;
            public int BlindBoxOrder;
            public BlindBoxDirection BlindBoxDirection;
            public Rect BoardRect;
            public RectTransform Root;
            public Button Button;
            public RoundedRectGraphic Background;
            public Image Icon;
            public CanvasGroup CanvasGroup;
            public LayoutElement LayoutElement;
            public GameObject CoveredMask;
            public RectTransform MoveGhost;
            public Coroutine MoveRoutine;
            public Coroutine ClearRoutine;
            public bool IsMoving;
            public bool IsClearing;
            public CardState State;
        }

        private sealed class LevelDefinition
        {
            public string DifficultyKey;
            public SlotDefinition[] Slots;
            public int TypeCount;

            public static LevelDefinition CreateEasy()
            {
                return new LevelDefinition
                {
                    DifficultyKey = "stack_match.difficulty.easy",
                    TypeCount = 3,
                    Slots = new[]
                    {
                        new SlotDefinition(4, 6, 0),
                        new SlotDefinition(7, 6, 0),
                        new SlotDefinition(10, 6, 0),
                        new SlotDefinition(4, 9, 0),
                        new SlotDefinition(7, 9, 0),
                        new SlotDefinition(10, 9, 0),
                        new SlotDefinition(4, 12, 0),
                        new SlotDefinition(7, 12, 0),
                        new SlotDefinition(10, 12, 0)
                    }
                };
            }

            public static LevelDefinition CreateHard()
            {
                var slots = new List<SlotDefinition>();
                var layerLeftPositions = new[]
                {
                    new[]
                    {
                        new Vector2Int(2, 4), new Vector2Int(3, 7),
                        new Vector2Int(5, 2), new Vector2Int(4, 6),
                        new Vector2Int(6, 7), new Vector2Int(5, 5),
                        new Vector2Int(2, 10), new Vector2Int(2, 13),
                        new Vector2Int(2, 16), new Vector2Int(6, 10),
                        new Vector2Int(3, 16), new Vector2Int(2, 2)
                    },
                    new[]
                    {
                        new Vector2Int(2, 2), new Vector2Int(5, 3),
                        new Vector2Int(3, 4), new Vector2Int(3, 8),
                        new Vector2Int(6, 6), new Vector2Int(6, 2),
                        new Vector2Int(4, 11), new Vector2Int(3, 13),
                        new Vector2Int(4, 16), new Vector2Int(5, 12),
                        new Vector2Int(5, 16), new Vector2Int(4, 2)
                    },
                    new[]
                    {
                        new Vector2Int(2, 7), new Vector2Int(2, 3),
                        new Vector2Int(2, 6), new Vector2Int(2, 9),
                        new Vector2Int(3, 12), new Vector2Int(3, 14),
                        new Vector2Int(2, 15), new Vector2Int(5, 14),
                        new Vector2Int(5, 13), new Vector2Int(6, 9),
                        new Vector2Int(5, 15), new Vector2Int(4, 4)
                    },
                    new[]
                    {
                        new Vector2Int(4, 2), new Vector2Int(4, 4),
                        new Vector2Int(2, 11), new Vector2Int(2, 14),
                        new Vector2Int(6, 5), new Vector2Int(4, 5),
                        new Vector2Int(4, 14), new Vector2Int(5, 8),
                        new Vector2Int(6, 14), new Vector2Int(6, 8),
                        new Vector2Int(6, 16), new Vector2Int(2, 8)
                    },
                    new[]
                    {
                        new Vector2Int(3, 3), new Vector2Int(2, 8),
                        new Vector2Int(3, 6), new Vector2Int(4, 9),
                        new Vector2Int(6, 4), new Vector2Int(4, 8),
                        new Vector2Int(3, 9), new Vector2Int(4, 10),
                        new Vector2Int(6, 13), new Vector2Int(4, 12),
                        new Vector2Int(6, 15), new Vector2Int(2, 6)
                    },
                    new[]
                    {
                        new Vector2Int(3, 5), new Vector2Int(4, 7),
                        new Vector2Int(5, 7), new Vector2Int(5, 6),
                        new Vector2Int(6, 3), new Vector2Int(5, 10),
                        new Vector2Int(5, 9), new Vector2Int(4, 13),
                        new Vector2Int(6, 12), new Vector2Int(4, 15),
                        new Vector2Int(4, 6), new Vector2Int(2, 14)
                    },
                    new[]
                    {
                        new Vector2Int(2, 5), new Vector2Int(3, 2),
                        new Vector2Int(4, 3), new Vector2Int(3, 10),
                        new Vector2Int(2, 12), new Vector2Int(5, 4),
                        new Vector2Int(3, 11), new Vector2Int(3, 15),
                        new Vector2Int(6, 11), new Vector2Int(5, 11),
                        new Vector2Int(6, 6), new Vector2Int(6, 4)
                    }
                };

                for (var layer = 0; layer < layerLeftPositions.Length; layer++)
                {
                    AddMirroredGridLayer(slots, layerLeftPositions[layer], layer);
                }

                AddGridLayer(slots, new[]
                {
                    new Vector2Int(4, 5), new Vector2Int(6, 5), new Vector2Int(8, 5), new Vector2Int(10, 5),
                    new Vector2Int(5, 7), new Vector2Int(7, 7), new Vector2Int(9, 7),
                    new Vector2Int(4, 9), new Vector2Int(6, 9), new Vector2Int(8, 9), new Vector2Int(10, 9),
                    new Vector2Int(5, 11), new Vector2Int(7, 11), new Vector2Int(9, 11),
                    new Vector2Int(4, 13), new Vector2Int(6, 13), new Vector2Int(8, 13), new Vector2Int(10, 13)
                }, 7);
                AddBlindBox(slots, 0, 0, 8, 6, 8, BlindBoxDirection.Vertical);
                AddBlindBox(slots, 1, 14, 8, 6, 14, BlindBoxDirection.Vertical);

                return new LevelDefinition
                {
                    DifficultyKey = "stack_match.difficulty.hard",
                    TypeCount = 18,
                    Slots = slots.ToArray()
                };
            }

            private static void AddGridLayer(List<SlotDefinition> slots, Vector2Int[] positions, int layer)
            {
                for (var i = 0; i < positions.Length; i++)
                {
                    var position = positions[i];
                    slots.Add(new SlotDefinition(position.x, position.y, layer));
                }
            }

            private static void AddMirroredGridLayer(List<SlotDefinition> slots, Vector2Int[] leftPositions, int layer)
            {
                for (var i = 0; i < leftPositions.Length; i++)
                {
                    var position = leftPositions[i];
                    slots.Add(new SlotDefinition(position.x, position.y, layer));
                    slots.Add(new SlotDefinition(BoardGridColumns - position.x - CardGridSize, BoardGridRows - position.y - CardGridSize, layer));
                }
            }

            private static void AddBlindBox(List<SlotDefinition> slots, int group, int gridX, int gridY, int count, int layerBase, BlindBoxDirection direction)
            {
                for (var i = 0; i < count; i++)
                {
                    slots.Add(SlotDefinition.CreateBlindBox(gridX, gridY, layerBase + i, group, i, direction));
                }
            }
        }

        private struct SlotDefinition
        {
            public readonly int GridX;
            public readonly int GridY;
            public readonly int Layer;
            public readonly bool IsBlindBox;
            public readonly int BlindBoxGroup;
            public readonly int BlindBoxOrder;
            public readonly BlindBoxDirection BlindBoxDirection;

            public SlotDefinition(int gridX, int gridY, int layer)
            {
                GridX = gridX;
                GridY = gridY;
                Layer = layer;
                IsBlindBox = false;
                BlindBoxGroup = -1;
                BlindBoxOrder = -1;
                BlindBoxDirection = BlindBoxDirection.Horizontal;
            }

            private SlotDefinition(int gridX, int gridY, int layer, int blindBoxGroup, int blindBoxOrder, BlindBoxDirection blindBoxDirection)
            {
                GridX = gridX;
                GridY = gridY;
                Layer = layer;
                IsBlindBox = true;
                BlindBoxGroup = blindBoxGroup;
                BlindBoxOrder = blindBoxOrder;
                BlindBoxDirection = blindBoxDirection;
            }

            public static SlotDefinition CreateBlindBox(int gridX, int gridY, int layer, int blindBoxGroup, int blindBoxOrder, BlindBoxDirection blindBoxDirection)
            {
                return new SlotDefinition(gridX, gridY, layer, blindBoxGroup, blindBoxOrder, blindBoxDirection);
            }
        }

        public StackMatchGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "StackMatchView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public static int LevelCount
        {
            get { return LevelDefinitions.Length; }
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("StackMatchTop"));
            titleLabel = topBarRefs.TitleText;
            statusLabel = topBarRefs.ScoreText;

            BuildContentSection();
            BuildBottomSection();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            settlementShown = false;
            score = 0;
            clearedSetCount = 0;
            moveOutUsed = false;
            shuffleUsed = false;
            undoUsed = false;
            trayCards.Clear();
            movedOutCards.Clear();
            undoStack.Clear();

            EnsureLevelProgress();
            if (shouldStartFromFirstLevel)
            {
                levelProgress.Select(0);
                shouldStartFromFirstLevel = false;
            }

            currentLevelIndex = levelProgress.CurrentLevelIndex;
            currentRunSeed = CreateRandomSeed();
            EnsureIconsLoaded();
            BuildLevel(LevelDefinitions[currentLevelIndex]);
            RefreshAll();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.stack-match.help", null);
        }

        protected override void OnPauseRequested()
        {
            if (settlementShown)
            {
                return;
            }

            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();

            if (moveOutButton != null)
            {
                moveOutButton.onClick.RemoveListener(OnMoveOutClicked);
            }

            if (shuffleButton != null)
            {
                shuffleButton.onClick.RemoveListener(OnShuffleClicked);
            }

            if (undoButton != null)
            {
                undoButton.onClick.RemoveListener(OnUndoClicked);
            }

            for (var i = 0; i < cards.Count; i++)
            {
                StopCardAnimations(cards[i]);
            }
        }

        private void BuildContentSection()
        {
            var contentObject = CreateRectObject("StackMatchContent", Shell.ContentHost);
            contentRoot = contentObject.GetComponent<RectTransform>();
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -12f));

            var frame = EnsureRoundedRectGraphic(contentObject, new Color32(255, 255, 255, 0), 0f, false);
            frame.raycastTarget = false;

            var boardObject = CreateRectObject("StackMatchBoard", contentRoot);
            boardRoot = boardObject.GetComponent<RectTransform>();
            boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.sizeDelta = new Vector2(640f, 1050f);
            boardRoot.anchoredPosition = new Vector2(0f, 58f);

            var trayFrameObject = CreateRectObject("StackMatchTray", contentRoot);
            var trayFrameRoot = trayFrameObject.GetComponent<RectTransform>();
            trayFrameRoot.anchorMin = new Vector2(0.5f, 0f);
            trayFrameRoot.anchorMax = new Vector2(0.5f, 0f);
            trayFrameRoot.pivot = new Vector2(0.5f, 0f);
            trayFrameRoot.sizeDelta = new Vector2(626f, 114f);
            trayFrameRoot.anchoredPosition = new Vector2(0f, 22f);
            EnsureRoundedRectGraphic(trayFrameObject, TrayFrameColor, 22f, true);

            var movedOutObject = CreateRectObject("MovedOutCards", contentRoot);
            movedOutRoot = movedOutObject.GetComponent<RectTransform>();
            movedOutRoot.anchorMin = new Vector2(0.5f, 0f);
            movedOutRoot.anchorMax = new Vector2(0.5f, 0f);
            movedOutRoot.pivot = new Vector2(0.5f, 0f);
            movedOutRoot.sizeDelta = new Vector2(360f, 88f);
            movedOutRoot.anchoredPosition = new Vector2(0f, 148f);
            var movedOutLayout = movedOutObject.AddComponent<HorizontalLayoutGroup>();
            movedOutLayout.childAlignment = TextAnchor.MiddleCenter;
            movedOutLayout.spacing = -8f;
            movedOutLayout.childControlWidth = false;
            movedOutLayout.childControlHeight = false;
            movedOutLayout.childForceExpandWidth = false;
            movedOutLayout.childForceExpandHeight = false;

            var trayCardRowObject = CreateRectObject("TrayCards", trayFrameRoot);
            trayRoot = trayCardRowObject.GetComponent<RectTransform>();
            Stretch(trayRoot, Vector2.zero, Vector2.one, new Vector2(12f, 9f), new Vector2(-12f, -9f));
            var trayLayout = trayCardRowObject.AddComponent<HorizontalLayoutGroup>();
            trayLayout.childAlignment = TextAnchor.MiddleLeft;
            trayLayout.spacing = TrayCardSpacing;
            trayLayout.childControlWidth = false;
            trayLayout.childControlHeight = false;
            trayLayout.childForceExpandWidth = false;
            trayLayout.childForceExpandHeight = false;
        }

        private void BuildBottomSection()
        {
            var bottomRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("StackMatchBottom"));
            bottomRoot = bottomRefs.Root;
            bottomRefs.ActionBar.sizeDelta = new Vector2(412f, 88f);

            moveOutButton = CreateBottomTextButton(bottomRefs.ActionBar, "MoveOutButton", UiTextCatalog.Get("stack_match.action.move_out"));
            shuffleButton = CreateBottomTextButton(bottomRefs.ActionBar, "ShuffleButton", UiTextCatalog.Get("stack_match.action.shuffle"));
            undoButton = CreateBottomTextButton(bottomRefs.ActionBar, "UndoButton", UiTextCatalog.Get("stack_match.action.undo"));

            moveOutButton.onClick.AddListener(OnMoveOutClicked);
            shuffleButton.onClick.AddListener(OnShuffleClicked);
            undoButton.onClick.AddListener(OnUndoClicked);
        }

        private static Button CreateBottomTextButton(Transform parent, string name, string labelText)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(LayoutElement));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(116f, 72f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 116f;
            layoutElement.preferredHeight = 72f;
            layoutElement.layoutPriority = 1;

            var button = buttonObject.GetComponent<Button>();
            var backgroundObject = CreateRectObject("Background", buttonRect);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            Stretch(backgroundRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color32(53, 125, 97, 255);
            button.targetGraphic = backgroundImage;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelObject = CreateRectObject("Label", buttonRect);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 21f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.text = labelText;

            return button;
        }

        private void BuildLevel(LevelDefinition level)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                StopCardAnimations(cards[i]);
                if (cards[i].Root != null)
                {
                    UnityEngine.Object.Destroy(cards[i].Root.gameObject);
                }
            }

            cards.Clear();
            var layoutVariant = 0;
            var runSlots = new SlotDefinition[level.Slots.Length];
            for (var i = 0; i < level.Slots.Length; i++)
            {
                runSlots[i] = CreateRunSlot(level.Slots[i], layoutVariant);
            }

            var types = BuildTypeSequence(level, runSlots);
            for (var i = 0; i < runSlots.Length; i++)
            {
                var card = CreateCard(i, types[i], runSlots[i]);
                cards.Add(card);
            }

            cards.Sort(delegate(CardView left, CardView right)
            {
                var layerCompare = left.Layer.CompareTo(right.Layer);
                return layerCompare != 0 ? layerCompare : left.Index.CompareTo(right.Index);
            });

            for (var i = 0; i < cards.Count; i++)
            {
                cards[i].Root.SetSiblingIndex(i);
            }
        }

        private int[] BuildTypeSequence(LevelDefinition level, SlotDefinition[] slots)
        {
            var result = new int[slots.Length];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = -1;
            }

            var remainingTypes = BuildTypePool(level, slots.Length);

            var random = new System.Random(currentRunSeed);
            var exposedSlots = GetOpeningExposedNormalSlotIndices(slots);
            ShuffleList(exposedSlots, random);
            var openingMatchSetCount = Mathf.Min(4, exposedSlots.Count / MatchCount, slots.Length / MatchCount);
            for (var set = 0; set < openingMatchSetCount; set++)
            {
                var type = set % Mathf.Max(1, level.TypeCount);
                for (var offset = 0; offset < MatchCount; offset++)
                {
                    var slotIndex = exposedSlots[set * MatchCount + offset];
                    result[slotIndex] = type;
                    remainingTypes.Remove(type);
                }
            }

            var shuffledRemaining = remainingTypes.ToArray();
            Shuffle(shuffledRemaining, currentRunSeed ^ 0x5172C1);
            var cursor = 0;
            for (var i = 0; i < result.Length; i++)
            {
                if (result[i] >= 0)
                {
                    continue;
                }

                result[i] = shuffledRemaining[cursor];
                cursor += 1;
            }

            return result;
        }

        private static List<int> BuildTypePool(LevelDefinition level, int slotCount)
        {
            var matchSetCount = slotCount / MatchCount;
            var activeTypeCount = Mathf.Max(1, Mathf.Min(level.TypeCount, matchSetCount));
            var result = new List<int>(slotCount);
            for (var set = 0; set < matchSetCount; set++)
            {
                var type = set % activeTypeCount;
                for (var count = 0; count < MatchCount; count++)
                {
                    result.Add(type);
                }
            }

            return result;
        }

        private static List<int> GetOpeningExposedNormalSlotIndices(SlotDefinition[] slots)
        {
            var result = new List<int>();
            for (var i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsBlindBox && !IsNormalSlotCovered(slots, i))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static bool IsNormalSlotCovered(SlotDefinition[] slots, int targetIndex)
        {
            var target = slots[targetIndex];
            var targetRect = GetBoardRect(target);
            for (var i = 0; i < slots.Length; i++)
            {
                if (i == targetIndex || slots[i].IsBlindBox || !IsSlotDrawnAbove(slots, targetIndex, i))
                {
                    continue;
                }

                if (RectsOverlap(targetRect, GetBoardRect(slots[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSlotDrawnAbove(SlotDefinition[] slots, int targetIndex, int otherIndex)
        {
            var target = slots[targetIndex];
            var other = slots[otherIndex];
            return other.Layer > target.Layer || (other.Layer == target.Layer && otherIndex > targetIndex);
        }

        private static Rect GetBoardRect(SlotDefinition slot)
        {
            var position = GetBoardPosition(slot);
            return new Rect(position.x - CardWidth * 0.5f, position.y - CardHeight * 0.5f, CardWidth, CardHeight);
        }

        private SlotDefinition CreateRunSlot(SlotDefinition slot, int layoutVariant)
        {
            if (slot.IsBlindBox || layoutVariant <= 0)
            {
                return slot;
            }

            if (layoutVariant == 1)
            {
                return new SlotDefinition(BoardGridColumns - slot.GridX - CardGridSize, slot.GridY, slot.Layer);
            }

            var offset = slot.Layer % 2 == 0 ? 1 : -1;
            return new SlotDefinition(Mathf.Clamp(slot.GridX + offset, 0, BoardGridColumns - CardGridSize), slot.GridY, slot.Layer);
        }

        private CardView CreateCard(int index, int typeId, SlotDefinition slot)
        {
            var cardObject = CreateRectObject("StackMatchTile_" + index, boardRoot);
            var rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            var boardPosition = GetBoardPosition(slot);
            rect.anchoredPosition = boardPosition;
            rect.localScale = Vector3.one;

            var shadowObject = CreateRectObject("Shadow", rect);
            var shadow = EnsureRoundedRectGraphic(shadowObject, CardShadowColor, 18f, false);
            Stretch(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, -5f), new Vector2(0f, -1f));

            var faceObject = CreateRectObject("Face", rect);
            var background = EnsureRoundedRectGraphic(faceObject, CardColor, 18f, true);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var highlightObject = CreateRectObject("Highlight", faceObject.transform);
            var highlight = EnsureRoundedRectGraphic(highlightObject, CardHighlightColor, 14f, false);
            Stretch(highlight.rectTransform, new Vector2(0f, 0.58f), Vector2.one, new Vector2(9f, 2f), new Vector2(-9f, -7f));

            var button = cardObject.AddComponent<Button>();
            button.targetGraphic = background;
            ConfigureCardButtonColors(button);
            var canvasGroup = cardObject.AddComponent<CanvasGroup>();
            var layoutElement = cardObject.AddComponent<LayoutElement>();

            var iconObject = CreateRectObject("Icon", rect);
            var icon = iconObject.AddComponent<Image>();
            icon.sprite = loadedIcons.Count > 0 ? loadedIcons[typeId % loadedIcons.Count] : null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Stretch(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

            var maskObject = CreateRectObject("CoveredMask", rect);
            var maskGraphic = EnsureRoundedRectGraphic(maskObject, CoveredMaskColor, 18f, false);
            maskGraphic.raycastTarget = false;
            Stretch(maskGraphic.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            maskObject.SetActive(false);

            var card = new CardView
            {
                Index = index,
                TypeId = typeId,
                Layer = slot.Layer,
                GridX = slot.GridX,
                GridY = slot.GridY,
                IsBlindBox = slot.IsBlindBox,
                BlindBoxGroup = slot.BlindBoxGroup,
                BlindBoxOrder = slot.BlindBoxOrder,
                BlindBoxDirection = slot.BlindBoxDirection,
                BoardRect = new Rect(boardPosition.x - CardWidth * 0.5f, boardPosition.y - CardHeight * 0.5f, CardWidth, CardHeight),
                Root = rect,
                Button = button,
                Background = background,
                Icon = icon,
                CanvasGroup = canvasGroup,
                LayoutElement = layoutElement,
                CoveredMask = maskObject,
                State = CardState.Board
            };

            button.onClick.AddListener(delegate { OnCardClicked(card); });
            return card;
        }

        private static Vector2 GetBoardPosition(SlotDefinition slot)
        {
            var position = GridToBoardPosition(slot.GridX, slot.GridY);
            if (slot.IsBlindBox)
            {
                position += GetBlindBoxOffset(slot.BlindBoxOrder, slot.BlindBoxDirection);
            }

            return position;
        }

        private static Vector2 GridToBoardPosition(int gridX, int gridY)
        {
            var x = (gridX + CardGridSize * 0.5f - BoardGridColumns * 0.5f) * GridCellWidth;
            var y = (BoardGridRows * 0.5f - gridY - CardGridSize * 0.5f) * GridCellHeight;
            return new Vector2(x, y);
        }

        private static Vector2 GetBlindBoxOffset(int order, BlindBoxDirection direction)
        {
            if (direction == BlindBoxDirection.Horizontal)
            {
                return new Vector2(order * GridCellWidth * BlindBoxStepScale, 0f);
            }

            return new Vector2(0f, -order * GridCellHeight * BlindBoxStepScale);
        }

        private void OnCardClicked(CardView card)
        {
            if (card != null && card.State == CardState.MovedOut)
            {
                OnMovedOutCardClicked(card);
                return;
            }

            if (card == null || settlementShown || card.State != CardState.Board || IsCovered(card))
            {
                return;
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            MoveCardToTray(card);
            undoStack.Add(card);
            RefreshAll(false);
        }

        private void OnMovedOutCardClicked(CardView card)
        {
            if (card == null || settlementShown || card.State != CardState.MovedOut || trayCards.Count >= TrayCapacity)
            {
                return;
            }

            movedOutCards.Remove(card);
            MoveCardToTray(card);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            RefreshAll();
        }

        private void MoveCardToTray(CardView card)
        {
            var fromBoard = card.State == CardState.Board;
            var startWorldPosition = card.Root.position;
            var movingGhost = fromBoard ? CreateMoveGhost(card) : null;
            card.State = CardState.Tray;
            card.Root.SetParent(trayRoot, false);
            card.Root.sizeDelta = new Vector2(TrayCardWidth, TrayCardHeight);
            card.LayoutElement.ignoreLayout = false;
            card.Root.localScale = Vector3.one;
            card.Background.color = CardColor;
            card.Button.interactable = false;
            card.Icon.color = Color.white;
            card.CanvasGroup.alpha = 0f;
            SetCoveredMaskVisible(card, false);

            var insertIndex = GetTrayInsertIndex(card.TypeId);
            trayCards.Insert(insertIndex, card);
            if (fromBoard)
            {
                PlayMoveToTrayAnimation(card, movingGhost, startWorldPosition, GetTrayCardPosition(insertIndex));
            }
            else
            {
                card.CanvasGroup.alpha = 1f;
            }

            RefreshTrayPositions();
            if (!fromBoard)
            {
                ResolveTrayAfterMove();
            }
        }

        private bool ResolveTrayMatches()
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < trayCards.Count; i++)
            {
                var type = trayCards[i].TypeId;
                counts[type] = counts.ContainsKey(type) ? counts[type] + 1 : 1;
            }

            foreach (var pair in counts)
            {
                if (pair.Value >= MatchCount)
                {
                    RemoveMatchedTrayCards(pair.Key);
                    clearedSetCount += 1;
                    score += CoinsPerMatch;
                    MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.55f);
                    return true;
                }
            }

            return false;
        }

        private void ResolveTrayAfterMove()
        {
            if (settlementShown)
            {
                return;
            }

            if (HasMovingTrayCard())
            {
                return;
            }

            while (ResolveTrayMatches())
            {
            }

            if (IsWin())
            {
                ShowResult(true);
            }
            else if (trayCards.Count >= TrayCapacity)
            {
                ShowResult(false);
            }
            else
            {
                RefreshAll(false);
            }
        }

        private bool HasMovingTrayCard()
        {
            for (var i = 0; i < trayCards.Count; i++)
            {
                if (trayCards[i].IsMoving)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveMatchedTrayCards(int typeId)
        {
            var removed = 0;
            for (var i = trayCards.Count - 1; i >= 0 && removed < MatchCount; i--)
            {
                var card = trayCards[i];
                if (card.TypeId != typeId)
                {
                    continue;
                }

                card.State = CardState.Removed;
                trayCards.RemoveAt(i);
                undoStack.Remove(card);
                PlayMatchClearAnimation(card);
                removed += 1;
            }

            RefreshTrayPositions();
        }

        private void OnShuffleClicked()
        {
            if (settlementShown || shuffleUsed)
            {
                return;
            }

            var boardCards = new List<CardView>();
            var types = new List<int>();
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].State == CardState.Board)
                {
                    boardCards.Add(cards[i]);
                    types.Add(cards[i].TypeId);
                }
            }

            if (boardCards.Count <= 1)
            {
                return;
            }

            var values = types.ToArray();
            Shuffle(values, CreateRandomSeed());
            for (var i = 0; i < boardCards.Count; i++)
            {
                boardCards[i].TypeId = values[i];
                RefreshCardVisual(boardCards[i]);
            }

            shuffleUsed = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            RefreshAll();
        }

        private void OnMoveOutClicked()
        {
            if (settlementShown || moveOutUsed || trayCards.Count < MoveOutCount || movedOutCards.Count > 0 || HasMovingTrayCard())
            {
                return;
            }

            for (var i = 0; i < MoveOutCount; i++)
            {
                var card = trayCards[0];
                trayCards.RemoveAt(0);
                undoStack.Remove(card);
                card.State = CardState.MovedOut;
                card.Root.SetParent(movedOutRoot, false);
                card.Root.sizeDelta = new Vector2(CardWidth * 0.72f, CardHeight * 0.72f);
                card.Root.localScale = Vector3.one;
                card.LayoutElement.ignoreLayout = false;
                card.LayoutElement.preferredWidth = CardWidth * 0.72f;
                card.LayoutElement.preferredHeight = CardHeight * 0.72f;
                card.CanvasGroup.alpha = 1f;
                card.Button.interactable = true;
                movedOutCards.Add(card);
            }

            moveOutUsed = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            RefreshAll();
        }

        private void OnUndoClicked()
        {
            if (settlementShown || undoUsed || undoStack.Count <= 0 || HasMovingTrayCard())
            {
                return;
            }

            var card = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            if (card == null || card.State != CardState.Tray)
            {
                undoUsed = true;
                RefreshAll();
                return;
            }

            undoUsed = true;
            trayCards.Remove(card);
            card.State = CardState.Board;
            StopCardAnimations(card);
            card.Root.SetParent(boardRoot, false);
            card.Root.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.LayoutElement.ignoreLayout = false;
            card.LayoutElement.preferredWidth = -1f;
            card.LayoutElement.preferredHeight = -1f;
            card.Root.anchoredPosition = card.BoardRect.center;
            card.Root.localScale = Vector3.one;
            card.CanvasGroup.alpha = 1f;
            card.Button.interactable = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            RefreshAll();
        }

        private void ShowResult(bool won)
        {
            if (settlementShown)
            {
                return;
            }

            settlementShown = true;
            if (won)
            {
                levelProgress.UnlockNext();
            }

            var settlement = CreateSettlement(won);
            var primaryAction = won && levelProgress.CanGoNext()
                ? MiniGameRewardSettlementPrimaryAction.NextLevel
                : won
                    ? MiniGameRewardSettlementPrimaryAction.Retry
                    : MiniGameRewardSettlementPrimaryAction.Retry;

            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "StackMatchSettlementPanel",
                    Style = won ? MiniGameRewardSettlementPanelStyle.Success : MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = primaryAction,
                    Title = UiTextCatalog.Get(won ? "stack_match.settlement.win_title" : "stack_match.settlement.fail_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("stack_match.settlement.level"), (currentLevelIndex + 1).ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("stack_match.settlement.cleared"), clearedSetCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate
                {
                    if (won && levelProgress.GoNext())
                    {
                        ResetGame();
                    }
                    else if (!won)
                    {
                        ResetGame();
                    }
                    else
                    {
                        ResetGame();
                    }
                },
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            settlementShown = true;
            var settlement = CreateSettlement(false);
            ShowBackHallRewardSettlementPanel(
                settlement,
                "StackMatchSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("stack_match.settlement.level"), (currentLevelIndex + 1).ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("stack_match.settlement.cleared"), clearedSetCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement(bool won)
        {
            var coinCount = clearedSetCount * CoinsPerMatch;
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = won ? 1 : 0,
                Summary = won
                    ? UiTextCatalog.Format("stack_match.settlement.win_summary", currentLevelIndex + 1, clearedSetCount, coinCount, won ? 1 : 0)
                    : UiTextCatalog.Format("stack_match.settlement.fail_summary", currentLevelIndex + 1, clearedSetCount, coinCount)
            };
        }

        private bool IsWin()
        {
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].State != CardState.Removed)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCovered(CardView target)
        {
            if (target.IsBlindBox)
            {
                return IsBlindBoxCovered(target);
            }

            for (var i = 0; i < cards.Count; i++)
            {
                var other = cards[i];
                if (other == target || other.IsBlindBox || other.State != CardState.Board || !IsDrawnAbove(target, other))
                {
                    continue;
                }

                if (HasBoardRectCover(target, other))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBlindBoxCovered(CardView target)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var other = cards[i];
                if (other == target || !other.IsBlindBox || other.State != CardState.Board)
                {
                    continue;
                }

                if (other.BlindBoxGroup == target.BlindBoxGroup && other.BlindBoxOrder > target.BlindBoxOrder)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDrawnAbove(CardView target, CardView other)
        {
            return other.Layer > target.Layer || (other.Layer == target.Layer && other.Index > target.Index);
        }

        private static bool HasBoardRectCover(CardView target, CardView other)
        {
            return RectsOverlap(target.BoardRect, other.BoardRect);
        }

        private static bool RectsOverlap(Rect left, Rect right)
        {
            return left.xMin < right.xMax
                && left.xMax > right.xMin
                && left.yMin < right.yMax
                && left.yMax > right.yMin;
        }

        private void RefreshAll(bool refreshTray = true)
        {
            RefreshHud();
            for (var i = 0; i < cards.Count; i++)
            {
                RefreshCard(cards[i]);
            }

            if (refreshTray)
            {
                RefreshTrayPositions();
            }

            RefreshMovedOutPositions();

            if (moveOutButton != null)
            {
                moveOutButton.interactable = !settlementShown && !moveOutUsed && trayCards.Count >= MoveOutCount && movedOutCards.Count == 0 && !HasMovingTrayCard();
            }

            if (shuffleButton != null)
            {
                shuffleButton.interactable = !settlementShown && !shuffleUsed;
            }

            if (undoButton != null)
            {
                undoButton.interactable = !settlementShown && !undoUsed && undoStack.Count > 0 && !HasMovingTrayCard();
            }
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.stack-match.name");
            }

            var level = LevelDefinitions[currentLevelIndex];
            if (statusLabel != null)
            {
                statusLabel.text = UiTextCatalog.Format(
                    "stack_match.hud.status",
                    currentLevelIndex + 1,
                    UiTextCatalog.Get(level.DifficultyKey),
                    score);
            }

        }

        private void RefreshCard(CardView card)
        {
            if (card == null || card.Root == null)
            {
                return;
            }

            if (card.State == CardState.Removed)
            {
                if (!card.IsClearing)
                {
                    card.Root.gameObject.SetActive(false);
                }

                return;
            }

            var onBoard = card.State == CardState.Board;
            var movedOut = card.State == CardState.MovedOut;
            card.Root.gameObject.SetActive(true);
            if (onBoard)
            {
                StopCardAnimations(card);
                card.Root.SetParent(boardRoot, false);
                card.Root.sizeDelta = new Vector2(CardWidth, CardHeight);
                card.LayoutElement.ignoreLayout = false;
                card.LayoutElement.preferredWidth = -1f;
                card.LayoutElement.preferredHeight = -1f;
                card.Root.anchoredPosition = card.BoardRect.center;
                card.Root.localScale = Vector3.one;
                card.CanvasGroup.alpha = 1f;
                card.Root.SetSiblingIndex(card.Layer * 100 + card.Index);
            }
            else if (movedOut)
            {
                card.Root.SetParent(movedOutRoot, false);
                card.Root.sizeDelta = new Vector2(CardWidth * 0.72f, CardHeight * 0.72f);
                card.LayoutElement.ignoreLayout = false;
                card.LayoutElement.preferredWidth = CardWidth * 0.72f;
                card.LayoutElement.preferredHeight = CardHeight * 0.72f;
                card.Root.localScale = Vector3.one;
                card.CanvasGroup.alpha = 1f;
            }

            var covered = onBoard && IsCovered(card);
            card.Button.interactable = (onBoard && !covered && !settlementShown) || (movedOut && !settlementShown && trayCards.Count < TrayCapacity);
            card.Background.color = CardColor;
            card.Icon.color = Color.white;
            SetCoveredMaskVisible(card, covered);
            RefreshCardVisual(card);
        }

        private void RefreshCardVisual(CardView card)
        {
            if (card.Icon != null)
            {
                card.Icon.sprite = loadedIcons.Count > 0 ? loadedIcons[card.TypeId % loadedIcons.Count] : null;
            }

        }

        private void RefreshTrayPositions()
        {
            for (var i = 0; i < trayCards.Count; i++)
            {
                var card = trayCards[i];
                if (card.IsMoving)
                {
                    card.Root.SetParent(trayRoot, false);
                    card.Root.sizeDelta = new Vector2(TrayCardWidth, TrayCardHeight);
                    card.LayoutElement.ignoreLayout = false;
                    card.LayoutElement.preferredWidth = -1f;
                    card.LayoutElement.preferredHeight = -1f;
                    card.Root.SetSiblingIndex(i);
                    card.Button.interactable = false;
                    SetCoveredMaskVisible(card, false);
                    continue;
                }

                card.Root.SetParent(trayRoot, false);
                card.Root.sizeDelta = new Vector2(TrayCardWidth, TrayCardHeight);
                card.LayoutElement.ignoreLayout = false;
                card.LayoutElement.preferredWidth = -1f;
                card.LayoutElement.preferredHeight = -1f;
                card.Root.SetSiblingIndex(i);
                card.Background.color = CardColor;
                card.Button.interactable = false;
                card.Icon.color = Color.white;
                card.CanvasGroup.alpha = 1f;
                SetCoveredMaskVisible(card, false);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(trayRoot);
        }

        private void RefreshMovedOutPositions()
        {
            for (var i = 0; i < movedOutCards.Count; i++)
            {
                var card = movedOutCards[i];
                card.Root.SetParent(movedOutRoot, false);
                card.Root.SetSiblingIndex(i);
                card.Root.sizeDelta = new Vector2(CardWidth * 0.72f, CardHeight * 0.72f);
                card.LayoutElement.ignoreLayout = false;
                card.LayoutElement.preferredWidth = CardWidth * 0.72f;
                card.LayoutElement.preferredHeight = CardHeight * 0.72f;
                card.Background.color = CardColor;
                card.Icon.color = Color.white;
                card.CanvasGroup.alpha = 1f;
                card.Button.interactable = !settlementShown && trayCards.Count < TrayCapacity;
                SetCoveredMaskVisible(card, false);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(movedOutRoot);
        }

        private int GetTrayInsertIndex(int typeId)
        {
            for (var i = trayCards.Count - 1; i >= 0; i--)
            {
                if (trayCards[i].TypeId == typeId)
                {
                    return i + 1;
                }
            }

            return trayCards.Count;
        }

        private Vector2 GetTrayCardPosition(int index)
        {
            var rect = trayRoot.rect;
            var x = -rect.width * trayRoot.pivot.x + TrayCardWidth * 0.5f + index * (TrayCardWidth + TrayCardSpacing);
            var y = rect.height * (0.5f - trayRoot.pivot.y);
            return new Vector2(x, y);
        }

        private RectTransform CreateMoveGhost(CardView card)
        {
            if (card == null || card.Root == null || contentRoot == null)
            {
                return null;
            }

            var ghostObject = UnityEngine.Object.Instantiate(card.Root.gameObject, contentRoot, false);
            ghostObject.name = card.Root.name + "_MoveGhost";

            var ghost = ghostObject.GetComponent<RectTransform>();
            ghost.anchorMin = new Vector2(0.5f, 0.5f);
            ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = card.Root.sizeDelta;
            ghost.localScale = Vector3.one;
            ghost.anchoredPosition = (Vector2)contentRoot.InverseTransformPoint(card.Root.position);
            ghost.SetAsLastSibling();

            var button = ghostObject.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }

            var canvasGroup = ghostObject.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
            }

            var layoutElement = ghostObject.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = true;
            }

            return ghost;
        }

        private void PlayMoveToTrayAnimation(CardView card, RectTransform movingGhost, Vector3 startWorldPosition, Vector2 targetPosition)
        {
            if (card == null || card.Root == null || trayRoot == null || contentRoot == null)
            {
                return;
            }

            if (movingGhost == null)
            {
                card.CanvasGroup.alpha = 1f;
                return;
            }

            if (card.MoveRoutine != null)
            {
                HostBehaviour.StopCoroutine(card.MoveRoutine);
            }

            card.MoveGhost = movingGhost;
            card.MoveRoutine = HostBehaviour.StartCoroutine(AnimateMoveToTray(card, movingGhost, startWorldPosition, targetPosition));
        }

        private IEnumerator AnimateMoveToTray(CardView card, RectTransform movingGhost, Vector3 startWorldPosition, Vector2 targetPosition)
        {
            card.IsMoving = true;
            var rect = movingGhost;
            var startPosition = (Vector2)contentRoot.InverseTransformPoint(startWorldPosition);
            var targetWorldPosition = trayRoot.TransformPoint(targetPosition);
            var endPosition = (Vector2)contentRoot.InverseTransformPoint(targetWorldPosition);

            if (rect != null)
            {
                rect.anchoredPosition = startPosition;
                rect.localScale = Vector3.one;
            }

            var elapsed = 0f;
            while (elapsed < MoveToTrayDuration && (card.State == CardState.Tray || card.State == CardState.Removed))
            {
                if (rect == null)
                {
                    card.IsMoving = false;
                    card.MoveRoutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / MoveToTrayDuration));
                rect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, t);
                yield return null;
            }

            if (card.State == CardState.Tray || card.State == CardState.Removed)
            {
                if (rect == null)
                {
                    card.IsMoving = false;
                    card.MoveRoutine = null;
                    yield break;
                }

                rect.anchoredPosition = endPosition;
                rect.localScale = Vector3.one * 0.88f;
            }

            if (rect != null)
            {
                UnityEngine.Object.Destroy(rect.gameObject);
            }

            card.MoveGhost = null;
            card.IsMoving = false;
            card.MoveRoutine = null;
            if (card.State == CardState.Tray)
            {
                card.LayoutElement.ignoreLayout = false;
                card.CanvasGroup.alpha = 1f;
                RefreshTrayPositions();
                ResolveTrayAfterMove();
            }
            else if (card.State == CardState.Removed)
            {
                card.CanvasGroup.alpha = 1f;
            }
        }

        private void PlayMatchClearAnimation(CardView card)
        {
            if (card == null || card.Root == null)
            {
                return;
            }

            card.IsClearing = true;
            card.ClearRoutine = HostBehaviour.StartCoroutine(AnimateMatchClear(card));
        }

        private IEnumerator AnimateMatchClear(CardView card)
        {
            if (card.MoveRoutine != null)
            {
                yield return card.MoveRoutine;
            }

            if (card.Root == null)
            {
                card.IsClearing = false;
                card.ClearRoutine = null;
                yield break;
            }

            card.Root.SetParent(trayRoot, false);
            card.Root.SetAsLastSibling();
            card.Root.gameObject.SetActive(true);
            card.LayoutElement.ignoreLayout = true;
            card.CanvasGroup.alpha = 1f;

            var startScale = card.Root.localScale;
            var elapsed = 0f;
            while (elapsed < MatchClearDuration)
            {
                if (card.Root == null)
                {
                    card.IsClearing = false;
                    card.ClearRoutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(Mathf.Clamp01(elapsed / MatchClearDuration));
                card.Root.localScale = Vector3.Lerp(startScale, Vector3.one * 0.35f, t);
                card.CanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            card.Root.gameObject.SetActive(false);
            card.Root.localScale = Vector3.one;
            card.CanvasGroup.alpha = 1f;
            card.IsClearing = false;
            card.ClearRoutine = null;
        }

        private void StopCardAnimations(CardView card)
        {
            StopCardMoveAnimation(card);
            if (card != null && card.ClearRoutine != null)
            {
                HostBehaviour.StopCoroutine(card.ClearRoutine);
                card.ClearRoutine = null;
                card.IsClearing = false;
            }
        }

        private void StopCardMoveAnimation(CardView card)
        {
            if (card != null && card.MoveRoutine != null)
            {
                HostBehaviour.StopCoroutine(card.MoveRoutine);
                card.MoveRoutine = null;
                card.IsMoving = false;
            }

            if (card != null && card.MoveGhost != null)
            {
                UnityEngine.Object.Destroy(card.MoveGhost.gameObject);
                card.MoveGhost = null;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static void SetCoveredMaskVisible(CardView card, bool visible)
        {
            if (card != null && card.CoveredMask != null && card.CoveredMask.activeSelf != visible)
            {
                card.CoveredMask.SetActive(visible);
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void EnsureLevelProgress()
        {
            if (levelProgress == null)
            {
                levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, LevelDefinitions.Length);
            }
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
        }

        private static void Shuffle(int[] values, int seed)
        {
            var random = new System.Random(seed);
            for (var i = values.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
        }

        private static void ShuffleList<T>(List<T> values, System.Random random)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
        }

        private static int CreateRandomSeed()
        {
            return Guid.NewGuid().GetHashCode();
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.66f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void ConfigureCardButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
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
    }
}
