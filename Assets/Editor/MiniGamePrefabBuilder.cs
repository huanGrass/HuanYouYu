using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall.EditorTools
{
    public static class MiniGamePrefabBuilder
    {
        private const string ShuffleButtonSpritePath = "Assets/Common/Resources/HallTheme/shuffle_button.png";
        private const string HintButtonSpritePath = "Assets/Common/Resources/HallTheme/hint_button.png";
        private const string ClassicLinkSourcePrefabPath = "Assets/Games/ClassicLink/Resources/ClassicLinkView.prefab";
        private const string Match3SourcePrefabPath = "Assets/Games/Match3/Resources/Match3View.prefab";
        private const string ClassicLinkTopPrefabPath = "Assets/Games/ClassicLink/Resources/ClassicLinkTop.prefab";
        private const string ClassicLinkContentPrefabPath = "Assets/Games/ClassicLink/Resources/ClassicLinkContent.prefab";
        private const string ClassicLinkBottomPrefabPath = "Assets/Games/ClassicLink/Resources/ClassicLinkBottom.prefab";
        private const string Match3TopPrefabPath = "Assets/Games/Match3/Resources/Match3Top.prefab";
        private const string Match3ContentPrefabPath = "Assets/Games/Match3/Resources/Match3Content.prefab";
        private const string Match3BottomPrefabPath = "Assets/Games/Match3/Resources/Match3Bottom.prefab";
        private const string SnakeTopPrefabPath = "Assets/Games/Snake/Resources/SnakeTop.prefab";
        private const string SnakeContentPrefabPath = "Assets/Games/Snake/Resources/SnakeContent.prefab";

        [MenuItem("Tools/小游戏/构建界面预制体")]
        public static void BuildAll()
        {
            SplitGamePrefabIfSourceExists(
                ClassicLinkSourcePrefabPath,
                "Shell/TopBar",
                "Shell/BoardArea",
                "Shell/Footer",
                "ClassicLinkTop",
                "ClassicLinkContent",
                "ClassicLinkBottom",
                ClassicLinkTopPrefabPath,
                ClassicLinkContentPrefabPath,
                ClassicLinkBottomPrefabPath);

            SplitGamePrefabIfSourceExists(
                Match3SourcePrefabPath,
                "Shell/TopBar",
                "Shell/BoardFrame",
                "Shell/Footer",
                "Match3Top",
                "Match3Content",
                "Match3Bottom",
                Match3TopPrefabPath,
                Match3ContentPrefabPath,
                Match3BottomPrefabPath);

            BuildTopPrefab(ClassicLinkTopPrefabPath, "ClassicLinkTop", "经典连连看");
            BuildTopPrefab(Match3TopPrefabPath, "Match3Top", "蔬果消消乐");
            BuildBottomPrefab(ClassicLinkBottomPrefabPath, "ClassicLinkBottom");
            BuildBottomPrefab(Match3BottomPrefabPath, "Match3Bottom");

            OptimizeBoardPrefab(ClassicLinkContentPrefabPath, "BoardGrid", "TileTemplate", "Tile_1_1");
            OptimizeBoardPrefab(Match3ContentPrefabPath, "BoardSurface/BoardGrid", "TileTemplate", "MatchTile_0_0");

            DecorateClassicLinkContentPrefab(ClassicLinkContentPrefabPath);
            DecorateMatch3ContentPrefab(Match3ContentPrefabPath);
            BuildSnakePrefabs();

            DeleteAssetIfExists(ClassicLinkSourcePrefabPath);
            DeleteAssetIfExists(Match3SourcePrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MiniGame split prefabs generated for Top/Content/Bottom.");
        }

        public static void BuildAllFromCommandLine()
        {
            BuildAll();
        }

        private static void SplitGamePrefabIfSourceExists(
            string sourcePrefabPath,
            string topPath,
            string contentPath,
            string bottomPath,
            string topName,
            string contentName,
            string bottomName,
            string topOutputPath,
            string contentOutputPath,
            string bottomOutputPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath) == null)
            {
                EnsureRequiredSplitPrefab(topOutputPath);
                EnsureRequiredSplitPrefab(contentOutputPath);
                EnsureRequiredSplitPrefab(bottomOutputPath);
                return;
            }

            SplitGamePrefab(
                sourcePrefabPath,
                topPath,
                contentPath,
                bottomPath,
                topName,
                contentName,
                bottomName,
                topOutputPath,
                contentOutputPath,
                bottomOutputPath);
        }

        private static void SplitGamePrefab(
            string sourcePrefabPath,
            string topPath,
            string contentPath,
            string bottomPath,
            string topName,
            string contentName,
            string bottomName,
            string topOutputPath,
            string contentOutputPath,
            string bottomOutputPath)
        {
            var root = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load prefab: " + sourcePrefabPath);
            }

            try
            {
                SaveSectionPrefab(root.transform.Find(topPath) as RectTransform, topName, topOutputPath);
                SaveSectionPrefab(root.transform.Find(contentPath) as RectTransform, contentName, contentOutputPath);
                SaveSectionPrefab(root.transform.Find(bottomPath) as RectTransform, bottomName, bottomOutputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SaveSectionPrefab(RectTransform source, string targetName, string outputPath)
        {
            if (source == null)
            {
                throw new InvalidOperationException("Missing section for prefab output: " + outputPath);
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));
            var sectionObject = UnityEngine.Object.Instantiate(source.gameObject);
            sectionObject.name = targetName;
            NormalizeStandaloneRect(sectionObject.GetComponent<RectTransform>());

            try
            {
                PrefabUtility.SaveAsPrefabAsset(sectionObject, outputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sectionObject);
            }
        }

        private static void OptimizeBoardPrefab(string prefabPath, string boardPath, string templateName, string fallbackTemplateName)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load prefab: " + prefabPath);
            }

            try
            {
                var board = root.transform.Find(boardPath);
                if (board == null)
                {
                    throw new InvalidOperationException("Board path not found: " + boardPath + " in " + prefabPath);
                }

                var template = board.Find(templateName) ?? board.Find(fallbackTemplateName);
                if (template == null)
                {
                    throw new InvalidOperationException("Template tile not found in: " + prefabPath);
                }

                var staleChildren = new List<GameObject>();
                for (var i = 0; i < board.childCount; i++)
                {
                    var child = board.GetChild(i);
                    if (child != template)
                    {
                        staleChildren.Add(child.gameObject);
                    }
                }

                for (var i = 0; i < staleChildren.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(staleChildren[i]);
                }

                template.name = templateName;
                template.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DecorateClassicLinkContentPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load prefab: " + prefabPath);
            }

            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                NormalizeStandaloneRect(rootRect);

                var boardGrid = root.transform.Find("BoardGrid") as RectTransform;
                var lineLayer = root.transform.Find("LineLayer") as RectTransform;
                if (boardGrid == null || lineLayer == null)
                {
                    throw new InvalidOperationException("ClassicLink content prefab is missing BoardGrid or LineLayer.");
                }

                DeleteChildIfExists(root.transform, "BoardCard");
                DeleteChildIfExists(root.transform, "BoardFrameLight");

                CreateOrUpdatePanel(
                    root.transform,
                    "BoardShadow",
                    new Vector2(0.04f, 0.04f),
                    new Vector2(0.96f, 0.96f),
                    new Vector2(0f, -5f),
                    new Color(0.31f, 0.42f, 0.26f, 0.08f),
                    28f,
                    0);

                CreateOrUpdatePanel(
                    root.transform,
                    "BoardCardFull",
                    new Vector2(0.03f, 0.05f),
                    new Vector2(0.97f, 0.97f),
                    Vector2.zero,
                    new Color(1f, 0.97f, 0.90f, 0.68f),
                    30f,
                    1);

                Stretch(boardGrid, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f), Vector2.zero, Vector2.zero);
                Stretch(lineLayer, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), Vector2.zero, Vector2.zero);
                lineLayer.SetAsLastSibling();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DecorateMatch3ContentPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("Failed to load prefab: " + prefabPath);
            }

            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                NormalizeStandaloneRect(rootRect);
                var rootGraphic = root.GetComponent<RoundedRectGraphic>();

                var boardSurface = root.transform.Find("BoardSurface") as RectTransform;
                var boardGrid = root.transform.Find("BoardSurface/BoardGrid") as RectTransform;
                var animationLayer = root.transform.Find("BoardSurface/AnimationLayer") as RectTransform;
                if (boardSurface == null || boardGrid == null || animationLayer == null)
                {
                    throw new InvalidOperationException("Match3 content prefab is missing BoardSurface, BoardGrid or AnimationLayer.");
                }

                DeleteChildIfExists(root.transform, "BoardCard");
                DeleteChildIfExists(root.transform, "BoardCardFull");

                if (rootGraphic != null)
                {
                    rootGraphic.color = new Color(0f, 0f, 0f, 0f);
                    rootGraphic.CornerRadius = 0f;
                    rootGraphic.raycastTarget = false;
                }

                CreateOrUpdatePanel(
                    root.transform,
                    "BoardShadow",
                    new Vector2(0.18f, 0.12f),
                    new Vector2(0.82f, 0.88f),
                    new Vector2(0f, -4f),
                    new Color(0.31f, 0.42f, 0.26f, 0.09f),
                    28f,
                    0);

                CreateOrUpdatePanel(
                    root.transform,
                    "BoardFrameLight",
                    new Vector2(0.16f, 0.10f),
                    new Vector2(0.84f, 0.90f),
                    Vector2.zero,
                    new Color(1f, 0.985f, 0.94f, 0.18f),
                    30f,
                    1);

                Stretch(boardSurface, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);

                var boardSurfaceGraphic = boardSurface.GetComponent<RoundedRectGraphic>();
                if (boardSurfaceGraphic == null)
                {
                    boardSurfaceGraphic = boardSurface.gameObject.AddComponent<RoundedRectGraphic>();
                }

                boardSurfaceGraphic.color = new Color(0f, 0f, 0f, 0f);
                boardSurfaceGraphic.CornerRadius = 0f;
                boardSurfaceGraphic.raycastTarget = false;

                Stretch(boardGrid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Stretch(animationLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                animationLayer.SetAsLastSibling();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildTopPrefab(string outputPath, string rootName, string title)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));

            var root = new GameObject(rootName, typeof(RectTransform));
            NormalizeStandaloneRect(root.GetComponent<RectTransform>());

            try
            {
                CreateOrUpdatePanel(
                    root.transform,
                    "HeaderShadow",
                    new Vector2(0.23f, 0.22f),
                    new Vector2(0.77f, 0.84f),
                    new Vector2(0f, -4f),
                    new Color(0.31f, 0.42f, 0.26f, 0.10f),
                    28f,
                    0);

                var header = new GameObject(
                    "Header",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RoundedRectGraphic),
                    typeof(VerticalLayoutGroup),
                    typeof(LayoutElement));
                header.transform.SetParent(root.transform, false);
                header.transform.SetAsLastSibling();

                var headerRect = header.GetComponent<RectTransform>();
                Stretch(headerRect, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.86f), Vector2.zero, Vector2.zero);

                var headerGraphic = header.GetComponent<RoundedRectGraphic>();
                headerGraphic.color = new Color(1f, 0.98f, 0.92f, 0.68f);
                headerGraphic.CornerRadius = 28f;
                headerGraphic.raycastTarget = false;

                var headerLayout = header.GetComponent<VerticalLayoutGroup>();
                headerLayout.padding = new RectOffset(22, 22, 14, 14);
                headerLayout.spacing = 2f;
                headerLayout.childAlignment = TextAnchor.MiddleCenter;
                headerLayout.childControlWidth = true;
                headerLayout.childControlHeight = false;
                headerLayout.childForceExpandWidth = true;
                headerLayout.childForceExpandHeight = false;

                header.GetComponent<LayoutElement>().preferredHeight = 96f;

                var titleText = CreateText("Title", title, 33, FontStyles.Bold, TextAlignmentOptions.Center);
                titleText.transform.SetParent(header.transform, false);
                titleText.color = new Color(0.29f, 0.39f, 0.22f, 1f);
                titleText.enableWordWrapping = false;
                titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

                var scoreText = CreateText("Score", "分数 0", 24, FontStyles.Bold, TextAlignmentOptions.Center);
                scoreText.transform.SetParent(header.transform, false);
                scoreText.color = new Color(0.82f, 0.58f, 0.25f, 1f);
                scoreText.enableWordWrapping = false;
                scoreText.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildBottomPrefab(string outputPath, string rootName)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));

            var root = new GameObject(rootName, typeof(RectTransform), typeof(LayoutElement));
            NormalizeStandaloneRect(root.GetComponent<RectTransform>());

            try
            {
                var shuffleSprite = LoadRequiredSprite(ShuffleButtonSpritePath);
                var hintSprite = LoadRequiredSprite(HintButtonSpritePath);

                root.GetComponent<LayoutElement>().preferredHeight = 144f;

                var actionBar = new GameObject(
                    "ActionBar",
                    typeof(RectTransform),
                    typeof(HorizontalLayoutGroup),
                    typeof(ContentSizeFitter));
                actionBar.transform.SetParent(root.transform, false);
                actionBar.transform.SetAsLastSibling();

                var actionBarRect = actionBar.GetComponent<RectTransform>();
                actionBarRect.anchorMin = new Vector2(0.5f, 0.5f);
                actionBarRect.anchorMax = new Vector2(0.5f, 0.5f);
                actionBarRect.pivot = new Vector2(0.5f, 0.5f);
                actionBarRect.anchoredPosition = new Vector2(0f, 4f);
                actionBarRect.sizeDelta = new Vector2(216f, 88f);

                var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 32f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                var fitter = actionBar.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                CreateOrUpdateBarBackground(
                    actionBar.transform,
                    "TrayShadow",
                    new Vector2(26f, 14f),
                    -4f,
                    new Color(0.31f, 0.42f, 0.26f, 0.10f),
                    34f,
                    0);

                CreateOrUpdateBarBackground(
                    actionBar.transform,
                    "ActionTray",
                    new Vector2(24f, 12f),
                    0f,
                    new Color(1f, 0.98f, 0.92f, 0.66f),
                    32f,
                    1);

                BuildActionButton(actionBar.transform, "ShuffleButton", shuffleSprite);
                BuildActionButton(actionBar.transform, "HintButton", hintSprite);

                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildSnakePrefabs()
        {
            BuildRootOnlyPrefab(SnakeTopPrefabPath, "SnakeTop");
            BuildRootOnlyPrefab(SnakeContentPrefabPath, "SnakeContent");
        }

        private static void BuildRootOnlyPrefab(string outputPath, string rootName)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(outputPath)?.Replace("\\", "/"));

            var root = new GameObject(rootName, typeof(RectTransform));
            NormalizeStandaloneRect(root.GetComponent<RectTransform>());

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildActionButton(Transform parent, string name, Sprite iconSprite)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(84f, 84f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            button.colors = colors;

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 84f;
            layout.preferredHeight = 84f;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);

            var iconRect = iconObject.GetComponent<RectTransform>();
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            button.targetGraphic = iconImage;
        }

        private static void DeleteChildIfExists(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static GameObject CreateOrUpdatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredOffset,
            Color color,
            float cornerRadius,
            int siblingIndex)
        {
            var panel = parent.Find(name)?.gameObject;
            if (panel == null)
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
                panel.transform.SetParent(parent, false);
            }

            var rect = panel.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = anchoredOffset;
            rect.SetSiblingIndex(siblingIndex);

            var graphic = panel.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            return panel;
        }

        private static GameObject CreateOrUpdateBarBackground(
            Transform parent,
            string name,
            Vector2 padding,
            float yOffset,
            Color color,
            float cornerRadius,
            int siblingIndex)
        {
            var panel = parent.Find(name)?.gameObject;
            if (panel == null)
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(LayoutElement));
                panel.transform.SetParent(parent, false);
            }

            var rect = panel.GetComponent<RectTransform>();
            Stretch(
                rect,
                Vector2.zero,
                Vector2.one,
                new Vector2(-padding.x, -padding.y + yOffset),
                new Vector2(padding.x, padding.y + yOffset));
            rect.SetSiblingIndex(siblingIndex);

            var graphic = panel.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;

            var layout = panel.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;
            return panel;
        }

        private static TextMeshProUGUI CreateText(string name, string content, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private static Sprite LoadRequiredSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Sprite not found: " + assetPath);
            }

            return sprite;
        }

        private static void EnsureRequiredSplitPrefab(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
            {
                throw new InvalidOperationException("Split prefab not found and source prefab is unavailable: " + assetPath);
            }
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void NormalizeStandaloneRect(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var folderName = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("Invalid folder path: " + assetPath);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
