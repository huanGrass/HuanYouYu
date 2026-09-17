using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class BubblePopGameView : MiniGameBase
    {
        public const string GameIdConstant = "bubble-pop";
        private BubblePopBoard board;
        private AspectRatioFitter boardFitter;
        private Button resetButton;
        private MiniGameDropdown sizeDropdown;
        private MiniGameDropdown colorDropdown;
        private MiniGameDropdown reboundDropdown;

        public BubblePopGameView(MonoBehaviour host, Transform parent,
            Action<MiniGameSettlement> onComplete, Action onExit)
            : base(GameIdConstant, "BubblePopView", host, parent, onComplete, onExit) { }

        protected override MiniGameShellLayout CreateShellLayout()
        { return new MiniGameShellLayout(256f, MiniGameShellLayout.DefaultBottomInset, MiniGameShellBottomMode.DefaultSlot); }

        protected override void BuildOrBindSections()
        {
            var top = MiniGameShellTopBarBuilder.CreateTopBar(Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("BubblePopHeader"));
            top.TitleText.text = UiTextCatalog.Get("game.bubble-pop.name");
            top.ScoreText.text = UiTextCatalog.Get("bubble-pop.hint");
            top.Root.offsetMin = new Vector2(0f, 84f);
            var area = new GameObject("BubblePopArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(Shell.ContentHost, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(12f, 12f);
            area.offsetMax = new Vector2(-12f, -12f);

            var root = new GameObject("BubblePopBoard", typeof(RectTransform), typeof(AspectRatioFitter));
            root.transform.SetParent(area, false);
            board = root.AddComponent<BubblePopBoard>();
            board.Build();
            boardFitter = root.GetComponent<AspectRatioFitter>();
            boardFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            boardFitter.aspectRatio = board.ColumnCount / (float)board.RowCount;
            BuildSettings();

            var bottom = MiniGameShellBottomBarBuilder.CreateBottomContainer(Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("BubblePopActions"));
            resetButton = MiniGameShellBottomBarBuilder.CreateTextActionButton(bottom.ActionBar,
                "BubblePopResetButton", UiTextCatalog.Get("bubble-pop.reset"), 180f);
            resetButton.onClick.AddListener(ResetGame);
            MiniGameSfxPlayer.Attach(resetButton);
        }
        private void BuildSettings()
        {
            var row = new GameObject("BubblePopSettings", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = row.GetComponent<RectTransform>();
            rect.SetParent(Shell.TopHost, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(20f, 4f);
            rect.offsetMax = new Vector2(-20f, 80f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 16f;
            sizeDropdown = CreateSettingsDropdown(rect, "BubblePopSize", "bubble-pop.size",
                new[] { "bubble-pop.size.small", "bubble-pop.size.medium", "bubble-pop.size.large",
                    "bubble-pop.size.extra_large", "bubble-pop.size.huge" }, 1,
                index => {
                    board.CancelGesture();
                    var columns = index < 3 ? 4 + index * 2 : index == 3 ? 12 : 20;
                    var rows = index < 3 ? columns + 1 : index == 3 ? 16 : 24;
                    board.SetSize(columns, rows);
                    boardFitter.aspectRatio = columns / (float)rows;
                });
            colorDropdown = CreateSettingsDropdown(rect, "BubblePopColor", "bubble-pop.color",
                new[] { "bubble-pop.color.rainbow", "bubble-pop.color.mixed", "bubble-pop.color.blue",
                    "bubble-pop.color.mint", "bubble-pop.color.pink", "bubble-pop.color.purple", "bubble-pop.color.yellow" }, 0,
                index => { board.CancelGesture(); board.SetColorMode(index); });
            reboundDropdown = CreateSettingsDropdown(rect, "BubblePopRebound", "bubble-pop.rebound",
                new[] { "bubble-pop.rebound.never", "bubble-pop.rebound.five", "bubble-pop.rebound.one", "bubble-pop.rebound.instant" }, 0,
                index => { board.CancelGesture(); board.SetReboundMode(index); });
        }

        private static MiniGameDropdown CreateSettingsDropdown(Transform parent, string name,
            string labelKey, string[] optionKeys, int selected, Action<int> changed)
        {
            var group = new GameObject(name + "Group", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            group.transform.SetParent(parent, false);
            group.GetComponent<LayoutElement>().preferredWidth = 210f;
            group.GetComponent<LayoutElement>().preferredHeight = 72f;
            var layout = group.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            var labelObject = new GameObject(name + "Label", typeof(RectTransform), typeof(LayoutElement));
            labelObject.transform.SetParent(group.transform, false);
            labelObject.GetComponent<LayoutElement>().preferredWidth = 206f;
            labelObject.GetComponent<LayoutElement>().preferredHeight = 22f;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = MiniGameFontProvider.DefaultFont;
            label.fontSize = 19f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.42f, 0.34f, 0.18f);
            label.raycastTarget = false;
            label.text = UiTextCatalog.Get(labelKey);
            var dropdownObject = new GameObject(name + "Dropdown", typeof(RectTransform));
            dropdownObject.transform.SetParent(group.transform, false);
            var dropdown = dropdownObject.AddComponent<MiniGameDropdown>();
            var options = new string[optionKeys.Length];
            for (var i = 0; i < options.Length; i++) options[i] = UiTextCatalog.Get(optionKeys[i]);
            dropdown.Configure(options, selected, changed, 206f, 44f, 42f, 5, label.color,
                "huanyouyu.dropdown." + GameIdConstant + "." + name);
            return dropdown;
        }
        protected override void ResetGame() { board.CancelGesture(); board.ResetBoard(); }
        public override void Tick(float deltaTime) { board.Tick(deltaTime); }
        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        { return ("game.bubble-pop.help", null); }
        protected override void OnPauseRequested()
        {
            sizeDropdown.Close();
            colorDropdown.Close();
            reboundDropdown.Close();

            board.SetPaused(true);
            Shell.ShowPausePopup(() => { Shell.ClosePopup(); board.SetPaused(false); },
                () => { Shell.ClosePopup(); ExitToHall?.Invoke(); });
        }
        protected override void OnBeforeDispose()
        {
            sizeDropdown.Close();
            colorDropdown.Close();
            reboundDropdown.Close();


            board.SetPaused(true);
            resetButton.onClick.RemoveAllListeners();
            Shell.ClosePopup();
        }
    }
}
