using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall.Editor
{
    public static class HallThemeBuilder
    {
        private const string ReferenceFolder = "参考/新参考";
        private const string ThemeFolder = "Assets/Common/Resources/HallTheme";
        private const string HallPrefabPath = "Assets/Hall/Resources/HallView.prefab";
        private const string BackgroundAssetPath = ThemeFolder + "/hall_bg.png";
        private const string MenuButtonAssetPath = ThemeFolder + "/menu_button.png";
        private const string MenuPanelAssetPath = ThemeFolder + "/popup_panel.png";
        private const string MenuPanelTopDecorAssetPath = ThemeFolder + "/popup_panel_top_decor_side.png";
        private const string TitleAssetPath = ThemeFolder + "/hall_title.png";
        private const string CardAssetPath = ThemeFolder + "/hall_card.png";
        private const string ButtonAssetPath = ThemeFolder + "/hall_button.png";
        private const string TabSelectedAssetPath = ThemeFolder + "/hall_tab_selected.png";
        private const string TabUnselectedAssetPath = ThemeFolder + "/hall_tab_unselected.png";
        private const string ChestIconAssetPath = "Assets/Common/Resources/GameIcons/chest.png";
        private const string FavoriteStarIconAssetPath = "Assets/Common/Resources/GameIcons/star.png";
        private static readonly string[] HeaderTagTextKeys =
        {
            "hall.tag.all",
            "hall.tag.eliminate",
            "hall.tag.puzzle",
            "hall.tag.number",
            "hall.tag.action",
            "hall.tag.simulation",
            "hall.tag.merge"
        };

        [MenuItem("Tools/小游戏大厅/应用新参考主题")]
        public static void ApplyNewReferenceTheme()
        {
            EnsureFolder("Assets/Hall/Resources");
            EnsureFolder(ThemeFolder);
            SyncThemeTexture("背景.png", BackgroundAssetPath, 2048);
            SyncThemeTexture("标题.png", TitleAssetPath, 1024);
            SyncThemeTexture("卡片.png", CardAssetPath, 1024);
            SyncThemeTexture("按钮.png", ButtonAssetPath, 1024);
            SyncThemeTexture("tab按钮选中.png", TabSelectedAssetPath, 1024);
            SyncThemeTexture("tab按钮未选中.png", TabUnselectedAssetPath, 1024);

            AssetDatabase.Refresh();
            BuildHallViewPrefab();
            NormalizeHallRuntimeTemplates();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mini game hall theme applied from 参考/新参考.");
        }

        [MenuItem("Tools/小游戏大厅/重建大厅预制体")]
        public static void RebuildHallPrefab()
        {
            EnsureFolder("Assets/Hall/Resources");
            EnsureHeaderTitleBarInHallPrefab();
            EnsureCardTitleAndIconInHallPrefab();
            EnsureCardBadgesInHallPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mini game hall prefab updated.");
        }

        private static void SyncThemeTexture(string sourceFileName, string targetAssetPath, int maxSize)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Unable to resolve project root.");
            }

            var sourcePath = Path.Combine(projectRoot, ReferenceFolder, sourceFileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Reference texture not found.", sourcePath);
            }

            var targetPath = Path.Combine(projectRoot, targetAssetPath.Replace('/', Path.DirectorySeparatorChar));
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (!File.Exists(targetPath) || !FilesEqual(sourcePath, targetPath))
            {
                File.Copy(sourcePath, targetPath, true);
            }

            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(targetAssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("TextureImporter not found: " + targetAssetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 50;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        private static bool FilesEqual(string left, string right)
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }

            var leftBytes = File.ReadAllBytes(left);
            var rightBytes = File.ReadAllBytes(right);
            for (var i = 0; i < leftBytes.Length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void BuildHallViewPrefab()
        {
            var hallRoot = new GameObject("HallView", typeof(RectTransform));
            Stretch(hallRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var shell = CreateRectOnly("Shell");
            shell.transform.SetParent(hallRoot.transform, false);
            Stretch(shell.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var background = CreateImage("Background", LoadSprite(BackgroundAssetPath), false);
            background.transform.SetParent(shell.transform, false);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var titleBar = CreateHeaderTitleBar("HeaderTitleBar");
            titleBar.transform.SetParent(shell.transform, false);
            var titleBarRect = titleBar.GetComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0.5f, 1f);
            titleBarRect.anchorMax = new Vector2(0.5f, 1f);
            titleBarRect.pivot = new Vector2(0.5f, 0.5f);
            titleBarRect.anchoredPosition = new Vector2(0f, -112f);
            titleBarRect.sizeDelta = new Vector2(0f, 0f);

            var title = CreateImage("Title", LoadSprite(TitleAssetPath), true);
            title.transform.SetParent(titleBar.transform, false);
            title.rectTransform.sizeDelta = new Vector2(430f, 104f);
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredWidth = 430f;
            titleLayout.preferredHeight = 104f;

            var headerTagBar = CreateHeaderTagBar("HeaderTagBar");
            headerTagBar.transform.SetParent(shell.transform, false);
            var headerTagBarRect = headerTagBar.GetComponent<RectTransform>();
            headerTagBarRect.anchorMin = new Vector2(0.5f, 1f);
            headerTagBarRect.anchorMax = new Vector2(0.5f, 1f);
            headerTagBarRect.pivot = new Vector2(0.5f, 0.5f);
            headerTagBarRect.anchoredPosition = new Vector2(0f, -194f);
            headerTagBarRect.sizeDelta = new Vector2(620f, 54f);

            var scrollFrame = new GameObject(
                "ScrollFrame",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            scrollFrame.transform.SetParent(shell.transform, false);
            Stretch(
                scrollFrame.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(-342f, 132f),
                new Vector2(342f, -230f));

            var scrollFrameImage = scrollFrame.GetComponent<Image>();
            scrollFrameImage.color = new Color(1f, 1f, 1f, 0f);
            scrollFrameImage.raycastTarget = true;

            var favoritesContent = CreateHallGridContent("FavoritesContent");
            favoritesContent.transform.SetParent(scrollFrame.transform, false);

            var allGamesContent = CreateHallGridContent("AllGamesContent");
            allGamesContent.transform.SetParent(scrollFrame.transform, false);
            allGamesContent.SetActive(false);

            var profileContent = CreateProfileContent("ProfileContent");
            profileContent.transform.SetParent(scrollFrame.transform, false);
            profileContent.SetActive(false);

            var runtimeTemplates = CreateRectOnly("RuntimeTemplates");
            runtimeTemplates.transform.SetParent(shell.transform, false);
            Stretch(runtimeTemplates.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            runtimeTemplates.SetActive(false);

            var cardTemplate = BuildHallCardTemplate("CardTemplate");
            cardTemplate.transform.SetParent(runtimeTemplates.transform, false);

            var profileTemplate = BuildHallProfileTemplate("ProfileTemplate");
            profileTemplate.transform.SetParent(runtimeTemplates.transform, false);

            var bottomNavButtons = CreateRectOnly("BottomNavButtons");
            bottomNavButtons.transform.SetParent(shell.transform, false);
            var bottomNavRect = bottomNavButtons.GetComponent<RectTransform>();
            bottomNavRect.anchorMin = new Vector2(0.5f, 0f);
            bottomNavRect.anchorMax = new Vector2(0.5f, 0f);
            bottomNavRect.pivot = new Vector2(0.5f, 0.5f);
            bottomNavRect.anchoredPosition = new Vector2(0f, 88f);
            bottomNavRect.sizeDelta = new Vector2(732f, 84f);

            var navLayout = bottomNavButtons.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 12f;
            navLayout.childAlignment = TextAnchor.MiddleCenter;
            navLayout.childControlWidth = false;
            navLayout.childControlHeight = false;
            navLayout.childForceExpandWidth = false;
            navLayout.childForceExpandHeight = false;

            CreateNavButton(bottomNavButtons.transform, "FavoritesTab", "收藏", "Assets/Common/Resources/GameIcons/star.png");
            CreateNavButton(bottomNavButtons.transform, "AllGamesTab", "全部游戏", "Assets/Common/Resources/GameIcons/nav_all_games.png");
            CreateNavButton(bottomNavButtons.transform, "ProfileTab", "成长", "Assets/Common/Resources/GameIcons/nav_growth.png");

            var headerMenu = CreateHeaderMenu(shell.transform);
            headerMenu.transform.SetAsLastSibling();

            PrefabUtility.SaveAsPrefabAsset(hallRoot, HallPrefabPath);
            UnityEngine.Object.DestroyImmediate(hallRoot);
        }

        private static void NormalizeHallRuntimeTemplates()
        {
            var root = PrefabUtility.LoadPrefabContents(HallPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load hall prefab for normalization.");
            }

            try
            {
                var cardTemplate = root.transform.Find("Shell/RuntimeTemplates/CardTemplate");
                if (cardTemplate == null)
                {
                    throw new InvalidOperationException("CardTemplate not found in HallView.");
                }

                var rootButton = cardTemplate.GetComponent<Button>();
                if (rootButton != null)
                {
                    UnityEngine.Object.DestroyImmediate(rootButton, true);
                }

                var action = cardTemplate.Find("Action");
                if (action == null)
                {
                    throw new InvalidOperationException("Action not found under CardTemplate.");
                }

                var actionGraphic = action.GetComponent<RoundedRectGraphic>();
                if (actionGraphic != null)
                {
                    UnityEngine.Object.DestroyImmediate(actionGraphic, true);
                }

                var actionButton = action.GetComponent<Button>();
                if (actionButton == null)
                {
                    actionButton = action.gameObject.AddComponent<Button>();
                }

                var actionBackground = action.Find("Background")?.GetComponent<Image>();
                if (actionBackground == null)
                {
                    throw new InvalidOperationException("Action background not found under CardTemplate.");
                }

                actionButton.targetGraphic = actionBackground;

                PrefabUtility.SaveAsPrefabAsset(root, HallPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureCardBadgesInHallPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(HallPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load hall prefab for card badge update.");
            }

            try
            {
                var cardTemplate = root.transform.Find("Shell/RuntimeTemplates/CardTemplate") as RectTransform;
                if (cardTemplate == null)
                {
                    throw new InvalidOperationException("CardTemplate not found in HallView.");
                }

                ReplaceCardBadges(cardTemplate);

                PrefabUtility.SaveAsPrefabAsset(root, HallPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureCardTitleAndIconInHallPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(HallPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load hall prefab for card layout update.");
            }

            try
            {
                var cardTemplate = root.transform.Find("Shell/RuntimeTemplates/CardTemplate") as RectTransform;
                if (cardTemplate == null)
                {
                    throw new InvalidOperationException("CardTemplate not found in HallView.");
                }

                var title = cardTemplate.Find("Title") as RectTransform;
                if (title == null)
                {
                    throw new InvalidOperationException("Title not found under CardTemplate.");
                }

                title.anchorMin = new Vector2(0.5f, 1f);
                title.anchorMax = new Vector2(0.5f, 1f);
                title.pivot = new Vector2(0.5f, 0.5f);
                title.anchoredPosition = new Vector2(0f, -35f);
                title.sizeDelta = new Vector2(228f, 40f);

                var icon = cardTemplate.Find("Icon") as RectTransform;
                if (icon == null)
                {
                    throw new InvalidOperationException("Icon not found under CardTemplate.");
                }

                icon.anchorMin = new Vector2(0.5f, 0.5f);
                icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = new Vector2(0f, 20f);
                icon.sizeDelta = new Vector2(200f, 150f);

                PrefabUtility.SaveAsPrefabAsset(root, HallPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureHeaderTitleBarInHallPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(HallPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load hall prefab for header title update.");
            }

            try
            {
                var shell = root.transform.Find("Shell");
                if (shell == null)
                {
                    throw new InvalidOperationException("Shell not found in HallView.");
                }

                var existingLegacyTitle = shell.Find("Title");
                if (existingLegacyTitle != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingLegacyTitle.gameObject);
                }

                var existingTitleBar = shell.Find("HeaderTitleBar");
                if (existingTitleBar != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingTitleBar.gameObject);
                }

                var existingMenu = shell.Find("HeaderMenu");
                if (existingMenu != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingMenu.gameObject);
                }

                var titleBar = CreateHeaderTitleBar("HeaderTitleBar");
                titleBar.transform.SetParent(shell, false);
                var titleBarRect = titleBar.GetComponent<RectTransform>();
                titleBarRect.anchorMin = new Vector2(0.5f, 1f);
                titleBarRect.anchorMax = new Vector2(0.5f, 1f);
                titleBarRect.pivot = new Vector2(0.5f, 0.5f);
                titleBarRect.anchoredPosition = new Vector2(0f, -112f);
                titleBarRect.sizeDelta = new Vector2(0f, 0f);

                var title = CreateImage("Title", LoadSprite(TitleAssetPath), true);
                title.transform.SetParent(titleBar.transform, false);
                title.rectTransform.sizeDelta = new Vector2(430f, 104f);
                var titleLayout = title.gameObject.AddComponent<LayoutElement>();
                titleLayout.preferredWidth = 430f;
                titleLayout.preferredHeight = 104f;

                var headerMenu = CreateHeaderMenu(shell);
                headerMenu.transform.SetAsLastSibling();

                PrefabUtility.SaveAsPrefabAsset(root, HallPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject CreateHallGridContent(string name)
        {
            var content = new GameObject(
                name,
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(ContentSizeFitter));
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var grid = content.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.padding = new RectOffset(8, 8, 16, 18);
            grid.spacing = new Vector2(14f, 14f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.cellSize = new Vector2(320f, 451f);

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        private static GameObject CreateProfileContent(string name)
        {
            var content = new GameObject(
                name,
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 16, 16);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        private static void CreateNavButton(Transform parent, string name, string labelText, string iconAssetPath)
        {
            var buttonRoot = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            buttonRoot.transform.SetParent(parent, false);
            buttonRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(236f, 68f);

            var panel = buttonRoot.GetComponent<RoundedRectGraphic>();
            panel.color = new Color(1f, 1f, 1f, 0f);
            panel.CornerRadius = 28f;

            var layoutElement = buttonRoot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 236f;
            layoutElement.preferredHeight = 68f;

            var background = CreateImage("Background", LoadSprite(TabUnselectedAssetPath), false);
            background.transform.SetParent(buttonRoot.transform, false);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var content = CreateRectOnly("Content");
            content.transform.SetParent(buttonRoot.transform, false);
            Stretch(content.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconRoot = CreateRectOnly("IconRoot");
            iconRoot.transform.SetParent(content.transform, false);
            var iconRootElement = iconRoot.AddComponent<LayoutElement>();
            iconRootElement.preferredWidth = 30f;
            iconRootElement.preferredHeight = 30f;

            var icon = CreateImage("Image", LoadSprite(iconAssetPath), true);
            icon.transform.SetParent(iconRoot.transform, false);
            Stretch(icon.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            icon.color = new Color(0.49f, 0.35f, 0.16f, 1f);

            var label = CreateText("Label", labelText, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            label.transform.SetParent(content.transform, false);
            label.color = new Color(0.49f, 0.35f, 0.16f, 1f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 18f;
            var labelElement = label.gameObject.AddComponent<LayoutElement>();
            labelElement.preferredWidth = 146f;
            labelElement.preferredHeight = 36f;
        }

        private static GameObject BuildHallCardTemplate(string name)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Shadow));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(334f, 305f);

            var rootGraphic = root.GetComponent<RoundedRectGraphic>();
            rootGraphic.color = new Color(1f, 1f, 1f, 0f);
            rootGraphic.CornerRadius = 28f;

            var shadow = root.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.33f, 0.41f, 0.18f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var background = CreateImage("Background", LoadSprite(CardAssetPath), false);
            background.transform.SetParent(root.transform, false);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var title = CreateText("Title", "标题", 26, FontStyles.Bold, TextAlignmentOptions.Center);
            title.transform.SetParent(root.transform, false);
            title.color = new Color(0.26f, 0.24f, 0.20f, 1f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 28f;
            title.enableWordWrapping = false;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, -35f);
            titleRect.sizeDelta = new Vector2(228f, 40f);

            var icon = CreateRectOnly("Icon");
            icon.transform.SetParent(root.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 20f);
            iconRect.sizeDelta = new Vector2(200f, 150f);

            CreateFavoriteBadge(root.transform);
            CreateChestBadge(root.transform);

            var action = new GameObject(
                "Action",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Shadow),
                typeof(Button));
            action.transform.SetParent(root.transform, false);
            var actionRect = action.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(0.5f, 0f);
            actionRect.anchorMax = new Vector2(0.5f, 0f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionRect.anchoredPosition = new Vector2(0f, 65f);
            actionRect.sizeDelta = new Vector2(186f, 58f);

            var actionBackground = CreateImage("Background", LoadSprite(ButtonAssetPath), false);
            actionBackground.transform.SetParent(action.transform, false);
            Stretch(actionBackground.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            action.GetComponent<Button>().targetGraphic = actionBackground;

            var actionText = CreateText("ActionText", "开始", 35, FontStyles.Bold, TextAlignmentOptions.Center);
            actionText.transform.SetParent(action.transform, false);
            actionText.color = Color.white;
            Stretch(actionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            actionText.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            actionText.rectTransform.sizeDelta = new Vector2(0f, 10f);

            var costText = CreateText("CostText", "消耗 1体力", 14, FontStyles.Bold, TextAlignmentOptions.Center);
            costText.transform.SetParent(root.transform, false);
            costText.color = new Color(0.63f, 0.47f, 0.18f, 1f);
            var costRect = costText.rectTransform;
            costRect.anchorMin = new Vector2(0.5f, 0f);
            costRect.anchorMax = new Vector2(0.5f, 0f);
            costRect.pivot = new Vector2(0.5f, 0.5f);
            costRect.anchoredPosition = new Vector2(0f, 50f);
            costRect.sizeDelta = new Vector2(160f, 24f);
            costText.gameObject.SetActive(false);
            return root;
        }

        private static GameObject BuildHallProfileTemplate(string name)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Shadow),
                typeof(LayoutElement));
            root.GetComponent<LayoutElement>().preferredHeight = 328f;
            var shadow = root.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.33f, 0.41f, 0.18f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var background = CreateImage("Background", LoadSprite(CardAssetPath), false);
            background.transform.SetParent(root.transform, false);
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var headerRow = CreateRectOnly("HeaderRow");
            headerRow.transform.SetParent(root.transform, false);
            var headerRect = headerRow.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.offsetMin = new Vector2(34f, -96f);
            headerRect.offsetMax = new Vector2(-34f, -34f);

            var headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 8f;
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            var levelText = CreateText("LevelText", "Lv.1", 28, FontStyles.Bold, TextAlignmentOptions.Left);
            levelText.transform.SetParent(headerRow.transform, false);
            levelText.color = new Color(0.28f, 0.24f, 0.18f, 1f);
            var levelElement = levelText.gameObject.AddComponent<LayoutElement>();
            levelElement.flexibleWidth = 1f;
            levelElement.preferredHeight = 36f;

            var expText = CreateText("ExpText", "0/100 EXP", 20, FontStyles.Bold, TextAlignmentOptions.Right);
            expText.transform.SetParent(headerRow.transform, false);
            expText.color = new Color(0.78f, 0.45f, 0.13f, 1f);
            expText.gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;

            var progressRoot = new GameObject(
                "ProgressRoot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            progressRoot.transform.SetParent(root.transform, false);
            var progressRootGraphic = progressRoot.GetComponent<RoundedRectGraphic>();
            progressRootGraphic.color = new Color(0.95f, 0.89f, 0.72f, 0.95f);
            progressRootGraphic.CornerRadius = 18f;
            var progressRootRect = progressRoot.GetComponent<RectTransform>();
            progressRootRect.anchorMin = new Vector2(0f, 1f);
            progressRootRect.anchorMax = new Vector2(1f, 1f);
            progressRootRect.offsetMin = new Vector2(34f, -140f);
            progressRootRect.offsetMax = new Vector2(-34f, -108f);

            var progressFill = new GameObject(
                "ProgressFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            progressFill.transform.SetParent(progressRoot.transform, false);
            var progressFillGraphic = progressFill.GetComponent<RoundedRectGraphic>();
            progressFillGraphic.color = new Color(0.98f, 0.63f, 0.16f, 1f);
            progressFillGraphic.CornerRadius = 14f;
            Stretch(progressFill.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var summaryText = CreateText("SummaryText", "累计经验 0 · 距离下一级还差 100", 18, FontStyles.Bold, TextAlignmentOptions.Left);
            summaryText.transform.SetParent(root.transform, false);
            summaryText.color = new Color(0.36f, 0.30f, 0.22f, 1f);
            var summaryRect = summaryText.rectTransform;
            summaryRect.anchorMin = new Vector2(0f, 1f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.offsetMin = new Vector2(34f, -192f);
            summaryRect.offsetMax = new Vector2(-34f, -152f);

            var hintText = CreateText("HintText", "通过累计金币和宝箱持续积累经验", 16, FontStyles.Normal, TextAlignmentOptions.Left);
            hintText.transform.SetParent(root.transform, false);
            hintText.color = new Color(0.43f, 0.37f, 0.29f, 1f);
            hintText.enableWordWrapping = true;
            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.offsetMin = new Vector2(34f, 28f);
            hintRect.offsetMax = new Vector2(-34f, -206f);
            return root;
        }

        private static GameObject CreateRectOnly(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static GameObject CreateHeaderTitleBar(string name)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return root;
        }

        private static GameObject CreateHeaderMenu(Transform parent)
        {
            var root = new GameObject(
                "HeaderMenu",
                typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var backdrop = new GameObject(
                "Backdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            backdrop.transform.SetParent(root.transform, false);
            Stretch(backdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.SetActive(false);
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.56f);
            backdropImage.raycastTarget = true;
            backdrop.GetComponent<Button>().transition = Selectable.Transition.None;
            backdrop.GetComponent<Button>().targetGraphic = backdropImage;

            var menuButton = CreateImage("MenuButton", LoadSprite(MenuButtonAssetPath), true);
            menuButton.transform.SetParent(root.transform, false);
            var menuButtonRect = menuButton.rectTransform;
            menuButtonRect.anchorMin = new Vector2(0f, 1f);
            menuButtonRect.anchorMax = new Vector2(0f, 1f);
            menuButtonRect.pivot = new Vector2(0.5f, 0.5f);
            menuButtonRect.anchoredPosition = new Vector2(44f, -56f);
            menuButtonRect.sizeDelta = new Vector2(88f, 88f);
            var menuButtonControl = menuButton.gameObject.AddComponent<Button>();
            menuButtonControl.transition = Selectable.Transition.None;
            menuButtonControl.targetGraphic = menuButton;
            var menuButtonLayout = menuButton.gameObject.AddComponent<LayoutElement>();
            menuButtonLayout.preferredWidth = 88f;
            menuButtonLayout.preferredHeight = 88f;

            var menuPanel = new GameObject(
                "MenuPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(LayoutElement));
            menuPanel.transform.SetParent(root.transform, false);
            menuPanel.SetActive(false);
            var menuPanelRect = menuPanel.GetComponent<RectTransform>();
            menuPanelRect.anchorMin = new Vector2(0f, 1f);
            menuPanelRect.anchorMax = new Vector2(0f, 1f);
            menuPanelRect.pivot = new Vector2(0f, 1f);
            menuPanelRect.anchoredPosition = new Vector2(16f, -104f);
            menuPanelRect.sizeDelta = new Vector2(262f, 324f);

            var menuPanelImage = menuPanel.GetComponent<Image>();
            menuPanelImage.sprite = LoadSprite(MenuPanelAssetPath);
            menuPanelImage.type = Image.Type.Sliced;
            menuPanelImage.color = Color.white;
            menuPanelImage.raycastTarget = true;

            PopupPanelTopDecorUtility.CreateMirroredTopDecor(menuPanel.transform, LoadSprite(MenuPanelTopDecorAssetPath), 262f);

            var menuPanelButton = menuPanel.GetComponent<Button>();
            menuPanelButton.transition = Selectable.Transition.None;
            menuPanelButton.targetGraphic = menuPanelImage;

            var menuPanelLayout = menuPanel.GetComponent<VerticalLayoutGroup>();
            menuPanelLayout.padding = new RectOffset(18, 18, 18, 18);
            menuPanelLayout.spacing = 12f;
            menuPanelLayout.childAlignment = TextAnchor.UpperCenter;
            menuPanelLayout.childControlWidth = true;
            menuPanelLayout.childControlHeight = false;
            menuPanelLayout.childForceExpandWidth = true;
            menuPanelLayout.childForceExpandHeight = false;

            var menuFitter = menuPanel.GetComponent<ContentSizeFitter>();
            menuFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            menuFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            menuPanel.GetComponent<LayoutElement>().preferredWidth = 262f;
            menuPanel.GetComponent<LayoutElement>().preferredHeight = 324f;

            CreateMenuEntryButton(menuPanel.transform, "AboutGameButton", "关于游戏");
            CreateMenuEntryButton(menuPanel.transform, "SettingsButton", "设置");
            CreateMenuEntryButton(menuPanel.transform, "GameClubButton", "游戏圈");
            CreateMenuEntryButton(menuPanel.transform, "ShareButton", "分享");

            return root;
        }

        private static void CreateMenuEntryButton(Transform parent, string name, string text)
        {
            var buttonRoot = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            buttonRoot.transform.SetParent(parent, false);

            var buttonRect = buttonRoot.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(0f, 60f);

            var background = buttonRoot.GetComponent<RoundedRectGraphic>();
            background.color = new Color(1f, 0.99f, 0.96f, 1f);
            background.CornerRadius = 18f;
            background.raycastTarget = true;

            var button = buttonRoot.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;

            var label = CreateText("Label", text, 24, FontStyles.Bold, TextAlignmentOptions.Left);
            label.transform.SetParent(buttonRoot.transform, false);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(18f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);
            label.color = new Color(0.35f, 0.28f, 0.18f, 1f);

            buttonRoot.GetComponent<LayoutElement>().preferredHeight = 60f;
        }

        private static GameObject CreateHeaderStats(string name)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform));

            CreateHeaderStatsBackground(root.transform);

            var chestStat = CreateRectOnly("ChestStat");
            chestStat.transform.SetParent(root.transform, false);
            var chestStatRect = chestStat.GetComponent<RectTransform>();
            chestStatRect.anchorMin = new Vector2(0.5f, 0.5f);
            chestStatRect.anchorMax = new Vector2(0.5f, 0.5f);
            chestStatRect.pivot = new Vector2(0.5f, 0.5f);
            chestStatRect.anchoredPosition = Vector2.zero;
            chestStatRect.sizeDelta = new Vector2(170f, 48f);

            var icon = CreateImage("ChestIcon", LoadSprite(ChestIconAssetPath), true);
            icon.transform.SetParent(chestStat.transform, false);
            icon.color = Color.white;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 0f);
            iconRect.sizeDelta = new Vector2(55f, 55f);

            var countText = CreateText("CountText", "0", 28, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            countText.transform.SetParent(chestStat.transform, false);
            countText.color = new Color(0.28f, 0.31f, 0.37f, 1f);
            countText.raycastTarget = false;
            countText.enableWordWrapping = false;
            countText.overflowMode = TextOverflowModes.Overflow;
            countText.alignment = TextAlignmentOptions.MidlineLeft;
            var countTextRect = countText.rectTransform;
            countTextRect.anchorMin = new Vector2(0f, 0.5f);
            countTextRect.anchorMax = new Vector2(0f, 0.5f);
            countTextRect.pivot = new Vector2(0f, 0.5f);
            countTextRect.anchoredPosition = new Vector2(62f, 0f);
            countTextRect.sizeDelta = new Vector2(92f, 34f);

            return root;
        }

        private static GameObject CreateHeaderTagBar(string name)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));

            var background = root.GetComponent<RoundedRectGraphic>();
            background.color = new Color(1f, 0.98f, 0.88f, 0.88f);
            background.CornerRadius = 22f;
            background.raycastTarget = false;

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 7, 7);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var i = 0; i < HeaderTagTextKeys.Length; i++)
            {
                CreateHeaderTagButton(root.transform, i, UiTextCatalog.Get(HeaderTagTextKeys[i]), i == 0);
            }

            return root;
        }

        private static void CreateHeaderTagButton(Transform parent, int index, string label, bool selected)
        {
            var buttonRoot = new GameObject(
                "Tag_" + index,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic),
                typeof(Button),
                typeof(LayoutElement));
            buttonRoot.transform.SetParent(parent, false);

            var buttonRect = buttonRoot.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(selected ? 90f : 76f, 40f);

            var layoutElement = buttonRoot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = buttonRect.sizeDelta.x;
            layoutElement.preferredHeight = buttonRect.sizeDelta.y;

            var graphic = buttonRoot.GetComponent<RoundedRectGraphic>();
            graphic.color = selected ? new Color(1f, 0.62f, 0.14f, 1f) : new Color(1f, 1f, 0.96f, 0.95f);
            graphic.CornerRadius = 18f;
            graphic.raycastTarget = true;

            var button = buttonRoot.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.interactable = false;

            var text = CreateText("Label", label, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            text.transform.SetParent(buttonRoot.transform, false);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            text.color = selected ? Color.white : new Color(0.32f, 0.42f, 0.19f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
        }

        private static void CreateHeaderStatsBackground(Transform parent)
        {
            CreateHeaderStatsLayer(parent, "BackdropOuter", new Vector2(324f, 50f), new Color(0f, 0f, 0f, 0.12f), 18f);
            CreateHeaderStatsLayer(parent, "BackdropMiddle", new Vector2(316f, 46f), new Color(0f, 0f, 0f, 0.20f), 16f);
            CreateHeaderStatsLayer(parent, "BackdropInner", new Vector2(306f, 42f), new Color(0f, 0f, 0f, 0.30f), 14f);
        }

        private static void CreateHeaderStatsLayer(Transform parent, string name, Vector2 size, Color color, float cornerRadius)
        {
            var layer = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            layer.transform.SetParent(parent, false);

            var rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var graphic = layer.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            graphic.CornerRadius = cornerRadius;
        }

        private static void ReplaceCardBadges(RectTransform cardTemplate)
        {
            var favoriteBadge = cardTemplate.Find("FavoriteBadge");
            if (favoriteBadge != null)
            {
                UnityEngine.Object.DestroyImmediate(favoriteBadge.gameObject);
            }

            var chestBadge = cardTemplate.Find("ChestBadge");
            if (chestBadge != null)
            {
                UnityEngine.Object.DestroyImmediate(chestBadge.gameObject);
            }

            CreateFavoriteBadge(cardTemplate);
            CreateChestBadge(cardTemplate);
        }

        private static void CreateFavoriteBadge(Transform parent)
        {
            var badgeObject = new GameObject(
                "FavoriteBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            badgeObject.transform.SetParent(parent, false);

            var badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 1f);
            badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-120f, -30f);
            badgeRect.sizeDelta = new Vector2(40f, 40f);

            var badgeImage = badgeObject.GetComponent<Image>();
            badgeImage.sprite = LoadSprite(FavoriteStarIconAssetPath);
            badgeImage.color = new Color(0.62f, 0.54f, 0.39f, 0.48f);
            badgeImage.preserveAspect = true;
            badgeImage.raycastTarget = true;

            var badgeButton = badgeObject.GetComponent<Button>();
            badgeButton.targetGraphic = badgeImage;
        }

        private static void CreateChestBadge(Transform parent)
        {
            var chestBadge = CreateRectOnly("ChestBadge");
            chestBadge.transform.SetParent(parent, false);
            var chestBadgeRect = chestBadge.GetComponent<RectTransform>();
            chestBadgeRect.anchorMin = new Vector2(1f, 1f);
            chestBadgeRect.anchorMax = new Vector2(1f, 1f);
            chestBadgeRect.pivot = new Vector2(1f, 1f);
            chestBadgeRect.anchoredPosition = new Vector2(8f, -1f);
            chestBadgeRect.sizeDelta = new Vector2(118f, 78f);

            var chestIcon = CreateImage("ChestIcon", LoadSprite(ChestIconAssetPath), true);
            chestIcon.transform.SetParent(chestBadge.transform, false);
            chestIcon.color = Color.white;
            var chestIconRect = chestIcon.rectTransform;
            chestIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            chestIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            chestIconRect.pivot = new Vector2(0.5f, 0.5f);
            chestIconRect.anchoredPosition = new Vector2(-3f, 10f);
            chestIconRect.sizeDelta = new Vector2(55f, 55f);

            var countText = CreateText("CountText", "0", 20, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            countText.transform.SetParent(chestIcon.transform, false);
            countText.color = new Color(1f, 0.97f, 0.9f, 1f);
            countText.raycastTarget = false;
            countText.overflowMode = TextOverflowModes.Overflow;
            countText.enableAutoSizing = true;
            countText.fontSizeMin = 18f;
            countText.fontSizeMax = 40f;
            countText.fontSize = 22.75f;
            var countTextRect = countText.rectTransform;
            countTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            countTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            countTextRect.pivot = new Vector2(0f, 0.5f);
            countTextRect.anchoredPosition = Vector2.zero;
            countTextRect.sizeDelta = new Vector2(72f, 40f);
        }

        private static Image CreateImage(string name, Sprite sprite, bool preserveAspect)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, string content, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.color = Color.white;
            return text;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite not found: " + assetPath);
            }

            return sprite;
        }

        private static void Stretch(RectTransform rectTransform, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var normalized = assetFolder.Replace('\\', '/');
            var parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            var name = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Invalid asset folder: " + assetFolder);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
