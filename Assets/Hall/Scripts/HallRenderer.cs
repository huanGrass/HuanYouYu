using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 大厅界面渲染器：负责绑定大厅预制体、切页签、以及动态填充卡片数据。
    /// </summary>
    internal sealed partial class HallRenderer
    {
        private const string PrefabResourcePath = "HallView";
        private const string TabSelectedSpritePath = "HallTheme/hall_tab_selected";
        private const string TabUnselectedSpritePath = "HallTheme/hall_tab_unselected";
        private const string FavoriteStarSpritePath = "GameIcons/star";
        private const string ChestIconSpritePath = "GameIcons/chest";
        private const string CoinIconSpritePath = "GameIcons/coin";
        private const string MoreGamesInProgressTextureResourcePath = "HallCardIcons/more_games_in_progress";
        private const string MoreGamesInProgressCardId = "more-games-in-progress";
        private const string ChestToastTextKey = "hall.chest.toast";
        private const float ChestToastDuration = 1.6f;
        private const float ChestToastHorizontalPadding = 18f;
        private const float ChestToastVerticalPadding = 10f;
        private const float ChestToastMinWidth = 244f;
        private const float ChestToastMaxWidth = 360f;
        private const float ScrollSensitivity = 24f;
        private const float ResponsiveHorizontalMargin = 36f;
        private const float MinCardFitScale = 0.6f;
        private const float AllGamesMinCellWidth = 220f;
        private const int AllGamesPreferredMinColumnCount = 3;
        private const int AllGamesMaxColumnCount = 5;
        private const float HeaderTitlePulseBaseScale = 1f;
        private const float HeaderTitlePulsePeakScale = 1.03f;
        private const float HeaderTitlePulseCycleSeconds = 3f;
        private static readonly string[] AllGamesTagTextKeys =
        {
            "hall.tag.all",
            "hall.tag.eliminate",
            "hall.tag.puzzle",
            "hall.tag.number",
            "hall.tag.action",
            "hall.tag.simulation",
            "hall.tag.merge"
        };
        private static readonly string[] AllGamesTagCategories =
        {
            string.Empty,
            "eliminate",
            "puzzle",
            "number",
            "action",
            "simulation",
            "merge"
        };
        private const string StartButtonHighlightRootName = "StartButtonHighlight";
        private const string StartButtonBreathGlowName = "BreathGlow";
        private const string StartButtonSweepShineName = "SweepShine";
        private const string HeaderStatSweepRootName = "IconSweepRoot";
        private const string HeaderStatSweepShineName = "SweepShine";
        private static readonly Color FavoriteStarActiveColor = new Color(1f, 0.83f, 0.24f, 1f);
        private static readonly Color FavoriteStarInactiveColor = new Color(0.62f, 0.54f, 0.39f, 0.48f);
        private static readonly Color ChestCountOutlineColor = new Color(0.29f, 0.42f, 0.18f, 1f);

        /// <summary>
        /// 大厅页签枚举。
        /// </summary>
        private enum HallTab
        {
            Favorites,
            AllGames,
            Profile
        }

        /// <summary>
        /// 底部导航按钮的运行时绑定数据。
        /// </summary>
        private sealed class NavButtonBinding
        {
            public HallTab Tab;
            public Image BackgroundImage;
        }

        private sealed class HeaderTagBinding
        {
            public int Index;
            public RoundedRectGraphic Graphic;
            public TextMeshProUGUI Label;
            public LayoutElement LayoutElement;
            public RectTransform RectTransform;
        }

        /// <summary>
        /// 成长页显示用的经验快照。
        /// </summary>
        private sealed class GrowthSnapshot
        {
            public int Level;
            public int CurrentExp;
            public int RequiredExp;
            public int TotalExp;
        }

        private readonly Action<string> enterGame;
        private readonly Action<string> toggleFavorite;
        private GameObject root;
        private RectTransform favoritesContentRoot;
        private RectTransform allGamesContentRoot;
        private RectTransform profileContentRoot;
        private RectTransform headerStatsRoot;
        private RectTransform headerTagBarRoot;
        private RectTransform headerTitleBarRoot;
        private RectTransform bottomNavRoot;
        private ScrollRect scrollRect;
        private TextMeshProUGUI headerChestCountText;
        private TextMeshProUGUI headerCoinCountText;
        private RectTransform overlayRoot;
        private GameObject allGamesCardTemplate;
        private GameObject profileCardTemplate;
        private Sprite tabSelectedSprite;
        private Sprite tabUnselectedSprite;
        private Sprite favoriteStarSprite;
        private Sprite chestIconSprite;
        private Sprite coinIconSprite;
        private Material chestCountOutlineMaterial;
        private Material chestCountOutlineBaseMaterial;
        private Texture2D moreGamesInProgressTexture;
        private Canvas rootCanvas;
        private readonly List<NavButtonBinding> navButtons = new List<NavButtonBinding>();
        private readonly List<HeaderTagBinding> headerTagButtons = new List<HeaderTagBinding>();
        private readonly List<MiniGameCardViewModel> cachedCards = new List<MiniGameCardViewModel>();
        private HallTab currentTab = HallTab.Favorites;
        private int selectedHeaderTagIndex;
        private ToastRunner toastRunner;

        /// <summary>
        /// 初始化大厅渲染器并立即构建默认页签。
        /// </summary>
        public HallRenderer(
            Transform parent,
            Action<string> enterGameAction,
            Action<string> toggleFavoriteAction)
        {
            enterGame = enterGameAction;
            toggleFavorite = toggleFavoriteAction;

            if (!TryBuildFromPrefab(parent))
            {
                throw new InvalidOperationException("HallView prefab not found or invalid at Resources/" + PrefabResourcePath);
            }

            Canvas.ForceUpdateCanvases();
            ApplyResponsiveLayout();
            SetCurrentTab(HallTab.Favorites, true);
        }

        /// <summary>
        /// 从大厅主预制体和卡片模板预制体完成初始化绑定。
        /// </summary>
        private bool TryBuildFromPrefab(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                return false;
            }

            root = UnityEngine.Object.Instantiate(prefab, parent, false);
            root.name = "HallView";
            rootCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            var rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            var shell = root.transform.Find("Shell");
            favoritesContentRoot = root.transform.Find("Shell/ScrollFrame/FavoritesContent") as RectTransform;
            allGamesContentRoot = root.transform.Find("Shell/ScrollFrame/AllGamesContent") as RectTransform;
            profileContentRoot = root.transform.Find("Shell/ScrollFrame/ProfileContent") as RectTransform;
            headerStatsRoot = shell != null ? shell.Find("HeaderStats") as RectTransform : null;
            headerTagBarRoot = shell != null ? shell.Find("HeaderTagBar") as RectTransform : null;
            headerTitleBarRoot = shell != null ? shell.Find("HeaderTitleBar") as RectTransform : null;
            bottomNavRoot = root.transform.Find("Shell/BottomNavButtons") as RectTransform;
            var scrollFrame = root.transform.Find("Shell/ScrollFrame") as RectTransform;
            scrollRect = scrollFrame != null ? scrollFrame.GetComponent<ScrollRect>() : null;
            if (scrollRect != null && scrollRect.viewport == null)
            {
                scrollRect.viewport = scrollFrame;
            }

            if (favoritesContentRoot == null || allGamesContentRoot == null || profileContentRoot == null || bottomNavRoot == null || scrollRect == null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            navButtons.Clear();
            if (!BindNavButton(HallTab.Favorites, "FavoritesTab") || !BindNavButton(HallTab.AllGames, "AllGamesTab") || !BindNavButton(HallTab.Profile, "ProfileTab"))
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = ScrollSensitivity;

            var runtimeTemplates = root.transform.Find("Shell/RuntimeTemplates");
            allGamesCardTemplate = runtimeTemplates != null ? runtimeTemplates.Find("CardTemplate")?.gameObject : null;
            profileCardTemplate = runtimeTemplates != null ? runtimeTemplates.Find("ProfileTemplate")?.gameObject : null;
            tabSelectedSprite = Resources.Load<Sprite>(TabSelectedSpritePath);
            tabUnselectedSprite = Resources.Load<Sprite>(TabUnselectedSpritePath);
            favoriteStarSprite = Resources.Load<Sprite>(FavoriteStarSpritePath);
            chestIconSprite = Resources.Load<Sprite>(ChestIconSpritePath);
            coinIconSprite = Resources.Load<Sprite>(CoinIconSpritePath);
            moreGamesInProgressTexture = Resources.Load<Texture2D>(MoreGamesInProgressTextureResourcePath);
            if (allGamesCardTemplate == null || profileCardTemplate == null || tabSelectedSprite == null || tabUnselectedSprite == null || favoriteStarSprite == null || chestIconSprite == null || coinIconSprite == null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
                return false;
            }

            overlayRoot = EnsureOverlayRoot();
            toastRunner = root.AddComponent<ToastRunner>();
            EnsureHeaderStats(shell, scrollFrame);
            EnsureHeaderTagBar(shell, scrollFrame);
            EnsureHeaderTitlePulse();
            EnsureHeaderMenu(shell);

            return true;
        }

        /// <summary>
        /// 绑定底部导航按钮的交互和视觉引用。
        /// </summary>
        private void EnsureHeaderStats(Transform shell, RectTransform scrollFrame)
        {
            if (shell == null)
            {
                headerStatsRoot = null;
                headerChestCountText = null;
                headerCoinCountText = null;
                return;
            }

            if (headerStatsRoot == null)
            {
                headerStatsRoot = CreateHeaderStats(shell);
            }

            if (headerStatsRoot != null && scrollFrame != null)
            {
                headerStatsRoot.SetSiblingIndex(scrollFrame.GetSiblingIndex());
            }

            headerChestCountText = headerStatsRoot != null ? headerStatsRoot.Find("ChestStat/CountText")?.GetComponent<TextMeshProUGUI>() : null;
            headerCoinCountText = headerStatsRoot != null ? headerStatsRoot.Find("CoinStat/CountText")?.GetComponent<TextMeshProUGUI>() : null;
            UpdateHeaderStats();
            EnsureHeaderStatIconEffects();
        }

        private void EnsureHeaderTagBar(Transform shell, RectTransform scrollFrame)
        {
            if (shell == null)
            {
                headerTagBarRoot = null;
                return;
            }

            if (headerTagBarRoot == null)
            {
                headerTagBarRoot = CreateHeaderTagBar(shell);
            }

            if (headerTagBarRoot != null && scrollFrame != null)
            {
                headerTagBarRoot.SetSiblingIndex(scrollFrame.GetSiblingIndex());
            }

            BindHeaderTagButtons();
            UpdateHeaderTagBarVisibility();
            UpdateHeaderTagSelection();
        }

        private bool BindNavButton(HallTab tab, string buttonName)
        {
            var buttonTransform = root.transform.Find("Shell/BottomNavButtons/" + buttonName);
            var button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            var graphic = buttonTransform != null ? buttonTransform.GetComponent<RoundedRectGraphic>() : null;
            var backgroundImage = buttonTransform != null ? buttonTransform.Find("Background")?.GetComponent<Image>() : null;
            var label = buttonTransform != null ? buttonTransform.Find("Content/Label")?.GetComponent<TextMeshProUGUI>() : null;
            var iconRoot = buttonTransform != null ? buttonTransform.Find("Content/IconRoot") : null;
            if (button == null || graphic == null || backgroundImage == null || label == null || iconRoot == null)
            {
                return false;
            }

            label.text = GetTabTitle(tab);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { SetCurrentTab(tab, false); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.68f);

            navButtons.Add(new NavButtonBinding
            {
                Tab = tab,
                BackgroundImage = backgroundImage
            });
            return true;
        }

        /// <summary>
        /// 当前大厅是否可见。
        /// </summary>
        public bool IsVisible
        {
            get { return root.activeSelf; }
        }

        /// <summary>
        /// 当前是否停留在“全部游戏”页签。
        /// </summary>
        public bool IsAllGamesTabActive
        {
            get { return currentTab == HallTab.AllGames; }
        }

        /// <summary>
        /// 显示大厅并刷新布局。
        /// </summary>
        public void Show()
        {
            root.SetActive(true);
        }

        /// <summary>
        /// 隐藏大厅。
        /// </summary>
        public void Hide()
        {
            CloseHeaderMenu();
            CloseActiveModal();
            root.SetActive(false);
        }

        /// <summary>
        /// 刷新大厅数据源，并根据当前页签重建内容。
        /// </summary>
        public void Refresh(IList<MiniGameCardViewModel> cards)
        {
            cachedCards.Clear();
            for (var i = 0; i < cards.Count; i++)
            {
                cachedCards.Add(cards[i]);
            }

            RebuildCurrentTab();
        }

        /// <summary>
        /// 仅更新当前可见页签里指定游戏卡片的收藏表现，避免整页重建。
        /// </summary>
        public void RefreshFavoriteBadge(string gameId, bool isFavorite)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            var cardRoot = FindCardRoot(gameId);
            if (cardRoot == null)
            {
                return;
            }

            UpdateFavoriteBadge(cardRoot, isFavorite, gameId);
        }

        /// <summary>
        /// 同步缓存中的收藏状态，并在当前可见页签里更新对应卡片。
        /// </summary>
        public void RefreshFavoriteState(string gameId, bool isFavorite, int favoriteOrder)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            for (var i = 0; i < cachedCards.Count; i++)
            {
                var card = cachedCards[i];
                if (card == null || card.Definition == null || card.Definition.Id != gameId)
                {
                    continue;
                }

                card.IsFavorite = isFavorite;
                card.FavoriteOrder = favoriteOrder;
                break;
            }

            RefreshFavoriteBadge(gameId, isFavorite);
        }

        /// <summary>
        /// 当前页签对应的内容容器。
        /// </summary>
        private RectTransform CurrentContentRoot
        {
            get
            {
                switch (currentTab)
                {
                    case HallTab.AllGames:
                        return allGamesContentRoot;
                    case HallTab.Profile:
                        return profileContentRoot;
                    default:
                        return favoritesContentRoot;
                }
            }
        }

        /// <summary>
        /// 重建当前页签的所有卡片内容。
        /// </summary>
        private void RebuildCurrentTab()
        {
            ClearChildren(favoritesContentRoot);
            ClearChildren(allGamesContentRoot);
            ClearChildren(profileContentRoot);

            if (currentTab == HallTab.Profile)
            {
                CreateProfilePlaceholder();
            }
            else if (currentTab == HallTab.Favorites)
            {
                var favoriteCards = new List<MiniGameCardViewModel>();
                for (var i = 0; i < cachedCards.Count; i++)
                {
                    if (cachedCards[i].Definition != null && cachedCards[i].IsFavorite)
                    {
                        favoriteCards.Add(cachedCards[i]);
                    }
                }

                favoriteCards.Sort(CompareFavoriteCards);
                for (var i = 0; i < favoriteCards.Count; i++)
                {
                    CreateAllGamesCard(favoriteCards[i], favoritesContentRoot);
                }
            }
            else
            {
                for (var i = 0; i < cachedCards.Count; i++)
                {
                    if (ShouldShowAllGamesCard(cachedCards[i]))
                    {
                        CreateAllGamesCard(cachedCards[i], allGamesContentRoot);
                    }
                }

                CreateMoreGamesInProgressCard(allGamesContentRoot);
            }

            ApplyResponsiveLayout();
            scrollRect.content = CurrentContentRoot;
            UpdateNavSelection();
            UpdateHeaderStats();

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        private bool ShouldShowAllGamesCard(MiniGameCardViewModel card)
        {
            if (card == null || card.Definition == null || !card.Definition.IsPlayable)
            {
                return false;
            }

            if (selectedHeaderTagIndex <= 0 || selectedHeaderTagIndex >= AllGamesTagCategories.Length)
            {
                return true;
            }

            return string.Equals(card.Definition.Category, AllGamesTagCategories[selectedHeaderTagIndex], StringComparison.Ordinal);
        }

        private static int CompareFavoriteCards(MiniGameCardViewModel left, MiniGameCardViewModel right)
        {
            var leftOrder = left != null ? left.FavoriteOrder : int.MaxValue;
            var rightOrder = right != null ? right.FavoriteOrder : int.MaxValue;
            var orderCompare = leftOrder.CompareTo(rightOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            var leftId = left != null && left.Definition != null ? left.Definition.Id : string.Empty;
            var rightId = right != null && right.Definition != null ? right.Definition.Id : string.Empty;
            return string.Compare(leftId, rightId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 创建“全部游戏”页单张卡片，并根据可玩状态设置交互。
        /// </summary>
        private void CreateAllGamesCard(MiniGameCardViewModel card, RectTransform targetContentRoot)
        {
            if (card == null || card.Definition == null || targetContentRoot == null)
            {
                return;
            }

            var cardSlot = CreateCardSlot(card.Definition.Id, targetContentRoot);
            var cardRoot = UnityEngine.Object.Instantiate(allGamesCardTemplate, cardSlot, false);
            cardRoot.name = card.Definition.Id + "_Card";
            ApplyCardScale(cardRoot, targetContentRoot);

            var cardButton = cardRoot.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
                cardButton.interactable = false;
                cardButton.enabled = false;
            }

            var actionButton = cardRoot.transform.Find("Action")?.GetComponent<Button>();
            if (actionButton == null)
            {
                return;
            }

            actionButton.interactable = card.Definition.IsPlayable;
            actionButton.onClick.RemoveAllListeners();

            if (card.Definition.IsPlayable)
            {
                var gameId = card.Definition.Id;
                actionButton.onClick.AddListener(delegate { enterGame(gameId); });
                MiniGameSfxPlayer.Attach(actionButton, MiniGameSfxType.UiTap, 0.74f);
            }

            EnsureStartButtonHighlight(actionButton, card.Definition.IsPlayable);

            var icon = cardRoot.transform.Find("Icon");
            if (icon != null)
            {
                ClearChildren(icon as RectTransform);
                UpdateCardIcon(icon, card.Definition);
            }

            var title = cardRoot.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
            {
                title.text = card.Definition.Name;
                title.overflowMode = TextOverflowModes.Ellipsis;
            }

            UpdateFavoriteBadge(cardRoot.transform, card.IsFavorite, card.Definition.Id);
            UpdateChestBadge(cardRoot.transform, card.Progress);

            var actionText = cardRoot.transform.Find("Action/ActionText")?.GetComponent<TextMeshProUGUI>();
            if (actionText != null)
            {
                actionText.text = card.Definition.IsPlayable
                    ? "\u5f00\u59cb"
                    : UiTextCatalog.Get("hall.action.developing");
            }

            var costText = cardRoot.transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            if (costText != null)
            {
                costText.gameObject.SetActive(false);
            }
        }

        private void CreateMoreGamesInProgressCard(RectTransform targetContentRoot)
        {
            if (targetContentRoot == null || allGamesCardTemplate == null || moreGamesInProgressTexture == null)
            {
                return;
            }

            var cardSlot = CreateCardSlot(MoreGamesInProgressCardId, targetContentRoot);
            var cardRoot = UnityEngine.Object.Instantiate(allGamesCardTemplate, cardSlot, false);
            cardRoot.name = MoreGamesInProgressCardId + "_Card";
            ApplyCardScale(cardRoot, targetContentRoot);

            var cardButton = cardRoot.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
                cardButton.interactable = false;
                cardButton.enabled = false;
            }

            RemoveNode(cardRoot.transform.Find("Title"));
            RemoveNode(cardRoot.transform.Find("Action"));
            RemoveNode(cardRoot.transform.Find("FavoriteBadge"));
            RemoveNode(cardRoot.transform.Find("ChestBadge"));
            RemoveNode(cardRoot.transform.Find("CostText"));
            RemoveNode(cardRoot.transform.Find("Background"));

            var icon = cardRoot.transform.Find("Icon") as RectTransform;
            if (icon != null)
            {
                ClearChildren(icon);
                Stretch(icon, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
                UpdatePromptCardIcon(icon, moreGamesInProgressTexture);
            }
        }

        private static void EnsureStartButtonHighlight(Button actionButton, bool enableEffect)
        {
            if (actionButton == null)
            {
                return;
            }

            var effect = actionButton.GetComponent<StartButtonHighlightEffect>();
            if (!enableEffect)
            {
                if (effect != null)
                {
                    UnityEngine.Object.Destroy(effect);
                }

                var highlightRoot = actionButton.transform.Find(StartButtonHighlightRootName);
                if (highlightRoot != null)
                {
                    UnityEngine.Object.Destroy(highlightRoot.gameObject);
                }

                return;
            }

            if (effect == null)
            {
                effect = actionButton.gameObject.AddComponent<StartButtonHighlightEffect>();
            }

            effect.Configure();
        }

        private static RectTransform CreateCardSlot(string gameId, RectTransform parent)
        {
            var slotObject = new GameObject(
                string.IsNullOrWhiteSpace(gameId) ? "CardSlot" : gameId + "_CardSlot",
                typeof(RectTransform));
            slotObject.transform.SetParent(parent, false);

            var slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = Vector2.zero;
            slotRect.localScale = Vector3.one;
            return slotRect;
        }

        private void ApplyCardScale(GameObject cardRoot, RectTransform targetContentRoot)
        {
            if (cardRoot == null || targetContentRoot == null)
            {
                return;
            }

            var grid = targetContentRoot.GetComponent<GridLayoutGroup>();
            var cardRect = cardRoot.GetComponent<RectTransform>();
            if (grid == null || cardRect == null)
            {
                cardRoot.transform.localScale = Vector3.one;
                return;
            }

            var cardWidth = Mathf.Max(1f, cardRect.sizeDelta.x);
            var cardHeight = Mathf.Max(1f, cardRect.sizeDelta.y);
            var fitScale = Mathf.Min(grid.cellSize.x / cardWidth, grid.cellSize.y / cardHeight, 1f);
            fitScale = Mathf.Clamp(fitScale, MinCardFitScale, 1f);
            cardRoot.transform.localScale = new Vector3(fitScale, fitScale, 1f);
        }

        /// <summary>
        /// 创建“成长”页内容卡片。
        /// </summary>
        private void CreateProfilePlaceholder()
        {
            var growth = BuildGrowthSnapshot();
            var card = UnityEngine.Object.Instantiate(profileCardTemplate, profileContentRoot, false);
            card.name = "GrowthCard";

            var levelText = card.transform.Find("HeaderRow/LevelText")?.GetComponent<TextMeshProUGUI>();
            if (levelText != null)
            {
                levelText.text = string.Format("Lv.{0}", growth.Level);
            }

            var expText = card.transform.Find("HeaderRow/ExpText")?.GetComponent<TextMeshProUGUI>();
            if (expText != null)
            {
                expText.text = string.Format("{0}/{1} EXP", growth.CurrentExp, growth.RequiredExp);
            }

            var progressFill = card.transform.Find("ProgressRoot/ProgressFill") as RectTransform;
            if (progressFill != null)
            {
                var progressRatio = growth.RequiredExp > 0 ? (float)growth.CurrentExp / growth.RequiredExp : 0f;
                progressFill.anchorMin = new Vector2(0f, 0f);
                progressFill.anchorMax = new Vector2(Mathf.Clamp01(progressRatio), 1f);
            }

            var summaryText = card.transform.Find("SummaryText")?.GetComponent<TextMeshProUGUI>();
            if (summaryText != null)
            {
                summaryText.text = UiTextCatalog.Format(
                    "hall.profile.summary",
                    growth.TotalExp,
                    Mathf.Max(0, growth.RequiredExp - growth.CurrentExp));
            }

            var hintText = card.transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();
            if (hintText != null)
            {
                hintText.text = UiTextCatalog.Get("hall.profile.hint");
            }
        }

        /// <summary>
        /// 基于卡片进度计算成长页展示数据。
        /// </summary>
        private GrowthSnapshot BuildGrowthSnapshot()
        {
            var totalExp = 0;
            for (var i = 0; i < cachedCards.Count; i++)
            {
                var progress = cachedCards[i].Progress;
                if (progress == null)
                {
                    continue;
                }

                totalExp += Mathf.Max(0, progress.TotalChestCount) * 35;
                totalExp += Mathf.Max(0, progress.TotalCoinCount) / 50;
            }

            var level = 1;
            var expPool = Mathf.Max(0, totalExp);
            var required = GetLevelUpRequiredExp(level);
            while (expPool >= required && level < 99)
            {
                expPool -= required;
                level += 1;
                required = GetLevelUpRequiredExp(level);
            }

            return new GrowthSnapshot
            {
                Level = level,
                CurrentExp = expPool,
                RequiredExp = required,
                TotalExp = totalExp
            };
        }

        /// <summary>
        /// 计算指定等级升级所需经验。
        /// </summary>
        private void UpdateHeaderStats()
        {
            if (headerStatsRoot == null)
            {
                return;
            }

            if (headerChestCountText != null)
            {
                headerChestCountText.text = GetTotalChestCount().ToString();
            }

            if (headerCoinCountText != null)
            {
                headerCoinCountText.text = GetTotalCoinCount().ToString();
            }
        }

        private int GetTotalChestCount()
        {
            var totalChestCount = 0;
            for (var i = 0; i < cachedCards.Count; i++)
            {
                var progress = cachedCards[i] != null ? cachedCards[i].Progress : null;
                if (progress == null)
                {
                    continue;
                }

                totalChestCount += Mathf.Max(0, progress.TotalChestCount);
            }

            return totalChestCount;
        }

        private int GetTotalCoinCount()
        {
            var totalCoinCount = 0;
            for (var i = 0; i < cachedCards.Count; i++)
            {
                var progress = cachedCards[i] != null ? cachedCards[i].Progress : null;
                if (progress == null)
                {
                    continue;
                }

                totalCoinCount += Mathf.Max(0, progress.TotalCoinCount);
            }

            return totalCoinCount;
        }

        private static int GetLevelUpRequiredExp(int level)
        {
            return 100 + Mathf.Max(0, level - 1) * 60;
        }

        /// <summary>
        /// 在卡片图标区域创建并填充图标图片。
        /// </summary>
        private void UpdateCardIcon(Transform parent, MiniGameDefinition definition)
        {
            var texture = MiniGameHallIconCatalog.GetTexture(definition != null ? definition.Id : string.Empty);

            var imageObject = new GameObject("IconImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(parent, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            Stretch(imageRect, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);

            var image = imageObject.GetComponent<RawImage>();
            image.texture = texture;
            image.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0.10f);

            var fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 1f;
        }

        private static void UpdatePromptCardIcon(RectTransform parent, Texture2D texture)
        {
            if (parent == null)
            {
                return;
            }

            var imageObject = new GameObject("IconImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(parent, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            Stretch(imageRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var image = imageObject.GetComponent<RawImage>();
            image.texture = texture;
            image.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
            image.raycastTarget = false;

            var fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 1f;
        }

        /// <summary>
        /// 按键名加载游戏图标纹理（带缓存）。
        /// </summary>
        /// <summary>
        /// 切换页签并触发重建。
        /// </summary>
        private void SetCurrentTab(HallTab tab, bool forceRebuild)
        {
            if (!forceRebuild && currentTab == tab)
            {
                HideHeaderMenuPanel();
                return;
            }

            currentTab = tab;
            favoritesContentRoot.gameObject.SetActive(tab == HallTab.Favorites);
            allGamesContentRoot.gameObject.SetActive(tab == HallTab.AllGames);
            profileContentRoot.gameObject.SetActive(tab == HallTab.Profile);
            UpdateHeaderTagBarVisibility();
            HideHeaderMenuPanel();
            RebuildCurrentTab();
        }

        private void UpdateHeaderTagBarVisibility()
        {
            if (headerStatsRoot != null)
            {
                headerStatsRoot.gameObject.SetActive(currentTab != HallTab.AllGames);
            }

            if (headerTagBarRoot != null)
            {
                headerTagBarRoot.gameObject.SetActive(currentTab == HallTab.AllGames);
            }
        }

        private void SelectHeaderTag(int index)
        {
            var clampedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, AllGamesTagTextKeys.Length - 1));
            if (selectedHeaderTagIndex == clampedIndex)
            {
                HideHeaderMenuPanel();
                return;
            }

            selectedHeaderTagIndex = clampedIndex;
            HideHeaderMenuPanel();
            UpdateHeaderTagSelection();
            if (currentTab == HallTab.AllGames)
            {
                RebuildCurrentTab();
            }
        }

        private void ApplyResponsiveLayout()
        {
            Canvas.ForceUpdateCanvases();
            AdjustCardGridLayout(favoritesContentRoot);
            AdjustCardGridLayout(allGamesContentRoot);
            AdjustHeaderTitleLayout();
            AdjustBottomNavLayout();
            ApplyHeaderMenuLayout();
        }

        private void AdjustHeaderTitleLayout()
        {
            if (headerTitleBarRoot == null)
            {
                return;
            }

            var parent = headerTitleBarRoot.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            var availableWidth = Mathf.Max(0f, parent.rect.width - ResponsiveHorizontalMargin * 2f);
            var currentWidth = headerTitleBarRoot.rect.width;
            if (availableWidth <= 0f || currentWidth <= 0f)
            {
                return;
            }

            var targetScale = Mathf.Min(1f, availableWidth / currentWidth);
            headerTitleBarRoot.localScale = new Vector3(targetScale, targetScale, 1f);
        }

        private void EnsureHeaderTitlePulse()
        {
            if (headerTitleBarRoot == null)
            {
                return;
            }

            var titleImage = headerTitleBarRoot.Find("Title/Image") as RectTransform;
            if (titleImage == null)
            {
                var titleRoot = headerTitleBarRoot.Find("Title") as RectTransform;
                titleImage = titleRoot != null && titleRoot.GetComponent<Image>() != null
                    ? titleRoot
                    : null;
            }

            if (titleImage == null)
            {
                return;
            }

            var pulse = titleImage.GetComponent<HeaderTitlePulseEffect>();
            if (pulse == null)
            {
                pulse = titleImage.gameObject.AddComponent<HeaderTitlePulseEffect>();
            }

            pulse.Configure(HeaderTitlePulseBaseScale, HeaderTitlePulsePeakScale, HeaderTitlePulseCycleSeconds);
        }

        private void AdjustCardGridLayout(RectTransform contentRoot)
        {
            if (contentRoot == null || scrollRect == null)
            {
                return;
            }

            var grid = contentRoot.GetComponent<GridLayoutGroup>();
            var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
            if (grid == null || viewport == null)
            {
                return;
            }

            var baseCellWidth = Mathf.Max(1f, grid.cellSize.x);
            var baseCellHeight = Mathf.Max(1f, grid.cellSize.y);
            var aspect = baseCellHeight / baseCellWidth;
            var preferredColumnCount = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, grid.constraintCount)
                : 1;
            var horizontalContentWidth = viewport.rect.width - grid.padding.left - grid.padding.right;
            if (horizontalContentWidth <= 0f)
            {
                return;
            }

            var columnCount = ResolveColumnCount(contentRoot, horizontalContentWidth, grid.spacing.x, baseCellWidth, preferredColumnCount);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columnCount;

            var availableWidth = horizontalContentWidth - grid.spacing.x * (columnCount - 1);
            if (availableWidth <= 0f)
            {
                return;
            }

            var fittedCellWidth = Mathf.Min(baseCellWidth, Mathf.Floor(availableWidth / columnCount));
            var fittedCellHeight = Mathf.Round(fittedCellWidth * aspect);
            grid.cellSize = new Vector2(fittedCellWidth, fittedCellHeight);
        }

        private int ResolveColumnCount(
            RectTransform contentRoot,
            float horizontalContentWidth,
            float horizontalSpacing,
            float baseCellWidth,
            int preferredColumnCount)
        {
            if (contentRoot != allGamesContentRoot)
            {
                return preferredColumnCount;
            }

            var targetCellWidth = Mathf.Min(baseCellWidth, AllGamesMinCellWidth);
            var autoColumnCount = Mathf.FloorToInt((horizontalContentWidth + horizontalSpacing) / (targetCellWidth + horizontalSpacing));
            if (autoColumnCount < AllGamesPreferredMinColumnCount && horizontalContentWidth >= (targetCellWidth * 2f) + horizontalSpacing)
            {
                autoColumnCount = 2;
            }

            return Mathf.Clamp(autoColumnCount, 1, AllGamesMaxColumnCount);
        }

        private void UpdateChestBadge(Transform cardRoot, MiniGameProgressData progress)
        {
            if (cardRoot == null)
            {
                return;
            }

            var count = progress != null ? Mathf.Max(0, progress.TotalChestCount) : 0;
            var badgeRoot = cardRoot.Find("ChestBadge") as RectTransform;
            if (badgeRoot == null)
            {
                return;
            }

            var countText = badgeRoot.Find("ChestIcon/CountText")?.GetComponent<TextMeshProUGUI>();
            if (countText != null)
            {
                EnsureChestCountTextMaterial(countText);
                countText.text = count.ToString();
            }

            var coinCount = progress != null ? Mathf.Max(0, progress.TotalCoinCount) : 0;
            EnsureChestBadgeButton(cardRoot, badgeRoot, count, coinCount);
        }

        private void EnsureChestCountTextMaterial(TextMeshProUGUI countText)
        {
            if (countText == null || countText.font == null || countText.font.material == null)
            {
                return;
            }

            var baseMaterial = countText.font.material;
            if (chestCountOutlineMaterial == null || chestCountOutlineBaseMaterial != baseMaterial)
            {
                chestCountOutlineMaterial = new Material(baseMaterial)
                {
                    name = "chest_count_outline_runtime",
                    hideFlags = HideFlags.DontSave
                };
                chestCountOutlineMaterial.EnableKeyword("OUTLINE_ON");
                chestCountOutlineMaterial.SetColor("_OutlineColor", ChestCountOutlineColor);
                chestCountOutlineMaterial.SetFloat("_OutlineWidth", 0.33f);
                chestCountOutlineMaterial.SetFloat("_FaceDilate", 0.04f);
                chestCountOutlineMaterial.SetFloat("_OutlineSoftness", 0.02f);
                chestCountOutlineBaseMaterial = baseMaterial;
            }

            if (countText.fontSharedMaterial != chestCountOutlineMaterial)
            {
                countText.fontSharedMaterial = chestCountOutlineMaterial;
            }
        }

        private void EnsureChestBadgeButton(Transform cardRoot, RectTransform badgeRoot, int chestCount, int coinCount)
        {
            if (cardRoot == null || badgeRoot == null)
            {
                return;
            }

            if (badgeRoot.GetComponent<CanvasRenderer>() == null)
            {
                badgeRoot.gameObject.AddComponent<CanvasRenderer>();
            }

            var badgeImage = badgeRoot.GetComponent<Image>();
            if (badgeImage == null)
            {
                badgeImage = badgeRoot.gameObject.AddComponent<Image>();
            }

            badgeImage.color = new Color(1f, 1f, 1f, 0f);
            badgeImage.raycastTarget = true;
            badgeImage.sprite = null;
            badgeImage.preserveAspect = false;

            var badgeButton = badgeRoot.GetComponent<Button>();
            if (badgeButton == null)
            {
                badgeButton = badgeRoot.gameObject.AddComponent<Button>();
            }

            badgeButton.transition = Selectable.Transition.None;
            badgeButton.targetGraphic = badgeImage;
            badgeButton.onClick.RemoveAllListeners();
            badgeButton.onClick.AddListener(delegate { ShowChestToast(cardRoot, chestCount, coinCount); });
            MiniGameSfxPlayer.Attach(badgeButton, MiniGameSfxType.UiTap, 0.56f);
        }

        private void ShowChestToast(Transform cardRoot, int chestCount, int coinCount)
        {
            if (cardRoot == null)
            {
                return;
            }

            var toastRoot = EnsureChestToast();
            if (toastRoot == null)
            {
                return;
            }

            var messageText = toastRoot.Find("Message")?.GetComponent<TextMeshProUGUI>();
            if (messageText != null)
            {
                messageText.text = UiTextCatalog.Format(ChestToastTextKey, Mathf.Max(0, chestCount), Mathf.Max(0, coinCount));
                UpdateChestToastLayout(toastRoot, messageText);
            }

            PositionChestToast(toastRoot, cardRoot);
            toastRoot.SetAsLastSibling();
            toastRoot.gameObject.SetActive(true);
            toastRunner?.Show(toastRoot.gameObject, ChestToastDuration);
        }

        private RectTransform EnsureChestToast()
        {
            var toastRoot = overlayRoot != null ? overlayRoot.Find("ChestToast") as RectTransform : null;
            if (toastRoot != null)
            {
                return toastRoot;
            }

            var toastObject = new GameObject(
                "ChestToast",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            if (overlayRoot == null)
            {
                return null;
            }

            toastObject.transform.SetParent(overlayRoot, false);

            toastRoot = toastObject.GetComponent<RectTransform>();
            toastRoot.anchorMin = new Vector2(0.5f, 1f);
            toastRoot.anchorMax = new Vector2(0.5f, 1f);
            toastRoot.pivot = new Vector2(0.5f, 0f);
            toastRoot.anchoredPosition = new Vector2(0f, 0f);
            toastRoot.sizeDelta = new Vector2(ChestToastMinWidth, 52f);
            toastRoot.gameObject.SetActive(false);

            var toastBackground = toastObject.GetComponent<RoundedRectGraphic>();
            toastBackground.color = new Color(0.18f, 0.20f, 0.23f, 0.96f);
            toastBackground.CornerRadius = 16f;
            toastBackground.raycastTarget = false;

            var messageObject = new GameObject(
                "Message",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            messageObject.transform.SetParent(toastRoot, false);

            var messageRect = messageObject.GetComponent<RectTransform>();
            Stretch(
                messageRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(ChestToastHorizontalPadding, ChestToastVerticalPadding),
                new Vector2(-ChestToastHorizontalPadding, -ChestToastVerticalPadding));

            var messageText = messageObject.GetComponent<TextMeshProUGUI>();
            messageText.font = TMP_Settings.defaultFontAsset;
            messageText.text = UiTextCatalog.Format(ChestToastTextKey, 0, 0);
            messageText.fontSize = 18f;
            messageText.fontStyle = FontStyles.Bold;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.enableWordWrapping = true;
            messageText.overflowMode = TextOverflowModes.Overflow;
            messageText.color = Color.white;
            messageText.raycastTarget = false;

            UpdateChestToastLayout(toastRoot, messageText);

            return toastRoot;
        }

        private void UpdateChestToastLayout(RectTransform toastRoot, TextMeshProUGUI messageText)
        {
            if (toastRoot == null || messageText == null)
            {
                return;
            }

            var availableWidth = ChestToastMaxWidth;
            if (overlayRoot != null)
            {
                availableWidth = Mathf.Min(ChestToastMaxWidth, Mathf.Max(ChestToastMinWidth, overlayRoot.rect.width - 24f));
            }

            var textMaxWidth = Mathf.Max(0f, availableWidth - (ChestToastHorizontalPadding * 2f));
            var preferredSize = messageText.GetPreferredValues(messageText.text, textMaxWidth, 0f);
            var toastWidth = Mathf.Clamp(preferredSize.x + (ChestToastHorizontalPadding * 2f), ChestToastMinWidth, availableWidth);
            var textWidth = Mathf.Max(0f, toastWidth - (ChestToastHorizontalPadding * 2f));
            preferredSize = messageText.GetPreferredValues(messageText.text, textWidth, 0f);
            var toastHeight = Mathf.Max(52f, preferredSize.y + (ChestToastVerticalPadding * 2f));
            toastRoot.sizeDelta = new Vector2(toastWidth, toastHeight);
        }

        private RectTransform EnsureOverlayRoot()
        {
            if (root == null)
            {
                return null;
            }

            var overlayParent = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            if (overlayParent == null)
            {
                return null;
            }

            var existing = overlayParent.Find("HallOverlay") as RectTransform;
            if (existing != null)
            {
                existing.SetAsLastSibling();
                return existing;
            }

            var overlayObject = new GameObject(
                "HallOverlay",
                typeof(RectTransform));
            overlayObject.transform.SetParent(overlayParent, false);

            var overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlayRect.SetAsLastSibling();
            return overlayRect;
        }

        private void PositionChestToast(RectTransform toastRoot, Transform cardRoot)
        {
            if (toastRoot == null || cardRoot == null || overlayRoot == null)
            {
                return;
            }

            var badgeRoot = cardRoot.Find("ChestBadge") as RectTransform;
            if (badgeRoot == null)
            {
                return;
            }

            var corners = new Vector3[4];
            badgeRoot.GetWorldCorners(corners);
            var badgeTopCenterWorld = (corners[1] + corners[2]) * 0.5f;
            var localPoint3 = overlayRoot.InverseTransformPoint(badgeTopCenterWorld);
            var localX = localPoint3.x;
            var localY = localPoint3.y - 3f;

            var overlayWidth = overlayRoot.rect.width;
            var toastWidth = toastRoot.rect.width;
            var horizontalPadding = 12f;
            var maxX = overlayWidth * 0.5f - toastWidth * 0.5f - horizontalPadding;
            var minX = -maxX;
            localX = Mathf.Clamp(localX, minX, maxX);

            toastRoot.position = overlayRoot.TransformPoint(new Vector3(localX, localY, 0f));
        }

        private void UpdateFavoriteBadge(Transform cardRoot, bool isFavorite, string gameId)
        {
            if (cardRoot == null)
            {
                return;
            }

            var badge = cardRoot.Find("FavoriteBadge")?.GetComponent<Image>();
            if (badge == null)
            {
                return;
            }

            var button = badge.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (toggleFavorite != null && !string.IsNullOrWhiteSpace(gameId))
                {
                    button.onClick.AddListener(delegate { toggleFavorite(gameId); });
                    MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.56f);
                }
            }

            badge.gameObject.SetActive(true);
            badge.sprite = favoriteStarSprite;
            badge.color = isFavorite ? FavoriteStarActiveColor : FavoriteStarInactiveColor;
        }

        private Transform FindCardRoot(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return null;
            }

            var targetName = gameId + "_Card";
            var contentRoot = CurrentContentRoot;
            if (contentRoot == null)
            {
                return null;
            }

            for (var i = 0; i < contentRoot.childCount; i++)
            {
                var slot = contentRoot.GetChild(i);
                if (slot == null)
                {
                    continue;
                }

                var cardRoot = slot.Find(targetName);
                if (cardRoot != null)
                {
                    return cardRoot;
                }
            }

            return null;
        }

        private void AdjustBottomNavLayout()
        {
            if (bottomNavRoot == null)
            {
                return;
            }

            var layout = bottomNavRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                return;
            }

            var parentWidth = ((RectTransform)bottomNavRoot.parent).rect.width;
            var maxContainerWidth = bottomNavRoot.sizeDelta.x;
            var targetWidth = Mathf.Min(maxContainerWidth, Mathf.Max(0f, parentWidth - ResponsiveHorizontalMargin));
            bottomNavRoot.sizeDelta = new Vector2(targetWidth, bottomNavRoot.sizeDelta.y);

            var buttonCount = bottomNavRoot.childCount;
            if (buttonCount <= 0)
            {
                return;
            }

            var availableWidth = targetWidth - layout.padding.left - layout.padding.right - layout.spacing * (buttonCount - 1);
            if (availableWidth <= 0f)
            {
                return;
            }

            var targetButtonWidth = Mathf.Floor(availableWidth / buttonCount);
            for (var i = 0; i < bottomNavRoot.childCount; i++)
            {
                var child = bottomNavRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = targetButtonWidth;
                }

                child.sizeDelta = new Vector2(targetButtonWidth, child.sizeDelta.y);
            }
        }

        private void EnsureHeaderStatIconEffects()
        {
            if (headerStatsRoot == null)
            {
                return;
            }

            ConfigureHeaderStatIconEffect(headerStatsRoot.Find("ChestStat/ChestIcon") as RectTransform);
            ConfigureHeaderStatIconEffect(headerStatsRoot.Find("CoinStat/CoinIcon") as RectTransform);
        }

        private static void ConfigureHeaderStatIconEffect(RectTransform iconRect)
        {
            if (iconRect == null)
            {
                return;
            }

            var effect = iconRect.GetComponent<HeaderStatIconEffect>();
            if (effect == null)
            {
                effect = iconRect.gameObject.AddComponent<HeaderStatIconEffect>();
            }

            effect.Configure();
        }

        private sealed class HeaderTitlePulseEffect : MonoBehaviour
        {
            private Vector3 baseScale = Vector3.one;
            private float baseFactor = 1f;
            private float peakFactor = 1.03f;
            private float cycleSeconds = 3f;
            private float elapsedSeconds;
            private bool configured;

            public void Configure(float baseScaleFactor, float peakScaleFactor, float cycleDurationSeconds)
            {
                if (!configured)
                {
                    baseScale = transform.localScale;
                    configured = true;
                }

                baseFactor = Mathf.Max(0.01f, baseScaleFactor);
                peakFactor = Mathf.Max(baseFactor, peakScaleFactor);
                cycleSeconds = Mathf.Max(0.01f, cycleDurationSeconds);
                elapsedSeconds = 0f;
                transform.localScale = baseScale * baseFactor;
            }

            private void Update()
            {
                if (!configured)
                {
                    return;
                }

                elapsedSeconds += Time.unscaledDeltaTime;
                ApplyCurrentScale();
            }

            private void OnDisable()
            {
                RestoreScale();
            }

            private void OnDestroy()
            {
                RestoreScale();
            }

            private void ApplyCurrentScale()
            {
                var amplitude = peakFactor - baseFactor;
                var normalized = (1f - Mathf.Cos((elapsedSeconds / cycleSeconds) * Mathf.PI * 2f)) * 0.5f;
                var currentFactor = baseFactor + (amplitude * normalized);
                transform.localScale = baseScale * currentFactor;
            }

            private void RestoreScale()
            {
                if (!configured)
                {
                    return;
                }

                transform.localScale = baseScale * baseFactor;
            }
        }

        private sealed class StartButtonHighlightEffect : MonoBehaviour
        {
            private const float BreathMinAlpha = 0.02f;
            private const float BreathMaxAlpha = 0.05f;
            private const float BreathCycleSeconds = 3.2f;
            private const float SweepIntervalMinSeconds = 4f;
            private const float SweepIntervalMaxSeconds = 6f;
            private const float SweepDurationMinSeconds = 0.9f;
            private const float SweepDurationMaxSeconds = 1.2f;
            private const float SweepPeakAlpha = 0.2f;
            private const float SweepWidth = 52f;
            private const float SweepHeightPadding = 34f;
            private const float SweepAngleZ = -24f;
            private static Sprite whiteSprite;

            private RectTransform highlightRoot;
            private Image breathGlow;
            private Image sweepShine;
            private float elapsedSeconds;
            private float nextSweepDelaySeconds;
            private float currentSweepDurationSeconds;
            private bool sweepActive;
            private bool configured;

            public void Configure()
            {
                EnsureVisuals();
                elapsedSeconds = 0f;
                sweepActive = false;
                currentSweepDurationSeconds = 0f;
                nextSweepDelaySeconds = UnityEngine.Random.Range(SweepIntervalMinSeconds, SweepIntervalMaxSeconds);
                configured = true;
                ApplyVisualState();
            }

            private void OnEnable()
            {
                if (!configured)
                {
                    return;
                }

                elapsedSeconds = 0f;
                sweepActive = false;
                currentSweepDurationSeconds = 0f;
                nextSweepDelaySeconds = UnityEngine.Random.Range(SweepIntervalMinSeconds, SweepIntervalMaxSeconds);
                ApplyVisualState();
            }

            private void Update()
            {
                if (!configured || highlightRoot == null || breathGlow == null || sweepShine == null)
                {
                    return;
                }

                elapsedSeconds += Time.unscaledDeltaTime;
                if (!sweepActive && elapsedSeconds >= nextSweepDelaySeconds)
                {
                    sweepActive = true;
                    currentSweepDurationSeconds = UnityEngine.Random.Range(SweepDurationMinSeconds, SweepDurationMaxSeconds);
                    elapsedSeconds = 0f;
                }
                else if (sweepActive && elapsedSeconds >= currentSweepDurationSeconds)
                {
                    sweepActive = false;
                    currentSweepDurationSeconds = 0f;
                    elapsedSeconds = 0f;
                    nextSweepDelaySeconds = UnityEngine.Random.Range(SweepIntervalMinSeconds, SweepIntervalMaxSeconds);
                }

                ApplyVisualState();
            }

            private void OnDisable()
            {
                RestoreVisualState();
            }

            private void OnDestroy()
            {
                RestoreVisualState();
            }

            private void EnsureVisuals()
            {
                if (highlightRoot == null)
                {
                    var rootTransform = transform.Find(StartButtonHighlightRootName) as RectTransform;
                    if (rootTransform == null)
                    {
                        var rootObject = new GameObject(
                            StartButtonHighlightRootName,
                            typeof(RectTransform),
                            typeof(CanvasRenderer),
                            typeof(RectMask2D));
                        rootTransform = rootObject.GetComponent<RectTransform>();
                        rootTransform.SetParent(transform, false);
                        Stretch(rootTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                        rootTransform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
                    }

                    highlightRoot = rootTransform;
                }

                var highlightSprite = GetHighlightSprite();

                breathGlow = EnsureHighlightImage(
                    highlightRoot,
                    StartButtonBreathGlowName,
                    highlightSprite,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 26f),
                    false);

                sweepShine = EnsureHighlightImage(
                    highlightRoot,
                    StartButtonSweepShineName,
                    highlightSprite,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-120f, 0f),
                    new Vector2(SweepWidth, GetSweepHeight()),
                    true);
            }

            private static Image EnsureHighlightImage(
                RectTransform parent,
                string name,
                Sprite sprite,
                Vector2 anchorMin,
                Vector2 anchorMax,
                Vector2 pivot,
                Vector2 anchoredPosition,
                Vector2 sizeDelta,
                bool rotated)
            {
                var child = parent.Find(name) as RectTransform;
                if (child == null)
                {
                    var childObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    child = childObject.GetComponent<RectTransform>();
                    child.SetParent(parent, false);
                }

                child.anchorMin = anchorMin;
                child.anchorMax = anchorMax;
                child.pivot = pivot;
                child.anchoredPosition = anchoredPosition;
                child.sizeDelta = sizeDelta;
                child.localRotation = rotated ? Quaternion.Euler(0f, 0f, SweepAngleZ) : Quaternion.identity;
                child.localScale = Vector3.one;

                var image = child.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                return image;
            }

            private static Sprite GetHighlightSprite()
            {
                if (whiteSprite == null)
                {
                    whiteSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }

                return whiteSprite;
            }

            private float GetSweepHeight()
            {
                var rect = transform as RectTransform;
                var height = rect != null ? rect.rect.height : 58f;
                return Mathf.Max(58f, height + SweepHeightPadding);
            }

            private void ApplyVisualState()
            {
                if (breathGlow == null || sweepShine == null)
                {
                    return;
                }

                breathGlow.rectTransform.sizeDelta = new Vector2(0f, 26f);
                var breathCycle = Mathf.Repeat(Time.unscaledTime / BreathCycleSeconds, 1f);
                var breathWave = 0.5f - 0.5f * Mathf.Cos(breathCycle * Mathf.PI * 2f);
                var breathAlpha = Mathf.Lerp(BreathMinAlpha, BreathMaxAlpha, breathWave);
                breathGlow.color = new Color(1f, 1f, 1f, breathAlpha);

                var sweepRect = sweepShine.rectTransform;
                sweepRect.sizeDelta = new Vector2(SweepWidth, GetSweepHeight());
                if (!sweepActive || currentSweepDurationSeconds <= 0f)
                {
                    sweepShine.color = new Color(1f, 1f, 1f, 0f);
                    sweepRect.anchoredPosition = new Vector2(-sweepRect.sizeDelta.x * 1.4f, 0f);
                    return;
                }

                var width = 186f;
                var actionRect = transform as RectTransform;
                if (actionRect != null && actionRect.rect.width > 0f)
                {
                    width = actionRect.rect.width;
                }

                var progress = Mathf.Clamp01(elapsedSeconds / currentSweepDurationSeconds);
                var startX = -width * 0.35f - sweepRect.sizeDelta.x;
                var endX = width * 1.35f;
                var currentX = Mathf.Lerp(startX, endX, progress);
                var currentY = Mathf.Lerp(12f, -6f, progress);
                sweepRect.anchoredPosition = new Vector2(currentX, currentY);
                var sweepAlpha = Mathf.Sin(progress * Mathf.PI) * SweepPeakAlpha;
                sweepShine.color = new Color(1f, 1f, 1f, sweepAlpha);
            }

            private void RestoreVisualState()
            {
                if (breathGlow != null)
                {
                    breathGlow.color = new Color(1f, 1f, 1f, 0f);
                }

                if (sweepShine != null)
                {
                    sweepShine.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        /// <summary>
        /// 刷新底部导航按钮的选中态样式。
        /// </summary>
        private sealed class HeaderStatIconEffect : MonoBehaviour
        {
            private const float SweepIntervalMinSeconds = 5f;
            private const float SweepIntervalMaxSeconds = 8f;
            private const float SweepDurationMinSeconds = 0.72f;
            private const float SweepDurationMaxSeconds = 0.98f;
            private const float SweepPeakAlpha = 0.3f;
            private const float SweepAngleZ = -24f;
            private static Sprite whiteSprite;

            private RectTransform iconRect;
            private Vector2 baseAnchoredPosition;
            private RectTransform sweepRoot;
            private Image sweepShine;
            private float sweepElapsedSeconds;
            private float nextSweepDelaySeconds;
            private float currentSweepDurationSeconds;
            private bool sweepActive;
            private bool configured;

            public void Configure()
            {
                iconRect = transform as RectTransform;
                if (iconRect == null)
                {
                    return;
                }

                if (!configured)
                {
                    baseAnchoredPosition = iconRect.anchoredPosition;
                    EnsureVisuals();
                    configured = true;
                }

                iconRect.anchoredPosition = baseAnchoredPosition;
                ResetSweepState();
                ApplyVisualState();
            }

            private void OnEnable()
            {
                if (!configured || iconRect == null)
                {
                    return;
                }

                iconRect.anchoredPosition = baseAnchoredPosition;
                ResetSweepState();
                ApplyVisualState();
            }

            private void Update()
            {
                if (!configured || iconRect == null || sweepShine == null || sweepRoot == null)
                {
                    return;
                }

                sweepElapsedSeconds += Time.unscaledDeltaTime;
                if (!sweepActive && sweepElapsedSeconds >= nextSweepDelaySeconds)
                {
                    sweepActive = true;
                    currentSweepDurationSeconds = UnityEngine.Random.Range(SweepDurationMinSeconds, SweepDurationMaxSeconds);
                    sweepElapsedSeconds = 0f;
                }
                else if (sweepActive && sweepElapsedSeconds >= currentSweepDurationSeconds)
                {
                    sweepActive = false;
                    currentSweepDurationSeconds = 0f;
                    sweepElapsedSeconds = 0f;
                    nextSweepDelaySeconds = UnityEngine.Random.Range(SweepIntervalMinSeconds, SweepIntervalMaxSeconds);
                }

                ApplyVisualState();
            }

            private void OnDisable()
            {
                RestoreVisualState();
            }

            private void OnDestroy()
            {
                RestoreVisualState();
            }

            private void EnsureVisuals()
            {
                if (sweepRoot == null)
                {
                    var rootTransform = transform.Find(HeaderStatSweepRootName) as RectTransform;
                    if (rootTransform == null)
                    {
                        var rootObject = new GameObject(
                            HeaderStatSweepRootName,
                            typeof(RectTransform),
                            typeof(CanvasRenderer),
                            typeof(RectMask2D));
                        rootTransform = rootObject.GetComponent<RectTransform>();
                        rootTransform.SetParent(transform, false);
                        Stretch(rootTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                        rootTransform.SetAsLastSibling();
                    }

                    sweepRoot = rootTransform;
                }

                var shineRect = sweepRoot.Find(HeaderStatSweepShineName) as RectTransform;
                if (shineRect == null)
                {
                    var shineObject = new GameObject(HeaderStatSweepShineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    shineRect = shineObject.GetComponent<RectTransform>();
                    shineRect.SetParent(sweepRoot, false);
                }

                shineRect.anchorMin = new Vector2(0f, 0.5f);
                shineRect.anchorMax = new Vector2(0f, 0.5f);
                shineRect.pivot = new Vector2(0.5f, 0.5f);
                shineRect.localRotation = Quaternion.Euler(0f, 0f, SweepAngleZ);
                shineRect.localScale = Vector3.one;
                shineRect.anchoredPosition = new Vector2(-48f, 0f);
                shineRect.sizeDelta = new Vector2(18f, GetSweepHeight());

                sweepShine = shineRect.GetComponent<Image>();
                sweepShine.sprite = GetWhiteSprite();
                sweepShine.type = Image.Type.Simple;
                sweepShine.preserveAspect = false;
                sweepShine.raycastTarget = false;
                sweepShine.color = new Color(1f, 1f, 1f, 0f);
            }

            private void ResetSweepState()
            {
                sweepElapsedSeconds = 0f;
                sweepActive = false;
                currentSweepDurationSeconds = 0f;
                nextSweepDelaySeconds = UnityEngine.Random.Range(SweepIntervalMinSeconds, SweepIntervalMaxSeconds);
            }

            private void ApplyVisualState()
            {
                iconRect.anchoredPosition = baseAnchoredPosition;

                var sweepRect = sweepShine.rectTransform;
                sweepRect.sizeDelta = new Vector2(sweepRect.sizeDelta.x, GetSweepHeight());
                if (!sweepActive || currentSweepDurationSeconds <= 0f)
                {
                    sweepShine.color = new Color(1f, 1f, 1f, 0f);
                    sweepRect.anchoredPosition = new Vector2(-GetSweepTravelWidth() * 0.55f, 0f);
                    return;
                }

                var progress = Mathf.Clamp01(sweepElapsedSeconds / currentSweepDurationSeconds);
                var travelWidth = GetSweepTravelWidth();
                var currentX = Mathf.Lerp(-travelWidth * 0.55f, travelWidth * 0.55f, progress);
                sweepRect.anchoredPosition = new Vector2(currentX, Mathf.Lerp(5f, -4f, progress));
                var sweepAlpha = Mathf.Sin(progress * Mathf.PI) * SweepPeakAlpha;
                sweepShine.color = new Color(1f, 1f, 1f, sweepAlpha);
            }

            private void RestoreVisualState()
            {
                if (iconRect != null)
                {
                    iconRect.anchoredPosition = baseAnchoredPosition;
                }

                if (sweepShine != null)
                {
                    sweepShine.color = new Color(1f, 1f, 1f, 0f);
                }
            }

            private float GetSweepTravelWidth()
            {
                return iconRect != null && iconRect.rect.width > 0f ? iconRect.rect.width + 24f : 79f;
            }

            private float GetSweepHeight()
            {
                return iconRect != null && iconRect.rect.height > 0f ? iconRect.rect.height + 18f : 73f;
            }

            private static Sprite GetWhiteSprite()
            {
                if (whiteSprite == null)
                {
                    whiteSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }

                return whiteSprite;
            }
        }

        private void UpdateNavSelection()
        {
            for (var i = 0; i < navButtons.Count; i++)
            {
                var binding = navButtons[i];
                var selected = binding.Tab == currentTab;
                if (binding.BackgroundImage != null)
                {
                    binding.BackgroundImage.sprite = selected ? tabSelectedSprite : tabUnselectedSprite;
                }
            }
        }

        /// <summary>
        /// 获取页签标题文案。
        /// </summary>
        private static string GetTabTitle(HallTab tab)
        {
            switch (tab)
            {
                case HallTab.AllGames:
                    return UiTextCatalog.Get("hall.tab.all_games");
                case HallTab.Profile:
                    return UiTextCatalog.Get("hall.tab.profile");
                default:
                    return UiTextCatalog.Get("hall.tab.favorites");
            }
        }

        /// <summary>
        /// 销毁容器下所有子节点。
        /// </summary>
        private RectTransform CreateHeaderStats(Transform shell)
        {
            var headerObject = new GameObject(
                "HeaderStats",
                typeof(RectTransform));
            headerObject.transform.SetParent(shell, false);

            var headerRect = headerObject.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 0.5f);
            headerRect.anchoredPosition = new Vector2(0f, -194f);
            headerRect.sizeDelta = new Vector2(398f, 42f);

            CreateHeaderStatsBackground(headerObject.transform);

            CreateHeaderStat(headerObject.transform, "ChestStat", "ChestIcon", chestIconSprite, new Vector2(-94f, 0f));
            CreateHeaderStat(headerObject.transform, "CoinStat", "CoinIcon", coinIconSprite, new Vector2(102f, 0f));

            return headerRect;
        }

        private RectTransform CreateHeaderTagBar(Transform shell)
        {
            var tagBarObject = new GameObject(
                "HeaderTagBar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            tagBarObject.transform.SetParent(shell, false);

            var tagBarRect = tagBarObject.GetComponent<RectTransform>();
            tagBarRect.anchorMin = new Vector2(0.5f, 1f);
            tagBarRect.anchorMax = new Vector2(0.5f, 1f);
            tagBarRect.pivot = new Vector2(0.5f, 0.5f);
            tagBarRect.anchoredPosition = new Vector2(0f, -194f);
            tagBarRect.sizeDelta = new Vector2(620f, 54f);

            var background = tagBarObject.GetComponent<RoundedRectGraphic>();
            background.color = new Color(1f, 0.98f, 0.88f, 0.88f);
            background.CornerRadius = 22f;
            background.raycastTarget = false;

            var layout = tagBarObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 7, 7);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var i = 0; i < AllGamesTagTextKeys.Length; i++)
            {
                CreateHeaderTagButton(tagBarObject.transform, i);
            }

            return tagBarRect;
        }

        private void BindHeaderTagButtons()
        {
            headerTagButtons.Clear();
            if (headerTagBarRoot == null)
            {
                return;
            }

            for (var i = 0; i < AllGamesTagTextKeys.Length; i++)
            {
                var tag = headerTagBarRoot.Find("Tag_" + i);
                if (tag == null)
                {
                    continue;
                }

                var binding = BindHeaderTagButton(tag, i);
                if (binding != null)
                {
                    headerTagButtons.Add(binding);
                }
            }
        }

        private HeaderTagBinding BindHeaderTagButton(Transform tag, int index)
        {
            var button = tag.GetComponent<Button>();
            var graphic = tag.GetComponent<RoundedRectGraphic>();
            var label = tag.Find("Label")?.GetComponent<TextMeshProUGUI>();
            var layoutElement = tag.GetComponent<LayoutElement>();
            var rectTransform = tag as RectTransform;
            if (button == null || graphic == null || label == null || layoutElement == null || rectTransform == null)
            {
                return null;
            }

            label.text = UiTextCatalog.Get(AllGamesTagTextKeys[index]);
            button.interactable = true;
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { SelectHeaderTag(index); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.68f);

            return new HeaderTagBinding
            {
                Index = index,
                Graphic = graphic,
                Label = label,
                LayoutElement = layoutElement,
                RectTransform = rectTransform
            };
        }

        private void UpdateHeaderTagSelection()
        {
            for (var i = 0; i < headerTagButtons.Count; i++)
            {
                var binding = headerTagButtons[i];
                var selected = binding.Index == selectedHeaderTagIndex;
                if (binding.Graphic != null)
                {
                    binding.Graphic.color = selected ? new Color(1f, 0.62f, 0.14f, 1f) : new Color(1f, 1f, 0.96f, 0.95f);
                }

                if (binding.Label != null)
                {
                    binding.Label.color = selected ? Color.white : new Color(0.32f, 0.42f, 0.19f, 1f);
                }

                var width = selected ? 90f : 76f;
                if (binding.LayoutElement != null)
                {
                    binding.LayoutElement.preferredWidth = width;
                }

                if (binding.RectTransform != null)
                {
                    binding.RectTransform.sizeDelta = new Vector2(width, 40f);
                }
            }
        }

        private void CreateHeaderTagButton(Transform parent, int index)
        {
            var buttonObject = new GameObject(
                "Tag_" + index,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var selected = index == selectedHeaderTagIndex;
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(selected ? 90f : 76f, 40f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = buttonRect.sizeDelta.x;
            layoutElement.preferredHeight = buttonRect.sizeDelta.y;

            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.color = selected ? new Color(1f, 0.62f, 0.14f, 1f) : new Color(1f, 1f, 0.96f, 0.95f);
            graphic.CornerRadius = 18f;
            graphic.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.interactable = true;
            button.onClick.AddListener(delegate { SelectHeaderTag(index); });
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.68f);

            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = UiTextCatalog.Get(AllGamesTagTextKeys[index]);
            label.fontSize = 22f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.color = selected ? Color.white : new Color(0.32f, 0.42f, 0.19f, 1f);
            label.raycastTarget = false;
        }

        private void CreateHeaderStatsBackground(Transform parent)
        {
            CreateHeaderStatsLayer(parent, "BackdropOuter", new Vector2(416f, 50f), new Color(0f, 0f, 0f, 0.12f), 18f);
            CreateHeaderStatsLayer(parent, "BackdropMiddle", new Vector2(408f, 46f), new Color(0f, 0f, 0f, 0.20f), 16f);
            CreateHeaderStatsLayer(parent, "BackdropInner", new Vector2(398f, 42f), new Color(0f, 0f, 0f, 0.30f), 14f);
        }

        private static void CreateHeaderStat(Transform parent, string statName, string iconName, Sprite iconSprite, Vector2 anchoredPosition)
        {
            var statObject = new GameObject(
                statName,
                typeof(RectTransform));
            statObject.transform.SetParent(parent, false);
            var statRect = statObject.GetComponent<RectTransform>();
            statRect.anchorMin = new Vector2(0.5f, 0.5f);
            statRect.anchorMax = new Vector2(0.5f, 0.5f);
            statRect.pivot = new Vector2(0.5f, 0.5f);
            statRect.anchoredPosition = anchoredPosition;
            statRect.sizeDelta = new Vector2(170f, 48f);

            var icon = new GameObject(
                iconName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            icon.transform.SetParent(statObject.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 0f);
            iconRect.sizeDelta = new Vector2(55f, 55f);

            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            var countTextObject = new GameObject(
                "CountText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            countTextObject.transform.SetParent(statObject.transform, false);
            var countRect = countTextObject.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0.5f);
            countRect.anchorMax = new Vector2(0f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.anchoredPosition = new Vector2(62f, 0f);
            countRect.sizeDelta = new Vector2(92f, 34f);

            var countText = countTextObject.GetComponent<TextMeshProUGUI>();
            countText.font = TMP_Settings.defaultFontAsset;
            countText.text = "0";
            countText.fontSize = 27f;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.MidlineLeft;
            countText.enableWordWrapping = false;
            countText.color = new Color(0.28f, 0.31f, 0.37f, 1f);
            countText.raycastTarget = false;
        }

        private static void CreateHeaderStatsLayer(Transform parent, string name, Vector2 size, Color color, float cornerRadius)
        {
            var layerObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            layerObject.transform.SetParent(parent, false);

            var layerRect = layerObject.GetComponent<RectTransform>();
            layerRect.anchorMin = new Vector2(0.5f, 0.5f);
            layerRect.anchorMax = new Vector2(0.5f, 0.5f);
            layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.anchoredPosition = Vector2.zero;
            layerRect.sizeDelta = size;

            var graphic = layerObject.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            graphic.CornerRadius = cornerRadius;
        }

        private static void ClearChildren(RectTransform rootTransform)
        {
            for (var i = rootTransform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(rootTransform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 将目标 RectTransform 设为拉伸并应用偏移。
        /// </summary>
        private static void Stretch(RectTransform rectTransform, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void RemoveNode(Transform transformNode)
        {
            if (transformNode != null)
            {
                UnityEngine.Object.Destroy(transformNode.gameObject);
            }
        }

        private sealed class ToastRunner : MonoBehaviour
        {
            private Coroutine activeRoutine;
            private GameObject activeToast;

            public void Show(GameObject toast, float duration)
            {
                if (toast == null)
                {
                    return;
                }

                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                    activeRoutine = null;
                }

                if (activeToast != null && activeToast != toast)
                {
                    activeToast.SetActive(false);
                }

                activeToast = toast;
                activeRoutine = StartCoroutine(HideAfterDelay(toast, duration));
            }

            private IEnumerator HideAfterDelay(GameObject toast, float duration)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, duration));
                if (toast != null)
                {
                    toast.SetActive(false);
                }

                if (activeToast == toast)
                {
                    activeToast = null;
                }

                activeRoutine = null;
            }

            private void OnDisable()
            {
                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                    activeRoutine = null;
                }

                activeToast = null;
            }
        }
    }
}
