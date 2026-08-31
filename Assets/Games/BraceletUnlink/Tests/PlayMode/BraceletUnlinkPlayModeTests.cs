using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public sealed class BraceletUnlinkPlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject hostObject;
        private MiniGameAppController controller;
        private BraceletUnlinkGameView game;
        private string editorSavePath;
        private string randomModeSaveKey;

        [Test]
        public void GameplayToolbarKeepsDevelopmentEditorEntryHidden()
        {
            var entry = GameObject.Find("BraceletLevelEditorButton");
            Assert.IsNotNull(entry);
            Assert.IsFalse(entry.GetComponent<Button>().interactable);
            Assert.IsTrue(entry.GetComponent<LayoutElement>().ignoreLayout);
            Assert.AreEqual(0f, entry.GetComponent<CanvasGroup>().alpha);
            Assert.IsFalse(entry.GetComponent<CanvasGroup>().blocksRaycasts);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Cleanup();
            MiniGameSaveStore.ClearPersistedState();
            editorSavePath = Path.Combine(
                Application.temporaryCachePath,
                "bracelet-unlink-editor-test-" + Guid.NewGuid().ToString("N") + ".json");
            randomModeSaveKey = "bracelet-unlink.random-mode.test." + Guid.NewGuid().ToString("N");
            SetEditorSavePathOverride(editorSavePath);
            SetRandomModeSaveKeyOverride(randomModeSaveKey);
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Cleanup();
            SetEditorSavePathOverride(null);
            SetRandomModeSaveKeyOverride(null);
            SetLevelDefinitionsOverride(null);
            if (!string.IsNullOrEmpty(editorSavePath) && File.Exists(editorSavePath))
            {
                File.Delete(editorSavePath);
            }
            if (!string.IsNullOrEmpty(randomModeSaveKey))
            {
                PlayerPrefs.DeleteKey(randomModeSaveKey);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialBoardCreatesRingPhysicsAndSharedMapBuckles()
        {
            yield return null;

            Assert.AreEqual(12, GetListField(game, "rings").Count);
            Assert.AreEqual(5, GetListField(game, "mapBuckles").Count);
            for (var i = 0; i < 12; i++)
            {
                var ring = GameObject.Find("Bracelet_" + i);
                Assert.IsNotNull(ring);
                Assert.IsNotNull(ring.GetComponent<Rigidbody2D>());
                Assert.Greater(ring.GetComponents<CircleCollider2D>().Length, 10);
                var ownedCount = 0;
                for (var child = 0; child < ring.transform.childCount; child++)
                {
                    if (ring.transform.GetChild(child).name.StartsWith("AttachedBuckle_"))
                    {
                        ownedCount += 1;
                        Assert.AreEqual(4, ring.transform.GetChild(child).GetComponentsInChildren<BoxCollider2D>().Length);
                    }
                }
                var expectedOwnedCount = i == 4 ? 3 : i == 9 || i == 11 ? 2 : i == 2 || i == 3 || i == 7 ? 1 : 0;
                Assert.AreEqual(expectedOwnedCount, ownedCount);
            }

            for (var i = 0; i < 5; i++)
            {
                var buckle = GameObject.Find("MapBuckle_" + i);
                Assert.IsNotNull(buckle);
                Assert.IsNull(buckle.GetComponent<BoxCollider2D>(), "金环中心必须为空，不能使用实心碰撞体");
                Assert.AreEqual(4, buckle.GetComponentsInChildren<BoxCollider2D>().Length);
                Assert.IsNull(GameObject.Find("MapBuckleBack_" + i), "地图金环不得放到手环后层，手环不能遮住金环");
            }
        }

        [Test]
        public void FirstRestartRestoresAttachedBuckleVisualScaleAfterOwnerWasShrunk()
        {
            var attached = GetListField(game, "attachedBuckles")[0];
            var owner = GetListField(game, "rings")[GetIntField(attached, "Owner")];
            var ownerRect = (RectTransform)GetFieldValue(owner, "Rect");
            var visual = (RectTransform)GetFieldValue(attached, "Visual");
            ownerRect.localScale = Vector3.one * 0.05f;
            visual.localScale = Vector3.one * 0.05f;

            GameObject.Find("RestartButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1f, ownerRect.localScale.x, 0.001f);
            Assert.AreEqual(1f, visual.localScale.x, 0.001f, "第一次刷新就必须完整恢复随环金环");
        }

        [UnityTest]
        public IEnumerator BuiltInCampaignStartsWithThreeRingPuzzleAndProvidesLevelSelection()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            Assert.IsNotNull(levelDataType);
            var factory = levelDataType.GetMethod(
                "CreateBuiltInLevels",
                BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(factory);

            Cleanup();
            SetLevelDefinitionsOverride(factory.Invoke(null, null));
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);

            Assert.AreEqual(3, GetListField(game, "rings").Count, "删除双环教学关后，第一关必须从原第三关开始");
            Assert.AreEqual(2, GetListField(game, "mapBuckles").Count);
            Assert.AreEqual(6, ((Array)GetFieldValue(game, "levelDefinitions")).Length);

            GameObject.Find("BraceletLevelSelectButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNotNull(GameObject.Find("BraceletUnlinkLevelSelectPanel"));
            Assert.IsNotNull(GameObject.Find("BraceletUnlinkLevelButton_1"));
            Assert.IsNotNull(GameObject.Find("BraceletUnlinkLevelButton_2"));
            Assert.IsNotNull(GameObject.Find("BraceletUnlinkLevelButton_6"));
            Assert.IsNull(GameObject.Find("BraceletUnlinkLevelButton_7"));
        }

        [Test]
        public void PublicLevelCountExposesCampaignToEditorProgressTools()
        {
            Assert.AreEqual(6, BraceletUnlinkGameView.LevelCount);
        }

        [UnityTest]
        public IEnumerator RandomModeUnlockRequiresEveryCampaignLevelCompletion()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            Cleanup();
            MiniGameSaveStore.ClearPersistedState();
            SetLevelDefinitionsOverride(factory.Invoke(null, null));
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);

            var unlockMethod = typeof(BraceletUnlinkGameView).GetMethod(
                "UnlockRandomModeIfCampaignFinished",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(unlockMethod);
            var randomButton = FindButtonIncludingInactive("BraceletRandomModeButton").gameObject;
            var progress = GetFieldValue(game, "levelProgress");
            var progressType = progress.GetType();
            var selectMethod = progressType.GetMethod("Select");
            var unlockNextMethod = progressType.GetMethod("UnlockNext");
            var completeMethod = progressType.GetMethod("CompleteCurrentLevel");
            Assert.IsNotNull(selectMethod);
            Assert.IsNotNull(unlockNextMethod);
            Assert.IsNotNull(completeMethod);

            for (var levelIndex = 0; levelIndex < BraceletUnlinkGameView.LevelCount - 1; levelIndex++)
            {
                Assert.IsTrue((bool)selectMethod.Invoke(progress, new object[] { levelIndex }));
                unlockNextMethod.Invoke(progress, null);
            }

            Assert.IsTrue((bool)selectMethod.Invoke(
                progress,
                new object[] { BraceletUnlinkGameView.LevelCount - 1 }));
            completeMethod.Invoke(progress, new object[] { 600 });
            unlockMethod.Invoke(game, null);
            Assert.IsFalse(randomButton.activeSelf, "只通关最终关不能绕过前置关卡解锁随机模式");

            for (var levelIndex = 0; levelIndex < BraceletUnlinkGameView.LevelCount - 1; levelIndex++)
            {
                Assert.IsTrue((bool)selectMethod.Invoke(progress, new object[] { levelIndex }));
                completeMethod.Invoke(progress, new object[] { 100 + levelIndex });
            }
            unlockMethod.Invoke(game, null);

            Assert.IsTrue(randomButton.activeSelf, "全部六关都有通关记录后必须解锁随机模式");
            Assert.IsTrue(PlayerPrefs.HasKey(MiniGameSaveStore.PlayerPrefsKey), "公共逐关成绩必须持久保存");

            controller.SetLevelProgress(BraceletUnlinkGameView.GameIdConstant, 0, 1);
            Cleanup();
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);

            randomButton = FindButtonIncludingInactive("BraceletRandomModeButton").gameObject;
            Assert.IsFalse(randomButton.activeSelf, "只开放第一关时，即使残留旧通关记录也不能显示随机入口");

            controller.ClearLevelCompletions(BraceletUnlinkGameView.GameIdConstant);
            Cleanup();
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);

            randomButton = FindButtonIncludingInactive("BraceletRandomModeButton").gameObject;
            Assert.IsFalse(randomButton.activeSelf, "清空逐关通关成绩后，旧随机存档不能继续强制显示入口");
        }

        [Test]
        public void BuiltInCampaignContainsSixLevelsAndEveryRingHasAConstraint()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            var levels = (Array)factory.Invoke(null, null);
            Assert.AreEqual(6, levels.Length);
            CollectionAssert.AreEqual(
                new[] { 1, 2, 4, 5, 7, 8 },
                GetLevelIds(levels),
                "精简或调整关卡顺序后，整数LevelId必须保持稳定");

            var expectedRingCounts = new[] { 3, 5, 7, 8, 10, 12 };
            for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                var rings = (Array)GetFieldValue(levels.GetValue(levelIndex), "Rings");
                var edges = (Array)GetFieldValue(levels.GetValue(levelIndex), "Edges");
                var connected = new HashSet<int>();
                var ringCount = 0;
                for (var ringIndex = 0; ringIndex < rings.Length; ringIndex++)
                {
                    if ((int)GetFieldValue(rings.GetValue(ringIndex), "Kind") != 0)
                    {
                        ringCount += 1;
                    }
                }
                for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
                {
                    if ((int)GetFieldValue(edges.GetValue(edgeIndex), "Kind") == 0)
                    {
                        continue;
                    }
                    connected.Add((int)GetFieldValue(edges.GetValue(edgeIndex), "SlotA"));
                    connected.Add((int)GetFieldValue(edges.GetValue(edgeIndex), "SlotB"));
                }
                Assert.AreEqual(expectedRingCounts[levelIndex], ringCount, "关卡手环数量应按难度逐步增加");
                Assert.AreEqual(ringCount, connected.Count, "每个非空手环必须参与至少一个金环关系");
            }
        }

        [Test]
        public void DifficultyLevelsStayConnectedAndUseSharedDependencyHubs()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            var levels = (Array)factory.Invoke(null, null);

            for (var levelIndex = 1; levelIndex < levels.Length - 1; levelIndex++)
            {
                var level = levels.GetValue(levelIndex);
                var rings = (Array)GetFieldValue(level, "Rings");
                var edges = (Array)GetFieldValue(level, "Edges");
                var occupied = new HashSet<int>();
                var neighbors = new Dictionary<int, List<int>>();
                var activeEdgeCount = 0;
                for (var slot = 0; slot < rings.Length; slot++)
                {
                    if ((int)GetFieldValue(rings.GetValue(slot), "Kind") == 0)
                    {
                        continue;
                    }
                    occupied.Add(slot);
                    neighbors[slot] = new List<int>();
                }
                for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
                {
                    var edge = edges.GetValue(edgeIndex);
                    if ((int)GetFieldValue(edge, "Kind") == 0)
                    {
                        continue;
                    }
                    var slotA = (int)GetFieldValue(edge, "SlotA");
                    var slotB = (int)GetFieldValue(edge, "SlotB");
                    neighbors[slotA].Add(slotB);
                    neighbors[slotB].Add(slotA);
                    activeEdgeCount += 1;
                }

                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                foreach (var start in occupied)
                {
                    queue.Enqueue(start);
                    break;
                }
                while (queue.Count > 0)
                {
                    var slot = queue.Dequeue();
                    if (!visited.Add(slot))
                    {
                        continue;
                    }
                    for (var neighborIndex = 0; neighborIndex < neighbors[slot].Count; neighborIndex++)
                    {
                        queue.Enqueue(neighbors[slot][neighborIndex]);
                    }
                }

                var maximumDegree = 0;
                foreach (var pair in neighbors)
                {
                    maximumDegree = Mathf.Max(maximumDegree, pair.Value.Count);
                }
                Assert.AreEqual(
                    occupied.Count,
                    visited.Count,
                    "第" + (levelIndex + 1) + "关不得再拆成互不影响的独立小题");
                Assert.GreaterOrEqual(
                    activeEdgeCount,
                    occupied.Count - 1,
                    "第" + (levelIndex + 1) + "关连接数量不足以形成完整依赖结构");
                Assert.GreaterOrEqual(
                    maximumDegree,
                    3,
                    "第" + (levelIndex + 1) + "关至少要有一个同时承担多条约束的枢纽手环");
            }

            Assert.GreaterOrEqual(GetLongestAttachedDependencyDepth(levels.GetValue(4)), 6, "第5关需要形成至少六层随环依赖");
        }

        [Test]
        public void BuiltInOpenRingGapsStayAwayFromOwnedAttachedBuckles()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            var getSlotPosition = levelDataType.GetMethod("GetSlotPosition", BindingFlags.Instance | BindingFlags.Public);
            var levels = (Array)factory.Invoke(null, null);

            for (var levelIndex = 0; levelIndex < levels.Length - 1; levelIndex++)
            {
                var level = levels.GetValue(levelIndex);
                var rings = (Array)GetFieldValue(level, "Rings");
                var edges = (Array)GetFieldValue(level, "Edges");
                for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
                {
                    var edge = edges.GetValue(edgeIndex);
                    var kind = (int)GetFieldValue(edge, "Kind");
                    if (kind != 2 && kind != 3)
                    {
                        continue;
                    }
                    var slotA = (int)GetFieldValue(edge, "SlotA");
                    var slotB = (int)GetFieldValue(edge, "SlotB");
                    var owner = kind == 2 ? slotA : slotB;
                    var target = kind == 2 ? slotB : slotA;
                    var ownerRing = rings.GetValue(owner);
                    if ((int)GetFieldValue(ownerRing, "Kind") == 2)
                    {
                        continue;
                    }
                    var ownerPosition = (Vector2)getSlotPosition.Invoke(level, new object[] { owner });
                    var targetPosition = (Vector2)getSlotPosition.Invoke(level, new object[] { target });
                    var buckleAngle = Mathf.Atan2(
                        targetPosition.y - ownerPosition.y,
                        targetPosition.x - ownerPosition.x) * Mathf.Rad2Deg;
                    var gapAngle = (float)GetFieldValue(ownerRing, "GapAngle");
                    Assert.GreaterOrEqual(
                        Mathf.Abs(Mathf.DeltaAngle(gapAngle, buckleAngle)),
                        55f,
                        "第" + (levelIndex + 1) + "关随环金环不能贴着所属手环缺口");
                }
            }
        }

        [Test]
        public void BuiltInOpenRingGapsUseTheSafestAvailableDirectionAwayFromMapBuckles()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            var getSlotPosition = levelDataType.GetMethod("GetSlotPosition", BindingFlags.Instance | BindingFlags.Public);
            var levels = (Array)factory.Invoke(null, null);
            for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                var level = levels.GetValue(levelIndex);
                var rings = (Array)GetFieldValue(level, "Rings");
                var edges = (Array)GetFieldValue(level, "Edges");
                for (var slot = 0; slot < rings.Length; slot++)
                {
                    var ring = rings.GetValue(slot);
                    if ((int)GetFieldValue(ring, "Kind") != 1)
                    {
                        continue;
                    }
                    var mapAngles = new List<float>();
                    for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
                    {
                        var edge = edges.GetValue(edgeIndex);
                        if ((int)GetFieldValue(edge, "Kind") != 1)
                        {
                            continue;
                        }
                        var slotA = (int)GetFieldValue(edge, "SlotA");
                        var slotB = (int)GetFieldValue(edge, "SlotB");
                        if (slotA != slot && slotB != slot)
                        {
                            continue;
                        }
                        var other = slotA == slot ? slotB : slotA;
                        var position = (Vector2)getSlotPosition.Invoke(level, new object[] { slot });
                        var otherPosition = (Vector2)getSlotPosition.Invoke(level, new object[] { other });
                        mapAngles.Add(Mathf.Atan2(
                            otherPosition.y - position.y,
                            otherPosition.x - position.x) * Mathf.Rad2Deg);
                    }
                    if (mapAngles.Count == 0)
                    {
                        continue;
                    }
                    var actualGap = (float)GetFieldValue(ring, "GapAngle");
                    var actualClearance = MinimumAngleDistance(actualGap, mapAngles);
                    var bestPossible = 0f;
                    for (var candidate = -180; candidate < 180; candidate++)
                    {
                        bestPossible = Mathf.Max(bestPossible, MinimumAngleDistance(candidate, mapAngles));
                    }
                    Assert.GreaterOrEqual(
                        actualClearance,
                        Mathf.Min(55f, bestPossible) - 1.1f,
                        "第" + (levelIndex + 1) + "关开放手环" + slot + "的初始缺口不应贴着地图金环");
                }
            }
        }

        [Test]
        public void SavedLevelGapSanitizerMovesAnOldOpeningAwayFromItsGoldLoop()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            var clone = levelDataType.GetMethod("Clone", BindingFlags.Instance | BindingFlags.Public);
            var getSlotPosition = levelDataType.GetMethod("GetSlotPosition", BindingFlags.Instance | BindingFlags.Public);
            var sanitize = levelDataType.GetMethod(
                "EnsureInitialGoldLoopGapClearance",
                BindingFlags.Instance | BindingFlags.Public);
            var level = clone.Invoke(((Array)factory.Invoke(null, null)).GetValue(1), null);
            var rings = (Array)GetFieldValue(level, "Rings");
            var edges = (Array)GetFieldValue(level, "Edges");
            object attachedEdge = null;
            for (var i = 0; i < edges.Length; i++)
            {
                var kind = (int)GetFieldValue(edges.GetValue(i), "Kind");
                if (kind == 2 || kind == 3)
                {
                    attachedEdge = edges.GetValue(i);
                    break;
                }
            }
            Assert.IsNotNull(attachedEdge);
            var edgeKind = (int)GetFieldValue(attachedEdge, "Kind");
            var slotA = (int)GetFieldValue(attachedEdge, "SlotA");
            var slotB = (int)GetFieldValue(attachedEdge, "SlotB");
            var owner = edgeKind == 2 ? slotA : slotB;
            var target = edgeKind == 2 ? slotB : slotA;
            var ownerPosition = (Vector2)getSlotPosition.Invoke(level, new object[] { owner });
            var targetPosition = (Vector2)getSlotPosition.Invoke(level, new object[] { target });
            var buckleAngle = Mathf.Atan2(
                targetPosition.y - ownerPosition.y,
                targetPosition.x - ownerPosition.x) * Mathf.Rad2Deg;
            SetFieldValue(rings.GetValue(owner), "GapAngle", buckleAngle);

            Assert.IsTrue((bool)sanitize.Invoke(level, null), "旧存档中贴着随环金环的缺口必须被迁移");
            var migratedGap = (float)GetFieldValue(rings.GetValue(owner), "GapAngle");
            Assert.GreaterOrEqual(Mathf.Abs(Mathf.DeltaAngle(migratedGap, buckleAngle)), 55f);
        }

        [UnityTest]
        public IEnumerator FirstFiveBuiltInLevelsAreSolvableThroughRotationRules()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod(
                "CreateBuiltInLevels",
                BindingFlags.Static | BindingFlags.Public);
            var builtInLevels = (Array)factory.Invoke(null, null);

            for (var levelIndex = 0; levelIndex < builtInLevels.Length - 1; levelIndex++)
            {
                Cleanup();
                var singleLevel = Array.CreateInstance(levelDataType, 1);
                singleLevel.SetValue(builtInLevels.GetValue(levelIndex), 0);
                SetLevelDefinitionsOverride(singleLevel);
                hostObject = new GameObject("BraceletUnlinkTestHost");
                controller = hostObject.AddComponent<MiniGameAppController>();
                yield return null;
                controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
                yield return null;
                game = GetActiveGame(controller);

                var solved = false;
                for (var pass = 0; pass < 24 && !solved; pass++)
                {
                    var progressBefore = GetPuzzleReleaseProgress();
                    var openingAttachedLoops = GetListField(game, "attachedBuckles");
                    for (var linkIndex = levelIndex == 2 ? openingAttachedLoops.Count - 1 : -1;
                         linkIndex >= 0;
                         linkIndex--)
                    {
                        var link = openingAttachedLoops[linkIndex];
                        if (!GetBoolField(link, "IsThreadingTarget"))
                        {
                            continue;
                        }
                        var currentRings = GetListField(game, "rings");
                        var owner = currentRings[GetIntField(link, "Owner")];
                        var target = currentRings[GetIntField(link, "Target")];
                        var ownerPosition = (Vector2)GetFieldValue(owner, "InitialPosition");
                        var targetPosition = (Vector2)GetFieldValue(target, "InitialPosition");
                        TryRotateRingToAngle(target, Mathf.Atan2(
                            ownerPosition.y - targetPosition.y,
                            ownerPosition.x - targetPosition.x) * Mathf.Rad2Deg);
                    }

                    var mapLoops = GetListField(game, "mapBuckles");
                    for (var buckleIndex = 0; buckleIndex < mapLoops.Count; buckleIndex++)
                    {
                        var buckle = mapLoops[buckleIndex];
                        if (!GetBoolField(buckle, "IsActive"))
                        {
                            continue;
                        }
                        var currentRings = GetListField(game, "rings");
                        TryRotateRingToAngle(
                            currentRings[GetIntField(buckle, "RingB")],
                            GetFloatField(buckle, "RingBAngle"));
                        if (GetBoolField(buckle, "IsActive"))
                        {
                            TryRotateRingToAngle(
                                currentRings[GetIntField(buckle, "RingA")],
                                GetFloatField(buckle, "RingAAngle"));
                        }
                    }

                    var attachedLoops = GetListField(game, "attachedBuckles");
                    for (var linkIndex = attachedLoops.Count - 1; linkIndex >= 0; linkIndex--)
                    {
                        var link = attachedLoops[linkIndex];
                        if (!GetBoolField(link, "IsThreadingTarget"))
                        {
                            continue;
                        }
                        var currentRings = GetListField(game, "rings");
                        var owner = currentRings[GetIntField(link, "Owner")];
                        var target = currentRings[GetIntField(link, "Target")];
                        var ownerPosition = (Vector2)GetFieldValue(owner, "InitialPosition");
                        var targetPosition = (Vector2)GetFieldValue(target, "InitialPosition");
                        TryRotateRingToAngle(target, Mathf.Atan2(
                            ownerPosition.y - targetPosition.y,
                            ownerPosition.x - targetPosition.x) * Mathf.Rad2Deg);
                    }

                    solved = GetBoolField(game, "isCompleted");
                    if (!solved && GetPuzzleReleaseProgress() <= progressBefore)
                    {
                        break;
                    }
                }

                Assert.IsTrue(
                    solved,
                    "第" + (levelIndex + 1) + "关必须存在由真实碰撞与解套规则组成的逐步解法；剩余："
                    + DescribeRemainingPuzzleConstraints());
            }
        }

        [Test]
        public void RandomGeneratorIsDeterministicAndOnlyUsesValidEditorOperations()
        {
            var first = GenerateRandomLevel(1234567, 9);
            var repeated = GenerateRandomLevel(1234567, 9);
            var different = GenerateRandomLevel(7654321, 9);
            var firstJson = JsonUtility.ToJson(first);
            Assert.AreEqual(firstJson, JsonUtility.ToJson(repeated), "相同种子必须生成完全相同的随机关卡");
            Assert.AreNotEqual(firstJson, JsonUtility.ToJson(different), "不同种子不应固定生成同一关");

            var rings = (Array)GetFieldValue(first, "Rings");
            var edges = (Array)GetFieldValue(first, "Edges");
            CollectionAssert.AreEqual(
                new[] { 5, 4, 5, 4, 5 },
                (int[])GetFieldValue(first, "RowLengths"),
                "随机模式必须使用五行、最宽五列的六方向棋盘");
            var occupied = 0;
            var activeEdges = 0;
            var mapEdges = 0;
            var attachedEdges = 0;
            var closedRings = 0;
            var connectionCounts = new int[rings.Length];
            for (var i = 0; i < rings.Length; i++)
            {
                var kind = (int)GetFieldValue(rings.GetValue(i), "Kind");
                if (kind != 0)
                {
                    occupied += 1;
                }
                closedRings += kind == 2 ? 1 : 0;
            }
            for (var i = 0; i < edges.Length; i++)
            {
                var kind = (int)GetFieldValue(edges.GetValue(i), "Kind");
                if (kind == 0)
                {
                    continue;
                }
                activeEdges += 1;
                mapEdges += kind == 1 ? 1 : 0;
                attachedEdges += kind == 2 || kind == 3 ? 1 : 0;
                var slotA = GetIntField(edges.GetValue(i), "SlotA");
                var slotB = GetIntField(edges.GetValue(i), "SlotB");
                Assert.AreNotEqual(0, (int)GetFieldValue(rings.GetValue(slotA), "Kind"));
                Assert.AreNotEqual(0, (int)GetFieldValue(rings.GetValue(slotB), "Kind"));
                connectionCounts[slotA] += 1;
                connectionCounts[slotB] += 1;
            }

            Assert.That(occupied, Is.InRange(14, 23), "随机模式手环数量必须在14到23之间");
            Assert.Greater(activeEdges, 15, "随机模式必须保留第八关核心并增加真实约束");
            Assert.That(closedRings, Is.InRange(3, 7), "完整环数量必须在可解的安全位置范围内随机");
            for (var slot = 0; slot < rings.Length; slot++)
            {
                var isOccupied = (int)GetFieldValue(rings.GetValue(slot), "Kind") != 0;
                if (isOccupied)
                {
                    Assert.Greater(connectionCounts[slot], 0, "每个手环都必须参与至少一个真实金环连接");
                }
                else
                {
                    Assert.AreEqual(0, connectionCounts[slot], "空格不得残留金环连接");
                }
            }
            Assert.Greater(mapEdges, 0);
            Assert.Greater(attachedEdges, 0);

            var generatedRingCounts = new HashSet<int>();
            var generatedClosedCounts = new HashSet<int>();
            for (var seed = 1; seed <= 256; seed++)
            {
                var generated = GenerateRandomLevel(seed, seed % 19);
                int generatedClosed;
                generatedRingCounts.Add(CountGeneratedRings(generated, out generatedClosed));
                generatedClosedCounts.Add(generatedClosed);
            }
            for (var ringCount = 14; ringCount <= 23; ringCount++)
            {
                Assert.IsTrue(generatedRingCounts.Contains(ringCount), "随机生成必须覆盖手环数量：" + ringCount);
            }
            Assert.GreaterOrEqual(generatedClosedCounts.Count, 4, "完整环数量必须随种子产生明显变化");
        }

        [UnityTest]
        public IEnumerator RandomGeneratedLevelsAreSolvableThroughActualRotationRules()
        {
            var samples = new Dictionary<int, object>();
            var sampleSeeds = new Dictionary<int, int>();
            for (var seed = 1; seed <= 512 && samples.Count < 10; seed++)
            {
                var generated = GenerateRandomLevel(seed, seed % 23);
                int closedCount;
                var ringCount = CountGeneratedRings(generated, out closedCount);
                if (!samples.ContainsKey(ringCount))
                {
                    samples.Add(ringCount, generated);
                    sampleSeeds.Add(ringCount, seed);
                }
            }
            Assert.AreEqual(10, samples.Count, "真实求解测试必须覆盖14到23的每一种手环数量");

            for (var ringCount = 14; ringCount <= 23; ringCount++)
            {
                Cleanup();
                var generated = samples[ringCount];
                var levelType = generated.GetType();
                var singleLevel = Array.CreateInstance(levelType, 1);
                singleLevel.SetValue(generated, 0);
                SetLevelDefinitionsOverride(singleLevel);
                hostObject = new GameObject("BraceletUnlinkTestHost");
                controller = hostObject.AddComponent<MiniGameAppController>();
                yield return null;
                controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
                yield return null;
                game = GetActiveGame(controller);

                var solved = TrySolveGeneratedLevel(32);
                Assert.IsTrue(solved, "随机种子 " + sampleSeeds[ringCount] + "（" + ringCount + "环）必须能按实际旋转与碰撞规则通关；剩余："
                    + DescribeRemainingPuzzleConstraints() + "；数据：" + JsonUtility.ToJson(generated));
            }
        }

        [UnityTest]
        public IEnumerator CompletingFinalCampaignLevelUnlocksPersistentRandomMode()
        {
            Cleanup();
            var level = GenerateRandomLevel(314159, 0);
            var singleLevel = Array.CreateInstance(level.GetType(), 1);
            singleLevel.SetValue(level, 0);
            SetLevelDefinitionsOverride(singleLevel);
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);

            var randomButton = FindButtonIncludingInactive("BraceletRandomModeButton").gameObject;
            Assert.IsNotNull(randomButton);
            Assert.IsFalse(randomButton.activeSelf, "通关最后一关之前不得显示随机模式入口");
            Assert.IsTrue(TrySolveGeneratedLevel(32));
            Assert.IsTrue(randomButton.activeSelf, "通关最后一关后应立即显示随机模式入口");
            Assert.IsTrue(PlayerPrefs.HasKey(randomModeSaveKey), "随机模式解锁状态必须持久保存");

            randomButton.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(GetBoolField(game, "isRandomMode"));
            var summaryLabel = GetFieldValue(game, "summaryLabel");
            var randomSummary = (string)summaryLabel.GetType().GetProperty("text").GetValue(summaryLabel);
            StringAssert.StartsWith("随机模式", randomSummary);
            StringAssert.DoesNotContain("第", randomSummary, "随机模式不应显示没有玩法意义的累计局数");
            var firstRandomJson = JsonUtility.ToJson(GetFieldValue(game, "levelData"));

            randomButton.GetComponent<Button>().onClick.Invoke();
            var skippedRandomJson = JsonUtility.ToJson(GetFieldValue(game, "levelData"));
            Assert.AreNotEqual(firstRandomJson, skippedRandomJson, "当前随机局尚未操作时，再点随机必须更换盘面");

            var operatedRing = (RectTransform)GetFieldValue(GetListField(game, "rings")[0], "Rect");
            var dragEvent = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    operatedRing.TransformPoint(new Vector3(60f, 0f, 0f)))
            };
            operatedRing.GetComponent<IBeginDragHandler>().OnBeginDrag(dragEvent);
            operatedRing.GetComponent<IEndDragHandler>().OnEndDrag(dragEvent);
            Assert.IsTrue(GetBoolField(game, "hasGameplayInteraction"), "真实拖动后必须记录为已有操作");

            GameObject.Find("RestartButton").GetComponent<Button>().onClick.Invoke();
            var restartPopup = GameObject.Find("MiniGamePopup");
            Assert.IsNotNull(restartPopup, "已经操作过的关卡点重置必须先弹出确认框");
            restartPopup.transform.Find("Dialog/Buttons/CancelButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNull(GameObject.Find("MiniGamePopup"), "取消重置后必须关闭确认框");
            Assert.IsTrue(GetBoolField(game, "hasGameplayInteraction"), "取消重置后必须保留当前操作记录");

            randomButton.GetComponent<Button>().onClick.Invoke();
            restartPopup = GameObject.Find("MiniGamePopup");
            Assert.IsNotNull(restartPopup, "已经操作过的随机局再点随机必须先弹出确认框");
            Assert.AreEqual(skippedRandomJson, JsonUtility.ToJson(GetFieldValue(game, "levelData")), "确认前必须保留当前盘面");
            restartPopup.transform.Find("Dialog/Buttons/CancelButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNull(GameObject.Find("MiniGamePopup"), "取消后必须关闭确认框");
            Assert.IsTrue(GetBoolField(game, "hasGameplayInteraction"), "取消后必须保留当前操作记录");

            randomButton.GetComponent<Button>().onClick.Invoke();
            restartPopup = GameObject.Find("MiniGamePopup");
            restartPopup.transform.Find("Dialog/Buttons/ConfirmButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(skippedRandomJson, JsonUtility.ToJson(GetFieldValue(game, "levelData")), "已经操作过的随机局再点随机只能重置当前盘面");
            Assert.AreEqual(0, GetIntField(game, "gestureCount"), "确认重新开始后必须清零操作次数");
            Assert.IsFalse(GetBoolField(game, "hasGameplayInteraction"), "确认重新开始后必须清除已有操作标记");

            Cleanup();
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);
            randomButton = GameObject.Find("BraceletRandomModeButton");
            Assert.IsTrue(randomButton.activeSelf, "重新进入游戏后随机模式仍应保持解锁");
            randomButton.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(skippedRandomJson, JsonUtility.ToJson(GetFieldValue(game, "levelData")), "未通关时应根据保存的种子恢复同一随机盘面");
        }

        [Test]
        public void SavedFourthLevelRingDataMatchesImportedJson()
        {
            var rings = GetListField(game, "rings");
            var expectedAngles = new[] { 135f, 60f, 150f, 90f, 0f, -180f, 150f, 0f, 0f, 0f, -120f, 0f };
            var expectedClosed = new HashSet<int> { 4, 7, 9 };
            Assert.AreEqual(expectedAngles.Length, rings.Count);
            for (var i = 0; i < rings.Count; i++)
            {
                Assert.AreEqual(expectedAngles[i], GetFloatField(rings[i], "InitialGapAngle"), 0.01f, "第四关缺口角度必须来自保存的JSON");
                Assert.AreEqual(expectedClosed.Contains(i), GetBoolField(rings[i], "IsClosed"), "第四关完整环类型必须来自保存的JSON");
            }
        }

        [Test]
        public void EveryGoldLoopRendersAboveBracelets()
        {
            var mapLoopCount = GetListField(game, "mapBuckles").Count;
            for (var i = 0; i < mapLoopCount; i++)
            {
                var mapLoop = GameObject.Find("MapBuckle_" + i).transform;
                Assert.AreEqual("MapBuckleFrontLayer", mapLoop.parent.name);
                Assert.Greater(mapLoop.parent.GetSiblingIndex(), GameObject.Find("RingLayer").transform.GetSiblingIndex());
            }

            var links = GetListField(game, "attachedBuckles");
            for (var attachedIndex = 0; attachedIndex < links.Count; attachedIndex++)
            {
                var attachedVisual = (RectTransform)GetFieldValue(links[attachedIndex], "Visual");
                Assert.IsNotNull(attachedVisual);
                Assert.AreEqual("MapBuckleFrontLayer", attachedVisual.parent.name, "随环金环可见图形必须统一放在手环前景层");
            }
        }

        [Test]
        public void MapBuckleReferencesTwoDifferentRings()
        {
            var buckles = GetListField(game, "mapBuckles");
            var seenPairs = new HashSet<string>();
            var connectionCounts = new int[12];
            for (var i = 0; i < buckles.Count; i++)
            {
                var ringA = GetIntField(buckles[i], "RingA");
                var ringB = GetIntField(buckles[i], "RingB");
                Assert.AreNotEqual(ringA, ringB);
                Assert.IsTrue(seenPairs.Add(Mathf.Min(ringA, ringB) + ":" + Mathf.Max(ringA, ringB)));
                connectionCounts[ringA] += 1;
                connectionCounts[ringB] += 1;
            }

            for (var i = 0; i < connectionCounts.Length; i++)
            {
                var hasAttachedLoop = ((IList)GetFieldValue(GetListField(game, "rings")[i], "AttachedLinkIds")).Count > 0;
                Assert.IsTrue(connectionCounts[i] > 0 || hasAttachedLoop, "初始关卡不得出现没有任何连接却仍留在场上的手环: " + i);
            }
        }

        [Test]
        public void GoldLoopsExtendIntoBraceletInterior()
        {
            var mapRoot = GameObject.Find("MapBuckle_0").GetComponent<RectTransform>();
            var ringA = GameObject.Find("Bracelet_0").GetComponent<RectTransform>();
            var ringB = GameObject.Find("Bracelet_1").GetComponent<RectTransform>();
            var mapHalfLength = mapRoot.rect.width * 0.5f;
            var distanceToA = Vector2.Distance(mapRoot.anchoredPosition, ringA.anchoredPosition);
            var distanceToB = Vector2.Distance(mapRoot.anchoredPosition, ringB.anchoredPosition);
            Assert.LessOrEqual(distanceToA - mapHalfLength, 46.5f, "地图金环一端必须越过环带并明确延伸进第一个手环内孔");
            Assert.LessOrEqual(distanceToB - mapHalfLength, 46.5f, "地图金环另一端必须越过环带并明确延伸进第二个手环内孔");
            var ringDiameter = ringA.rect.width;
            Assert.That(mapRoot.rect.width / ringDiameter, Is.InRange(0.38f, 0.43f), "横向金环应保持视频中短而厚的比例，不能变成长横梁");

            var attached = GameObject.Find("Bracelet_2").transform.Find("AttachedBuckle_0").GetComponent<RectTransform>();
            var owner = GameObject.Find("Bracelet_2").GetComponent<RectTransform>();
            var target = GameObject.Find("Bracelet_0").GetComponent<RectTransform>();
            var direction = ((Vector2)target.anchoredPosition - owner.anchoredPosition).normalized;
            var ownerSideRadius = attached.anchoredPosition.magnitude - attached.rect.width * 0.5f;
            var targetSide = owner.anchoredPosition + direction * (attached.anchoredPosition.magnitude + attached.rect.width * 0.5f);
            Assert.AreEqual(76f, ownerSideRadius, 1f, "随环金环细端必须略微压入所属手环，不能像悬空挂件");
            Assert.LessOrEqual(Vector2.Distance(target.anchoredPosition, targetSide), 46.5f, "随环金环粗端必须越过目标环带并伸入目标手环内孔");
            var attachedVisual = (RectTransform)GetFieldValue(GetListField(game, "attachedBuckles")[0], "Visual");
            var attachedGraphic = attachedVisual.GetComponent<GoldLoopGraphic>();
            Assert.IsTrue(attachedGraphic.IsAttachedLoop);
            Assert.AreEqual(56f, attachedVisual.rect.width, 0.1f, "随环金环必须保持横向细长比例");
            Assert.AreEqual(40f, attachedVisual.rect.height, 0.1f, "随环金环不能继续呈现接近正方形的圆头挂件比例");
            var bodyStartFromOwner = ownerSideRadius + attachedGraphic.AttachedBodyStartOffset;
            var targetOuterEdgeFromOwner = Vector2.Distance(owner.anchoredPosition, target.anchoredPosition) - 82f;
            Assert.LessOrEqual(
                bodyStartFromOwner,
                targetOuterEdgeFromOwner,
                "随环金环粗部必须在接触目标手环外缘前完全展开，覆盖整段被套环带");
        }

        [Test]
        public void AttachedLoopsRecordOwnerAndTargetAsRealConstraints()
        {
            var links = GetListField(game, "attachedBuckles");
            Assert.AreEqual(10, links.Count);
            for (var i = 0; i < links.Count; i++)
            {
                Assert.AreNotEqual(GetIntField(links[i], "Owner"), GetIntField(links[i], "Target"));
                Assert.IsTrue(GetBoolField(links[i], "IsActive"));
            }

            var rings = GetListField(game, "rings");
            Assert.Greater(((IList)GetFieldValue(rings[5], "AttachedLinkIds")).Count, 0, "白色手环必须被真实随环金环约束，不能只是画一个装饰");
        }

        [Test]
        public void SavedFourthLevelGoldLoopOwnershipMatchesJson()
        {
            var mapPairs = new HashSet<string>();
            var mapBuckles = GetListField(game, "mapBuckles");
            for (var i = 0; i < mapBuckles.Count; i++)
            {
                mapPairs.Add(GetIntField(mapBuckles[i], "RingA") + ":" + GetIntField(mapBuckles[i], "RingB"));
            }

            CollectionAssert.AreEquivalent(
                new[] { "0:1", "6:7", "7:8", "7:10", "9:10" },
                mapPairs,
                "第四关地图金环归属必须与保存的JSON完全一致");

            var attachedPairs = new HashSet<string>();
            var attached = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attached.Count; i++)
            {
                attachedPairs.Add(GetIntField(attached[i], "Owner") + ":" + GetIntField(attached[i], "Target"));
            }

            CollectionAssert.AreEquivalent(
                new[] { "2:0", "4:1", "3:2", "4:3", "7:3", "4:8", "9:5", "9:6", "11:8", "11:10" },
                attachedPairs,
                "第四关随环金环的所属端和目标端必须与保存的JSON完全一致");
        }

        [Test]
        public void ClosedRingsUseFullCircleGraphicsAndColliders()
        {
            var closedIds = new[] { 4 };
            for (var i = 0; i < closedIds.Length; i++)
            {
                var ring = GameObject.Find("Bracelet_" + closedIds[i]);
                Assert.IsTrue(ring.GetComponent<BraceletRingGraphic>().IsClosed);
                Assert.GreaterOrEqual(ring.GetComponents<CircleCollider2D>().Length, 22, "完整手环必须使用完整一圈碰撞，不能保留隐藏缺口");
            }

            var openRing = GameObject.Find("Bracelet_3");
            Assert.IsFalse(openRing.GetComponent<BraceletRingGraphic>().IsClosed);
            Assert.Less(openRing.GetComponents<CircleCollider2D>().Length, 22);
        }

        [Test]
        public void LastMapBuckleIsConfirmedDiagonalCrossRowConnection()
        {
            var buckle = FindMapBuckleBySlots(9, 12);
            Assert.IsNotNull(buckle);

            var root = (RectTransform)GetFieldValue(buckle, "Root");
            var expected = Mathf.Atan2(-145f, -84.5f) * Mathf.Rad2Deg;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(root.localEulerAngles.z, expected)), 0.1f);
        }

        [Test]
        public void GoldLoopCenterIsWideEnoughForBraceletBandToPassThrough()
        {
            var root = GameObject.Find("MapBuckle_0").GetComponent<RectTransform>();
            var topRail = root.Find("TopRail").GetComponent<BoxCollider2D>();
            var bottomRail = root.Find("BottomRail").GetComponent<BoxCollider2D>();
            var topInnerEdge = topRail.transform.localPosition.y - topRail.size.y * 0.5f;
            var bottomInnerEdge = bottomRail.transform.localPosition.y + bottomRail.size.y * 0.5f;

            Assert.Greater(topInnerEdge - bottomInnerEdge, 28f, "金环孔洞必须能容纳手环环带");
            Assert.IsNull(root.GetComponent<BoxCollider2D>(), "金环中心不得有实心碰撞体");
        }

        [Test]
        public void GoldLoopGraphicAppliesComponentTintAndAlphaToRenderedVertices()
        {
            var graphic = GameObject.Find("MapBuckle_0").GetComponent<GoldLoopGraphic>();
            MethodInfo populate = null;
            var methods = typeof(GoldLoopGraphic).GetMethods(InstancePrivate);
            for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                var parameters = methods[methodIndex].GetParameters();
                if (methods[methodIndex].Name == "OnPopulateMesh"
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(VertexHelper))
                {
                    populate = methods[methodIndex];
                    break;
                }
            }
            Assert.IsNotNull(populate);

            var originalColor = graphic.color;
            graphic.color = new Color(0f, 0.5f, 1f, 0.25f);
            var helper = new VertexHelper();
            populate.Invoke(graphic, new object[] { helper });
            var vertices = new List<UIVertex>();
            helper.GetUIVertexStream(vertices);
            helper.Dispose();
            graphic.color = originalColor;

            Assert.Greater(vertices.Count, 0);
            for (var i = 0; i < vertices.Count; i++)
            {
                Assert.That(vertices[i].color.a, Is.InRange(63, 64), "最终网格顶点必须继承GoldLoopGraphic.color的透明度");
            }
            Assert.Less(vertices[0].color.r, vertices[0].color.b, "最终网格顶点必须继承GoldLoopGraphic.color的色调");
        }

        [Test]
        public void SingleOpeningCanPassThroughWithoutLockingRing()
        {
            var rings = GetListField(game, "rings");
            var buckles = GetListField(game, "mapBuckles");
            var buckle = buckles[0];
            var movingTarget = GetFloatField(buckle, "RingAAngle");
            var otherTarget = GetFloatField(buckle, "RingBAngle");
            InvokeSetRingAngle(rings[1], otherTarget + 90f);
            InvokeSetRingAngle(rings[0], movingTarget);
            InvokeCheckMapBuckleMatches(rings[0], movingTarget, 0f);

            InvokeSetRingAngle(rings[0], movingTarget + 40f);

            Assert.AreEqual(movingTarget + 40f, GetFloatField(rings[0], "GapAngle"), 0.5f);
            Assert.IsTrue(GetBoolField(buckle, "IsActive"));
            Assert.AreEqual(0, GetIntField(game, "clearedMapBuckleCount"));
        }

        [Test]
        public void BothOpeningsDockedClearMapBuckleButKeepAttachedBuckles()
        {
            var rings = GetListField(game, "rings");
            var buckles = GetListField(game, "mapBuckles");
            var buckle = buckles[0];
            SetIncomingAttachedLinksThreading(rings[0], false);
            SetIncomingAttachedLinksThreading(rings[1], false);
            var ringATarget = GetFloatField(buckle, "RingAAngle");
            var ringBTarget = GetFloatField(buckle, "RingBAngle");
            InvokeSetRingAngle(rings[0], ringATarget);
            InvokeSetRingAngle(rings[1], ringBTarget);
            InvokeCheckMapBuckleMatches(rings[1], ringBTarget, 0f);

            Assert.IsFalse(GetBoolField(buckles[0], "IsActive"));
            var eliminatedRoot = (RectTransform)GetFieldValue(buckles[0], "Root");
            var initialPosition = eliminatedRoot.anchoredPosition;
            var initialRotation = eliminatedRoot.localEulerAngles.z;
            Assert.IsTrue(eliminatedRoot.gameObject.activeSelf, "地图金环解除碰撞后必须保留到退场动画结束");
            var rails = (BoxCollider2D[])GetFieldValue(buckles[0], "RailColliders");
            Assert.IsFalse(rails[0].enabled);
            Assert.IsFalse(rails[1].enabled);
            game.Tick(0.2f);
            Assert.Greater(eliminatedRoot.anchoredPosition.y, initialPosition.y, "地图金环消除时应轻微上浮");
            Assert.Less(eliminatedRoot.GetComponent<GoldLoopGraphic>().color.a, 1f, "地图金环消除时应逐渐淡出");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(eliminatedRoot.localEulerAngles.z, initialRotation)), 0.01f, "地图金环退场不得旋转");
            Assert.IsTrue(eliminatedRoot.gameObject.activeSelf);
            game.Tick(0.3f);
            Assert.IsTrue(eliminatedRoot.gameObject.activeSelf, "地图金环必须与手环使用相同的0.62秒退场时长");
            game.Tick(0.2f);
            Assert.IsFalse(eliminatedRoot.gameObject.activeSelf);
            Assert.AreEqual(1, GetIntField(game, "clearedMapBuckleCount"));
            var attached = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attached.Count; i++)
            {
                Assert.IsTrue(GetBoolField(attached[i], "IsActive"));
                Assert.IsTrue(((RectTransform)GetFieldValue(attached[i], "Visual")).gameObject.activeSelf);
            }
        }

        [Test]
        public void BothMapOpeningsClearBuckleEvenWhenRingsHaveOtherConstraints()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[0];
            var ringA = rings[GetIntField(buckle, "RingA")];
            var ringB = rings[GetIntField(buckle, "RingB")];
            Assert.Greater(GetIncomingAttachedLinkCount(ringA), 0);
            Assert.Greater(GetIncomingAttachedLinkCount(ringB), 0);
            InvokeSetRingAngle(ringA, GetFloatField(buckle, "RingAAngle"));
            InvokeSetRingAngle(ringB, GetFloatField(buckle, "RingBAngle"));

            InvokeCheckMapBuckleMatches(ringB, GetFloatField(buckle, "RingBAngle"), 0f);

            Assert.IsFalse(GetBoolField(buckle, "IsActive"), "两侧缺口同时到位时，地图金环必须直接消除，不受其他约束影响");
            Assert.IsFalse(GetBoolField(ringA, "IsCleared"));
            Assert.IsFalse(GetBoolField(ringB, "IsCleared"));
        }

        [UnityTest]
        public IEnumerator ThirdLevelTopRightMapBuckleWaitsForBothOpenings()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod(
                "CreateSixRingChainLevel",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(factory);
            var regressionLevel = factory.Invoke(null, null);

            for (var direction = 0; direction < 2; direction++)
            {
                Cleanup();
                var singleLevel = Array.CreateInstance(levelDataType, 1);
                singleLevel.SetValue(regressionLevel, 0);
                SetLevelDefinitionsOverride(singleLevel);
                hostObject = new GameObject("BraceletUnlinkTestHost");
                controller = hostObject.AddComponent<MiniGameAppController>();
                yield return null;
                controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
                yield return null;
                game = GetActiveGame(controller);

                var rings = GetListField(game, "rings");
                object middle = null;
                object right = null;
                for (var ringIndex = 0; ringIndex < rings.Count; ringIndex++)
                {
                    if (GetIntField(rings[ringIndex], "SlotId") == 1) middle = rings[ringIndex];
                    if (GetIntField(rings[ringIndex], "SlotId") == 2) right = rings[ringIndex];
                }
                Assert.IsNotNull(middle);
                Assert.IsNotNull(right);
                var moving = direction == 0 ? middle : right;
                var other = direction == 0 ? right : middle;
                var buckle = FindMapBuckleBySlots(1, 2);
                Assert.IsNotNull(buckle);
                var movingIsA = GetIntField(buckle, "RingA") == GetIntField(moving, "Id");
                var movingTarget = GetFloatField(buckle, movingIsA ? "RingAAngle" : "RingBAngle");
                var otherTarget = GetFloatField(buckle, movingIsA ? "RingBAngle" : "RingAAngle");
                InvokeSetRingAngle(other, otherTarget + 90f);
                InvokeSetRingAngle(moving, movingTarget);

                InvokeCheckMapBuckleMatches(moving, movingTarget - 10f, 20f);

                Assert.IsTrue(GetBoolField(buckle, "IsActive"), "第三关第一排右侧地图金环不能因单侧缺口到位而消失");
                Assert.IsTrue(
                    GetBoolField(buckle, movingIsA ? "RingAThreaded" : "RingBThreaded"),
                    "单侧缺口到位不能被永久记录为解套");
                Assert.IsTrue(
                    GetBoolField(buckle, movingIsA ? "RingBThreaded" : "RingAThreaded"),
                    "未对准缺口的另一端必须继续保持穿套");
                Assert.IsFalse(GetBoolField(other, "IsCleared"), "另一端缺口未对准时，对应手环不能被连带消除");

                InvokeSetRingAngle(other, otherTarget);
                InvokeCheckMapBuckleMatches(other, otherTarget - 10f, 20f);
                Assert.IsFalse(GetBoolField(buckle, "IsActive"), "两端先后真正解套后，地图金环才应消除");
            }
        }

        [Test]
        public void PlayerRotationCanAlignBothOpeningsAndClearFirstMapBuckle()
        {
            var rings = GetListField(game, "rings");
            var buckles = GetListField(game, "mapBuckles");
            var buckle = buckles[0];
            SetIncomingAttachedLinksThreading(rings[0], false);
            SetIncomingAttachedLinksThreading(rings[1], false);
            InvokeSetRingAngle(rings[1], GetFloatField(buckle, "RingBAngle"));
            var target = GetFloatField(buckle, "RingAAngle");
            InvokeSetRingAngle(rings[0], target);
            InvokeCheckMapBuckleMatches(rings[0], target, 0f);

            Assert.IsFalse(GetBoolField(buckles[0], "IsActive"));
            Assert.AreEqual(1, GetIntField(game, "clearedMapBuckleCount"));
        }

        [Test]
        public void LastMapBuckleSideReleasesRingWithoutWaitingForOtherEnd()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[0];
            var moving = rings[GetIntField(buckle, "RingA")];
            var other = rings[GetIntField(buckle, "RingB")];
            SetIncomingAttachedLinksThreading(moving, false);
            var movingTarget = GetFloatField(buckle, "RingAAngle");
            InvokeSetRingAngle(other, GetFloatField(buckle, "RingBAngle") + 90f);
            InvokeSetRingAngle(moving, movingTarget);

            InvokeCheckMapBuckleMatches(moving, movingTarget - 10f, 20f);

            Assert.IsTrue(GetBoolField(moving, "IsCleared"), "地图金环是最后约束时，本端缺口到位就应独立脱套");
            Assert.IsFalse(GetBoolField(other, "IsCleared"));
            Assert.IsTrue(GetBoolField(buckle, "IsActive"), "另一端仍被套住时，地图金环本体必须保留");
            Assert.AreEqual(0, GetIntField(game, "clearedMapBuckleCount"));
        }

        [Test]
        public void RingClearsWhenItsOnlyMapBuckleHasAlreadyReleasedTheOtherEnd()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[2];
            var ring = rings[GetIntField(buckle, "RingB")];
            var releasingRing = rings[GetIntField(buckle, "RingA")];
            var ringId = GetIntField(ring, "Id");
            var releasingRingId = GetIntField(releasingRing, "Id");
            Assert.AreEqual(1, ((IList)GetFieldValue(ring, "LinkedBuckleIds")).Count);
            Assert.Greater(((IList)GetFieldValue(releasingRing, "LinkedBuckleIds")).Count, 1);
            SetFieldValue(ring, "IsClosed", true);
            SetFieldValue(releasingRing, "IsClosed", false);
            var attachedLinks = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attachedLinks.Count; i++)
            {
                var link = attachedLinks[i];
                var ownerId = GetIntField(link, "Owner");
                var targetId = GetIntField(link, "Target");
                if (ownerId == ringId || targetId == ringId
                    || ownerId == releasingRingId || targetId == releasingRingId)
                {
                    SetFieldValue(link, "IsThreadingTarget", false);
                }
            }
            var releasingTarget = GetFloatField(buckle, "RingAAngle");
            InvokeSetRingAngle(releasingRing, releasingTarget);

            InvokeTryReleaseUnlinkedRing(ring);
            Assert.IsFalse(GetBoolField(ring, "IsCleared"), "地图金环两端仍穿套时必须继续束缚手环");

            InvokeCheckMapBuckleMatches(releasingRing, releasingTarget - 10f, 20f);

            Assert.IsTrue(GetBoolField(ring, "IsCleared"), "地图金环另一端已解套时，剩余一端不应继续束缚手环");
            Assert.IsFalse(GetBoolField(releasingRing, "IsCleared"), "解开其中一个地图金环后，手环仍应受到其他地图金环约束");
            Assert.IsFalse(GetBoolField(buckle, "IsActive"), "手环清除后，已经没有双端穿套关系的地图金环也应消失");
            Assert.AreEqual(1, GetIntField(game, "clearedMapBuckleCount"));
        }

        [Test]
        public void ClosedRingClearsWhenEveryOppositeMapEndIsDockedTogether()
        {
            var rings = GetListField(game, "rings");
            object center = null;
            object left = null;
            object right = null;
            object lowerLeft = null;
            for (var i = 0; i < rings.Count; i++)
            {
                var slot = GetIntField(rings[i], "SlotId");
                if (slot == 9) center = rings[i];
                if (slot == 8) left = rings[i];
                if (slot == 10) right = rings[i];
                if (slot == 12) lowerLeft = rings[i];
            }

            Assert.IsNotNull(center);
            Assert.IsTrue(GetBoolField(center, "IsClosed"));
            var centerId = GetIntField(center, "Id");
            var attachedLinks = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attachedLinks.Count; i++)
            {
                if (GetIntField(attachedLinks[i], "Owner") == centerId)
                {
                    SetFieldValue(attachedLinks[i], "IsThreadingTarget", false);
                }
            }

            var leftBuckle = FindMapBuckleBySlots(8, 9);
            var rightBuckle = FindMapBuckleBySlots(9, 10);
            var diagonalBuckle = FindMapBuckleBySlots(9, 12);
            var leftId = GetIntField(left, "Id");
            SetFieldValue(leftBuckle, GetIntField(leftBuckle, "RingA") == leftId ? "RingAThreaded" : "RingBThreaded", false);
            SetFieldValue(left, "IsCleared", true);

            Assert.Greater(GetIncomingAttachedLinkCount(right), 0, "右侧手环应保留截图中的随环约束");
            Assert.Greater(GetIncomingAttachedLinkCount(lowerLeft), 0, "左下手环应保留截图中的随环约束");
            var rightTarget = GetIntField(rightBuckle, "RingA") == GetIntField(right, "Id")
                ? GetFloatField(rightBuckle, "RingAAngle")
                : GetFloatField(rightBuckle, "RingBAngle");
            var diagonalTarget = GetIntField(diagonalBuckle, "RingA") == GetIntField(lowerLeft, "Id")
                ? GetFloatField(diagonalBuckle, "RingAAngle")
                : GetFloatField(diagonalBuckle, "RingBAngle");

            InvokeSetRingAngle(right, rightTarget);
            InvokeCheckMapBuckleMatches(right, rightTarget - 10f, 20f);
            Assert.IsFalse(GetBoolField(center, "IsCleared"), "还有一个地图出口未到位时完整环不能提前消除");

            InvokeSetRingAngle(lowerLeft, diagonalTarget);
            InvokeCheckMapBuckleMatches(lowerLeft, diagonalTarget - 10f, 20f);

            Assert.IsTrue(GetBoolField(center, "IsCleared"), "所有地图金环另一端都进入缺口后，完整环应整体消除");
            Assert.IsFalse(GetBoolField(rightBuckle, "IsActive"));
            Assert.IsFalse(GetBoolField(diagonalBuckle, "IsActive"));
        }

        [Test]
        public void MapBuckleSideRemainsThreadedWhenGapPassesButOtherConstraintKeepsRingBound()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[0];
            var moving = rings[GetIntField(buckle, "RingB")];
            var other = rings[GetIntField(buckle, "RingA")];
            Assert.Greater(GetIncomingAttachedLinkCount(moving), 0);
            InvokeSetRingAngle(other, GetFloatField(buckle, "RingAAngle") + 90f);
            var movingTarget = GetFloatField(buckle, "RingBAngle");
            InvokeSetRingAngle(moving, movingTarget);

            InvokeCheckMapBuckleMatches(moving, movingTarget - 10f, 20f);

            Assert.IsTrue(GetBoolField(buckle, "RingBThreaded"), "手环仍受其他约束时，缺口经过地图金环不能被永久记录为解套");
            Assert.IsTrue(GetBoolField(buckle, "IsActive"), "另一端尚未解套时，地图金环本体必须保留");
            Assert.IsFalse(GetBoolField(moving, "IsCleared"), "随环金环仍在穿套时，手环本身不能连带消除");

            InvokeSetRingAngle(moving, movingTarget + 40f);
            InvokeCheckMapBuckleMatches(moving, movingTarget, 40f);

            Assert.IsTrue(GetBoolField(buckle, "RingBThreaded"), "缺口离开后，地图金环应继续套在当前手环上");
        }

        [UnityTest]
        public IEnumerator SecondLevelRightMapBuckleClearsAfterEmptySideAndRemainingOpeningAlign()
        {
            var levelDataType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkLevelData");
            var factory = levelDataType.GetMethod("CreateBuiltInLevels", BindingFlags.Static | BindingFlags.Public);
            var builtInLevels = (Array)factory.Invoke(null, null);
            Cleanup();
            var singleLevel = Array.CreateInstance(levelDataType, 1);
            singleLevel.SetValue(builtInLevels.GetValue(1), 0);
            SetLevelDefinitionsOverride(singleLevel);
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);

            var rings = GetListField(game, "rings");
            object upperRight = null;
            object lowerRight = null;
            for (var ringIndex = 0; ringIndex < rings.Count; ringIndex++)
            {
                if (GetIntField(rings[ringIndex], "SlotId") == 2) upperRight = rings[ringIndex];
                if (GetIntField(rings[ringIndex], "SlotId") == 4) lowerRight = rings[ringIndex];
            }
            Assert.IsNotNull(upperRight);
            Assert.IsNotNull(lowerRight);
            var buckle = FindMapBuckleBySlots(2, 4);
            Assert.IsNotNull(buckle);
            Assert.Greater(GetIncomingAttachedLinkCount(lowerRight), 0, "第二关右下手环应仍由随环金环束缚");
            var upperRightIsA = GetIntField(buckle, "RingA") == GetIntField(upperRight, "Id");
            SetFieldValue(buckle, upperRightIsA ? "RingAThreaded" : "RingBThreaded", false);
            SetFieldValue(upperRight, "IsCleared", true);
            var lowerTarget = GetFloatField(buckle, upperRightIsA ? "RingBAngle" : "RingAAngle");
            InvokeSetRingAngle(lowerRight, lowerTarget);

            InvokeCheckMapBuckleMatches(lowerRight, lowerTarget - 10f, 20f);

            Assert.IsFalse(GetBoolField(buckle, "IsActive"), "上端已空且下端缺口到位后，第二关右侧地图金环必须消除");
            Assert.IsFalse(GetBoolField(lowerRight, "IsCleared"), "地图金环消除后，随环金环仍应继续束缚右下手环");
            Assert.Greater(GetIncomingAttachedLinkCount(lowerRight), 0);
        }

        [Test]
        public void DockedMapOpeningLetsUnconstrainedOppositeRingClearWithoutReleasingMovingRing()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[2];
            var ring = rings[GetIntField(buckle, "RingB")];
            var moving = rings[GetIntField(buckle, "RingA")];
            var ringId = GetIntField(ring, "Id");
            Assert.AreEqual(1, ((IList)GetFieldValue(ring, "LinkedBuckleIds")).Count);
            Assert.Greater(((IList)GetFieldValue(moving, "LinkedBuckleIds")).Count, 1);
            Assert.Greater(((IList)GetFieldValue(moving, "OwnedAttachedBuckleIds")).Count, 0);
            SetFieldValue(ring, "IsClosed", true);
            SetFieldValue(moving, "IsClosed", false);
            var attachedLinks = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attachedLinks.Count; i++)
            {
                var link = attachedLinks[i];
                if (GetIntField(link, "Owner") == ringId || GetIntField(link, "Target") == ringId)
                {
                    SetFieldValue(link, "IsThreadingTarget", false);
                }
            }
            var movingTarget = GetFloatField(buckle, "RingAAngle");
            InvokeSetRingAngle(moving, movingTarget);

            InvokeCheckMapBuckleMatches(moving, movingTarget - 10f, 20f);

            Assert.IsTrue(GetBoolField(ring, "IsCleared"), "当前端缺口对准地图金环时，无其他约束的对面手环应能脱离");
            Assert.IsFalse(GetBoolField(moving, "IsCleared"), "当前手环仍被随环金环套住时不能一起消除");
            Assert.IsFalse(GetBoolField(buckle, "IsActive"), "对面手环脱离后，正位于当前缺口内的地图金环应消失");
        }

        [Test]
        public void ClosedRingInvisibleAngleCannotClearMapBuckle()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[0];
            var moving = rings[GetIntField(buckle, "RingA")];
            var other = rings[GetIntField(buckle, "RingB")];
            SetFieldValue(moving, "IsClosed", true);
            InvokeSetRingAngle(other, GetFloatField(buckle, "RingBAngle"));
            var movingTarget = GetFloatField(buckle, "RingAAngle");

            InvokeCheckMapBuckleMatches(moving, movingTarget - 10f, 20f);

            Assert.IsTrue(GetBoolField(buckle, "IsActive"), "完整环内部保存的不可见角度不得参与地图金环缺口判定");
            Assert.AreEqual(0, GetIntField(game, "clearedMapBuckleCount"));
        }

        [Test]
        public void AttachedBuckleRotatesWithRingWhileMapBuckleStaysFixed()
        {
            var ring = GameObject.Find("Bracelet_2").GetComponent<RectTransform>();
            var attached = ring.Find("AttachedBuckle_0").GetComponent<RectTransform>();
            var mapBuckle = GameObject.Find("MapBuckle_2").GetComponent<RectTransform>();
            var mapPosition = mapBuckle.position;
            var mapRotation = mapBuckle.rotation;
            var attachedStart = attached.position;

            InvokeSetRingAngle(GetListField(game, "rings")[2], 55f);

            Assert.Greater(Vector3.Distance(attachedStart, attached.position), 1f);
            Assert.Less(Vector3.Distance(mapPosition, mapBuckle.position), 0.01f);
            Assert.Less(Quaternion.Angle(mapRotation, mapBuckle.rotation), 0.01f);
        }

        [Test]
        public void AttachedBuckleOnlyReleasesTargetAfterAllOtherConstraintsAreGone()
        {
            var rings = GetListField(game, "rings");
            var mapBuckle = GetListField(game, "mapBuckles")[0];
            var link = GetListField(game, "attachedBuckles")[0];
            var owner = rings[GetIntField(link, "Owner")];
            var target = rings[GetIntField(link, "Target")];
            var ownerPosition = (Vector2)GetFieldValue(owner, "InitialPosition");
            var targetPosition = (Vector2)GetFieldValue(target, "InitialPosition");
            var ownerToTarget = Mathf.Atan2(
                targetPosition.y - ownerPosition.y,
                targetPosition.x - ownerPosition.x) * Mathf.Rad2Deg;
            var targetToOwner = ownerToTarget + 180f;
            InvokeSetRingAngle(owner, ownerToTarget - GetFloatField(link, "LocalAngle"));
            InvokeSetRingAngle(target, targetToOwner);

            InvokeCheckAttachedBuckleMatches(target, targetToOwner - 10f, 20f);

            Assert.IsTrue(GetBoolField(link, "IsThreadingTarget"), "地图金环仍存在时，缺口经过随环金环不能提前记录为已脱套");
            Assert.IsFalse(GetBoolField(target, "IsCleared"));
            InvokeSetRingAngle(target, targetToOwner + 40f);
            InvokeCheckAttachedBuckleMatches(target, targetToOwner, 40f);
            InvokeEliminate(mapBuckle);
            Assert.IsTrue(GetBoolField(link, "IsThreadingTarget"), "之后消除地图金环不能沿用此前无效的缺口经过记录");
            Assert.IsFalse(GetBoolField(target, "IsCleared"), "目标手环仍被随环金环套住时不能随地图金环一起消除");

            InvokeSetRingAngle(target, targetToOwner);
            InvokeCheckAttachedBuckleMatches(target, targetToOwner - 10f, 20f);

            Assert.IsTrue(GetBoolField(link, "IsActive"), "目标手环从随环金环中脱出时，金环必须仍留在所属手环上");
            Assert.IsFalse(GetBoolField(link, "IsThreadingTarget"), "随环金环成为最后约束后，缺口再次经过才应真正脱套");
            Assert.IsTrue(GetBoolField(target, "IsCleared"));
            Assert.IsTrue(((RectTransform)GetFieldValue(link, "Visual")).gameObject.activeSelf);
        }

        [Test]
        public void RingDoesNotClearWhileItsOwnedAttachedLoopStillThreadsAnotherRing()
        {
            var rings = GetListField(game, "rings");
            var mapBuckle = GetListField(game, "mapBuckles")[1];
            var ringA = rings[GetIntField(mapBuckle, "RingA")];
            var ringB = rings[GetIntField(mapBuckle, "RingB")];
            Assert.Greater(((IList)GetFieldValue(ringB, "OwnedAttachedBuckleIds")).Count, 0);

            InvokeEliminate(mapBuckle);

            Assert.IsFalse(GetBoolField(ringB, "IsCleared"), "自身随环金环仍套着其他手环时，不能随地图金环一起消除");
        }

        [Test]
        public void OwnerClearsWhenEveryOwnedAttachedLoopIsEmptyOrInsideTargetGap()
        {
            var rings = GetListField(game, "rings");
            object owner = null;
            for (var i = 0; i < rings.Count; i++)
            {
                if (GetIntField(rings[i], "SlotId") == 6)
                {
                    owner = rings[i];
                    break;
                }
            }
            Assert.IsNotNull(owner);

            var ownerId = GetIntField(owner, "Id");
            var ownerPosition = (Vector2)GetFieldValue(owner, "InitialPosition");
            var links = GetListField(game, "attachedBuckles");
            var ownedCount = 0;
            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (GetIntField(link, "Target") == ownerId)
                {
                    SetFieldValue(link, "IsThreadingTarget", false);
                }
                if (GetIntField(link, "Owner") != ownerId)
                {
                    continue;
                }

                ownedCount += 1;
                var target = rings[GetIntField(link, "Target")];
                var targetPosition = (Vector2)GetFieldValue(target, "InitialPosition");
                var targetToOwner = Mathf.Atan2(
                    ownerPosition.y - targetPosition.y,
                    ownerPosition.x - targetPosition.x) * Mathf.Rad2Deg;
                InvokeSetRingAngle(target, targetToOwner);
            }
            Assert.GreaterOrEqual(ownedCount, 2);

            InvokeTryReleaseUnlinkedRing(owner);

            Assert.IsTrue(GetBoolField(owner, "IsCleared"), "所有随环金环为空或位于目标缺口时，所属手环应能整体消除");
        }

        [Test]
        public void GapLightlySnapsToNearbyMapBuckleAngle()
        {
            var rings = GetListField(game, "rings");
            var buckle = GetListField(game, "mapBuckles")[0];
            var ring = rings[GetIntField(buckle, "RingA")];
            var targetAngle = GetFloatField(buckle, "RingAAngle");
            InvokeSetRingAngle(ring, targetAngle - 14f);

            InvokeApplyRotation(GetIntField(ring, "Id"), 3f);

            Assert.AreEqual(targetAngle, GetFloatField(ring, "GapAngle"), 0.2f, "接近金环中心角度时应轻量吸附到正中");
        }

        [Test]
        public void GapLightlySnapsToNearbyAttachedBuckleAngle()
        {
            var rings = GetListField(game, "rings");
            var link = GetListField(game, "attachedBuckles")[0];
            var owner = rings[GetIntField(link, "Owner")];
            var target = rings[GetIntField(link, "Target")];
            var ownerPosition = (Vector2)GetFieldValue(owner, "InitialPosition");
            var targetPosition = (Vector2)GetFieldValue(target, "InitialPosition");
            var ownerToTarget = Mathf.Atan2(
                targetPosition.y - ownerPosition.y,
                targetPosition.x - ownerPosition.x) * Mathf.Rad2Deg;
            var targetToOwner = ownerToTarget + 180f;
            InvokeSetRingAngle(owner, ownerToTarget - GetFloatField(link, "LocalAngle"));
            InvokeSetRingAngle(target, targetToOwner - 14f);

            InvokeApplyRotation(GetIntField(target, "Id"), 3f);

            Assert.Less(
                Mathf.Abs(Mathf.DeltaAngle(GetFloatField(target, "GapAngle"), targetToOwner)),
                0.2f,
                "接近随环金环时也应吸附到缺口正中");
        }

        [Test]
        public void AttachedBuckleProtrudesBeyondRingOuterEdge()
        {
            var ring = GameObject.Find("Bracelet_2").GetComponent<RectTransform>();
            var attached = ring.Find("AttachedBuckle_0").GetComponent<RectTransform>();
            var buckleOuterRadius = attached.anchoredPosition.magnitude + attached.rect.width * 0.5f;
            var ringOuterRadius = ring.rect.width * 0.5f - 10f;

            Assert.Greater(attached.anchoredPosition.magnitude, ringOuterRadius, "随环金环中心必须位于环带外沿之外");
            Assert.Greater(buckleOuterRadius - ringOuterRadius, 10f, "随环金环碰撞体必须明显突出手环外沿");
        }

        [Test]
        public void PhysicsCollisionLimitsRotationIntoAnotherRingBuckle()
        {
            var rings = GetListField(game, "rings");
            var moving = rings[2];
            var attached = GetListField(game, "attachedBuckles");
            var obstacleRails = GetFieldValue(attached[1], "RailColliders") as BoxCollider2D[];
            var movingRails = GetFieldValue(attached[0], "RailColliders") as BoxCollider2D[];
            var obstacle = obstacleRails?[0];
            var movingBuckle = movingRails?[0];
            Assert.IsNotNull(obstacle);
            Assert.IsNotNull(movingBuckle);

            var obstacleTransform = obstacle.transform;
            var originalParent = obstacleTransform.parent;
            var originalPosition = obstacleTransform.position;
            var originalRotation = obstacleTransform.rotation;
            InvokeSetRingAngle(moving, 0f);
            var startPosition = movingBuckle.transform.position;
            InvokeSetRingAngle(moving, 30f);
            var blockedPosition = movingBuckle.transform.position;
            InvokeSetRingAngle(moving, 0f);
            obstacleTransform.SetParent(GameObject.Find("BraceletUnlinkBoard").transform, true);
            obstacleTransform.position = blockedPosition;
            Physics2D.SyncTransforms();

            var applied = InvokeApplyRotation(2, 30f);

            Assert.Less(applied, 28f);
            Assert.Less(Vector3.Distance(startPosition, movingBuckle.transform.position), Vector3.Distance(startPosition, blockedPosition));

            var legalAngle = GetFloatField(moving, "GapAngle");
            InvokeUpdateCollisionFeedback(moving, 30f, true);
            game.Tick(0.05f);
            var movingRect = GetFieldValue(moving, "Rect") as RectTransform;
            Assert.IsNotNull(movingRect);
            Assert.Greater(
                Mathf.Abs(Mathf.DeltaAngle(movingRect.localEulerAngles.z, legalAngle)),
                1f,
                "碰撞后手环画面必须朝受阻方向产生可见的小幅偏转");
            Assert.AreEqual(legalAngle, GetFloatField(moving, "GapAngle"), 0.001f, "碰撞反馈不得改变真实合法角度");
            game.Tick(0.2f);
            Assert.Less(
                Mathf.Abs(Mathf.DeltaAngle(movingRect.localEulerAngles.z, legalAngle)),
                0.01f,
                "碰撞偏转结束后必须回到真实合法角度");

            obstacleRails[0].enabled = false;
            obstacleRails[1].enabled = false;
            var allBuckles = GetListField(game, "mapBuckles");
            for (var i = 0; i < allBuckles.Count; i++)
            {
                var rails = GetFieldValue(allBuckles[i], "RailColliders") as BoxCollider2D[];
                rails[0].enabled = false;
                rails[1].enabled = false;
            }
            var reverse = InvokeApplyRotation(2, -15f);
            Assert.Less(reverse, -1f, "解除当前阻挡后必须能够立即反向转回去");
            obstacleRails[0].enabled = true;
            obstacleRails[1].enabled = true;
            obstacleTransform.SetParent(originalParent, true);
            obstacleTransform.position = originalPosition;
            obstacleTransform.rotation = originalRotation;
        }

        [UnityTest]
        public IEnumerator ThreadedMapAllowsBothRingsButAttachedOwnerIsPhysicallyConstrained()
        {
            GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            GameObject.Find("BraceletEditorClearButton").GetComponent<Button>().onClick.Invoke();

            var helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("RingSlot_0").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction0Button").GetComponent<Button>().onClick.Invoke();
            helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("RingSlot_1").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction0Button").GetComponent<Button>().onClick.Invoke();
            helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("EdgeSlot_0_1").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction0Button").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorTrialButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var rings = GetListField(game, "rings");
            Assert.AreEqual(2, rings.Count);
            var mapBuckle = GetListField(game, "mapBuckles")[0];
            InvokeSetRingAngle(rings[GetIntField(mapBuckle, "RingA")], GetFloatField(mapBuckle, "RingAAngle") + 90f);
            InvokeSetRingAngle(rings[GetIntField(mapBuckle, "RingB")], GetFloatField(mapBuckle, "RingBAngle") + 90f);
            AssertRingRotatesBothWays(rings[0]);
            AssertRingRotatesBothWays(rings[1]);

            GameObject.Find("BraceletReturnEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("EdgeSlot_0_1").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction1Button").GetComponent<Button>().onClick.Invoke();
            helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("RingSlot_0").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorTrialButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            rings = GetListField(game, "rings");
            Assert.AreEqual(0, GetListField(game, "mapBuckles").Count);
            var attachedLink = GetListField(game, "attachedBuckles")[0];
            var attachedOwner = rings[GetIntField(attachedLink, "Owner")];
            var attachedTarget = rings[GetIntField(attachedLink, "Target")];
            var ownerPosition = (Vector2)GetFieldValue(attachedOwner, "InitialPosition");
            var targetPosition = (Vector2)GetFieldValue(attachedTarget, "InitialPosition");
            var targetToOwner = Mathf.Atan2(
                ownerPosition.y - targetPosition.y,
                ownerPosition.x - targetPosition.x) * Mathf.Rad2Deg;
            InvokeSetRingAngle(attachedTarget, targetToOwner + 90f);
            AssertRingRotatesBothWays(attachedTarget);
            AssertOwnedAttachedLoopBlocksAtLeastOneDirection(attachedOwner);
        }

        [UnityTest]
        public IEnumerator ClearingAllMapAndAttachedConstraintsCompletesGame()
        {
            var rings = GetListField(game, "rings");
            var buckles = GetListField(game, "mapBuckles");
            for (var i = 0; i < buckles.Count; i++)
            {
                InvokeEliminate(buckles[i]);
            }

            Assert.AreEqual(5, GetIntField(game, "clearedMapBuckleCount"));
            var attachedLinks = GetListField(game, "attachedBuckles");
            Assert.Less(GetIntField(game, "clearedRingCount"), 12, "仍有随环穿套关系时，清空地图金环不能直接结束游戏");
            for (var i = 0; i < attachedLinks.Count; i++)
            {
                SetFieldValue(attachedLinks[i], "IsThreadingTarget", false);
            }
            for (var i = 0; i < rings.Count; i++)
            {
                InvokeTryReleaseUnlinkedRing(rings[i]);
            }
            Assert.AreEqual(12, GetIntField(game, "clearedRingCount"));
            for (var i = 0; i < attachedLinks.Count; i++)
            {
                Assert.IsTrue(GetBoolField(attachedLinks[i], "IsActive"), "随环金环必须在所属手环退场动画期间保持存在");
                Assert.IsTrue(((RectTransform)GetFieldValue(attachedLinks[i], "Visual")).gameObject.activeSelf);
            }
            game.Tick(0.5f);
            for (var i = 0; i < rings.Count; i++)
            {
                Assert.IsTrue(((RectTransform)GetFieldValue(rings[i], "Rect")).gameObject.activeSelf, "放慢后的手环动画在0.5秒时不应提前结束");
            }
            game.Tick(0.25f);
            for (var i = 0; i < attachedLinks.Count; i++)
            {
                Assert.IsFalse(GetBoolField(attachedLinks[i], "IsActive"), "随环金环必须与所属手环一起完成消除");
                Assert.IsFalse(((RectTransform)GetFieldValue(attachedLinks[i], "Visual")).gameObject.activeSelf);
            }
            yield return null;
            Assert.IsNotNull(GameObject.Find("BraceletUnlinkWinSettlementPanel"));
        }

        [Test]
        public void RingEliminationPlaysSettleSoundWithoutAnotherEliminationSource()
        {
            var previousSfxEnabled = MiniGameRuntimeSettings.SfxEnabled;
            MiniGameRuntimeSettings.SetSfxEnabled(true);
            try
            {
                var mapBuckles = GetListField(game, "mapBuckles");
                for (var i = 0; i < mapBuckles.Count; i++)
                {
                    SetFieldValue(mapBuckles[i], "IsActive", false);
                }
                var attachedBuckles = GetListField(game, "attachedBuckles");
                for (var i = 0; i < attachedBuckles.Count; i++)
                {
                    SetFieldValue(attachedBuckles[i], "IsActive", false);
                    SetFieldValue(attachedBuckles[i], "IsThreadingTarget", false);
                }

                var existingPlayer = UnityEngine.Object.FindObjectOfType<MiniGameSfxPlayer>();
                Assert.IsNotNull(existingPlayer);
                var clipCache = (IDictionary)GetFieldValue(existingPlayer, "clipCache");
                clipCache.Clear();

                InvokeTryReleaseUnlinkedRing(GetListField(game, "rings")[0]);

                var player = UnityEngine.Object.FindObjectOfType<MiniGameSfxPlayer>();
                Assert.IsNotNull(player, "手环自身消除必须触发音效播放器");
                Assert.IsTrue(clipCache.Contains(MiniGameSfxType.Settle), "手环自身消除必须播放解套音效");
            }
            finally
            {
                MiniGameRuntimeSettings.SetSfxEnabled(previousSfxEnabled);
            }
        }

        [Test]
        public void RingRaycastOnlyAcceptsVisibleBand()
        {
            var ring = GameObject.Find("Bracelet_0").GetComponent<BraceletRingGraphic>();
            var rect = ring.rectTransform;
            var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(Vector3.zero));
            var band = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(0f, 60f, 0f)));
            Assert.IsFalse(ring.IsRaycastLocationValid(center, null));
            Assert.IsTrue(ring.IsRaycastLocationValid(band, null));
        }

        [UnityTest]
        public IEnumerator DevelopmentEditorUsesHexSlotsAndSixDirectionEdges()
        {
            var entry = GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>();
            Assert.IsNotNull(entry);
            entry.onClick.Invoke();
            yield return null;

            var overlay = GameObject.Find("BraceletLevelEditorOverlay");
            Assert.IsNotNull(overlay);
            var helpers = GameObject.Find("EditorHelpers").transform;
            var ringSlotCount = 0;
            var edgeSlotCount = 0;
            for (var i = 0; i < helpers.childCount; i++)
            {
                if (helpers.GetChild(i).name.StartsWith("RingSlot_")) ringSlotCount += 1;
                if (helpers.GetChild(i).name.StartsWith("EdgeSlot_")) edgeSlotCount += 1;
            }
            Assert.AreEqual(14, ringSlotCount, "旧布局进入编辑器时必须完整保留4-3-4-3共14个手环格");
            Assert.Greater(edgeSlotCount, 20, "编辑器必须生成所有六方向邻接边槽");
            Assert.IsNull(helpers.Find("EdgeSlot_0_2"), "非六方向相邻格子之间不得生成边槽");
            Assert.IsNotNull(helpers.Find("EdgeSlot_0_4"), "跨行斜向相邻格必须生成普通边槽");
            Assert.IsNotNull(helpers.Find("RingSlot_0/EditableMarker"), "每个手环编辑位必须显示圆框标记");
            Assert.IsNotNull(helpers.Find("EdgeSlot_1_2/EditableMarker"), "可编辑金环位必须显示短框标记");
            Assert.IsFalse(helpers.Find("EdgeSlot_0_4").GetComponent<Button>().interactable, "缺少端点手环的边位不得显示为可编辑");

            helpers.Find("RingSlot_0").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction0Button").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(13, GetListField(game, "rings").Count, "空格放入手环后必须立即进入正式玩法对象列表");
            var added = GameObject.Find("Bracelet_0").GetComponent<RectTransform>();
            Assert.AreEqual(-253.5f, added.anchoredPosition.x, 0.1f);
            Assert.AreEqual(217.5f, added.anchoredPosition.y, 0.1f);
            Assert.IsNotNull(GameObject.Find("EditorHelpers").transform.Find("EdgeSlot_0_4/EditableMarker"), "补齐端点后边位必须立即显示可编辑标记");

            GameObject.Find("EditorHelpers").transform.Find("EdgeSlot_0_4").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction0Button").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(6, GetListField(game, "mapBuckles").Count, "合法边槽放置地图金环后必须立即使用正式金环对象预览");

            GameObject.Find("EditorHelpers").transform.Find("EdgeSlot_0_4").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var cameraObject = new GameObject("BraceletEditorScreenshotCamera", typeof(Camera));
            cameraObject.transform.SetParent(hostObject.transform, false);
            cameraObject.tag = "MainCamera";
            var screenshotPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PlayModeShots", "pm_bracelet_editor.png");
            yield return PlayModeScreenshotTests.CaptureRealScreenshot(screenshotPath);
            Assert.IsTrue(File.Exists(screenshotPath));
        }

        [UnityTest]
        public IEnumerator DevelopmentEditorChoosesAttachedOwnerByClickingTheRing()
        {
            GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("EdgeSlot_1_2").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction1Button").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var contextActions = GameObject.Find("BraceletLevelEditorOverlay").transform.Find("EditorContextActions");
            Assert.IsFalse(contextActions.gameObject.activeSelf, "选择随环金环后不应再要求理解A/B按钮");
            helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("RingSlot_1").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(4, GetListField(game, "mapBuckles").Count);
            Assert.AreEqual(11, GetListField(game, "attachedBuckles").Count);
            Assert.IsTrue(HasAttachedLinkBetweenSlots(1, 2, 1), "点击哪只手环，金环就必须归属于哪只手环");
        }

        [UnityTest]
        public IEnumerator DevelopmentEditorCanClearAndZoomBoard()
        {
            GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var board = GameObject.Find("BraceletUnlinkBoard").GetComponent<RectTransform>();
            var helpers = GameObject.Find("EditorHelpers").transform;
            var slider = GameObject.Find("BraceletEditorZoomSlider").GetComponent<Slider>();
            var initialScale = board.localScale.x;
            slider.value = 1f;
            yield return null;
            Assert.Greater(board.localScale.x, initialScale + 0.5f, "缩放滑杆必须连续放大棋盘");
            Assert.AreEqual(board.localScale.x, helpers.localScale.x, 0.001f, "物件层与编辑点击层必须同步缩放");

            var initialPan = board.anchoredPosition;
            var dragHandler = GameObject.Find("BraceletEditorBoardViewport").GetComponent<BraceletEditorBoardDragHandler>();
            dragHandler.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(80f, 40f) });
            yield return null;
            Assert.AreNotEqual(initialPan, board.anchoredPosition, "放大后拖动画布必须能平移视图");

            GameObject.Find("BraceletEditorClearButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, GetListField(game, "rings").Count, "一键清空必须移除全部手环");
            Assert.AreEqual(0, GetListField(game, "mapBuckles").Count, "一键清空必须移除全部地图金环");
            Assert.AreEqual(0, GetListField(game, "attachedBuckles").Count, "一键清空必须移除全部随环金环");

            GameObject.Find("BraceletEditorCloseButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual("BraceletUnlinkBoardPanel", board.parent.name, "关闭编辑器后棋盘必须恢复到正常游戏层级");
            Assert.AreEqual((float)GetFieldValue(game, "gameplayBoardScale"), board.localScale.x, 0.001f, "关闭编辑器后必须恢复正常游戏视图缩放");
        }

        [UnityTest]
        public IEnumerator DevelopmentEditorCanResizeBoardAndManageMultipleLevels()
        {
            GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            GameObject.Find("BraceletEditorBoardLargerButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var helpers = GameObject.Find("EditorHelpers").transform;
            Assert.IsNotNull(helpers.Find("RingSlot_22"), "放大后的5-4-5-4-5棋盘必须提供23个固定六边形格");
            Assert.IsNull(helpers.Find("RingSlot_23"));
            Assert.AreEqual(0, GetListField(game, "rings").Count, "切换棋盘尺寸必须从合法空布局开始编辑");

            GameObject.Find("BraceletEditorNewLevelButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            helpers = GameObject.Find("EditorHelpers").transform;
            Assert.IsNotNull(helpers.Find("RingSlot_1"), "新关卡默认从适合前期教学的两个手环格开始");
            Assert.IsNull(helpers.Find("RingSlot_2"));
            Assert.AreEqual(1, GetIntField(game, "editorLevelIndex"));
            Assert.AreEqual(2, GetListField(game, "editorLevels").Count);

            GameObject.Find("BraceletEditorSaveButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var json = File.ReadAllText(editorSavePath);
            StringAssert.Contains("\"FormatVersion\": 8", json);
            StringAssert.Contains("\"LevelId\":", json);
            StringAssert.Contains("\"Levels\"", json);
            StringAssert.Contains("\"RowLengths\"", json);
        }

        [UnityTest]
        public IEnumerator DevelopmentEditorSavesJsonAndAutomaticallyLoadsItInANewView()
        {
            GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            GameObject.Find("BraceletEditorClearButton").GetComponent<Button>().onClick.Invoke();
            var helpers = GameObject.Find("EditorHelpers").transform;
            helpers.Find("RingSlot_0").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorContextAction0Button").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("BraceletEditorSaveButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(File.Exists(editorSavePath), "保存按钮必须创建真实JSON文件");
            var json = File.ReadAllText(editorSavePath);
            Assert.IsTrue(json.Contains("\n"), "JSON存档必须使用便于人工编辑的格式化内容");
            StringAssert.Contains("\"FormatVersion\": 8", json);
            StringAssert.Contains("\"LevelId\":", json);
            StringAssert.Contains("\"Levels\"", json);
            StringAssert.Contains("\"Rings\"", json);
            StringAssert.Contains("\"Kind\": 1", json);

            Cleanup();
            hostObject = new GameObject("BraceletUnlinkTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(BraceletUnlinkGameView.GameIdConstant);
            yield return null;
            game = GetActiveGame(controller);
            GameObject.Find("BraceletLevelEditorButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, GetListField(game, "rings").Count, "重新创建游戏视图后首次打开编辑器必须自动读取JSON存档");
            Assert.AreEqual(0, GetListField(game, "mapBuckles").Count);
            Assert.AreEqual(0, GetListField(game, "attachedBuckles").Count);
        }

        private void InvokeEliminate(object buckle)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("EliminateMapBuckle", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(game, new[] { buckle });
        }

        private void InvokeSetRingAngle(object ring, float angle)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("SetRingAngle", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(game, new[] { ring, (object)angle });
        }

        private void InvokeTryReleaseUnlinkedRing(object ring)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("TryReleaseUnlinkedRing", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(game, new[] { ring });
        }

        private void SetIncomingAttachedLinksThreading(object ring, bool isThreading)
        {
            var ringId = GetIntField(ring, "Id");
            var links = GetListField(game, "attachedBuckles");
            for (var i = 0; i < links.Count; i++)
            {
                if (GetIntField(links[i], "Target") == ringId)
                {
                    SetFieldValue(links[i], "IsThreadingTarget", isThreading);
                }
            }
        }

        private int GetIncomingAttachedLinkCount(object ring)
        {
            var ringId = GetIntField(ring, "Id");
            var links = GetListField(game, "attachedBuckles");
            var count = 0;
            for (var i = 0; i < links.Count; i++)
            {
                if (GetIntField(links[i], "Target") == ringId && GetBoolField(links[i], "IsThreadingTarget"))
                {
                    count += 1;
                }
            }
            return count;
        }

        private float InvokeApplyRotation(int ringId, float delta)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("ApplyRingRotation", InstancePrivate);
            Assert.IsNotNull(method);
            return (float)method.Invoke(game, new object[] { ringId, delta });
        }

        private void RotateRingToAngle(object ring, float targetAngle, string context = null)
        {
            var ringId = GetIntField(ring, "Id");
            var startAngle = GetFloatField(ring, "GapAngle");
            var requested = Mathf.DeltaAngle(startAngle, targetAngle);
            InvokeApplyRotation(ringId, requested);
            var currentAngle = GetFloatField(ring, "GapAngle");
            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) > 12.1f)
            {
                var reverseRoute = requested >= 0f
                    ? -Mathf.Repeat(currentAngle - targetAngle, 360f)
                    : Mathf.Repeat(targetAngle - currentAngle, 360f);
                InvokeApplyRotation(ringId, reverseRoute);
            }
            Assert.LessOrEqual(
                Mathf.Abs(Mathf.DeltaAngle(GetFloatField(ring, "GapAngle"), targetAngle)),
                12.1f,
                (context ?? "解法") + "要求的缺口方向必须能通过碰撞约束旋转到达");
        }

        private bool TryRotateRingToAngle(object ring, float targetAngle)
        {
            if (ring == null || GetBoolField(ring, "IsCleared") || GetBoolField(ring, "IsClosed"))
            {
                return false;
            }

            var ringId = GetIntField(ring, "Id");
            var startAngle = GetFloatField(ring, "GapAngle");
            var requested = Mathf.DeltaAngle(startAngle, targetAngle);
            InvokeApplyRotation(ringId, requested);
            var currentAngle = GetFloatField(ring, "GapAngle");
            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) > 12.1f)
            {
                var reverseRoute = requested >= 0f
                    ? -Mathf.Repeat(currentAngle - targetAngle, 360f)
                    : Mathf.Repeat(targetAngle - currentAngle, 360f);
                InvokeApplyRotation(ringId, reverseRoute);
            }
            return Mathf.Abs(Mathf.DeltaAngle(GetFloatField(ring, "GapAngle"), targetAngle)) <= 12.1f;
        }

        private int GetPuzzleReleaseProgress()
        {
            var progress = GetIntField(game, "clearedRingCount") * 4
                + GetIntField(game, "clearedMapBuckleCount") * 4;
            var mapLoops = GetListField(game, "mapBuckles");
            for (var i = 0; i < mapLoops.Count; i++)
            {
                progress += GetBoolField(mapLoops[i], "RingADocked") ? 1 : 0;
                progress += GetBoolField(mapLoops[i], "RingBDocked") ? 1 : 0;
            }
            var attachedLoops = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attachedLoops.Count; i++)
            {
                progress += GetBoolField(attachedLoops[i], "IsThreadingTarget") ? 0 : 2;
            }
            return progress;
        }

        private string DescribeRemainingPuzzleConstraints()
        {
            var remaining = new List<string>();
            var maps = GetListField(game, "mapBuckles");
            for (var i = 0; i < maps.Count; i++)
            {
                if (GetBoolField(maps[i], "IsActive"))
                {
                    remaining.Add("地图" + i
                        + "(A" + (GetBoolField(maps[i], "RingAThreaded") ? "穿套" : "解套")
                        + "/" + (GetBoolField(maps[i], "RingADocked") ? "就位" : "未就位")
                        + ",B" + (GetBoolField(maps[i], "RingBThreaded") ? "穿套" : "解套")
                        + "/" + (GetBoolField(maps[i], "RingBDocked") ? "就位" : "未就位") + ")");
                }
            }
            var attached = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attached.Count; i++)
            {
                if (GetBoolField(attached[i], "IsThreadingTarget"))
                {
                    remaining.Add("随环" + i + "(" + GetIntField(attached[i], "Owner") + "→" + GetIntField(attached[i], "Target") + ")");
                }
            }
            return string.Join("、", remaining);
        }

        private void AssertRingRotatesBothWays(object ring)
        {
            var ringId = GetIntField(ring, "Id");
            var startAngle = GetFloatField(ring, "GapAngle");
            Assert.Greater(InvokeApplyRotation(ringId, 30f), 29f, "合法穿套关系不得阻挡顺时针旋转");
            InvokeSetRingAngle(ring, startAngle);
            Assert.Less(InvokeApplyRotation(ringId, -30f), -29f, "合法穿套关系不得阻挡逆时针旋转");
            InvokeSetRingAngle(ring, startAngle);
        }

        private void AssertOwnedAttachedLoopBlocksAtLeastOneDirection(object ownerRing)
        {
            var ringId = GetIntField(ownerRing, "Id");
            var startAngle = GetFloatField(ownerRing, "GapAngle");
            var clockwise = InvokeApplyRotation(ringId, 30f);
            InvokeSetRingAngle(ownerRing, startAngle);
            var counterClockwise = InvokeApplyRotation(ringId, -30f);
            InvokeSetRingAngle(ownerRing, startAngle);
            Assert.IsTrue(
                Mathf.Abs(clockwise) < 29f || Mathf.Abs(counterClockwise) < 29f,
                "所属手环带着随环金环旋转时，金环必须被目标手环至少挡住一个方向");

            var blockedDirection = Mathf.Abs(clockwise) < 29f ? 1f : -1f;
            InvokeApplyRotation(ringId, blockedDirection * 30f);
            var reverse = InvokeApplyRotation(ringId, blockedDirection * -10f);
            Assert.Greater(
                reverse * -blockedDirection,
                1f,
                "随环金环被目标手环挡住后，所属手环必须还能反向退回");
            InvokeSetRingAngle(ownerRing, startAngle);
        }

        private void InvokeCheckMapBuckleMatches(object ring, float startAngle, float appliedDelta)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("CheckMapBuckleMatches", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(game, new[] { ring, (object)startAngle, appliedDelta });
        }

        private void InvokeCheckAttachedBuckleMatches(object ring, float startAngle, float appliedDelta)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("CheckAttachedBuckleMatches", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(game, new[] { ring, (object)startAngle, appliedDelta });
        }

        private void InvokeUpdateCollisionFeedback(object ring, float requestedDelta, bool isBlocked)
        {
            var method = typeof(BraceletUnlinkGameView).GetMethod("UpdateCollisionFeedback", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(game, new[] { ring, (object)requestedDelta, isBlocked });
        }

        private static BraceletUnlinkGameView GetActiveGame(MiniGameAppController appController)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field);
            var value = field.GetValue(appController) as BraceletUnlinkGameView;
            Assert.IsNotNull(value);
            return value;
        }

        private static IList GetListField(object target, string fieldName)
        {
            return (IList)GetFieldValue(target, fieldName);
        }

        private object FindMapBuckleBySlots(int slotA, int slotB)
        {
            var rings = GetListField(game, "rings");
            var ringIdA = -1;
            var ringIdB = -1;
            for (var i = 0; i < rings.Count; i++)
            {
                var slot = GetIntField(rings[i], "SlotId");
                if (slot == slotA) ringIdA = GetIntField(rings[i], "Id");
                if (slot == slotB) ringIdB = GetIntField(rings[i], "Id");
            }

            var buckles = GetListField(game, "mapBuckles");
            for (var i = 0; i < buckles.Count; i++)
            {
                var first = GetIntField(buckles[i], "RingA");
                var second = GetIntField(buckles[i], "RingB");
                if (first == ringIdA && second == ringIdB || first == ringIdB && second == ringIdA)
                {
                    return buckles[i];
                }
            }
            return null;
        }

        private bool HasAttachedLinkBetweenSlots(int slotA, int slotB, int ownerSlot)
        {
            var rings = GetListField(game, "rings");
            var slotByRingId = new Dictionary<int, int>();
            for (var i = 0; i < rings.Count; i++)
            {
                slotByRingId[GetIntField(rings[i], "Id")] = GetIntField(rings[i], "SlotId");
            }

            var attached = GetListField(game, "attachedBuckles");
            for (var i = 0; i < attached.Count; i++)
            {
                var owner = slotByRingId[GetIntField(attached[i], "Owner")];
                var target = slotByRingId[GetIntField(attached[i], "Target")];
                if (owner == ownerSlot && (owner == slotA && target == slotB || owner == slotB && target == slotA))
                {
                    return true;
                }
            }
            return false;
        }

        private static int GetLongestAttachedDependencyDepth(object level)
        {
            var edges = (Array)GetFieldValue(level, "Edges");
            var targetsByOwner = new Dictionary<int, List<int>>();
            for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                var edge = edges.GetValue(edgeIndex);
                var kind = (int)GetFieldValue(edge, "Kind");
                if (kind != 2 && kind != 3)
                {
                    continue;
                }
                var slotA = (int)GetFieldValue(edge, "SlotA");
                var slotB = (int)GetFieldValue(edge, "SlotB");
                var owner = kind == 2 ? slotA : slotB;
                var target = kind == 2 ? slotB : slotA;
                List<int> targets;
                if (!targetsByOwner.TryGetValue(owner, out targets))
                {
                    targets = new List<int>();
                    targetsByOwner.Add(owner, targets);
                }
                targets.Add(target);
            }

            var longest = 0;
            foreach (var owner in targetsByOwner.Keys)
            {
                longest = Mathf.Max(longest, GetAttachedDependencyDepth(owner, targetsByOwner, new HashSet<int>()));
            }
            return longest;
        }

        private static int GetAttachedDependencyDepth(
            int owner,
            IDictionary<int, List<int>> targetsByOwner,
            ISet<int> path)
        {
            if (!path.Add(owner))
            {
                return 0;
            }
            List<int> targets;
            if (!targetsByOwner.TryGetValue(owner, out targets))
            {
                path.Remove(owner);
                return 0;
            }
            var longest = 0;
            for (var i = 0; i < targets.Count; i++)
            {
                longest = Mathf.Max(longest, 1 + GetAttachedDependencyDepth(targets[i], targetsByOwner, path));
            }
            path.Remove(owner);
            return longest;
        }

        private static float MinimumAngleDistance(float angle, IList<float> targets)
        {
            var minimum = 180f;
            for (var i = 0; i < targets.Count; i++)
            {
                minimum = Mathf.Min(minimum, Mathf.Abs(Mathf.DeltaAngle(angle, targets[i])));
            }
            return minimum;
        }

        private static int GetIntField(object target, string fieldName)
        {
            return (int)GetFieldValue(target, fieldName);
        }

        private static bool GetBoolField(object target, string fieldName)
        {
            return (bool)GetFieldValue(target, fieldName);
        }

        private static float GetFloatField(object target, string fieldName)
        {
            return (float)GetFieldValue(target, fieldName);
        }

        private static int CountLinksTouchingRing(IList links, int ringId)
        {
            var count = 0;
            for (var i = 0; i < links.Count; i++)
            {
                if (GetIntField(links[i], "Owner") == ringId || GetIntField(links[i], "Target") == ringId)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static int CountOwnedLinks(IList links, int ringId)
        {
            var count = 0;
            for (var i = 0; i < links.Count; i++)
            {
                if (GetIntField(links[i], "Owner") == ringId)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static object GetFieldValue(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate | BindingFlags.Public);
            Assert.IsNotNull(field);
            return field.GetValue(target);
        }

        private static void SetFieldValue(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate | BindingFlags.Public);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static void SetEditorSavePathOverride(string path)
        {
            var field = typeof(BraceletUnlinkGameView).GetField(
                "editorSavePathOverride",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(null, path);
        }

        private static void SetLevelDefinitionsOverride(object definitions)
        {
            var field = typeof(BraceletUnlinkGameView).GetField(
                "levelDefinitionsOverride",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(null, definitions);
        }

        private static object GenerateRandomLevel(int seed, int completedCount)
        {
            var generatorType = typeof(BraceletUnlinkGameView).Assembly.GetType(
                "HuanYouYu.MiniGameHall.BraceletUnlinkRandomLevelGenerator");
            Assert.IsNotNull(generatorType);
            var generate = generatorType.GetMethod("Generate", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(generate);
            return generate.Invoke(null, new object[] { seed, completedCount });
        }

        private static int CountGeneratedRings(object generated, out int closedCount)
        {
            closedCount = 0;
            var occupiedCount = 0;
            var rings = (Array)GetFieldValue(generated, "Rings");
            for (var slot = 0; slot < rings.Length; slot++)
            {
                var kind = (int)GetFieldValue(rings.GetValue(slot), "Kind");
                occupiedCount += kind == 0 ? 0 : 1;
                closedCount += kind == 2 ? 1 : 0;
            }
            return occupiedCount;
        }

        private Button FindButtonIncludingInactive(string objectName)
        {
            var buttons = hostObject.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name == objectName)
                {
                    return buttons[i];
                }
            }
            Assert.Fail("未找到按钮：" + objectName);
            return null;
        }

        private bool TrySolveGeneratedLevel(int maxPasses)
        {
            for (var pass = 0; pass < maxPasses && !GetBoolField(game, "isCompleted"); pass++)
            {
                var attachedLoops = GetListField(game, "attachedBuckles");
                for (var linkIndex = attachedLoops.Count - 1; linkIndex >= 0; linkIndex--)
                {
                    var link = attachedLoops[linkIndex];
                    if (!GetBoolField(link, "IsThreadingTarget"))
                    {
                        continue;
                    }
                    var currentRings = GetListField(game, "rings");
                    var owner = currentRings[GetIntField(link, "Owner")];
                    var target = currentRings[GetIntField(link, "Target")];
                    var ownerPosition = (Vector2)GetFieldValue(owner, "InitialPosition");
                    var targetPosition = (Vector2)GetFieldValue(target, "InitialPosition");
                    TryRotateRingToAngle(target, Mathf.Atan2(
                        ownerPosition.y - targetPosition.y,
                        ownerPosition.x - targetPosition.x) * Mathf.Rad2Deg);
                }

                var mapLoops = GetListField(game, "mapBuckles");
                for (var buckleIndex = 0; buckleIndex < mapLoops.Count; buckleIndex++)
                {
                    var buckle = mapLoops[buckleIndex];
                    if (!GetBoolField(buckle, "IsActive"))
                    {
                        continue;
                    }
                    var currentRings = GetListField(game, "rings");
                    TryRotateRingToAngle(
                        currentRings[GetIntField(buckle, "RingB")],
                        GetFloatField(buckle, "RingBAngle"));
                    if (GetBoolField(buckle, "IsActive"))
                    {
                        TryRotateRingToAngle(
                            currentRings[GetIntField(buckle, "RingA")],
                            GetFloatField(buckle, "RingAAngle"));
                    }
                }
            }
            return GetBoolField(game, "isCompleted");
        }

        private static void SetRandomModeSaveKeyOverride(string key)
        {
            var field = typeof(BraceletUnlinkGameView).GetField(
                "randomModeSaveKeyOverride",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(null, key);
        }

        private static int[] GetLevelIds(Array levels)
        {
            var ids = new int[levels.Length];
            for (var index = 0; index < levels.Length; index++)
            {
                ids[index] = (int)GetFieldValue(levels.GetValue(index), "LevelId");
            }
            return ids;
        }

        private static void Cleanup()
        {
            var controllers = UnityEngine.Object.FindObjectsOfType<MiniGameAppController>();
            for (var index = 0; index < controllers.Length; index++)
            {
                if (controllers[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(controllers[index].gameObject);
                }
            }

            var eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>();
            for (var index = 0; index < eventSystems.Length; index++)
            {
                if (eventSystems[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(eventSystems[index].gameObject);
                }
            }

            DestroyIfExists("BraceletUnlinkTestHost");
        }

        private static void DestroyIfExists(string objectName)
        {
            var target = GameObject.Find(objectName);
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
