using System;
using System.Collections;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public class MiniGameHallFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            ResetProgress();
        }

        [UnityTest]
        public IEnumerator FirstLaunchSeedsDefaultFavorites()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsTrue(controller.IsFavorite("classic-link"), "First launch should seed Classic Link as a default favorite.");
            Assert.IsTrue(controller.IsFavorite("game2048"), "First launch should seed 2048 as a default favorite.");
            Assert.IsTrue(controller.IsFavorite("match-3"), "First launch should seed Match-3 as a default favorite.");
            Assert.IsTrue(controller.IsFavorite("water-sort"), "First launch should seed Water Sort as a default favorite.");

            Assert.IsNotNull(GameObject.Find("classic-link_Card"), "Favorites tab should show Classic Link by default.");
            Assert.IsNotNull(GameObject.Find("game2048_Card"), "Favorites tab should show 2048 by default.");
            Assert.IsNotNull(GameObject.Find("match-3_Card"), "Favorites tab should show Match-3 by default.");
            Assert.IsNotNull(GameObject.Find("water-sort_Card"), "Favorites tab should show Water Sort by default.");
            Assert.IsNull(GameObject.Find("more-games-in-progress_Card"), "Favorites tab should not show the more-games prompt card.");
        }

        [UnityTest]
        public IEnumerator HallCardsLoadDedicatedIconsAndMoreGamesPromptCard()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before validating card icons.");

            AssertCardIconTextureName("classic-link_Card", "classic_link");
            AssertCardIconTextureName("game2048_Card", "game2048");
            AssertCardIconTextureName("match-3_Card", "match_3");
            AssertCardIconTextureName("water-sort_Card", "water-sort");

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            AssertCardIconTextureName("tetris_Card", "tetris");
            AssertCardIconTextureName("watermelon-merge_Card", "watermelon-merge");
            AssertCardIconTextureName("water-sort_Card", "water-sort");
            AssertCardIconTextureName("memory-flip_Card", "memory-flip");
            AssertCardIconTextureName("breakout_Card", "breakout");
            AssertCardIconTextureName("goldminer_Card", "goldminer");
            AssertCardIconTextureName("gomoku_Card", "gomoku");
            AssertCardIconTextureName("minesweeper_Card", "minesweeper");
            AssertCardIconTextureName("needlehit_Card", "needlehit");
            AssertCardIconTextureName("reversi_Card", "reversi");
            AssertCardIconTextureName("nonogram_Card", "nonogram");
            AssertCardIconTextureName("jumpjump_Card", "jumpjump");
            AssertCardIconTextureName("whacamole_Card", "whacamole");
            AssertCardIconTextureName("lightsout_Card", "lightsout");
            AssertCardIconTextureName("rivercrossing_Card", "rivercrossing");
            AssertCardIconTextureName("slidingpuzzle_Card", "slidingpuzzle");
            AssertCardIconTextureName("towerofhanoi_Card", "towerofhanoi");
            AssertCardIconTextureName("waterpouring_Card", "waterpouring");
            AssertCardIconTextureName("control-point_Card", "control-point");
            AssertCardIconTextureName("bulls-cows_Card", "bulls-cows");
            AssertCardIconTextureName("arrow-escape_Card", "arrow-escape");
            AssertCardIconTextureName("more-games-in-progress_Card", "more_games_in_progress");
            Assert.IsNull(GameObject.Find("point-defense_Card"), "Point Defense card should no longer exist in all games.");
            Assert.IsNull(GameObject.Find("star-farm_Card"), "Star Farm card should no longer exist in all games.");

            var promptCard = GameObject.Find("more-games-in-progress_Card");
            Assert.IsNotNull(promptCard, "More-games prompt card should exist in all games.");
            Assert.IsNotNull(promptCard.transform.parent, "More-games prompt card should be mounted under a slot.");
            Assert.AreEqual(promptCard.transform.parent.parent.childCount - 1, promptCard.transform.parent.GetSiblingIndex(), "More-games prompt card should be appended to the end of all games.");
        }

        [UnityTest]
        public IEnumerator HallMenuButtonSitsLowerThanTheVeryTopEdge()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before checking the menu button.");

            var menuButton = GameObject.Find("HallView")?.transform.Find("Shell/HeaderMenu/MenuButton") as RectTransform;
            Assert.IsNotNull(menuButton, "Hall menu button should exist.");
            Assert.LessOrEqual(menuButton.anchoredPosition.y, -66f, "Hall menu button should be shifted down to avoid the top edge.");
        }

        [UnityTest]
        public IEnumerator HallDoesNotShowTutorialOverlay()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before checking tutorial absence.");
            Assert.IsNull(GameObject.Find("MiniGameTutorialOverlay"), "Hall should not show a tutorial overlay.");
        }

        [UnityTest]
        public IEnumerator Game2048DoesNotShowTutorialOverlay()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);
            Assert.IsNotNull(controller, "Hall controller should load before entering 2048.");

            controller.EnterGame(Game2048View.GameIdConstant);
            yield return null;

            Assert.IsNull(GameObject.Find("MiniGameTutorialOverlay"), "2048 should not show a tutorial overlay.");
        }

        [UnityTest]
        public IEnumerator HallMenuContainsShareButtonAndCanInvokeIt()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before checking the share button.");

            var shareButton = GameObject.Find("HallView")?.transform.Find("Shell/HeaderMenu/MenuPanel/ShareButton")?.GetComponent<Button>();
            Assert.IsNotNull(shareButton, "Hall share button should exist.");

            var shareLabel = shareButton.transform.Find("Label")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(shareLabel, "Hall share button should expose a TMP label.");
            var textProperty = shareLabel.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Hall share button label should expose a text property.");
            Assert.AreEqual("分享", textProperty.GetValue(shareLabel, null) as string, "Hall share button should use the shared Chinese label.");

            shareButton.onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HallMenuContainsGameClubButtonAndCanInvokeIt()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before checking the game club button.");

            var gameClubButton = GameObject.Find("HallView")?.transform.Find("Shell/HeaderMenu/MenuPanel/GameClubButton")?.GetComponent<Button>();
            Assert.IsNotNull(gameClubButton, "Hall game club button should exist.");

            var gameClubLabel = gameClubButton.transform.Find("Label")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(gameClubLabel, "Hall game club button should expose a TMP label.");
            var textProperty = gameClubLabel.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Hall game club button label should expose a text property.");
            Assert.AreEqual("游戏圈", textProperty.GetValue(gameClubLabel, null) as string, "Hall game club button should use the shared Chinese label.");

            gameClubButton.onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AboutMenuOpensAnnouncementPopupAndCloseButtonDismissesIt()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before opening the announcement popup.");

            var aboutButton = GameObject.Find("HallView")?.transform.Find("Shell/HeaderMenu/MenuPanel/AboutGameButton")?.GetComponent<Button>();
            Assert.IsNotNull(aboutButton, "Hall about button should exist.");
            AssertMenuButtonLabel(aboutButton.transform, "公告");

            aboutButton.onClick.Invoke();
            yield return null;

            var overlay = GameObject.Find("HallOverlay");
            Assert.IsNotNull(overlay, "Hall overlay should exist for announcement rendering.");

            var popup = overlay.transform.Find("AnnouncementPopup");
            Assert.IsNotNull(popup, "About menu should open the generated announcement popup.");

            Assert.IsNotNull(popup.Find("Dialog/Sidebar/Tab_RecentUpdates"), "Announcement popup should include the recent updates tab.");
            Assert.IsNotNull(popup.Find("Dialog/Sidebar/Tab_AboutGame"), "Announcement popup should include the about game tab.");
            Assert.IsNotNull(popup.Find("Dialog/Sidebar/Tab_Credits"), "Announcement popup should include the credits tab.");
            Assert.IsNull(popup.Find("Dialog/Sidebar/Tab_Feedback"), "Announcement popup should not include the feedback tab.");
            Assert.IsNull(popup.Find("Dialog/Sidebar/Tab_Preview"), "Announcement popup should not include the preview tab.");
            Assert.IsNull(popup.Find("Dialog/Sidebar/Tab_Events"), "Announcement popup should not include the events tab.");
            var latestUpdateCard = popup.Find("Dialog/ContentFrame/Viewport/Content/VersionCard_20260610");
            var mayUpdateCard = popup.Find("Dialog/ContentFrame/Viewport/Content/VersionCard_20260505");
            var firstUpdateCard = popup.Find("Dialog/ContentFrame/Viewport/Content/VersionCard_20260501");
            Assert.IsNotNull(latestUpdateCard, "Announcement popup should render the latest update card by default.");
            Assert.IsNotNull(mayUpdateCard, "Announcement popup should render the 2026-05-05 update card.");
            Assert.IsNotNull(firstUpdateCard, "Announcement popup should render the 2026-05-01 update card.");
            AssertAnnouncementText(popup.Find("Dialog/ContentFrame/Viewport/Content/TitleRow/Title"), "最近更新");
            AssertAnnouncementText(latestUpdateCard.Find("VersionBadge/Label"), "2026-06-10");
            AssertAnnouncementText(latestUpdateCard.Find("Body"), "优化大厅体验");
            AssertAnnouncementText(latestUpdateCard.Find("Body"), "连连看新增首局提示");
            AssertAnnouncementText(mayUpdateCard.Find("VersionBadge/Label"), "2026-05-05");
            AssertAnnouncementText(mayUpdateCard.Find("Body"), "叠牌消消");
            AssertAnnouncementText(mayUpdateCard.Find("Body"), "游戏圈");
            AssertAnnouncementText(firstUpdateCard.Find("VersionBadge/Label"), "2026-05-01");
            AssertAnnouncementText(firstUpdateCard.Find("Body"), "点灯谜题");
            Assert.IsNotNull(popup.Find("Dialog/ContentFrame")?.GetComponent<ScrollRect>(), "Announcement content should be scrollable for multiple versions.");
            Assert.IsTrue(popup.Find("Dialog/ContentFrame/Viewport")?.GetComponent<Image>()?.raycastTarget, "Announcement viewport should receive drag events for scrolling.");
            Assert.IsNotNull(popup.Find("Dialog/ContentFrame/Viewport")?.GetComponent<RectMask2D>(), "Announcement viewport should clip scrolled content inside the popup.");
            Assert.IsNull(popup.Find("Dialog/ContentFrame/Viewport/Content/Footer"), "Announcement footer should stay outside clipped scroll content.");
            var fixedFooter = popup.Find("Dialog/Footer") as RectTransform;
            Assert.IsNotNull(fixedFooter, "Announcement footer should stay fixed inside the popup.");
            AssertAnnouncementText(fixedFooter.Find("Label"), "小游戏还在持续更新中");
            AssertFooterPosition(fixedFooter);
            AssertFooterLeafAnchored(fixedFooter, "FooterLeafLeft", false);
            AssertFooterLeafAnchored(fixedFooter, "FooterLeafRight", true);

            var aboutTab = popup.Find("Dialog/Sidebar/Tab_AboutGame")?.GetComponent<Button>();
            Assert.IsNotNull(aboutTab, "Announcement about game tab should be clickable.");
            aboutTab.onClick.Invoke();
            yield return null;
            Assert.IsNull(popup.Find("Dialog/Footer"), "Announcement fixed footer should only appear on recent updates.");
            AssertAnnouncementText(popup.Find("Dialog/ContentFrame/Viewport/Content/TitleRow/Title"), "关于游戏");
            var aboutTextBlock = popup.Find("Dialog/ContentFrame/Viewport/Content/PlainTextBlock") as RectTransform;
            AssertAnnouncementText(aboutTextBlock, "轻量玩法合集");
            AssertAnnouncementText(aboutTextBlock, "欢迎加入游戏圈反馈问题、分享建议");
            Assert.Greater(aboutTextBlock?.rect.height ?? 0f, 320f, "About game content should fill the announcement content area.");
            Assert.Greater(aboutTextBlock?.rect.width ?? 0f, 260f, "About game content should use the same broad text area as recent updates.");
            var announcementGameClubButton = popup.Find("Dialog/ContentFrame/Viewport/Content/GameClubButton")?.GetComponent<Button>();
            Assert.IsNotNull(announcementGameClubButton, "Announcement about game tab should include a game club button.");
            AssertAnnouncementText(announcementGameClubButton.transform.Find("Label"), "进入游戏圈");
            announcementGameClubButton.onClick.Invoke();
            yield return null;

            var creditsTab = popup.Find("Dialog/Sidebar/Tab_Credits")?.GetComponent<Button>();
            Assert.IsNotNull(creditsTab, "Announcement credits tab should be clickable.");
            creditsTab.onClick.Invoke();
            yield return null;
            AssertAnnouncementText(popup.Find("Dialog/ContentFrame/Viewport/Content/TitleRow/Title"), "共创名单");
            var creditsTextBlock = popup.Find("Dialog/ContentFrame/Viewport/Content/PlainTextBlock") as RectTransform;
            AssertAnnouncementText(creditsTextBlock, "幻之小草");
            Assert.Greater(creditsTextBlock?.rect.height ?? 0f, 320f, "Credits content should fill the announcement content area.");
            Assert.Greater(creditsTextBlock?.rect.width ?? 0f, 260f, "Credits content should use the same broad text area as recent updates.");
            AssertAnnouncementText(popup.Find("Dialog/ContentFrame/Viewport/Content/CreditsNote/Label"), "注：如需加入名单");

            Assert.IsNotNull(popup.Find("Dialog/CloseButton/Inner"), "Announcement close button should use a circular inner fill.");
            Assert.IsNotNull(popup.Find("Dialog/CloseButton/StrokeA"), "Announcement close button should use straight X strokes.");
            Assert.IsNotNull(popup.Find("Dialog/CloseButton/StrokeB"), "Announcement close button should use straight X strokes.");

            var closeButton = popup.Find("Dialog/CloseButton")?.GetComponent<Button>();
            Assert.IsNotNull(closeButton, "Announcement popup should expose a close button.");

            closeButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(overlay.transform.Find("AnnouncementPopup"), "Close button should dismiss the announcement popup.");
        }

        [UnityTest]
        public IEnumerator AnnouncementPopupFitsNarrowWideCanvas()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var canvas = GameObject.Find("MiniGameCanvas");
            Assert.IsNotNull(canvas, "Hall canvas should exist.");

            var canvasRect = canvas.GetComponent<RectTransform>();
            Assert.IsNotNull(canvasRect, "Hall canvas should expose a RectTransform.");
            canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 430f);
            canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 900f);
            Canvas.ForceUpdateCanvases();

            var aboutButton = GameObject.Find("HallView")?.transform.Find("Shell/HeaderMenu/MenuPanel/AboutGameButton")?.GetComponent<Button>();
            Assert.IsNotNull(aboutButton, "Hall about button should exist.");
            aboutButton.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var overlay = GameObject.Find("HallOverlay");
            Assert.IsNotNull(overlay, "Hall overlay should exist for announcement rendering.");

            var popup = overlay.transform.Find("AnnouncementPopup");
            Assert.IsNotNull(popup, "About menu should open the generated announcement popup.");

            var dialog = popup.Find("Dialog") as RectTransform;
            var contentFrame = popup.Find("Dialog/ContentFrame") as RectTransform;
            var updateCard = popup.Find("Dialog/ContentFrame/Viewport/Content/VersionCard_20260610") as RectTransform;
            var fixedFooter = popup.Find("Dialog/Footer") as RectTransform;
            Assert.IsNotNull(dialog, "Announcement dialog should exist.");
            Assert.IsNotNull(contentFrame, "Announcement content frame should exist.");
            Assert.IsNotNull(updateCard, "Announcement latest update card should be visible.");
            Assert.IsNotNull(fixedFooter, "Announcement footer should remain visible outside the clipped scroll area.");
            Assert.LessOrEqual(dialog.rect.width, 430f - 52f - 20f + 0.1f, "Announcement dialog should clamp to narrow canvas width.");
            Assert.Greater(contentFrame.rect.width, 120f, "Announcement content area should keep enough width on narrow canvas.");
            Assert.Greater(updateCard.rect.width, 100f, "Announcement update card should remain measurable on narrow canvas.");
            Assert.Greater(fixedFooter.rect.height, 40f, "Announcement fixed footer should keep enough vertical space on narrow canvas.");
            AssertAnnouncementText(updateCard.Find("Body"), "优化大厅体验");
            AssertAnnouncementText(fixedFooter.Find("Label"), "小游戏还在持续更新中");
            AssertFooterPosition(fixedFooter);
        }

        [UnityTest]
        public IEnumerator CanEnterAndSettleGameThenReturnToHall()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame("classic-link");
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "Classic Link game should be active.");
            Assert.AreEqual("classic-link", controller.ActiveGameId);

            controller.CompleteCurrentGame(new MiniGameSettlement
            {
                Score = 7,
                ChestCount = 2,
                Summary = "测试结算"
            });
            yield return null;

            Assert.IsFalse(controller.HasActiveGame, "Game should be disposed after settlement.");
            Assert.IsTrue(controller.IsHallVisible, "Hall should be visible after settlement.");

            var progress = controller.GetProgress("classic-link");
            Assert.AreEqual(1, progress.PlayCount);
            Assert.AreEqual(7, progress.BestScore);
            Assert.AreEqual(2, progress.TotalChestCount);
            Assert.AreEqual(0, progress.TotalCoinCount);

            var cardObject = GameObject.Find("classic-link_Card");
            Assert.IsNotNull(cardObject, "Classic Link card should still be visible in hall.");

            var chestBadge = cardObject.transform.Find("ChestBadge") as RectTransform;
            Assert.IsNotNull(chestBadge, "Card should expose a chest badge.");
            Assert.AreEqual(1f, chestBadge.anchorMin.x, 0.001f, "Chest badge should anchor to the right edge of the card.");
            Assert.Less(chestBadge.anchoredPosition.y, 0f, "Chest badge should sit near the top edge of the card.");

            var chestCountText = cardObject.transform.Find("ChestBadge/ChestIcon/CountText")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(chestCountText, "Chest badge should expose a count label.");
            var textProperty = chestCountText.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Chest badge count label should expose a text property.");
            Assert.AreEqual("2", textProperty.GetValue(chestCountText, null) as string, "Chest badge should show the accumulated chest count.");

            AssertHeaderStatsVisible();

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsNull(GameObject.Find("HeaderStats"), "All games page should hide the header chest and coin strip.");
            AssertHeaderTagBarVisible();

            var tagBar = GameObject.Find("HeaderTagBar");
            var allTagGraphic = tagBar.transform.Find("Tag_0")?.GetComponent<RoundedRectGraphic>();
            var eliminateTagButton = tagBar.transform.Find("Tag_1")?.GetComponent<Button>();
            var eliminateTagGraphic = tagBar.transform.Find("Tag_1")?.GetComponent<RoundedRectGraphic>();
            Assert.IsNotNull(allTagGraphic, "All tag should expose a graphic.");
            Assert.IsNotNull(eliminateTagButton, "Eliminate tag should expose a clickable button.");
            Assert.IsNotNull(eliminateTagGraphic, "Eliminate tag should expose a graphic.");

            var allTagSelectedColor = allTagGraphic.color;
            var eliminateTagUnselectedColor = eliminateTagGraphic.color;
            eliminateTagButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(eliminateTagUnselectedColor, allTagGraphic.color, "Clicking a tag should unselect the previous tag.");
            Assert.AreEqual(allTagSelectedColor, eliminateTagGraphic.color, "Clicking a tag should select the clicked tag.");
            Assert.IsNotNull(GameObject.Find("classic-link_Card"), "Eliminate tag should keep eliminate games visible.");
            Assert.IsNull(GameObject.Find("sudoku_Card"), "Eliminate tag should hide number games.");
            Assert.IsNull(GameObject.Find("snake_Card"), "Eliminate tag should hide action games.");
        }

        [UnityTest]
        public IEnumerator ClickingChestBadgeShowsChestToast()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame("classic-link");
            yield return null;

            controller.CompleteCurrentGame(new MiniGameSettlement
            {
                Score = 7,
                ChestCount = 2,
                CoinCount = 35,
                Summary = "测试结算"
            });
            yield return null;

            var cardObject = GameObject.Find("classic-link_Card");
            Assert.IsNotNull(cardObject, "Classic Link card should still be visible in hall.");

            var chestBadgeButton = cardObject.transform.Find("ChestBadge")?.GetComponent<Button>();
            Assert.IsNotNull(chestBadgeButton, "Chest badge button should exist on the card.");

            chestBadgeButton.onClick.Invoke();
            yield return null;

            var overlay = GameObject.Find("HallOverlay");
            Assert.IsNotNull(overlay, "Hall overlay should exist for toast rendering.");

            var toast = overlay.transform.Find("ChestToast");
            Assert.IsNotNull(toast, "Clicking the chest badge should show a toast.");
            Assert.IsTrue(toast.gameObject.activeSelf, "Chest toast should be visible immediately after clicking.");

            var messageText = toast.Find("Message")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(messageText, "Chest toast message text was not found.");
            var textProperty = messageText.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Chest toast message text should expose a text property.");
            Assert.AreEqual("已从该玩法累计获得 2 个宝箱，35 金币", textProperty.GetValue(messageText, null) as string, "Chest toast should reflect the accumulated chest and coin counts.");

            yield return new WaitForSecondsRealtime(1.8f);
            Assert.IsFalse(toast.gameObject.activeSelf, "Chest toast should auto-hide after a short delay.");
        }

        [UnityTest]
        public IEnumerator ClickingRightSideChestBadgeKeepsToastInsideOverlay()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.CompleteCurrentGame(new MiniGameSettlement
            {
                Score = 10,
                ChestCount = 12,
                CoinCount = 12345,
                Summary = "测试结算"
            });
            yield return null;

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var cards = GameObject.FindObjectsOfType<RectTransform>();
            RectTransform rightMostCard = null;
            var maxX = float.MinValue;
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null || !card.gameObject.activeInHierarchy || !card.name.EndsWith("_Card"))
                {
                    continue;
                }

                var localX = card.position.x;
                if (localX > maxX && card.Find("ChestBadge") != null)
                {
                    maxX = localX;
                    rightMostCard = card;
                }
            }

            Assert.IsNotNull(rightMostCard, "A playable card with chest badge should be visible.");

            var chestBadgeButton = rightMostCard.Find("ChestBadge")?.GetComponent<Button>();
            Assert.IsNotNull(chestBadgeButton, "Chest badge button should exist on the selected card.");
            chestBadgeButton.onClick.Invoke();
            yield return null;

            var overlay = GameObject.Find("HallOverlay");
            Assert.IsNotNull(overlay, "Hall overlay should exist for toast rendering.");

            var toast = overlay.transform.Find("ChestToast") as RectTransform;
            Assert.IsNotNull(toast, "Clicking the chest badge should show a toast.");
            Assert.IsTrue(toast.gameObject.activeSelf, "Chest toast should be visible immediately after clicking.");

            var overlayRect = overlay.GetComponent<RectTransform>();
            Assert.IsNotNull(overlayRect, "Hall overlay rect should exist.");

            var toastCorners = new Vector3[4];
            toast.GetWorldCorners(toastCorners);
            var overlayCorners = new Vector3[4];
            overlayRect.GetWorldCorners(overlayCorners);

            Assert.GreaterOrEqual(toastCorners[0].x, overlayCorners[0].x - 0.5f, "Chest toast should stay within the left overlay edge.");
            Assert.LessOrEqual(toastCorners[2].x, overlayCorners[2].x + 0.5f, "Chest toast should stay within the right overlay edge.");
            Assert.Greater(toast.rect.height, 44f, "Long chest toast content should expand the background height.");
        }

        [UnityTest]
        public IEnumerator CanEnterGameByClickingPlayableActionButton()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var cardObject = GameObject.Find("classic-link_Card");
            Assert.IsNotNull(cardObject, "ClassicLink card was not found in hall.");

            var cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
            {
                Assert.IsFalse(cardButton.enabled, "Card root button should stay disabled.");
            }

            var actionObject = cardObject.transform.Find("Action");
            Assert.IsNotNull(actionObject, "Action button root was not found under the card.");
            var actionButton = actionObject.GetComponent<Button>();
            Assert.IsNotNull(actionButton, "Playable action should expose a Button component.");
            Assert.IsTrue(actionButton.interactable, "Start button should be clickable.");

            actionButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "Clicking the start button should enter the game.");
            Assert.AreEqual("classic-link", controller.ActiveGameId);
            Assert.IsFalse(controller.IsHallVisible, "Hall should be hidden after entering a game.");
        }

        [UnityTest]
        public IEnumerator PlayableStartButtonUsesGentleHighlightSweepWithoutScalePulse()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var playableCard = GameObject.Find("classic-link_Card");
            Assert.IsNotNull(playableCard, "Playable card was not found in all games tab.");

            var actionObject = playableCard.transform.Find("Action") as RectTransform;
            Assert.IsNotNull(actionObject, "Playable action root was not found.");
            Assert.That(actionObject.localScale.x, Is.EqualTo(1f).Within(0.001f), "Start button should keep the default X scale.");
            Assert.That(actionObject.localScale.y, Is.EqualTo(1f).Within(0.001f), "Start button should keep the default Y scale.");

            var highlightRoot = actionObject.Find("StartButtonHighlight");
            Assert.IsNotNull(highlightRoot, "Playable start button should create a highlight overlay.");

            var breathGlow = highlightRoot.Find("BreathGlow")?.GetComponent<Image>();
            var sweepShine = highlightRoot.Find("SweepShine")?.GetComponent<Image>();
            Assert.IsNotNull(breathGlow, "Highlight overlay should expose a breath glow image.");
            Assert.IsNotNull(sweepShine, "Highlight overlay should expose a sweep shine image.");

            var initialBreathAlpha = breathGlow.color.a;
            Assert.GreaterOrEqual(initialBreathAlpha, 0.015f, "Breath glow should start near the configured low alpha.");
            Assert.LessOrEqual(initialBreathAlpha, 0.055f, "Breath glow should stay within the configured low alpha range.");
            Assert.That(sweepShine.color.a, Is.EqualTo(0f).Within(0.001f), "Sweep shine should start hidden.");

            yield return new WaitForSecondsRealtime(0.9f);

            var animatedBreathAlpha = breathGlow.color.a;
            Assert.AreNotEqual(initialBreathAlpha, animatedBreathAlpha, "Breath glow alpha should change slightly over time.");
            Assert.GreaterOrEqual(animatedBreathAlpha, 0.015f, "Breath glow should remain soft after animating.");
            Assert.LessOrEqual(animatedBreathAlpha, 0.055f, "Breath glow should remain within the configured range.");
            Assert.That(actionObject.localScale.x, Is.EqualTo(1f).Within(0.001f), "Breathing should not change the button X scale.");
            Assert.That(actionObject.localScale.y, Is.EqualTo(1f).Within(0.001f), "Breathing should not change the button Y scale.");

            var sweepDetected = false;
            var deadline = Time.realtimeSinceStartup + 6.4f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (sweepShine.color.a > 0.01f)
                {
                    sweepDetected = true;
                    break;
                }

                yield return null;
            }

            Assert.IsTrue(sweepDetected, "Start button should trigger a soft sweep highlight within the configured interval.");
            Assert.Greater(sweepShine.rectTransform.anchoredPosition.x, -120f, "Sweep shine should move across the button while active.");

            yield return new WaitForSecondsRealtime(1.4f);

            Assert.That(sweepShine.color.a, Is.EqualTo(0f).Within(0.01f), "Sweep shine should fade out after one pass.");
            Assert.That(actionObject.localScale.x, Is.EqualTo(1f).Within(0.001f), "Sweep highlight should not change the button X scale.");
            Assert.That(actionObject.localScale.y, Is.EqualTo(1f).Within(0.001f), "Sweep highlight should not change the button Y scale.");

            var promptCard = GameObject.Find("more-games-in-progress_Card");
            Assert.IsNotNull(promptCard, "More-games prompt card should exist in all games tab.");
            AssertPromptCardLayout(promptCard);
        }

        [UnityTest]
        public IEnumerator AllGamesPageShowsHeaderTagBarInsteadOfResourceStats()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.CompleteCurrentGame(new MiniGameSettlement
            {
                Score = 10,
                ChestCount = 2,
                CoinCount = 35,
                Summary = "测试结算"
            });
            yield return null;

            AssertHeaderStatsVisible();

            var headerTagBar = GameObject.Find("HallView")?.transform.Find("Shell/HeaderTagBar")?.gameObject;
            Assert.IsNotNull(headerTagBar, "Hall should create the all-games tag bar.");
            Assert.IsFalse(headerTagBar.activeInHierarchy, "Tag bar should stay hidden before entering all games.");

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsNull(GameObject.Find("HeaderStats"), "All games page should hide the header stats strip.");
            AssertHeaderTagBarVisible();
        }

        [UnityTest]
        public IEnumerator ClickingFavoriteBadgeCanToggleFavorite()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var cardObject = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(cardObject, "Nonogram card was not found in all games tab.");

            var cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
            {
                Assert.IsFalse(cardButton.enabled, "Card root button should be disabled.");
            }

            var favoriteBadgeButton = cardObject.transform.Find("FavoriteBadge")?.GetComponent<Button>();
            Assert.IsNotNull(favoriteBadgeButton, "Favorite badge button should exist on all games card.");
            favoriteBadgeButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(controller.IsFavorite("nonogram"), "Clicking the favorite badge should add the game to favorites.");

            var favoritesTab = GameObject.Find("FavoritesTab");
            Assert.IsNotNull(favoritesTab, "Favorites tab was not found.");
            favoritesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var favoriteCard = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(favoriteCard, "Favorited game should appear in favorites tab.");

            var waterSortCard = GameObject.Find("water-sort_Card");
            Assert.IsNotNull(waterSortCard, "Default favorite Water Sort card should still be visible in favorites tab.");
            Assert.IsNotNull(favoriteCard.transform.parent, "Favorited card should be mounted under a slot.");
            Assert.IsNotNull(waterSortCard.transform.parent, "Water Sort card should be mounted under a slot.");
            Assert.Greater(favoriteCard.transform.parent.GetSiblingIndex(), waterSortCard.transform.parent.GetSiblingIndex(), "Newly favorited game should be placed after existing favorites.");

            var favoriteIconRect = favoriteCard.transform.Find("Icon") as RectTransform;
            Assert.IsNotNull(favoriteIconRect, "Favorite card icon root was not found.");
            Assert.AreEqual(200f, favoriteIconRect.sizeDelta.x, 0.01f, "Favorites tab should use the updated icon width.");
            Assert.AreEqual(150f, favoriteIconRect.sizeDelta.y, 0.01f, "Favorites tab should use the updated icon height.");
            Assert.GreaterOrEqual(favoriteCard.transform.localScale.x, 0.95f, "Favorites tab should keep a near-default card scale.");
            Assert.LessOrEqual(favoriteCard.transform.localScale.x, 1f, "Favorites tab should not enlarge the card beyond the template size.");

            var favoriteActionRect = favoriteCard.transform.Find("Action") as RectTransform;
            Assert.IsNotNull(favoriteActionRect, "Favorite card action root was not found.");
            Assert.AreEqual(186f, favoriteActionRect.sizeDelta.x, 0.01f, "Favorites tab should keep the default action button size.");

            var favoriteBadge = favoriteCard.transform.Find("FavoriteBadge")?.GetComponent<Image>();
            Assert.IsNotNull(favoriteBadge, "Favorite badge should exist on favorited card.");
            Assert.Greater(favoriteBadge.color.a, 0.9f, "Favorited card should show highlighted favorite badge.");
            Assert.AreEqual(40f, favoriteBadge.rectTransform.sizeDelta.x, 0.01f, "Favorites tab should use the updated visible favorite badge size.");
            Assert.AreEqual(40f, favoriteBadge.rectTransform.sizeDelta.y, 0.01f, "Favorites tab should use the updated visible favorite badge height.");
            Assert.AreEqual(-120f, favoriteBadge.rectTransform.anchoredPosition.x, 0.01f, "Favorites badge should use the updated horizontal offset.");
            Assert.AreEqual(-30f, favoriteBadge.rectTransform.anchoredPosition.y, 0.01f, "Favorites badge should use the updated vertical offset.");
        }

        [UnityTest]
        public IEnumerator ClickingFavoriteBadgeOnAllGamesShouldNotRebuildCard()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var cardObject = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(cardObject, "Nonogram card was not found in all games tab.");

            var favoriteBadgeButton = cardObject.transform.Find("FavoriteBadge")?.GetComponent<Button>();
            Assert.IsNotNull(favoriteBadgeButton, "Favorite badge button should exist on all games card.");

            var originalInstanceId = cardObject.GetInstanceID();
            favoriteBadgeButton.onClick.Invoke();
            yield return null;

            var updatedCardObject = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(updatedCardObject, "Nonogram card should still exist after toggling favorite.");
            Assert.AreEqual(originalInstanceId, updatedCardObject.GetInstanceID(), "Toggling favorite on all games tab should update the existing card instead of rebuilding it.");

            var favoriteBadge = updatedCardObject.transform.Find("FavoriteBadge")?.GetComponent<Image>();
            Assert.IsNotNull(favoriteBadge, "Favorite badge should still exist after toggling favorite.");
            Assert.Greater(favoriteBadge.color.a, 0.9f, "Favorite badge should become highlighted after toggling favorite.");
        }

        [UnityTest]
        public IEnumerator ClickingActiveAllGamesTabShouldNotRebuildCard()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var cardObject = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(cardObject, "Nonogram card was not found in all games tab.");
            var originalInstanceId = cardObject.GetInstanceID();

            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var updatedCardObject = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(updatedCardObject, "Nonogram card should still exist after clicking the active all games tab.");
            Assert.AreEqual(originalInstanceId, updatedCardObject.GetInstanceID(), "Clicking the active all games tab should not rebuild existing cards.");
        }

        [UnityTest]
        public IEnumerator AllGamesTabUsesAtLeastThreeColumnsInDefaultScene()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var allGamesContent = GameObject.Find("AllGamesContent");
            Assert.IsNotNull(allGamesContent, "All games content root was not found.");

            var grid = allGamesContent.GetComponent<GridLayoutGroup>();
            Assert.IsNotNull(grid, "All games content root should use GridLayoutGroup.");
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
            Assert.GreaterOrEqual(grid.constraintCount, 3, "Default scene should render all games tab with at least three columns.");

            var cardObject = GameObject.Find("nonogram_Card");
            Assert.IsNotNull(cardObject, "Nonogram card was not found in all games tab.");
            Assert.LessOrEqual(cardObject.transform.localScale.x, 1f, "All games tab should not enlarge the card beyond the template size.");
            Assert.Greater(cardObject.transform.localScale.x, 0.6f, "All games tab card scale should remain readable.");
            Assert.IsNotNull(cardObject.transform.parent, "Card should be parented under a slot.");
            Assert.IsTrue(cardObject.transform.parent.name.EndsWith("_CardSlot", StringComparison.Ordinal), "Card should be mounted under a grid slot container.");

            var iconRect = cardObject.transform.Find("Icon") as RectTransform;
            Assert.IsNotNull(iconRect, "Card icon root was not found.");
            Assert.AreEqual(200f, iconRect.sizeDelta.x, 0.01f, "All games tab should keep the same updated icon width as favorites.");
            Assert.AreEqual(150f, iconRect.sizeDelta.y, 0.01f, "All games tab should keep the same updated icon height as favorites.");

            var actionRect = cardObject.transform.Find("Action") as RectTransform;
            Assert.IsNotNull(actionRect, "Card action root was not found.");
            Assert.AreEqual(186f, actionRect.sizeDelta.x, 0.01f, "All games tab should keep the same internal action layout as favorites.");

            var costTextObject = cardObject.transform.Find("CostText")?.gameObject;
            Assert.IsNotNull(costTextObject, "Card cost text root was not found.");
            Assert.IsFalse(costTextObject.activeSelf, "Card cost text should be hidden when favorite badge is used.");

            var favoriteBadge = cardObject.transform.Find("FavoriteBadge")?.GetComponent<Image>();
            Assert.IsNotNull(favoriteBadge, "Favorite badge should exist on all games card.");
            Assert.Less(favoriteBadge.color.a, 0.8f, "Unfavorited card should show dimmed favorite badge.");
            Assert.AreEqual(40f, favoriteBadge.rectTransform.sizeDelta.x, 0.01f, "All games tab should keep the updated badge width baseline.");
            Assert.AreEqual(40f, favoriteBadge.rectTransform.sizeDelta.y, 0.01f, "All games tab should keep the updated badge height baseline.");
            Assert.AreEqual(-120f, favoriteBadge.rectTransform.anchoredPosition.x, 0.01f, "All games tab should keep the updated badge horizontal offset.");
            Assert.AreEqual(-30f, favoriteBadge.rectTransform.anchoredPosition.y, 0.01f, "All games tab should keep the updated badge vertical offset.");

            var promptCard = GameObject.Find("more-games-in-progress_Card");
            Assert.IsNotNull(promptCard, "More-games prompt card should exist in all games tab.");
            AssertPromptCardLayout(promptCard);
        }

        [UnityTest]
        public IEnumerator UnknownGameIdFallsBackToHall()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame("missing-game");
            yield return null;

            Assert.IsFalse(controller.HasActiveGame, "Unknown game id should not create a game runtime.");
            Assert.IsTrue(controller.IsHallVisible, "Hall should remain visible when game id is not registered.");
        }

        [UnityTest]
        public IEnumerator LegacySaveWithoutCoinFieldLoadsCoinAsZero()
        {
            ResetProgress();

            PlayerPrefs.SetString(
                MiniGameSaveStore.PlayerPrefsKey,
                "{\"Entries\":[{\"GameId\":\"classic-link\",\"PlayCount\":3,\"BestScore\":88,\"TotalChestCount\":5}],\"FavoriteGameIds\":[\"classic-link\"]}");
            PlayerPrefs.Save();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var progress = controller.GetProgress("classic-link");
            Assert.AreEqual(3, progress.PlayCount);
            Assert.AreEqual(88, progress.BestScore);
            Assert.AreEqual(5, progress.TotalChestCount);
            Assert.AreEqual(0, progress.TotalCoinCount, "Legacy save data without coin field should default to zero coins.");

            AssertHeaderStatsVisible();

            var allGamesTab = GameObject.Find("AllGamesTab");
            Assert.IsNotNull(allGamesTab, "All games tab was not found.");
            allGamesTab.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsNull(GameObject.Find("HeaderStats"), "All games page should hide the header stats strip.");
            AssertHeaderTagBarVisible();
        }

        [UnityTest]
        public IEnumerator ReturningFrom2048RestoresHallCanvasScaler()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            var canvas = controller.GetComponentInChildren<Canvas>();
            Assert.IsNotNull(canvas, "Controller canvas was not found.");

            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "Hall canvas scaler was not found.");

            var originalUiScaleMode = scaler.uiScaleMode;
            var originalReferenceResolution = scaler.referenceResolution;
            var originalScreenMatchMode = scaler.screenMatchMode;
            var originalMatchWidthOrHeight = scaler.matchWidthOrHeight;

            controller.EnterGame(Game2048View.GameIdConstant);
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "2048 game should be active.");
            Assert.AreEqual(originalUiScaleMode, scaler.uiScaleMode);
            Assert.AreEqual(originalReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(originalScreenMatchMode, scaler.screenMatchMode);
            Assert.AreEqual(originalMatchWidthOrHeight, scaler.matchWidthOrHeight);

            controller.ExitCurrentGameToHall();
            yield return null;

            Assert.IsTrue(controller.IsHallVisible, "Hall should be visible after exiting 2048.");
            Assert.AreEqual(originalUiScaleMode, scaler.uiScaleMode);
            Assert.AreEqual(originalReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(originalScreenMatchMode, scaler.screenMatchMode);
            Assert.AreEqual(originalMatchWidthOrHeight, scaler.matchWidthOrHeight);
        }

        [UnityTest]
        public IEnumerator HallDoesNotShowEditorLevelProgressControls()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before checking editor-only controls.");
            yield return null;

            Assert.IsNull(GameObject.Find("EditorLevelProgressPanel"), "Editor level progress controls should not be injected into the PlayMode hall.");
            Assert.IsNull(GameObject.Find("EditorOpenAllLevelsButton"), "Open-all-levels editor control should stay out of the game UI.");
            Assert.IsNull(GameObject.Find("EditorClearAllLevelsButton"), "Clear-all-levels editor control should stay out of the game UI.");
        }

        [UnityTest]
        public IEnumerator ClearSaveDataResetsHallProgressAndClassicLinkTutorialState()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before clearing save data.");

            controller.GrantSettlementReward(
                "classic-link",
                new MiniGameSettlement
                {
                    Score = 120,
                    ChestCount = 2,
                    CoinCount = 30
                });
            controller.SetLevelProgress("classic-link", 3, 6);
            controller.SetGameTutorialSeenVersion("classic-link", 1);
            controller.ToggleFavorite("classic-link");
            PlayerPrefs.Save();

            controller.ClearSaveData();
            yield return null;

            var progress = controller.GetProgress("classic-link");
            Assert.AreEqual(0, progress.PlayCount);
            Assert.AreEqual(0, progress.BestScore);
            Assert.AreEqual(0, progress.TotalChestCount);
            Assert.AreEqual(0, progress.TotalCoinCount);
            Assert.AreEqual(0, progress.CurrentLevelIndex);
            Assert.AreEqual(1, progress.UnlockedLevelCount);
            Assert.AreEqual(0, controller.GetGameTutorialSeenVersion("classic-link"));
            Assert.IsTrue(controller.IsFavorite("classic-link"), "Cleared save data should return the hall to first-launch default favorites.");
        }

        [UnityTest]
        public IEnumerator HeaderTitleImageUsesGentlePulseScale()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            Assert.IsNotNull(controller, "Hall controller should load before checking title pulse.");

            var titleImage = GameObject.Find("HallView")?.transform.Find("Shell/HeaderTitleBar/Title/Image");
            if (titleImage == null)
            {
                titleImage = GameObject.Find("HallView")?.transform.Find("Shell/HeaderTitleBar/Title");
            }

            Assert.IsNotNull(titleImage, "Header title image should exist in the hall view.");

            var initialScale = titleImage.localScale;
            Assert.That(initialScale.x, Is.EqualTo(1f).Within(0.001f), "Header title image should start from the default scale.");
            Assert.That(initialScale.y, Is.EqualTo(1f).Within(0.001f), "Header title image should start from the default scale.");

            yield return new WaitForSecondsRealtime(0.8f);

            var animatedScale = titleImage.localScale;
            Assert.Greater(animatedScale.x, 1f, "Header title image should scale up slightly during the pulse.");
            Assert.Greater(animatedScale.y, 1f, "Header title image should scale up slightly during the pulse.");
            Assert.LessOrEqual(animatedScale.x, 1.03f + 0.01f, "Header title image pulse should stay near the configured peak.");
            Assert.LessOrEqual(animatedScale.y, 1.03f + 0.01f, "Header title image pulse should stay near the configured peak.");

            yield return new WaitForSecondsRealtime(2.3f);

            var returnedScale = titleImage.localScale;
            Assert.That(returnedScale.x, Is.EqualTo(1f).Within(0.03f), "Header title image should return close to the base scale after one cycle.");
            Assert.That(returnedScale.y, Is.EqualTo(1f).Within(0.03f), "Header title image should return close to the base scale after one cycle.");
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static void AssertCardIconTextureName(string cardName, string expectedTextureName)
        {
            var cardObject = GameObject.Find(cardName);
            Assert.IsNotNull(cardObject, "Card was not found: " + cardName);

            var iconImage = cardObject.transform.Find("Icon/IconImage")?.GetComponent<RawImage>();
            Assert.IsNotNull(iconImage, "Card icon image was not found: " + cardName);
            Assert.IsNotNull(iconImage.texture, "Card icon texture should not be null: " + cardName);
            Assert.AreEqual(expectedTextureName, iconImage.texture.name, "Unexpected hall card icon texture for " + cardName);
        }

        private static void AssertPromptCardLayout(GameObject promptCard)
        {
            Assert.IsNotNull(promptCard, "Prompt card should exist before validating layout.");

            var iconImage = promptCard.transform.Find("Icon/IconImage")?.GetComponent<RawImage>();
            Assert.IsNotNull(iconImage, "Prompt card should render its dedicated image.");
            Assert.IsNotNull(iconImage.texture, "Prompt card image texture should not be null.");
            Assert.AreEqual("more_games_in_progress", iconImage.texture.name, "Prompt card should use the more-games image.");

            Assert.IsNull(promptCard.transform.Find("Title"), "Prompt card should not keep a title node.");
            Assert.IsNull(promptCard.transform.Find("Action"), "Prompt card should not keep an action node.");
            Assert.IsNull(promptCard.transform.Find("FavoriteBadge"), "Prompt card should not keep a favorite badge node.");
            Assert.IsNull(promptCard.transform.Find("ChestBadge"), "Prompt card should not keep a chest badge node.");
            Assert.IsNull(promptCard.transform.Find("CostText"), "Prompt card should not keep a cost text node.");
            Assert.IsNull(promptCard.transform.Find("Background"), "Prompt card should not keep the card background node.");
        }

        private static void AssertHeaderTagBarVisible()
        {
            var tagBar = GameObject.Find("HeaderTagBar");
            Assert.IsNotNull(tagBar, "All games page should expose a header tag bar.");
            Assert.IsTrue(tagBar.activeInHierarchy, "Header tag bar should be visible on all games page.");
            Assert.IsNull(tagBar.transform.Find("ChestStat"), "Header tag bar should not contain the old chest stat node.");
            Assert.IsNull(tagBar.transform.Find("CoinStat"), "Header tag bar should not contain the old coin stat node.");

            AssertHeaderTagLabel(tagBar.transform, "Tag_0", UiTextCatalog.Get("hall.tag.all"));
            AssertHeaderTagLabel(tagBar.transform, "Tag_1", UiTextCatalog.Get("hall.tag.eliminate"));
            AssertHeaderTagLabel(tagBar.transform, "Tag_2", UiTextCatalog.Get("hall.tag.puzzle"));
            AssertHeaderTagLabel(tagBar.transform, "Tag_3", UiTextCatalog.Get("hall.tag.number"));
            AssertHeaderTagLabel(tagBar.transform, "Tag_4", UiTextCatalog.Get("hall.tag.action"));
            AssertHeaderTagLabel(tagBar.transform, "Tag_5", UiTextCatalog.Get("hall.tag.simulation"));
            AssertHeaderTagLabel(tagBar.transform, "Tag_6", UiTextCatalog.Get("hall.tag.merge"));
            Assert.IsNull(tagBar.transform.Find("Tag_7"), "Header tag bar should not include extra tags.");
        }

        private static void AssertHeaderStatsVisible()
        {
            var headerStats = GameObject.Find("HeaderStats");
            Assert.IsNotNull(headerStats, "Favorites page should expose the header chest and coin strip.");
            Assert.IsTrue(headerStats.activeInHierarchy, "Header chest and coin strip should be visible on favorites page.");
            Assert.IsNotNull(headerStats.transform.Find("ChestStat"), "Header stats should contain the chest stat node.");
            Assert.IsNotNull(headerStats.transform.Find("CoinStat"), "Header stats should contain the coin stat node.");
        }

        private static void AssertHeaderTagLabel(Transform tagBar, string tagName, string expectedText)
        {
            var label = tagBar.Find(tagName + "/Label")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(label, "Header tag should expose a TMP label: " + tagName);
            var textProperty = label.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Header tag label should expose a text property: " + tagName);
            Assert.AreEqual(expectedText, textProperty.GetValue(label, null) as string, "Header tag label should match.");
        }

        private static void AssertAnnouncementText(Transform target, string expectedContains)
        {
            Assert.IsNotNull(target, "Announcement text target should exist.");
            var textComponent = target.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(textComponent, "Announcement text target should expose a TMP label.");
            var textProperty = textComponent.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Announcement TMP label should expose a text property.");
            var value = textProperty.GetValue(textComponent, null) as string;
            StringAssert.Contains(expectedContains, value, "Announcement text should be populated.");
        }

        private static void AssertMenuButtonLabel(Transform button, string expected)
        {
            var label = button.Find("Label")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(label, "Menu button should expose a TMP label.");
            var textProperty = label.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Menu button label should expose a text property.");
            Assert.AreEqual(expected, textProperty.GetValue(label, null) as string, "Menu button should use the shared Chinese label.");
        }

        private static void AssertFooterLeafAnchored(Transform footer, string leafName, bool right)
        {
            var leaf = footer.Find(leafName) as RectTransform;
            Assert.IsNotNull(leaf, "Announcement fixed footer should keep decorative leaf: " + leafName);
            Assert.That(leaf.anchorMin.x, Is.EqualTo(right ? 1f : 0f).Within(0.001f), "Footer leaf should be anchored to its side.");
            Assert.That(leaf.anchorMax.x, Is.EqualTo(right ? 1f : 0f).Within(0.001f), "Footer leaf should be anchored to its side.");
            Assert.That(leaf.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f), "Footer leaf X offset should stay on the footer edge.");
            Assert.That(leaf.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f), "Footer leaf should share the footer vertical center.");
        }

        private static void AssertFooterPosition(RectTransform footer)
        {
            Assert.That(footer.offsetMin.y, Is.EqualTo(54f).Within(0.001f), "Footer should move up as a whole.");
            Assert.That(footer.offsetMax.y, Is.EqualTo(104f).Within(0.001f), "Footer should move up as a whole.");
            var label = footer.Find("Label") as RectTransform;
            Assert.IsNotNull(label, "Announcement fixed footer should expose a label.");
            Assert.That(label.offsetMin.y, Is.EqualTo(0f).Within(0.001f), "Footer label should not move independently from footer.");
            Assert.That(label.offsetMax.y, Is.EqualTo(0f).Within(0.001f), "Footer label should not move independently from footer.");
        }

        private static IEnumerator LoadController(Action<MiniGameAppController> assign)
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 30; i++)
            {
                controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
                if (controller != null)
                {
                    break;
                }

                yield return null;
            }

            assign(controller);
        }
    }
}
