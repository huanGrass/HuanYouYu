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

            var headerStats = GameObject.Find("HeaderStats");
            Assert.IsNotNull(headerStats, "Hall should expose a header stats strip above the card list.");
            var headerChestCountText = headerStats.transform.Find("ChestStat/CountText")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(headerChestCountText, "Header stats strip should expose a chest count label.");
            var headerTextProperty = headerChestCountText.GetType().GetProperty("text");
            Assert.IsNotNull(headerTextProperty, "Header chest count label should expose a text property.");
            Assert.AreEqual("2", headerTextProperty.GetValue(headerChestCountText, null) as string, "Header stats strip should show the total chest count.");

            var headerCoinCountText = headerStats.transform.Find("CoinStat/CountText")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(headerCoinCountText, "Header stats strip should expose a coin count label.");
            var headerCoinTextProperty = headerCoinCountText.GetType().GetProperty("text");
            Assert.IsNotNull(headerCoinTextProperty, "Header coin count label should expose a text property.");
            Assert.AreEqual("0", headerCoinTextProperty.GetValue(headerCoinCountText, null) as string, "Header stats strip should show zero coins when no game awards them yet.");
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
        public IEnumerator HeaderStatIconsKeepStillWhileUsingSweep()
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

            var headerStats = GameObject.Find("HeaderStats")?.GetComponent<RectTransform>();
            Assert.IsNotNull(headerStats, "Hall should expose the header stats strip.");

            var chestIcon = headerStats.Find("ChestStat/ChestIcon") as RectTransform;
            var coinIcon = headerStats.Find("CoinStat/CoinIcon") as RectTransform;
            var chestCountText = headerStats.Find("ChestStat/CountText") as RectTransform;
            var coinCountText = headerStats.Find("CoinStat/CountText") as RectTransform;
            Assert.IsNotNull(chestIcon, "Header chest icon should exist.");
            Assert.IsNotNull(coinIcon, "Header coin icon should exist.");
            Assert.IsNotNull(chestCountText, "Header chest count text should exist.");
            Assert.IsNotNull(coinCountText, "Header coin count text should exist.");

            var chestSweep = headerStats.Find("ChestStat/ChestIcon/IconSweepRoot/SweepShine")?.GetComponent<Image>();
            var coinSweep = headerStats.Find("CoinStat/CoinIcon/IconSweepRoot/SweepShine")?.GetComponent<Image>();
            Assert.IsNotNull(chestSweep, "Header chest icon should expose a sweep shine image.");
            Assert.IsNotNull(coinSweep, "Header coin icon should expose a sweep shine image.");
            Assert.That(chestSweep.color.a, Is.EqualTo(0f).Within(0.001f), "Chest sweep should start hidden.");
            Assert.That(coinSweep.color.a, Is.EqualTo(0f).Within(0.001f), "Coin sweep should start hidden.");

            var headerBasePosition = headerStats.anchoredPosition;
            var chestIconBasePosition = chestIcon.anchoredPosition;
            var coinIconBasePosition = coinIcon.anchoredPosition;
            var chestTextBasePosition = chestCountText.anchoredPosition;
            var coinTextBasePosition = coinCountText.anchoredPosition;

            yield return new WaitForSecondsRealtime(1.1f);

            Assert.That(headerStats.anchoredPosition, Is.EqualTo(headerBasePosition), "Header stats strip should stay fixed.");
            Assert.That(chestIcon.anchoredPosition, Is.EqualTo(chestIconBasePosition), "Chest icon should stay fixed.");
            Assert.That(coinIcon.anchoredPosition, Is.EqualTo(coinIconBasePosition), "Coin icon should stay fixed.");
            Assert.That(chestCountText.anchoredPosition, Is.EqualTo(chestTextBasePosition), "Chest count text should stay fixed.");
            Assert.That(coinCountText.anchoredPosition, Is.EqualTo(coinTextBasePosition), "Coin count text should stay fixed.");

            var sweepDetected = false;
            var deadline = Time.realtimeSinceStartup + 8.8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (chestSweep.color.a > 0.01f || coinSweep.color.a > 0.01f)
                {
                    sweepDetected = true;
                    break;
                }

                yield return null;
            }

            Assert.IsTrue(sweepDetected, "At least one header stat icon should trigger a sweep within the configured interval.");
            if (chestSweep.color.a > 0.01f)
            {
                Assert.Greater(chestSweep.rectTransform.anchoredPosition.x, -43f, "Chest sweep should move across the icon while active.");
            }

            if (coinSweep.color.a > 0.01f)
            {
                Assert.Greater(coinSweep.rectTransform.anchoredPosition.x, -43f, "Coin sweep should move across the icon while active.");
            }

            var fadeDeadline = Time.realtimeSinceStartup + 2.4f;
            while (Time.realtimeSinceStartup < fadeDeadline && (chestSweep.color.a > 0.02f || coinSweep.color.a > 0.02f))
            {
                yield return null;
            }

            Assert.That(chestSweep.color.a, Is.EqualTo(0f).Within(0.02f), "Chest sweep should fade out after one pass.");
            Assert.That(coinSweep.color.a, Is.EqualTo(0f).Within(0.02f), "Coin sweep should fade out after one pass.");
            Assert.That(headerStats.anchoredPosition, Is.EqualTo(headerBasePosition), "Header stats strip should remain fixed after sweep.");
            Assert.That(chestIcon.anchoredPosition, Is.EqualTo(chestIconBasePosition), "Chest icon should remain fixed after sweep.");
            Assert.That(coinIcon.anchoredPosition, Is.EqualTo(coinIconBasePosition), "Coin icon should remain fixed after sweep.");
            Assert.That(chestCountText.anchoredPosition, Is.EqualTo(chestTextBasePosition), "Chest count text should remain fixed after sweep.");
            Assert.That(coinCountText.anchoredPosition, Is.EqualTo(coinTextBasePosition), "Coin count text should remain fixed after sweep.");
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

            var headerStats = GameObject.Find("HeaderStats");
            Assert.IsNotNull(headerStats, "Hall should expose a header stats strip after loading legacy save data.");
            var headerCoinCountText = headerStats.transform.Find("CoinStat/CountText")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(headerCoinCountText, "Header stats strip should expose a coin count label.");
            var textProperty = headerCoinCountText.GetType().GetProperty("text");
            Assert.IsNotNull(textProperty, "Header coin count label should expose a text property.");
            Assert.AreEqual("0", textProperty.GetValue(headerCoinCountText, null) as string, "Legacy save data should still render zero coins in the header.");
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
