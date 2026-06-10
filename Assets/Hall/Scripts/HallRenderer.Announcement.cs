using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed partial class HallRenderer
    {
        private const string AnnouncementLeafSpritePath = "GameIcons/leaf";
        private const float AnnouncementDialogWidth = 704f;
        private const float AnnouncementDialogHeight = 760f;
        private const float AnnouncementDialogHorizontalMargin = 26f;
        private const float AnnouncementDialogVerticalMargin = 118f;
        private const float AnnouncementCloseButtonOverflow = 20f;
        private const float AnnouncementPreferredSidebarWidth = 188f;
        private const float AnnouncementMinSidebarWidth = 150f;
        private const float AnnouncementContentBaseBottomInset = 56f;
        private const float AnnouncementContentFooterBottomInset = 118f;
        private const float AnnouncementFooterYOffset = 20f;

        private readonly List<AnnouncementTabBinding> announcementTabBindings = new List<AnnouncementTabBinding>();
        private RectTransform announcementDialogRoot;
        private RectTransform announcementContentFrameRoot;
        private RectTransform announcementContentRoot;
        private RectTransform announcementViewportRoot;
        private RectTransform announcementFixedFooterRoot;
        private ScrollRect announcementScrollRect;
        private AnnouncementTab announcementCurrentTab = AnnouncementTab.RecentUpdates;
        private float announcementSidebarWidth = AnnouncementPreferredSidebarWidth;
        private bool announcementCompactLayout;

        private enum AnnouncementTab
        {
            RecentUpdates,
            AboutGame,
            Credits
        }

        private sealed class AnnouncementTabBinding
        {
            public AnnouncementTab Tab;
            public RoundedRectGraphic Background;
            public TextMeshProUGUI Label;
        }

        private GameObject CreateAnnouncementPopup()
        {
            announcementTabBindings.Clear();
            announcementDialogRoot = null;
            announcementContentFrameRoot = null;
            announcementContentRoot = null;
            announcementViewportRoot = null;
            announcementFixedFooterRoot = null;
            announcementScrollRect = null;
            announcementCurrentTab = AnnouncementTab.RecentUpdates;
            announcementSidebarWidth = AnnouncementPreferredSidebarWidth;
            announcementCompactLayout = false;

            var root = CreateModalHost("AnnouncementPopup");
            if (root == null)
            {
                return null;
            }

            var dialog = CreateAnnouncementPanel(root.transform);
            CreateAnnouncementSidebar(dialog);
            CreateAnnouncementContentArea(dialog);
            CreateAnnouncementCloseButton(dialog);
            CreateAnnouncementDecor(dialog);
            SelectAnnouncementTab(AnnouncementTab.RecentUpdates);
            return root;
        }

        private RectTransform CreateAnnouncementPanel(Transform parent)
        {
            var outer = CreateRoundedRect(
                "Dialog",
                parent,
                new Color32(191, 218, 104, 255),
                34f,
                true,
                typeof(Shadow));
            var outerRect = outer.GetComponent<RectTransform>();
            announcementDialogRoot = outerRect;
            outerRect.anchorMin = new Vector2(0.5f, 0.5f);
            outerRect.anchorMax = new Vector2(0.5f, 0.5f);
            outerRect.pivot = new Vector2(0.5f, 0.5f);
            outerRect.sizeDelta = CalculateAnnouncementDialogSize(parent);

            var shadow = outer.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.18f, 0.28f, 0.10f, 0.34f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var inner = CreateRoundedRect(
                "InnerPanel",
                outer.transform,
                new Color32(255, 251, 238, 255),
                28f,
                true);
            Stretch(
                inner.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 12f),
                new Vector2(-12f, -12f));

            return outerRect;
        }

        private Vector2 CalculateAnnouncementDialogSize(Transform parent)
        {
            var availableSize = GetAnnouncementAvailableSize(parent);
            var maxWidth = Mathf.Max(340f, availableSize.x - (AnnouncementDialogHorizontalMargin * 2f) - AnnouncementCloseButtonOverflow);
            var maxHeight = Mathf.Max(420f, availableSize.y - (AnnouncementDialogVerticalMargin * 2f));
            var width = Mathf.Min(AnnouncementDialogWidth, maxWidth);
            var height = Mathf.Min(AnnouncementDialogHeight, maxHeight);
            return new Vector2(width, height);
        }

        private static Vector2 GetAnnouncementAvailableSize(Transform parent)
        {
            var parentRect = parent as RectTransform;
            if (parentRect != null)
            {
                var width = parentRect.rect.width;
                var height = parentRect.rect.height;
                if (width > 1f && height > 1f)
                {
                    return new Vector2(width, height);
                }
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private float CalculateAnnouncementSidebarWidth(RectTransform dialog)
        {
            if (dialog == null)
            {
                return AnnouncementPreferredSidebarWidth;
            }

            var dialogWidth = dialog.rect.width;
            if (dialogWidth <= 1f)
            {
                dialogWidth = dialog.sizeDelta.x;
            }

            var availableForSidebar = Mathf.Max(AnnouncementMinSidebarWidth, dialogWidth * 0.28f);
            return Mathf.Clamp(availableForSidebar, AnnouncementMinSidebarWidth, AnnouncementPreferredSidebarWidth);
        }

        private void CreateAnnouncementSidebar(RectTransform dialog)
        {
            announcementSidebarWidth = CalculateAnnouncementSidebarWidth(dialog);
            announcementCompactLayout = announcementSidebarWidth < 174f;

            var divider = CreateRoundedRect(
                "SidebarDivider",
                dialog,
                new Color(0.74f, 0.66f, 0.47f, 0.32f),
                1f,
                false);
            Stretch(
                divider.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(announcementSidebarWidth + 12f, 34f),
                new Vector2(announcementSidebarWidth + 14f, -34f));

            var sidebar = new GameObject("Sidebar", typeof(RectTransform), typeof(VerticalLayoutGroup));
            sidebar.transform.SetParent(dialog, false);
            var sidebarRect = sidebar.GetComponent<RectTransform>();
            sidebarRect.anchorMin = new Vector2(0f, 1f);
            sidebarRect.anchorMax = new Vector2(0f, 1f);
            sidebarRect.pivot = new Vector2(0f, 1f);
            sidebarRect.anchoredPosition = new Vector2(announcementCompactLayout ? 20f : 28f, announcementCompactLayout ? -78f : -88f);
            sidebarRect.sizeDelta = new Vector2(announcementSidebarWidth - (announcementCompactLayout ? 28f : 34f), 430f);

            var layout = sidebar.GetComponent<VerticalLayoutGroup>();
            layout.spacing = announcementCompactLayout ? 18f : 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateAnnouncementTabButton(sidebar.transform, AnnouncementTab.RecentUpdates);
            CreateAnnouncementTabButton(sidebar.transform, AnnouncementTab.AboutGame);
            CreateAnnouncementTabButton(sidebar.transform, AnnouncementTab.Credits);
        }

        private void CreateAnnouncementTabButton(Transform parent, AnnouncementTab tab)
        {
            var buttonObject = CreateRoundedRect(
                "Tab_" + tab,
                parent,
                Color.white,
                22f,
                true,
                typeof(Button),
                typeof(LayoutElement),
                typeof(Shadow));

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, announcementCompactLayout ? 72f : 78f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = announcementCompactLayout ? 72f : 78f;

            var shadow = buttonObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.31f, 0.27f, 0.17f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var background = buttonObject.GetComponent<RoundedRectGraphic>();
            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
            button.onClick.AddListener(delegate
            {
                SelectAnnouncementTab(tab);
            });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.66f);

            var label = CreateButtonLabel("Label", UiTextCatalog.Get(GetAnnouncementTabKey(tab)), announcementCompactLayout ? 21f : 24f);
            label.transform.SetParent(buttonObject.transform, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = announcementCompactLayout ? 17f : 20f;
            label.fontSizeMax = announcementCompactLayout ? 22f : 26f;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            announcementTabBindings.Add(new AnnouncementTabBinding
            {
                Tab = tab,
                Background = background,
                Label = label
            });
        }

        private void CreateAnnouncementContentArea(RectTransform dialog)
        {
            var contentFrame = new GameObject(
                "ContentFrame",
                typeof(RectTransform),
                typeof(ScrollRect));
            contentFrame.transform.SetParent(dialog, false);
            var contentFrameRect = contentFrame.GetComponent<RectTransform>();
            announcementContentFrameRoot = contentFrameRect;
            UpdateAnnouncementContentFrameLayout();

            var viewport = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D));
            viewport.transform.SetParent(contentFrame.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;

            var content = new GameObject(
                "Content",
                typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            announcementViewportRoot = viewport.GetComponent<RectTransform>();
            announcementContentRoot = content.GetComponent<RectTransform>();
            announcementScrollRect = contentFrame.GetComponent<ScrollRect>();
            announcementScrollRect.viewport = announcementViewportRoot;
            announcementScrollRect.content = announcementContentRoot;
            announcementScrollRect.horizontal = false;
            announcementScrollRect.vertical = true;
            announcementScrollRect.movementType = ScrollRect.MovementType.Clamped;
            announcementScrollRect.inertia = true;
            announcementScrollRect.scrollSensitivity = 28f;
            announcementContentRoot.anchorMin = new Vector2(0f, 1f);
            announcementContentRoot.anchorMax = new Vector2(1f, 1f);
            announcementContentRoot.pivot = new Vector2(0.5f, 1f);
            announcementContentRoot.anchoredPosition = Vector2.zero;
            announcementContentRoot.sizeDelta = Vector2.zero;
        }

        private void CreateAnnouncementCloseButton(RectTransform dialog)
        {
            var ringObject = CreateRoundedRect(
                "CloseButton",
                dialog,
                new Color32(177, 205, 79, 255),
                100f,
                true,
                typeof(Button),
                typeof(Shadow));
            var closeRect = ringObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-36f, -36f);
            closeRect.sizeDelta = new Vector2(58f, 58f);

            var shadow = ringObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.28f, 0.24f, 0.13f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -3f);

            var buttonGraphic = ringObject.GetComponent<RoundedRectGraphic>();
            var button = ringObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = buttonGraphic;
            button.onClick.AddListener(CloseActiveModal);
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiBack, 0.78f);
            EnsureMenuButtonPressEffect(button.transform);

            var innerObject = CreateRoundedRect(
                "Inner",
                ringObject.transform,
                new Color32(255, 252, 232, 255),
                100f,
                false);
            Stretch(innerObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            CreateCloseStroke(ringObject.transform, "StrokeA", 42f);
            CreateCloseStroke(ringObject.transform, "StrokeB", -42f);
        }

        private void CreateCloseStroke(Transform parent, string name, float rotation)
        {
            var stroke = CreateRoundedRect(
                name,
                parent,
                new Color32(87, 132, 39, 255),
                0f,
                false);
            var rect = stroke.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(28f, 7f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void CreateAnnouncementDecor(RectTransform dialog)
        {
            CreateAnnouncementLeaf(dialog, "LeafTopLeftA", new Vector2(50f, -36f), new Vector2(38f, 38f), -38f);
            CreateAnnouncementLeaf(dialog, "LeafTopLeftB", new Vector2(76f, -52f), new Vector2(30f, 30f), 18f);
            CreateAnnouncementLeaf(dialog, "LeafBottomRightA", new Vector2(-42f, 42f), new Vector2(42f, 42f), -34f, true);
            CreateAnnouncementLeaf(dialog, "LeafBottomRightB", new Vector2(-74f, 26f), new Vector2(32f, 32f), 18f, true);
        }

        private void CreateAnnouncementLeaf(RectTransform parent, string name, Vector2 position, Vector2 size, float rotation, bool rightAnchor = false)
        {
            var leaf = CreateImage(name, LoadSprite(AnnouncementLeafSpritePath), true);
            leaf.transform.SetParent(parent, false);
            leaf.color = new Color(1f, 1f, 1f, 0.82f);
            var rect = leaf.rectTransform;
            rect.anchorMin = rightAnchor ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            rect.anchorMax = rightAnchor ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            leaf.raycastTarget = false;
        }

        private void SelectAnnouncementTab(AnnouncementTab tab)
        {
            announcementCurrentTab = tab;
            for (var i = 0; i < announcementTabBindings.Count; i++)
            {
                var binding = announcementTabBindings[i];
                var selected = binding.Tab == announcementCurrentTab;
                binding.Background.color = selected
                    ? new Color32(244, 255, 144, 255)
                    : new Color32(255, 252, 242, 255);
                binding.Label.color = selected
                    ? new Color32(63, 72, 27, 255)
                    : new Color32(76, 59, 35, 255);
            }

            RebuildAnnouncementContent();
        }

        private void RebuildAnnouncementContent()
        {
            if (announcementContentRoot == null)
            {
                return;
            }

            ClearAnnouncementFixedFooter();
            UpdateAnnouncementContentFrameLayout();
            for (var i = announcementContentRoot.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(announcementContentRoot.GetChild(i).gameObject);
            }

            var y = 4f;
            y = CreateAnnouncementTitle(announcementContentRoot, UiTextCatalog.Get(GetAnnouncementTitleKey(announcementCurrentTab)), y);

            if (announcementCurrentTab == AnnouncementTab.RecentUpdates)
            {
                y = CreateAnnouncementUpdateCards(announcementContentRoot, y);
                CreateAnnouncementFixedFooter();
            }
            else if (announcementCurrentTab == AnnouncementTab.AboutGame)
            {
                y = CreateAnnouncementPlainTextBlock(announcementContentRoot, UiTextCatalog.Get(GetAnnouncementBodyKey(announcementCurrentTab)), y, true);
                y = CreateAnnouncementGameClubButton(announcementContentRoot, y);
            }
            else
            {
                y = CreateAnnouncementPlainTextBlock(announcementContentRoot, HallAnnouncementCatalog.CreditsText, y, true);
                y = CreateAnnouncementCreditsNote(announcementContentRoot, y);
            }

            var viewportHeight = announcementViewportRoot != null ? announcementViewportRoot.rect.height : 0f;
            announcementContentRoot.sizeDelta = new Vector2(0f, Mathf.Max(viewportHeight, Mathf.Abs(y) + 12f));
            announcementContentRoot.anchoredPosition = Vector2.zero;
            if (announcementScrollRect != null)
            {
                announcementScrollRect.verticalNormalizedPosition = 1f;
            }

            Canvas.ForceUpdateCanvases();
        }

        private void UpdateAnnouncementContentFrameLayout()
        {
            if (announcementContentFrameRoot == null)
            {
                return;
            }

            var bottomInset = announcementCurrentTab == AnnouncementTab.RecentUpdates
                ? AnnouncementContentFooterBottomInset
                : AnnouncementContentBaseBottomInset;
            Stretch(
                announcementContentFrameRoot,
                Vector2.zero,
                Vector2.one,
                new Vector2(announcementSidebarWidth + (announcementCompactLayout ? 30f : 42f), bottomInset),
                new Vector2(announcementCompactLayout ? -24f : -34f, -58f));
        }

        private void ClearAnnouncementFixedFooter()
        {
            if (announcementFixedFooterRoot == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(announcementFixedFooterRoot.gameObject);
            announcementFixedFooterRoot = null;
        }

        private float CreateAnnouncementTitle(Transform parent, string titleText, float y)
        {
            var row = new GameObject("TitleRow", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            var rect = row.GetComponent<RectTransform>();
            Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, y - 56f), new Vector2(-10f, y));

            CreateAnnouncementInlineLeaf(row.transform, "TitleLeafLeft", new Vector2(-128f, 0f), 24f, -30f);
            CreateAnnouncementInlineLeaf(row.transform, "TitleLeafRight", new Vector2(128f, 0f), 24f, 30f, true);

            var title = CreatePopupText("Title", titleText, 32f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.transform.SetParent(row.transform, false);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 34f;
            title.color = new Color32(91, 65, 34, 255);
            Stretch(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(52f, 0f), new Vector2(-52f, 0f));
            return y - 76f;
        }

        private float CreateAnnouncementUpdateCards(Transform parent, float y)
        {
            var updates = HallAnnouncementCatalog.Updates;
            for (var i = 0; i < updates.Count; i++)
            {
                y = CreateAnnouncementVersionCard(parent, updates[i], i == 0, y);
            }

            return y;
        }

        private float CreateAnnouncementSectionHeader(Transform parent, string text, float y)
        {
            var label = CreatePopupText("SectionHeader", text, 24f, FontStyles.Bold, TextAlignmentOptions.Left);
            label.transform.SetParent(parent, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 25f;
            label.color = new Color32(91, 65, 34, 255);
            Stretch(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, y - 34f), new Vector2(-18f, y));
            return y - 44f;
        }

        private void CreateAnnouncementInlineLeaf(Transform parent, string name, Vector2 anchoredPosition, float size, float rotation, bool mirrorX = false)
        {
            var leaf = CreateImage(name, LoadSprite(AnnouncementLeafSpritePath), true);
            leaf.transform.SetParent(parent, false);
            leaf.color = new Color(0.54f, 0.68f, 0.24f, 0.9f);
            var rect = leaf.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = new Vector3(mirrorX ? -1f : 1f, 1f, 1f);
            leaf.raycastTarget = false;
        }

        private float CreateAnnouncementMiniGameGrid(Transform parent, float y)
        {
            var grid = new GameObject("MiniGameGrid", typeof(RectTransform));
            grid.transform.SetParent(parent, false);
            var height = 220f;
            Stretch(grid.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, y - height), new Vector2(-14f, y));

            CreateAnnouncementMiniGameCard(grid.transform, "StackMatchCard", 0, "hall.announcement.game.stack_match.title", "hall.announcement.game.stack_match.body");
            CreateAnnouncementMiniGameCard(grid.transform, "BlockPuzzleCard", 1, "hall.announcement.game.block_puzzle.title", "hall.announcement.game.block_puzzle.body");
            CreateAnnouncementMiniGameCard(grid.transform, "BullsCowsCard", 2, "hall.announcement.game.bulls_cows.title", "hall.announcement.game.bulls_cows.body");
            CreateAnnouncementMiniGameCard(grid.transform, "ArrowEscapeCard", 3, "hall.announcement.game.arrow_escape.title", "hall.announcement.game.arrow_escape.body");
            return y - height - 16f;
        }

        private void CreateAnnouncementMiniGameCard(Transform parent, string name, int row, string titleKey, string bodyKey)
        {
            var card = CreateRoundedRect(name, parent, new Color(1f, 1f, 1f, 0.36f), 16f, false, typeof(Shadow));
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(0f, -row * 54f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 46f);

            var shadow = card.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.58f, 0.48f, 0.30f, 0.14f);
            shadow.effectDistance = new Vector2(0f, -2f);

            var border = CreateRoundedRect("Border", card.transform, new Color(0.82f, 0.72f, 0.49f, 0.62f), 16f, false);
            Stretch(border.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var fill = CreateRoundedRect("Fill", card.transform, new Color32(255, 252, 242, 245), 14f, false);
            Stretch(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            var title = CreatePopupText("Title", UiTextCatalog.Get(titleKey), 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.transform.SetParent(card.transform, false);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 23f;
            title.color = new Color32(76, 59, 35, 255);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            title.rectTransform.sizeDelta = new Vector2(88f, 0f);

            var body = CreatePopupText("Body", UiTextCatalog.Get(bodyKey), 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            body.transform.SetParent(card.transform, false);
            body.enableWordWrapping = false;
            body.overflowMode = TextOverflowModes.Ellipsis;
            body.enableAutoSizing = true;
            body.fontSizeMin = 15f;
            body.fontSizeMax = 18f;
            body.color = new Color32(91, 75, 48, 255);
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(112f, 0f), new Vector2(-14f, 0f));
        }

        private float CreateAnnouncementVersionCard(Transform parent, HallAnnouncementCatalog.UpdateEntry entry, bool featured, float y)
        {
            var dateText = entry != null ? entry.date : string.Empty;
            var bodyText = entry != null ? entry.body : string.Empty;
            var card = CreateRoundedRect(
                "VersionCard_" + BuildAnnouncementUpdateNodeName(dateText),
                parent,
                new Color(1f, 1f, 1f, 0.36f),
                16f,
                false,
                typeof(Shadow));
            var rect = card.GetComponent<RectTransform>();

            var shadow = card.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.58f, 0.48f, 0.30f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -2f);

            var border = CreateRoundedRect("Border", card.transform, new Color(0.82f, 0.72f, 0.49f, 0.68f), 16f, false);
            Stretch(border.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var fill = CreateRoundedRect("Fill", card.transform, new Color32(255, 252, 242, 245), 14f, false);
            Stretch(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            CreateAnnouncementBadge(card.transform, "VersionBadge", dateText, new Vector2(26f, -24f), false);

            var body = CreatePopupText("Body", bodyText, featured ? 21f : 20f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            body.transform.SetParent(card.transform, false);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Ellipsis;
            body.enableAutoSizing = true;
            body.fontSizeMin = 17f;
            body.fontSizeMax = featured ? 21f : 20f;
            body.lineSpacing = 10f;
            body.color = new Color32(67, 52, 32, 255);

            var parentRect = parent as RectTransform;
            var availableWidth = parentRect != null ? parentRect.rect.width - 88f : 0f;
            if (availableWidth <= 1f)
            {
                availableWidth = parentRect != null ? parentRect.sizeDelta.x - 88f : 0f;
            }

            var preferredHeight = Mathf.Ceil(body.GetPreferredValues(bodyText, Mathf.Max(120f, availableWidth), 0f).y);
            var height = Mathf.Max(featured ? 136f : 118f, preferredHeight + 78f);
            Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, y - height), new Vector2(-14f, y));
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 16f), new Vector2(-24f, -60f));
            return y - height - 18f;
        }

        private static string BuildAnnouncementUpdateNodeName(string dateText)
        {
            if (string.IsNullOrWhiteSpace(dateText))
            {
                return "Unknown";
            }

            var builder = new System.Text.StringBuilder();
            for (var i = 0; i < dateText.Length; i++)
            {
                var character = dateText[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.Length > 0 ? builder.ToString() : "Unknown";
        }

        private void CreateAnnouncementBadge(Transform parent, string name, string text, Vector2 anchoredPosition, bool right)
        {
            var badge = CreateRoundedRect(name, parent, new Color32(196, 219, 118, 255), 16f, false);
            var badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            badgeRect.anchorMax = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            badgeRect.pivot = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            badgeRect.anchoredPosition = anchoredPosition;
            badgeRect.sizeDelta = new Vector2(128f, 34f);

            var label = CreateButtonLabel("Label", text, 20f);
            label.transform.SetParent(badge.transform, false);
            label.color = right ? Color.white : new Color32(58, 62, 28, 255);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 21f;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 1f));
        }

        private float CreateAnnouncementOptimizationCard(Transform parent, float y)
        {
            var height = 154f;
            var card = CreateRoundedRect(
                "OptimizationCard",
                parent,
                new Color32(255, 252, 242, 246),
                16f,
                false,
                typeof(Shadow));
            var rect = card.GetComponent<RectTransform>();
            Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, y - height), new Vector2(-14f, y));

            var shadow = card.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.58f, 0.48f, 0.30f, 0.14f);
            shadow.effectDistance = new Vector2(0f, -2f);

            var border = CreateRoundedRect("Border", card.transform, new Color(0.82f, 0.72f, 0.49f, 0.58f), 16f, false);
            Stretch(border.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var body = CreatePopupText("Body", UiTextCatalog.Get("hall.announcement.optimization.body"), 20f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            body.transform.SetParent(card.transform, false);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Ellipsis;
            body.enableAutoSizing = true;
            body.fontSizeMin = 17f;
            body.fontSizeMax = 20f;
            body.lineSpacing = 10f;
            body.color = new Color32(67, 52, 32, 255);
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 16f), new Vector2(-20f, -16f));
            return y - height - 16f;
        }

        private void CreateAnnouncementFixedFooter()
        {
            if (announcementDialogRoot == null)
            {
                return;
            }

            var footer = new GameObject("Footer", typeof(RectTransform), typeof(LayoutElement));
            footer.transform.SetParent(announcementDialogRoot, false);
            announcementFixedFooterRoot = footer.GetComponent<RectTransform>();
            Stretch(
                announcementFixedFooterRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(announcementSidebarWidth + (announcementCompactLayout ? 30f : 42f), 34f + AnnouncementFooterYOffset),
                new Vector2(announcementCompactLayout ? -24f : -34f, 84f + AnnouncementFooterYOffset));

            var label = CreatePopupText(
                "Label",
                UiTextCatalog.Get("hall.announcement.footer"),
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            label.transform.SetParent(footer.transform, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 22f;
            label.color = new Color32(113, 93, 56, 255);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));

            CreateAnnouncementFooterLeaf(footer.transform, "FooterLeafLeft", false);
            CreateAnnouncementFooterLeaf(footer.transform, "FooterLeafRight", true);
        }

        private void CreateAnnouncementFooterLeaf(Transform parent, string name, bool right)
        {
            var leaf = CreateImage(name, LoadSprite(AnnouncementLeafSpritePath), true);
            leaf.transform.SetParent(parent, false);
            leaf.color = new Color(0.54f, 0.68f, 0.24f, 0.9f);
            leaf.raycastTarget = false;

            var rect = leaf.rectTransform;
            rect.anchorMin = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchorMax = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(22f, 22f);
            rect.localRotation = Quaternion.Euler(0f, 0f, right ? 28f : -28f);
            rect.localScale = new Vector3(right ? -1f : 1f, 1f, 1f);
        }

        private float CreateAnnouncementCreditsNote(Transform parent, float y)
        {
            var footer = new GameObject("CreditsNote", typeof(RectTransform), typeof(LayoutElement));
            footer.transform.SetParent(parent, false);
            Stretch(footer.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, y - 48f), new Vector2(-8f, y));

            var label = CreatePopupText(
                "Label",
                UiTextCatalog.Get("hall.announcement.credits.note"),
                19f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            label.transform.SetParent(footer.transform, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = 15f;
            label.fontSizeMax = 19f;
            label.color = new Color32(113, 93, 56, 230);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            return y - 56f;
        }

        private float CreateAnnouncementPlainTextBlock(Transform parent, string message, float y, bool reserveActionSpace = false)
        {
            var body = CreatePopupText(
                "PlainTextBlock",
                message,
                23f,
                FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            body.transform.SetParent(parent, false);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Ellipsis;
            body.enableAutoSizing = false;
            body.lineSpacing = 12f;
            body.color = new Color32(92, 74, 46, 255);

            var parentRect = parent as RectTransform;
            var availableWidth = parentRect != null ? parentRect.rect.width - 36f : 0f;
            if (availableWidth <= 1f)
            {
                availableWidth = parentRect != null ? parentRect.sizeDelta.x - 36f : 0f;
            }

            var viewportHeight = announcementViewportRoot != null ? announcementViewportRoot.rect.height : 0f;
            var bottomPadding = reserveActionSpace ? 104f : 28f;
            var minHeight = viewportHeight > 1f ? Mathf.Max(220f, viewportHeight + y - bottomPadding) : 220f;
            var height = Mathf.Max(minHeight, Mathf.Ceil(body.GetPreferredValues(body.text, Mathf.Max(120f, availableWidth), 0f).y) + 12f);
            Stretch(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, y - height), new Vector2(-18f, y));
            return y - height - 18f;
        }

        private float CreateAnnouncementGameClubButton(Transform parent, float y)
        {
            var buttonObject = CreateRoundedRect(
                "GameClubButton",
                parent,
                new Color32(244, 255, 144, 255),
                22f,
                true,
                typeof(Button),
                typeof(Shadow));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(188f, 58f);

            var shadow = buttonObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.31f, 0.27f, 0.17f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var background = buttonObject.GetComponent<RoundedRectGraphic>();
            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
            button.onClick.AddListener(ShowWechatGameClub);
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.68f);
            EnsureMenuButtonPressEffect(button.transform);

            var label = CreateButtonLabel("Label", UiTextCatalog.Get("hall.announcement.action.game_club"), 22f);
            label.transform.SetParent(buttonObject.transform, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 23f;
            label.color = new Color32(63, 72, 27, 255);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 1f));
            return y - 72f;
        }

        private GameObject CreateRoundedRect(string name, Transform parent, Color color, float radius, bool raycastTarget, params Type[] extraComponents)
        {
            var components = new List<Type>
            {
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic)
            };

            if (extraComponents != null)
            {
                for (var i = 0; i < extraComponents.Length; i++)
                {
                    components.Add(extraComponents[i]);
                }
            }

            var gameObject = new GameObject(name, components.ToArray());
            gameObject.transform.SetParent(parent, false);
            var graphic = gameObject.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return gameObject;
        }

        private static string GetAnnouncementTabKey(AnnouncementTab tab)
        {
            switch (tab)
            {
                case AnnouncementTab.RecentUpdates:
                    return "hall.announcement.tab.updates";
                case AnnouncementTab.AboutGame:
                    return "hall.announcement.tab.about_game";
                case AnnouncementTab.Credits:
                    return "hall.announcement.tab.credits";
                default:
                    return "hall.announcement.tab.updates";
            }
        }

        private static string GetAnnouncementTitleKey(AnnouncementTab tab)
        {
            switch (tab)
            {
                case AnnouncementTab.RecentUpdates:
                    return "hall.announcement.title.updates";
                case AnnouncementTab.AboutGame:
                    return "hall.announcement.title.about_game";
                case AnnouncementTab.Credits:
                    return "hall.announcement.title.credits";
                default:
                    return "hall.announcement.title.updates";
            }
        }

        private static string GetAnnouncementBodyKey(AnnouncementTab tab)
        {
            switch (tab)
            {
                case AnnouncementTab.AboutGame:
                    return "hall.announcement.about_game.body";
                default:
                    return "hall.announcement.about_game.body";
            }
        }
    }
}
