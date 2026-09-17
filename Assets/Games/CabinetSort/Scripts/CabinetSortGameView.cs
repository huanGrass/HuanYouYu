using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class CabinetSortGameView : MiniGameBase
    {
        public const string GameIdConstant = "cabinet-sort";
        private static readonly string[] IconKeys =
        {
            "apple", "orange", "strawberry", "grapes", "peach", "pineapple",
            "carrot", "corn", "mushroom", "pumpkin", "eggplant", "watermelon",
            "cabbage", "garlic", "tomato", "wheat", "flower", "leaf",
            "potion", "scroll", "key", "diamond", "star", "feather"
        };

        private CabinetSortBoard board;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI instruction;
        private RectTransform cupboard;
        private RawImage[] icons;
        private Outline[] iconOutlines;
        private RoundedRectGraphic[] slots;
        private Button[] buttons;
        private Button hintButton;
        private Button shuffleButton;
        private int selected = -1;
        private int hintFirst = -1;
        private int hintSecond = -1;
        private bool locked;
        private bool settled;

        public CabinetSortGameView(MonoBehaviour hostBehaviour, Transform parent,
            Action<MiniGameSettlement> onComplete, Action onExit)
            : base(GameIdConstant, "CabinetSortView", hostBehaviour, parent, onComplete, onExit) { }

        protected override void BuildOrBindSections()
        {
            var top = MiniGameShellTopBarBuilder.CreateTopBar(Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("CabinetSortTop"));
            titleLabel = top.TitleText;
            scoreLabel = top.ScoreText;
            scoreLabel.enableAutoSizing = true;
            scoreLabel.fontSizeMin = 14;
            scoreLabel.fontSizeMax = 24;
            var content = Rect("CabinetSortContent", Shell.ContentHost, Vector2.zero, Vector2.one, 14f);
            var instructionRoot = Rect("Instruction", content, new Vector2(0, .93f), Vector2.one, 0);
            instruction = instructionRoot.gameObject.AddComponent<TextMeshProUGUI>();
            instruction.font = MiniGameFontProvider.DefaultFont;
            instruction.fontSize = 22;
            instruction.enableAutoSizing = true;
            instruction.fontSizeMin = 14;
            instruction.fontSizeMax = 22;
            instruction.alignment = TextAlignmentOptions.Center;
            instruction.color = new Color32(73, 92, 48, 255);
            instruction.raycastTarget = false;
            var frame = Rect("CabinetFrame", content, Vector2.zero, new Vector2(1, .92f), 0);
            Panel(frame, new Color32(251, 232, 185, 255), 26);
            var wood = Rect("Wood", frame, Vector2.zero, Vector2.one, 12);
            Panel(wood, new Color32(200, 151, 89, 255), 18);
            cupboard = Rect("CabinetShelves", wood, Vector2.zero, Vector2.one, 10);
            motionRoot = Rect("ItemMotion", wood, Vector2.zero, Vector2.one, 0);
            tweenRunner = motionRoot.gameObject.AddComponent<UiTweenRunner>();
            var bottom = MiniGameShellBottomBarBuilder.CreateBottomContainer(Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("CabinetSortActions"));
            hintButton = MiniGameShellBottomBarBuilder.CreateHintButton(bottom.ActionBar).Button;
            shuffleButton = MiniGameShellBottomBarBuilder.CreateShuffleButton(bottom.ActionBar).Button;
            foreach (var button in new[] { hintButton, shuffleButton })
            {
                var colors = button.colors;
                colors.disabledColor = colors.normalColor;
                colors.highlightedColor = colors.normalColor;
                colors.selectedColor = colors.normalColor;
                button.colors = colors;
            }
            hintButton.onClick.AddListener(ShowHint);
            shuffleButton.onClick.AddListener(Shuffle);
        }

        protected override void ResetGame()
        {
            StopMotion();
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            board = new CabinetSortBoard(new System.Random());
            busyShelves = new bool[board.Count / 3];
            selected = hintFirst = hintSecond = -1;
            locked = settled = false;
            for (var i = cupboard.childCount - 1; i >= 0; i--)
            {
                cupboard.GetChild(i).gameObject.SetActive(false);
                UnityEngine.Object.Destroy(cupboard.GetChild(i).gameObject);
            }
            icons = new RawImage[board.Count];
            iconOutlines = new Outline[board.Count];
            slots = new RoundedRectGraphic[board.Count];
            buttons = new Button[board.Count];
            for (var group = 0; group < board.Count / 3; group++)
            {
                var row = group / CabinetSortBoard.ShelfColumns;
                var col = group % CabinetSortBoard.ShelfColumns;
                var shelf = Rect("Shelf_" + group, cupboard,
                    new Vector2(col / (float)CabinetSortBoard.ShelfColumns, 1 - (row + 1) / (float)CabinetSortBoard.ShelfRows),
                    new Vector2((col + 1) / (float)CabinetSortBoard.ShelfColumns, 1 - row / (float)CabinetSortBoard.ShelfRows), 4);
                Panel(shelf, new Color32(153, 112, 66, 255), 8);
                var ledge = Rect("Ledge", shelf, Vector2.zero, new Vector2(1, .1f), 0);
                Panel(ledge, new Color32(244, 208, 143, 255), 3);
                for (var position = 0; position < 3; position++)
                {
                    var index = group * 3 + position;
                    var slot = Rect("CabinetItem_" + index, shelf,
                        new Vector2(position / 3f, .12f), new Vector2((position + 1) / 3f, 1), 3);
                    slots[index] = Panel(slot, Color.clear, 10);
                    slots[index].raycastTarget = true;
                    var iconRect = Rect("Icon", slot, Vector2.zero, Vector2.one, 4);
                    iconRect.pivot = new Vector2(.5f, 0);
                    var aspect = iconRect.gameObject.AddComponent<AspectRatioFitter>();
                    aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    icons[index] = iconRect.gameObject.AddComponent<RawImage>();
                    icons[index].raycastTarget = false;
                    iconOutlines[index] = iconRect.gameObject.AddComponent<Outline>();
                    iconOutlines[index].effectDistance = new Vector2(1.5f, -1.5f);
                    iconOutlines[index].useGraphicAlpha = true;
                    iconOutlines[index].enabled = false;
                    buttons[index] = slot.gameObject.AddComponent<Button>();
                    buttons[index].targetGraphic = slots[index];
                    buttons[index].transition = Selectable.Transition.None;
                    buttons[index].onClick.AddListener(() => SelectItem(index));
                    var drag = slot.gameObject.AddComponent<ItemDragHandler>();
                    drag.Owner = this;
                    drag.Index = index;
                }
            }
            Refresh();
        }

        private void SelectItem(int index)
        {
            if (locked || settled || busyShelves[index / 3] || dragged >= 0 || board[index] < 0) return;
            if (index != hintFirst && index != hintSecond) hintFirst = hintSecond = -1;
            if (selected == index) selected = -1;
            else if (selected < 0) selected = index;
            else
            {
                StartSwap(selected, index);
                return;
            }
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, .8f);
            Refresh();
        }

        private void ShowHint()
        {
            if (locked || settled || animating || dragged >= 0) return;
            selected = -1;
            board.TryGetHint(out hintFirst, out hintSecond);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, .8f);
            Refresh();
        }

        private void Shuffle()
        {
            if (locked || settled || animating || dragged >= 0) return;
            board.Shuffle(new System.Random());
            selected = hintFirst = hintSecond = -1;
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, .8f);
            Refresh();
        }

        private void Refresh()
        {
            titleLabel.text = UiTextCatalog.Get("game.cabinet-sort.name");
            scoreLabel.text = UiTextCatalog.Format("cabinet-sort.hud", board.TotalCount - board.Remaining, board.TotalCount, board.HiddenCount, board.Moves);
            instruction.text = UiTextCatalog.Get(hintFirst >= 0 ? "cabinet-sort.hint" : "cabinet-sort.instruction");
            for (var i = 0; i < board.Count; i++)
            {
                if (!busyShelves[i / 3])
                {
                    icons[i].enabled = board[i] >= 0 && i != dragged;
                    icons[i].color = Color.white;
                    if (board[i] >= 0) SetIconTexture(i, board[i]);
                }
                slots[i].color = Color.clear;
                if (!busyShelves[i / 3])
                {
                    iconOutlines[i].enabled = board[i] >= 0 &&
                        (i == selected || i == dropTarget || i == hintFirst || i == hintSecond);
                    iconOutlines[i].effectColor = i == selected
                        ? new Color32(255, 247, 199, 210)
                        : new Color32(210, 255, 158, 210);
                }
                buttons[i].interactable = !locked && !settled && !busyShelves[i / 3] && dragged < 0 && board[i] >= 0;
            }
            hintButton.interactable = shuffleButton.interactable = !locked && !settled && !animating && dragged < 0;
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys() => ("game.cabinet-sort.help", null);

        private void SetIconTexture(int index, int value)
        {
            var texture = MiniGameIconCatalog.GetTexture(IconKeys[value]);
            icons[index].texture = texture;
            if (texture != null)
                icons[index].GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
        }

        protected override void OnPauseRequested()
        {
            if (settled) return;
            if (dragged >= 0) CancelDrag();
            locked = true;
            Refresh();
            Shell.ShowPausePopup(() => { locked = false; Shell.ClosePopup(); Refresh(); }, () => ShowResult(false));
        }

        private void ShowResult(bool completed)
        {
            if (settled) return;
            StopMotion();
            settled = locked = true;
            Refresh();
            var cleared = board.TotalCount - board.Remaining;
            var settlement = new MiniGameSettlement
            {
                Score = cleared / 3, CoinCount = cleared / 3 * 5, ChestCount = completed ? 1 : 0,
                Summary = UiTextCatalog.Format("cabinet-sort.summary", cleared, board.Moves)
            };
            var info = new MiniGameSettlementInfoRow(UiTextCatalog.Get("cabinet-sort.cleared"), cleared + "/" + board.TotalCount);
            var moves = new MiniGameSettlementInfoRow(UiTextCatalog.Get("cabinet-sort.moves"), board.Moves.ToString());
            if (!completed)
            {
                ShowBackHallRewardSettlementPanel(settlement, "CabinetSortSettlementPanel", info, moves,
                    () => CompleteGame?.Invoke(settlement));
                return;
            }
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1);
            ShowRewardSettlementPanel(settlement, new MiniGameRewardSettlementPanelParams
            {
                RootName = "CabinetSortSettlementPanel", Title = UiTextCatalog.Get("cabinet-sort.win"),
                PrimaryInfo = info, SecondaryInfo = moves,
                RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                CoinCount = settlement.CoinCount, ChestCount = settlement.ChestCount
            }, () => { GrantSettlementReward(settlement); ResetGame(); },
                () => CompleteGame?.Invoke(settlement), false);
        }

        protected override void OnBeforeDispose()
        {
            StopMotion();
            hintButton.onClick.RemoveListener(ShowHint);
            shuffleButton.onClick.RemoveListener(Shuffle);
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, float inset)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        private static RoundedRectGraphic Panel(RectTransform rect, Color color, float radius)
        {
            var panel = rect.gameObject.AddComponent<RoundedRectGraphic>();
            panel.color = color;
            panel.CornerRadius = radius;
            panel.raycastTarget = false;
            return panel;
        }
    }
}
