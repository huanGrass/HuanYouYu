using System;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Tests
{
    public sealed class MiniGameShellTests
    {
        [Test]
        public void BackgroundVisibilityToggleOnlyAffectsBackgroundNode()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object shell = null;

            try
            {
                shell = CreateShell(rootObject.transform);
                var shellRoot = GetShellRoot(shell);
                Assert.IsNotNull(shellRoot, "MiniGameShell root should exist.");

                var background = shellRoot.transform.Find("Background")?.gameObject;
                var topHost = shellRoot.transform.Find("TopHost")?.gameObject;
                var contentHost = shellRoot.transform.Find("ContentHost")?.gameObject;
                var bottomHost = shellRoot.transform.Find("BottomHost")?.gameObject;
                var popupHost = shellRoot.transform.Find("PopupHost")?.gameObject;
                var pauseButton = shellRoot.transform.Find("PauseButton")?.gameObject;

                Assert.IsNotNull(background, "Background should exist.");
                Assert.IsNotNull(topHost, "TopHost should exist.");
                Assert.IsNotNull(contentHost, "ContentHost should exist.");
                Assert.IsNotNull(bottomHost, "BottomHost should exist.");
                Assert.IsNotNull(popupHost, "PopupHost should exist.");
                Assert.IsNotNull(pauseButton, "PauseButton should exist.");
                Assert.IsTrue(background.activeSelf, "Background should be visible by default.");
                Assert.IsNull(GetShellType().GetMethod("SetPauseButtonVisible", BindingFlags.Instance | BindingFlags.Public), "PauseButton visibility should not be exposed to game views.");

                InvokeShellMethod(shell, "SetBackgroundVisible", false);

                Assert.IsFalse(background.activeSelf, "Background should be hidden after calling SetBackgroundVisible(false).");
                Assert.IsTrue(topHost.activeSelf, "TopHost should remain active.");
                Assert.IsTrue(contentHost.activeSelf, "ContentHost should remain active.");
                Assert.IsTrue(bottomHost.activeSelf, "BottomHost should remain active.");
                Assert.IsTrue(popupHost.activeSelf, "PopupHost should remain active.");
                Assert.IsTrue(pauseButton.activeSelf, "PauseButton should remain active.");

                InvokeShellMethod(shell, "SetBackgroundVisible", true);
                Assert.IsTrue(background.activeSelf, "Background should be visible again after calling SetBackgroundVisible(true).");
            }
            finally
            {
                DisposeShell(shell);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PauseButtonSitsLowerThanTheVeryTopEdge()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object shell = null;

            try
            {
                shell = CreateShell(rootObject.transform);
                var shellRoot = GetShellRoot(shell);
                Assert.IsNotNull(shellRoot, "MiniGameShell root should exist.");

                var pauseButton = shellRoot.transform.Find("PauseButton")?.GetComponent<RectTransform>();
                Assert.IsNotNull(pauseButton, "PauseButton should exist.");
                Assert.LessOrEqual(pauseButton.anchoredPosition.y, -28f, "PauseButton should be shifted down to avoid the top edge.");
            }
            finally
            {
                DisposeShell(shell);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PauseButtonStaysAboveGameplayHostsWhenNoPopupIsOpen()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object shell = null;

            try
            {
                shell = CreateShell(rootObject.transform);
                var shellRoot = GetShellRoot(shell);
                Assert.IsNotNull(shellRoot, "MiniGameShell root should exist.");

                InvokeApplyLayout(shell, new MiniGameShellLayout(MiniGameShellLayout.DefaultTopInset, 430f, MiniGameShellBottomMode.DefaultSlot));

                var pauseButton = shellRoot.transform.Find("PauseButton");
                var topHost = shellRoot.transform.Find("TopHost");
                var contentHost = shellRoot.transform.Find("ContentHost");
                var bottomHost = shellRoot.transform.Find("BottomHost");
                var popupHost = shellRoot.transform.Find("PopupHost");
                Assert.IsNotNull(pauseButton, "PauseButton should exist.");
                Assert.IsNotNull(topHost, "TopHost should exist.");
                Assert.IsNotNull(contentHost, "ContentHost should exist.");
                Assert.IsNotNull(bottomHost, "BottomHost should exist.");
                Assert.IsNotNull(popupHost, "PopupHost should exist.");

                Assert.Greater(pauseButton.GetSiblingIndex(), topHost.GetSiblingIndex(), "PauseButton should sit above the top host.");
                Assert.Greater(pauseButton.GetSiblingIndex(), contentHost.GetSiblingIndex(), "PauseButton should sit above the content host.");
                Assert.Greater(pauseButton.GetSiblingIndex(), bottomHost.GetSiblingIndex(), "PauseButton should sit above the bottom host.");
                Assert.Greater(pauseButton.GetSiblingIndex(), popupHost.GetSiblingIndex(), "PauseButton should sit above the empty popup host.");
            }
            finally
            {
                DisposeShell(shell);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void OpeningPausePopupMovesPopupAbovePauseButton()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object shell = null;

            try
            {
                shell = CreateShell(rootObject.transform);
                var shellRoot = GetShellRoot(shell);
                Assert.IsNotNull(shellRoot, "MiniGameShell root should exist.");

                InvokeShowPausePopup(shell);

                var pauseButton = shellRoot.transform.Find("PauseButton");
                var popupHost = shellRoot.transform.Find("PopupHost");
                Assert.IsNotNull(pauseButton, "PauseButton should exist.");
                Assert.IsNotNull(popupHost, "PopupHost should exist.");
                Assert.Greater(popupHost.GetSiblingIndex(), pauseButton.GetSiblingIndex(), "Active popup host should sit above the pause button.");
                Assert.IsNotNull(popupHost.Find("MiniGamePausePopup"), "Pause popup should be created after clicking pause.");

                InvokeClosePopup(shell);
                Assert.Greater(pauseButton.GetSiblingIndex(), popupHost.GetSiblingIndex(), "PauseButton should return above the empty popup host after closing popup.");
            }
            finally
            {
                DisposeShell(shell);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void LevelSelectWithManyLevelsUsesMaskedScrollViewport()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object levelSelect = null;

            try
            {
                levelSelect = CreateLevelSelect(rootObject.transform, 40);
                var panel = rootObject.transform.Find("LevelSelectTestRoot");
                Assert.IsNotNull(panel, "Level select root should exist.");

                var viewport = panel.Find("Dialog/LevelViewport");
                Assert.IsNotNull(viewport, "Level select should create a viewport.");
                Assert.IsNotNull(viewport.GetComponent<RectMask2D>(), "Level viewport should mask overflowing level buttons.");
                var viewportHitArea = viewport.GetComponent<Graphic>();
                Assert.IsNotNull(viewportHitArea, "Level viewport should catch taps between level buttons.");
                Assert.IsTrue(viewportHitArea.raycastTarget, "Level viewport should block backdrop clicks through button gaps.");
                Assert.AreEqual(0f, viewportHitArea.color.a, 0.001f, "Level viewport hit area should stay invisible.");
                var viewportBlocker = viewport.GetComponent<Button>();
                Assert.IsNotNull(viewportBlocker, "Level viewport should consume click events instead of letting them bubble to the backdrop.");
                Assert.AreSame(viewportHitArea, viewportBlocker.targetGraphic, "Level viewport click consumer should use the invisible hit area.");
                Assert.AreEqual(Selectable.Transition.None, viewportBlocker.transition, "Level viewport click consumer should not show button feedback.");

                var scrollRect = viewport.GetComponent<ScrollRect>();
                Assert.IsNotNull(scrollRect, "Level viewport should be scrollable.");
                Assert.IsFalse(scrollRect.horizontal, "Level select should not scroll horizontally.");
                Assert.IsTrue(scrollRect.vertical, "Level select should scroll vertically.");

                var content = viewport.Find("LevelGrid") as RectTransform;
                Assert.IsNotNull(content, "Level grid content should exist.");
                Assert.AreSame(content, scrollRect.content, "ScrollRect should use LevelGrid as content.");
                Assert.Greater(content.rect.height, ((RectTransform)viewport).rect.height, "Many levels should make the grid taller than the visible viewport.");

                var grid = content.GetComponent<GridLayoutGroup>();
                Assert.IsNotNull(grid, "Level grid should use a GridLayoutGroup.");
                Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint, "Level grid should use a fixed column count.");
                Assert.AreEqual(5, grid.constraintCount, "Level grid should show five levels per row.");
            }
            finally
            {
                DisposeDisposable(levelSelect);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void LevelSelectOpensScrolledToCurrentLateLevel()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object levelSelect = null;

            try
            {
                levelSelect = CreateLevelSelect(rootObject.transform, 60, 49);
                var panel = rootObject.transform.Find("LevelSelectTestRoot");
                Assert.IsNotNull(panel, "Level select root should exist.");

                var scrollRect = panel.Find("Dialog/LevelViewport")?.GetComponent<ScrollRect>();
                Assert.IsNotNull(scrollRect, "Level select should be scrollable.");
                Assert.LessOrEqual(scrollRect.verticalNormalizedPosition, 0.05f, "Opening on level 50 should scroll near the bottom so the current level is visible.");
            }
            finally
            {
                DisposeDisposable(levelSelect);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void WinSettlementRewardRowKeepsCoinRewardLeftAndUsesPlusPrefix()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object settlementView = null;

            try
            {
                settlementView = CreateWinSettlement(
                    rootObject.transform,
                    "WinSettlementLayoutTestRoot",
                    "测试完成",
                    "关卡",
                    "第 2 关",
                    "控制",
                    "6/6",
                    "奖励",
                    "NextLevel",
                    12345,
                    1);

                var panel = rootObject.transform.Find("WinSettlementLayoutTestRoot");
                Assert.IsNotNull(panel, "Win settlement root should exist.");
                var nextLabel = panel.Find("Dialog/NextButton/Label") as RectTransform;
                Assert.IsNotNull(nextLabel, "Primary action label should exist.");
                Assert.AreEqual("下一关", GetTextValue(nextLabel.gameObject), "NextLevel action should use the shared next-level text.");

                var rewardRow = panel.Find("Dialog/RewardRow");
                Assert.IsNotNull(rewardRow, "Reward row should exist.");

                var coinIcon = rewardRow.Find("CoinIcon") as RectTransform;
                var coinText = rewardRow.Find("CoinText") as RectTransform;
                var chestIcon = rewardRow.Find("ChestIcon") as RectTransform;
                var chestText = rewardRow.Find("ChestText") as RectTransform;
                Assert.IsNotNull(coinIcon, "Coin icon should exist.");
                Assert.IsNotNull(coinText, "Coin text should exist.");
                Assert.IsNotNull(chestIcon, "Chest icon should exist.");
                Assert.IsNotNull(chestText, "Chest text should exist.");

                Assert.LessOrEqual(coinIcon.anchoredPosition.x, 106f, "Coin reward should stay shifted left.");
                Assert.LessOrEqual(coinText.anchoredPosition.x + coinText.sizeDelta.x, chestIcon.anchoredPosition.x - (chestIcon.sizeDelta.x * 0.5f), "Coin text area should leave room before the chest icon.");
                Assert.AreEqual("+12345", GetTextValue(coinText.gameObject), "Coin reward should use a plus prefix.");
                Assert.AreEqual("+1", GetTextValue(chestText.gameObject), "Chest reward should use a plus prefix.");
            }
            finally
            {
                DisposeDisposable(settlementView);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void WinSettlementInfoRowsReserveSpaceBetweenLongLabelAndValue()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object settlementView = null;

            try
            {
                settlementView = CreateWinSettlement(
                    rootObject.transform,
                    "WinSettlementInfoRowSpacingTestRoot",
                    "挑战完成",
                    "得分",
                    "120",
                    "命中/失误",
                    "8/12",
                    "奖励",
                    "Retry",
                    16,
                    0);

                var panel = rootObject.transform.Find("WinSettlementInfoRowSpacingTestRoot");
                Assert.IsNotNull(panel, "Win settlement root should exist.");

                var row = panel.Find("Dialog/SecondaryInfoRow");
                Assert.IsNotNull(row, "Secondary info row should exist.");
                var label = row.Find("Label") as RectTransform;
                var value = row.Find("Value") as RectTransform;
                Assert.IsNotNull(label, "Info label should exist.");
                Assert.IsNotNull(value, "Info value should exist.");
                Assert.LessOrEqual(label.anchoredPosition.x + label.sizeDelta.x + 12f, value.offsetMin.x, "Info label should leave a stable gap before the value area.");

                Assert.IsTrue(GetBoolProperty(label.gameObject, "enableAutoSizing"), "Info label should autosize inside its reserved area.");
                Assert.IsTrue(GetBoolProperty(value.gameObject, "enableAutoSizing"), "Info value should autosize inside its reserved area.");
                Assert.AreEqual("命中/失误", GetTextValue(label.gameObject), "Long info label should keep its configured text.");
                Assert.AreEqual("8/12", GetTextValue(value.gameObject), "Info value should keep its configured text.");
            }
            finally
            {
                DisposeDisposable(settlementView);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void WinSettlementUsesTransparentBlockerUntilInputBlockEnds()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object settlementView = null;
            var nextClicks = 0;

            try
            {
                settlementView = CreateWinSettlement(
                    rootObject.transform,
                    "WinSettlementInputBlockTestRoot",
                    "测试完成",
                    "关卡",
                    "第 2 关",
                    "控制",
                    "6/6",
                    "奖励",
                    "NextLevel",
                    12,
                    1,
                    delegate { nextClicks++; },
                    null);

                var nextButton = rootObject.transform.Find("WinSettlementInputBlockTestRoot/Dialog/NextButton")?.GetComponent<Button>();
                Assert.IsNotNull(nextButton, "Next button should exist.");
                Assert.IsTrue(nextButton.interactable, "Next button should remain interactable while transparent blocker catches early taps.");

                var blocker = rootObject.transform.Find("WinSettlementInputBlockTestRoot/InputBlocker");
                Assert.IsNotNull(blocker, "Transparent input blocker should exist.");
                Assert.IsTrue(blocker.gameObject.activeSelf, "Transparent input blocker should start active.");
                Assert.Greater(blocker.GetSiblingIndex(), rootObject.transform.Find("WinSettlementInputBlockTestRoot/Dialog").GetSiblingIndex(), "Transparent input blocker should sit above the dialog.");
                var blockerGraphic = blocker.GetComponent<Graphic>();
                Assert.IsNotNull(blockerGraphic, "Transparent input blocker should have a raycast graphic.");
                Assert.AreEqual(0f, blockerGraphic.color.a, 0.001f, "Transparent input blocker should be invisible.");
                Assert.IsTrue(blockerGraphic.raycastTarget, "Transparent input blocker should catch raycasts.");

                InvokeTick(settlementView, 0.47f);
                Assert.IsTrue(blocker.gameObject.activeSelf, "Transparent input blocker should stay active before the block duration ends.");
                Assert.IsTrue(nextButton.interactable, "Next button should not be disabled during the input block.");

                InvokeTick(settlementView, 0.02f);
                Assert.IsFalse(blocker.gameObject.activeSelf, "Transparent input blocker should hide after the input block.");
                Assert.IsTrue(nextButton.interactable, "Next button should remain interactable after the input block.");
                nextButton.onClick.Invoke();
                Assert.AreEqual(1, nextClicks, "Click after the block duration should run the next-level callback.");
            }
            finally
            {
                DisposeDisposable(settlementView);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void RewardSettlementFailureStyleUsesStatusBadgeWithoutSparkles()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object settlementView = null;

            try
            {
                settlementView = CreateWinSettlement(
                    rootObject.transform,
                    "FailureSettlementStyleTestRoot",
                    "挑战失败",
                    "得分",
                    "120",
                    "进度",
                    "8/12",
                    "奖励",
                    "Retry",
                    16,
                    0,
                    null,
                    null,
                    "Failure");

                var panel = rootObject.transform.Find("FailureSettlementStyleTestRoot");
                Assert.IsNotNull(panel, "Failure settlement root should exist.");
                Assert.IsNull(panel.Find("Dialog/StarBadge"), "Failure style should not use the victory star badge.");
                Assert.IsNotNull(panel.Find("Dialog/StatusBadge"), "Failure style should create a status badge.");
                Assert.IsNull(panel.Find("Dialog/Sparkle_0"), "Failure style should not create celebratory sparkles.");
                var retryLabel = panel.Find("Dialog/NextButton/Label") as RectTransform;
                Assert.IsNotNull(retryLabel, "Retry action label should exist.");
                Assert.AreEqual("再来一局", GetTextValue(retryLabel.gameObject), "Retry action should use the shared retry text.");

                var title = panel.Find("Dialog/Title")?.GetComponent<Graphic>();
                Assert.IsNotNull(title, "Failure style title should exist.");
                Assert.Greater(title.color.r, title.color.g, "Failure style title should use the warmer failure color.");
            }
            finally
            {
                DisposeDisposable(settlementView);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void RewardSettlementBackHallActionUsesSinglePrimaryButton()
        {
            var rootObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            object settlementView = null;
            var hallClicks = 0;
            var nextClicks = 0;

            try
            {
                settlementView = CreateWinSettlement(
                    rootObject.transform,
                    "BackHallSettlementActionTestRoot",
                    "本局结算",
                    "得分",
                    "120",
                    "进度",
                    "8",
                    "奖励",
                    "BackHall",
                    16,
                    0,
                    delegate { nextClicks++; },
                    delegate { hallClicks++; },
                    "Neutral");

                var panel = rootObject.transform.Find("BackHallSettlementActionTestRoot");
                Assert.IsNotNull(panel, "Back hall settlement root should exist.");
                var primaryButton = panel.Find("Dialog/NextButton")?.GetComponent<Button>();
                Assert.IsNotNull(primaryButton, "Primary back hall button should exist.");
                Assert.AreEqual("返回大厅", GetTextValue(primaryButton.transform.Find("Label").gameObject), "BackHall action should use the shared back-hall text.");
                Assert.AreEqual(-428f, primaryButton.GetComponent<RectTransform>().anchoredPosition.y, 0.01f, "BackHall primary button should sit lower when it is the only visible action.");
                var secondaryButton = panel.Find("Dialog/BackHallButton")?.gameObject;
                Assert.IsNotNull(secondaryButton, "Secondary back hall button should still be created for layout consistency.");
                Assert.IsFalse(secondaryButton.activeSelf, "BackHall action should hide the duplicate secondary back hall button.");

                primaryButton.onClick.Invoke();

                Assert.AreEqual(0, nextClicks, "BackHall action should not run the next-level callback.");
                Assert.AreEqual(1, hallClicks, "BackHall action should run the back-hall callback.");
            }
            finally
            {
                DisposeDisposable(settlementView);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static object CreateShell(Transform parent)
        {
            var shellType = GetShellType();
            var constructor = shellType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Transform), typeof(string), typeof(Action), typeof(Func<string>) },
                null);

            Assert.IsNotNull(constructor, "MiniGameShell constructor was not found.");
            return constructor.Invoke(new object[] { parent, "MiniGameShellTestRoot", null, null });
        }

        private static object CreateLevelSelect(Transform parent, int levelCount)
        {
            return CreateLevelSelect(parent, levelCount, 0);
        }

        private static object CreateLevelSelect(Transform parent, int levelCount, int currentLevelIndex)
        {
            var levelSelectType = GetLevelSelectType();
            var createMethod = levelSelectType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(createMethod, "MiniGameLevelSelectView.Create method was not found.");

            return createMethod.Invoke(
                null,
                new object[]
                {
                    parent,
                    null,
                    levelCount,
                    currentLevelIndex,
                    levelCount,
                    "LevelSelectTestRoot",
                    "LevelButton_",
                    null,
                    null
                });
        }

        private static object CreateWinSettlement(
            Transform parent,
            string rootName,
            string title,
            string primaryLabel,
            string primaryValue,
            string secondaryLabel,
            string secondaryValue,
            string rewardLabel,
            string primaryActionName,
            int coinCount,
            int chestCount,
            Action onNextLevel = null,
            Action onBackHall = null,
            string styleName = null)
        {
            var settlementViewType = GetWinSettlementType();
            var panelParamsType = GetRuntimeType("HuanYouYu.MiniGameHall.MiniGameRewardSettlementPanelParams");
            var infoRowType = GetRuntimeType("HuanYouYu.MiniGameHall.MiniGameSettlementInfoRow");
            var infoRowConstructor = infoRowType.GetConstructor(new[] { typeof(string), typeof(string) });
            Assert.IsNotNull(infoRowConstructor, "MiniGameSettlementInfoRow constructor should exist.");

            var panelParams = Activator.CreateInstance(panelParamsType);
            SetField(panelParams, "RootName", rootName);
            if (!string.IsNullOrWhiteSpace(styleName))
            {
                var styleType = GetRuntimeType("HuanYouYu.MiniGameHall.MiniGameRewardSettlementPanelStyle");
                SetField(panelParams, "Style", Enum.Parse(styleType, styleName));
            }

            var primaryActionType = GetRuntimeType("HuanYouYu.MiniGameHall.MiniGameRewardSettlementPrimaryAction");
            SetField(panelParams, "PrimaryAction", Enum.Parse(primaryActionType, primaryActionName));
            SetField(panelParams, "Title", title);
            SetField(panelParams, "PrimaryInfo", infoRowConstructor.Invoke(new object[] { primaryLabel, primaryValue }));
            SetField(panelParams, "SecondaryInfo", infoRowConstructor.Invoke(new object[] { secondaryLabel, secondaryValue }));
            SetField(panelParams, "RewardLabel", rewardLabel);
            SetField(panelParams, "CoinCount", coinCount);
            SetField(panelParams, "ChestCount", chestCount);

            var createMethod = settlementViewType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(createMethod, "MiniGameWinSettlementView.Create method should exist.");
            return createMethod.Invoke(null, new object[] { parent, null, panelParams, onNextLevel, onBackHall });
        }

        private static GameObject GetShellRoot(object shell)
        {
            if (shell == null)
            {
                return null;
            }

            var property = GetShellType().GetProperty("Root", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "MiniGameShell.Root property was not found.");
            return property.GetValue(shell) as GameObject;
        }

        private static void InvokeShellMethod(object shell, string methodName, bool visible)
        {
            var method = GetShellType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "MiniGameShell method was not found: " + methodName);
            method.Invoke(shell, new object[] { visible });
        }

        private static void InvokeTick(object target, float deltaTime)
        {
            var method = target.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "Tick method was not found.");
            method.Invoke(target, new object[] { deltaTime });
        }

        private static void InvokeApplyLayout(object shell, MiniGameShellLayout layout)
        {
            var method = GetShellType().GetMethod("ApplyLayout", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "ApplyLayout method was not found.");
            method.Invoke(shell, new object[] { layout });
        }

        private static void InvokeShowPausePopup(object shell)
        {
            var method = GetShellType().GetMethod("ShowPausePopup", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "ShowPausePopup method was not found.");
            method.Invoke(shell, new object[] { null, null });
        }

        private static void InvokeClosePopup(object shell)
        {
            var method = GetShellType().GetMethod("ClosePopup", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "ClosePopup method was not found.");
            method.Invoke(shell, null);
        }

        private static void DisposeShell(object shell)
        {
            if (shell == null)
            {
                return;
            }

            var disposeMethod = GetShellType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public);
            if (disposeMethod != null)
            {
                disposeMethod.Invoke(shell, null);
            }
        }

        private static void DisposeDisposable(object disposable)
        {
            if (disposable == null)
            {
                return;
            }

            var disposeMethod = disposable.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public);
            if (disposeMethod != null)
            {
                disposeMethod.Invoke(disposable, null);
            }
        }

        private static Type GetShellType()
        {
            var runtimeAssembly = typeof(HuanYouYu.MiniGameHall.MiniGameShellLayout).Assembly;
            var shellType = runtimeAssembly.GetType("HuanYouYu.MiniGameHall.MiniGameShell", true);
            Assert.IsNotNull(shellType, "MiniGameShell type was not found.");
            return shellType;
        }

        private static Type GetLevelSelectType()
        {
            var levelSelectType = GetRuntimeType("HuanYouYu.MiniGameHall.MiniGameLevelSelectView");
            Assert.IsNotNull(levelSelectType, "MiniGameLevelSelectView type was not found.");
            return levelSelectType;
        }

        private static Type GetWinSettlementType()
        {
            var settlementViewType = GetRuntimeType("HuanYouYu.MiniGameHall.MiniGameWinSettlementView");
            Assert.IsNotNull(settlementViewType, "MiniGameWinSettlementView type was not found.");
            return settlementViewType;
        }

        private static Type GetRuntimeType(string typeName)
        {
            var runtimeAssembly = typeof(HuanYouYu.MiniGameHall.MiniGameShellLayout).Assembly;
            return runtimeAssembly.GetType(typeName, true);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }

        private static string GetTextValue(GameObject target)
        {
            var textComponent = target.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(textComponent, "TextMeshProUGUI component should exist.");
            var property = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "TextMeshProUGUI.text property should exist.");
            return property.GetValue(textComponent, null) as string;
        }

        private static bool GetBoolProperty(GameObject target, string propertyName)
        {
            var textComponent = target.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(textComponent, "TextMeshProUGUI component should exist.");
            var property = textComponent.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "TextMeshProUGUI property should exist: " + propertyName);
            return (bool)property.GetValue(textComponent, null);
        }
    }
}
