using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameNeedleHitView : MiniGameBase
    {
        public const string GameIdConstant = "needlehit";

        private const string ContentPrefabResourcePath = "NeedleHitContent";
        private const string NeedlePrefabResourcePath = "NeedleHitNeedle";

        private const float NeedleShaftLength = 144f;
        private const float NeedleShaftWidth = 12f;
        private const float NeedleHeadSize = 22f;
        private const float DiscRadius = 118f;
        private const float NeedleTravelSpeed = 2400f;
        private static readonly NeedleHitLevelDefinition[] LevelDefinitions =
        {
            new NeedleHitLevelDefinition(3, 82f, 7f, 1, 0f, 120f, 240f),
            new NeedleHitLevelDefinition(4, 92f, 7f, -1, 20f, 140f, 260f),
            new NeedleHitLevelDefinition(4, 104f, 6f, 1, 35f, 155f, 275f),
            new NeedleHitLevelDefinition(5, 116f, 6f, -1, 15f, 135f, 255f),
            new NeedleHitLevelDefinition(5, 128f, 5.5f, 1, 0f, 90f, 210f, 300f),
            new NeedleHitLevelDefinition(6, 138f, 5.5f, -1, 30f, 110f, 220f, 310f),
            new NeedleHitLevelDefinition(6, 150f, 5f, 1, 10f, 100f, 190f, 280f),
            new NeedleHitLevelDefinition(7, 164f, 5f, -1, 0f, 80f, 170f, 260f),
            new NeedleHitLevelDefinition(7, 176f, 4.5f, 1, 25f, 115f, 205f, 295f),
            new NeedleHitLevelDefinition(8, 188f, 4.5f, -1, 5f, 75f, 155f, 235f, 315f),
            new NeedleHitLevelDefinition(8, 202f, 4f, 1, 40f, 120f, 200f, 280f),
            new NeedleHitLevelDefinition(9, 216f, 4f, -1, 0f, 70f, 140f, 210f, 280f)
        };

        public static int LevelCount
        {
            get { return LevelDefinitions.Length; }
        }

        private readonly List<StuckNeedleState> stuckNeedles = new List<StuckNeedleState>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private Button fireButton;
        private Button levelSelectButton;

        private RectTransform contentRoot;
        private RectTransform boardRoot;
        private RectTransform tapZone;
        private RectTransform needleLayer;
        private RectTransform discPivot;
        private RectTransform discCore;
        private RectTransform launcherAnchor;

        private RectTransform flyingNeedle;
        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private int currentLevelIndex;
        private bool flyingNeedleInMotion;
        private int score;
        private int rotationDirection;
        private float discRotationDegrees;
        private bool interactionLocked;
        private NeedleHitRunState runState;

        private sealed class StuckNeedleState
        {
            public RectTransform Transform;
            public float LocalAngle;
        }

        private enum NeedleHitRunState
        {
            Idle,
            Running,
            Settling,
            Disposed
        }

        private NeedleHitLevelDefinition CurrentLevel
        {
            get { return LevelDefinitions[currentLevelIndex]; }
        }

        private float DiscRotationSpeed
        {
            get { return CurrentLevel.RotationSpeed; }
        }

        private float SafeAngleThreshold
        {
            get { return CurrentLevel.SafeAngleThreshold; }
        }

        public GameNeedleHitView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameNeedleHitView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("NeedleHitTop"));
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("NeedleHitBottom"));
            var bottomRoot = bottomContainerRefs.Root.gameObject;
            var contentSection = LoadRequiredSectionPrefab(ContentPrefabResourcePath, Shell.ContentHost, "NeedleHitContent");

            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;
            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottomContainerRefs.ActionBar).Button;

            contentRoot = contentSection.GetComponent<RectTransform>();
            boardRoot = RequireRectTransform(contentSection.transform, "BoardRoot");
            tapZone = RequireRectTransform(contentSection.transform, "BoardRoot/TapZone");
            needleLayer = RequireRectTransform(contentSection.transform, "BoardRoot/NeedleLayer");
            discPivot = RequireRectTransform(contentSection.transform, "BoardRoot/DiscPivot");
            discCore = RequireRectTransform(contentSection.transform, "BoardRoot/DiscPivot/DiscCore");
            launcherAnchor = RequireRectTransform(contentSection.transform, "BoardRoot/LauncherAnchor");

            ConfigurePlayfieldVisuals();

            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
            levelSelectButton.onClick.RemoveAllListeners();
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);

            fireButton.onClick.RemoveAllListeners();
            fireButton.onClick.AddListener(OnFireRequested);
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseLevelSelectView();
            CloseRewardSettlementPanel();
            EnsureLevelProgress();
            currentLevelIndex = levelProgress.CurrentLevelIndex;
            interactionLocked = false;
            score = 0;
            flyingNeedleInMotion = false;
            rotationDirection = LevelDefinitions[currentLevelIndex].RotationDirection;
            discRotationDegrees = 0f;
            runState = NeedleHitRunState.Running;

            ClearSpawnedNeedles();
            ApplyDiscRotation();
            SpawnInitialNeedles();
            SpawnNextNeedle();
            RefreshHud();
        }

        public override void Tick(float deltaTime)
        {
            if (runState != NeedleHitRunState.Running || interactionLocked)
            {
                return;
            }

            discRotationDegrees = NormalizeAngle(discRotationDegrees + (rotationDirection * DiscRotationSpeed * deltaTime));
            ApplyDiscRotation();

            if (!flyingNeedleInMotion || flyingNeedle == null)
            {
                return;
            }

            var anchoredPosition = flyingNeedle.anchoredPosition;
            anchoredPosition.y += NeedleTravelSpeed * deltaTime;
            if (anchoredPosition.y >= CalculateFlyingNeedleImpactY())
            {
                anchoredPosition.y = CalculateFlyingNeedleImpactY();
                flyingNeedle.anchoredPosition = anchoredPosition;
                ResolveFlyingNeedleImpact();
                return;
            }

            flyingNeedle.anchoredPosition = anchoredPosition;
        }

        protected override void OnPauseRequested()
        {
            if (runState != NeedleHitRunState.Running || interactionLocked)
            {
                return;
            }

            interactionLocked = true;
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            runState = NeedleHitRunState.Disposed;
            interactionLocked = true;
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (fireButton != null)
            {
                fireButton.onClick.RemoveListener(OnFireRequested);
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            }

            CloseLevelSelectView();
            CloseRewardSettlementPanel();

            ClearSpawnedNeedles();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.needlehit.help", null);
        }

        private void ConfigurePlayfieldVisuals()
        {
            Stretch(contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Stretch(boardRoot, Vector2.zero, Vector2.one, new Vector2(28f, 18f), new Vector2(-28f, -18f));
            Stretch(tapZone, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            Stretch(needleLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            discPivot.anchorMin = new Vector2(0.5f, 0.5f);
            discPivot.anchorMax = new Vector2(0.5f, 0.5f);
            discPivot.anchoredPosition = new Vector2(0f, 118f);
            discPivot.sizeDelta = Vector2.zero;
            discPivot.pivot = new Vector2(0.5f, 0.5f);

            discCore.anchorMin = new Vector2(0.5f, 0.5f);
            discCore.anchorMax = new Vector2(0.5f, 0.5f);
            discCore.anchoredPosition = Vector2.zero;
            discCore.sizeDelta = new Vector2(DiscRadius * 2f, DiscRadius * 2f);
            discCore.pivot = new Vector2(0.5f, 0.5f);

            launcherAnchor.anchorMin = new Vector2(0.5f, 0.5f);
            launcherAnchor.anchorMax = new Vector2(0.5f, 0.5f);
            launcherAnchor.anchoredPosition = new Vector2(0f, -430f);
            launcherAnchor.sizeDelta = Vector2.zero;
            launcherAnchor.pivot = new Vector2(0.5f, 0.5f);

            var boardGraphic = EnsureRoundedRectGraphic(
                boardRoot.gameObject,
                new Color32(247, 241, 225, 220),
                42f,
                false);
            boardGraphic.raycastTarget = false;

            var tapGraphic = EnsureRoundedRectGraphic(
                tapZone.gameObject,
                new Color32(248, 244, 233, 18),
                36f,
                true);
            fireButton = EnsureButton(tapZone.gameObject, tapGraphic);

            var discGraphic = EnsureRoundedRectGraphic(
                discCore.gameObject,
                new Color32(231, 183, 92, 255),
                DiscRadius,
                false);
            discGraphic.raycastTarget = false;

            var innerRing = EnsureChildRectTransform(discCore, "InnerRing");
            innerRing.anchorMin = new Vector2(0.5f, 0.5f);
            innerRing.anchorMax = new Vector2(0.5f, 0.5f);
            innerRing.anchoredPosition = Vector2.zero;
            innerRing.sizeDelta = new Vector2(152f, 152f);
            innerRing.pivot = new Vector2(0.5f, 0.5f);
            var ringGraphic = EnsureRoundedRectGraphic(
                innerRing.gameObject,
                new Color32(246, 224, 178, 255),
                76f,
                false);
            ringGraphic.raycastTarget = false;

            var centerDot = EnsureChildRectTransform(discCore, "CenterDot");
            centerDot.anchorMin = new Vector2(0.5f, 0.5f);
            centerDot.anchorMax = new Vector2(0.5f, 0.5f);
            centerDot.anchoredPosition = Vector2.zero;
            centerDot.sizeDelta = new Vector2(26f, 26f);
            centerDot.pivot = new Vector2(0.5f, 0.5f);
            var dotGraphic = EnsureRoundedRectGraphic(
                centerDot.gameObject,
                new Color32(108, 88, 42, 255),
                13f,
                false);
            dotGraphic.raycastTarget = false;
        }

        private void SpawnInitialNeedles()
        {
            var angles = CurrentLevel.InitialWorldAngles;
            for (var i = 0; i < angles.Length; i++)
            {
                var localAngle = NormalizeAngle(angles[i] - discRotationDegrees);
                var needle = CreateNeedleInstance("SeedNeedle" + i, discPivot);
                ConfigureNeedleAsStuck(needle, localAngle);
                stuckNeedles.Add(new StuckNeedleState
                {
                    Transform = needle,
                    LocalAngle = localAngle
                });
            }
        }

        private void SpawnNextNeedle()
        {
            if (needleLayer == null)
            {
                return;
            }

            flyingNeedle = CreateNeedleInstance("CurrentNeedle", needleLayer);
            ConfigureNeedleAsReady(flyingNeedle);
            flyingNeedleInMotion = false;
        }

        private RectTransform CreateNeedleInstance(string instanceName, Transform parent)
        {
            var needlePrefab = Resources.Load<GameObject>(NeedlePrefabResourcePath);
            if (needlePrefab == null)
            {
                throw new InvalidOperationException("Section prefab not found at Resources/" + NeedlePrefabResourcePath);
            }

            var instance = UnityEngine.Object.Instantiate(needlePrefab, parent, false);
            instance.name = instanceName;

            var needleTransform = instance.GetComponent<RectTransform>();
            if (needleTransform == null)
            {
                throw new InvalidOperationException("Needle prefab is missing RectTransform.");
            }

            SetupNeedleVisuals(needleTransform);
            return needleTransform;
        }

        private void SetupNeedleVisuals(RectTransform needleTransform)
        {
            needleTransform.anchorMin = new Vector2(0.5f, 0.5f);
            needleTransform.anchorMax = new Vector2(0.5f, 0.5f);
            needleTransform.sizeDelta = new Vector2(42f, DiscRadius + NeedleShaftLength + NeedleHeadSize + 12f);
            needleTransform.pivot = new Vector2(0.5f, 0f);
            needleTransform.localScale = Vector3.one;

            var shaft = RequireRectTransform(needleTransform, "Shaft");
            shaft.anchorMin = new Vector2(0.5f, 0f);
            shaft.anchorMax = new Vector2(0.5f, 0f);
            shaft.sizeDelta = new Vector2(NeedleShaftWidth, NeedleShaftLength);
            shaft.pivot = new Vector2(0.5f, 0f);

            var head = RequireRectTransform(needleTransform, "Head");
            head.anchorMin = new Vector2(0.5f, 0f);
            head.anchorMax = new Vector2(0.5f, 0f);
            head.sizeDelta = new Vector2(NeedleHeadSize, NeedleHeadSize);
            head.pivot = new Vector2(0.5f, 0.5f);

            var shaftGraphic = EnsureRoundedRectGraphic(
                shaft.gameObject,
                new Color32(86, 74, 52, 255),
                NeedleShaftWidth * 0.5f,
                false);
            shaftGraphic.raycastTarget = false;

            var headGraphic = EnsureRoundedRectGraphic(
                head.gameObject,
                new Color32(230, 107, 78, 255),
                NeedleHeadSize * 0.5f,
                false);
            headGraphic.raycastTarget = false;
        }

        private void ConfigureNeedleAsReady(RectTransform needleTransform)
        {
            if (needleTransform == null || launcherAnchor == null)
            {
                return;
            }

            needleTransform.SetParent(needleLayer, false);
            needleTransform.anchoredPosition = launcherAnchor.anchoredPosition;
            needleTransform.localRotation = Quaternion.identity;

            var shaft = RequireRectTransform(needleTransform, "Shaft");
            shaft.anchoredPosition = new Vector2(0f, NeedleHeadSize);

            var head = RequireRectTransform(needleTransform, "Head");
            head.anchoredPosition = new Vector2(0f, NeedleHeadSize * 0.5f);
        }

        private void ConfigureNeedleAsStuck(RectTransform needleTransform, float localAngle)
        {
            if (needleTransform == null || discPivot == null)
            {
                return;
            }

            needleTransform.SetParent(discPivot, false);
            needleTransform.anchoredPosition = Vector2.zero;
            needleTransform.localRotation = Quaternion.Euler(0f, 0f, localAngle);

            var shaft = RequireRectTransform(needleTransform, "Shaft");
            shaft.anchoredPosition = new Vector2(0f, DiscRadius - 8f);

            var head = RequireRectTransform(needleTransform, "Head");
            head.anchoredPosition = new Vector2(0f, DiscRadius + NeedleShaftLength - 2f);
        }

        private void ResolveFlyingNeedleImpact()
        {
            if (flyingNeedle == null)
            {
                return;
            }

            if (WillHitExistingNeedle())
            {
                FailCurrentRound();
                return;
            }

            var localAngle = NormalizeAngle(180f - discRotationDegrees);
            ConfigureNeedleAsStuck(flyingNeedle, localAngle);
            stuckNeedles.Add(new StuckNeedleState
            {
                Transform = flyingNeedle,
                LocalAngle = localAngle
            });

            flyingNeedle = null;
            flyingNeedleInMotion = false;
            score += 1;
            RefreshHud();
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.92f);
            if (score >= CurrentLevel.TargetNeedleCount)
            {
                CompleteCurrentRound();
                return;
            }

            SpawnNextNeedle();
        }

        private bool WillHitExistingNeedle()
        {
            for (var i = 0; i < stuckNeedles.Count; i++)
            {
                var worldAngle = NormalizeAngle(discRotationDegrees + stuckNeedles[i].LocalAngle);
                var delta = Mathf.Abs(Mathf.DeltaAngle(worldAngle, 180f));
                if (delta <= SafeAngleThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void FailCurrentRound()
        {
            runState = NeedleHitRunState.Settling;
            interactionLocked = true;
            flyingNeedleInMotion = false;
            MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.95f);

            var settlement = new MiniGameSettlement
            {
                Score = score,
                CoinCount = score * 3,
                ChestCount = 0,
                Summary = BuildSettlementSummaryText()
            };

            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "NeedleHitFailureSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("needlehit.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("needlehit.settlement.score"), score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("needlehit.settlement.target"), CurrentLevel.TargetNeedleCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void CompleteCurrentRound()
        {
            runState = NeedleHitRunState.Settling;
            interactionLocked = true;
            flyingNeedleInMotion = false;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.95f);

            var settlement = new MiniGameSettlement
            {
                Score = score,
                CoinCount = score * 3,
                ChestCount = 1,
                Summary = BuildWinSettlementSummaryText()
            };

            EnsureLevelProgress();
            levelProgress.UnlockNext();
            ShowWinSettlement(settlement);
        }

        private void OnLevelSelectClicked()
        {
            EnsureLevelProgress();
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            levelSelectView = MiniGameLevelSelectView.Create(
                Shell.PopupHost,
                titleLabel == null ? null : titleLabel.font,
                LevelDefinitions.Length,
                levelProgress.CurrentLevelIndex,
                levelProgress.UnlockedLevelCount,
                "NeedleHitLevelSelectPanel",
                "NeedleHitLevelButton_",
                SelectLevel,
                CloseLevelSelectView);
        }

        private void SelectLevel(int index)
        {
            EnsureLevelProgress();
            if (!levelProgress.Select(index))
            {
                return;
            }

            CloseLevelSelectView();
            ResetGame();
        }

        private void LoadNextLevel(MiniGameSettlement settlement)
        {
            EnsureLevelProgress();
            if (!levelProgress.GoNext())
            {
                CompleteGame?.Invoke(settlement);
                return;
            }

            GrantSettlementReward(settlement);
            ResetGame();
        }

        private void ShowWinSettlement(MiniGameSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            var level = CurrentLevel;
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "NeedleHitSettlementPanel",
                    Title = UiTextCatalog.Get("needlehit.settlement.title"),
                    PrimaryInfo = MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("needlehit.settlement.target"), level.TargetNeedleCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.NextLevel,
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate { LoadNextLevel(settlement); },
                delegate
                {
                    SaveNextLevelForReturn();
                    CompleteGame?.Invoke(settlement);
                },
                false);
        }

        private void SaveNextLevelForReturn()
        {
            EnsureLevelProgress();
            levelProgress.SaveNextAsCurrent();
        }

        private void CloseLevelSelectView()
        {
            if (levelSelectView != null)
            {
                levelSelectView.Dispose();
                levelSelectView = null;
            }
        }

        private void EnsureLevelProgress()
        {
            if (levelProgress == null)
            {
                levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, LevelDefinitions.Length);
            }
        }

        private void ExitCurrentRound()
        {
            runState = NeedleHitRunState.Settling;
            interactionLocked = true;
            flyingNeedleInMotion = false;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.95f, 1f);

            var settlement = new MiniGameSettlement
            {
                Score = score,
                CoinCount = score * 3,
                ChestCount = 0,
                Summary = BuildExitSettlementSummaryText()
            };

            ShowBackHallRewardSettlementPanel(
                settlement,
                "NeedleHitSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("needlehit.settlement.score"), score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("needlehit.settlement.target"), CurrentLevel.TargetNeedleCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void ClearSpawnedNeedles()
        {
            if (flyingNeedle != null)
            {
                UnityEngine.Object.Destroy(flyingNeedle.gameObject);
                flyingNeedle = null;
            }

            for (var i = 0; i < stuckNeedles.Count; i++)
            {
                if (stuckNeedles[i].Transform != null)
                {
                    UnityEngine.Object.Destroy(stuckNeedles[i].Transform.gameObject);
                }
            }

            stuckNeedles.Clear();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.needlehit.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = BuildScoreText();
            }
        }

        private void ApplyDiscRotation()
        {
            if (discPivot != null)
            {
                discPivot.localRotation = Quaternion.Euler(0f, 0f, discRotationDegrees);
            }
        }

        private float CalculateFlyingNeedleImpactY()
        {
            return discPivot.anchoredPosition.y - DiscRadius - NeedleShaftLength - 22f;
        }

        private string BuildScoreText()
        {
            return UiTextCatalog.Format("needlehit.hud.level_score", score, CurrentLevel.TargetNeedleCount);
        }

        private string BuildWinSettlementSummaryText()
        {
            return UiTextCatalog.Format("needlehit.settlement.win", score, score * 3, 1);
        }

        private string BuildSettlementSummaryText()
        {
            var failText = UiTextCatalog.Get("needlehit.settlement.fail");
            var summaryFormat = UiTextCatalog.Get("needlehit.settlement.summary");
            return failText + "\n" + string.Format(summaryFormat, score, score * 3);
        }

        private string BuildExitSettlementSummaryText()
        {
            var exitText = UiTextCatalog.Get("needlehit.settlement.exit");
            var summaryFormat = UiTextCatalog.Get("needlehit.settlement.summary");
            return exitText + "\n" + string.Format(summaryFormat, score, score * 3);
        }

        private void OnFireRequested()
        {
            if (runState != NeedleHitRunState.Running || interactionLocked || flyingNeedle == null || flyingNeedleInMotion)
            {
                return;
            }

            flyingNeedleInMotion = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.82f, 1.08f);
        }

        private void ResumeFromPause()
        {
            interactionLocked = false;
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            ExitCurrentRound();
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private static T RequireComponent<T>(Transform root, string relativePath)
            where T : Component
        {
            var target = root.Find(relativePath);
            var component = target != null ? target.GetComponent<T>() : null;
            if (component == null)
            {
                throw new InvalidOperationException("NeedleHit prefab structure is incomplete at " + relativePath);
            }

            return component;
        }

        private static RectTransform RequireRectTransform(Transform root, string relativePath)
        {
            var target = root.Find(relativePath) as RectTransform;
            if (target == null)
            {
                throw new InvalidOperationException("NeedleHit prefab structure is incomplete at " + relativePath);
            }

            return target;
        }

        private static RectTransform EnsureChildRectTransform(Transform parent, string childName)
        {
            var child = parent.Find(childName) as RectTransform;
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName, typeof(RectTransform));
            child = childObject.GetComponent<RectTransform>();
            child.SetParent(parent, false);
            return child;
        }

        private static RoundedRectGraphic EnsureRoundedRectGraphic(GameObject target, Color color, float radius, bool raycastTarget)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
            {
                target.AddComponent<CanvasRenderer>();
            }

            var graphic = target.GetComponent<RoundedRectGraphic>();
            if (graphic == null)
            {
                graphic = target.AddComponent<RoundedRectGraphic>();
            }

            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static Button EnsureButton(GameObject target, Graphic targetGraphic)
        {
            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.AddComponent<Button>();
            }

            button.targetGraphic = targetGraphic;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.98f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static GameObject LoadRequiredSectionPrefab(string resourcePath, Transform parent, string instanceName)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Section prefab not found at Resources/" + resourcePath);
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = instanceName;
            return instance;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static float NormalizeAngle(float value)
        {
            while (value < 0f)
            {
                value += 360f;
            }

            while (value >= 360f)
            {
                value -= 360f;
            }

            return value;
        }

        private sealed class NeedleHitLevelDefinition
        {
            public NeedleHitLevelDefinition(int targetNeedleCount, float rotationSpeed, float safeAngleThreshold, int rotationDirection, params float[] initialWorldAngles)
            {
                TargetNeedleCount = targetNeedleCount;
                RotationSpeed = rotationSpeed;
                SafeAngleThreshold = safeAngleThreshold;
                RotationDirection = rotationDirection >= 0 ? 1 : -1;
                InitialWorldAngles = initialWorldAngles ?? Array.Empty<float>();
            }

            public int TargetNeedleCount { get; }

            public float RotationSpeed { get; }

            public float SafeAngleThreshold { get; }

            public int RotationDirection { get; }

            public float[] InitialWorldAngles { get; }
        }
    }
}
