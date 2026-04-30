using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class GameGoldMinerView : MiniGameBase
    {
        public const string GameIdConstant = "goldminer";

        private const float SwingMinAngle = -72f;
        private const float SwingMaxAngle = 72f;
        private const float SwingSpeed = 86f;
        private const float IdleRopeLength = 58f;
        private const float LaunchSpeed = 880f;
        private const float RetractSpeed = 780f;
        private const float HookSize = 32f;
        private const float HookHitRadius = 18f;
        private const float TopSectionNudgeDown = 18f;
        private const float LayoutTopOffset = 112f;
        private const float LayoutBottomPadding = 92f;
        private const float MineWidthFactor = 0.38f;
        private const float OriginToMineTop = 136f;
        private const int MinTargetCount = 4;
        private const int MaxTargetCount = 8;
        private const int TargetPlacementAttempts = 48;
        private const float TargetMinNormalizedX = -0.82f;
        private const float TargetMaxNormalizedX = 0.82f;
        private const float TargetMinNormalizedDepth = 0.16f;
        private const float TargetMaxNormalizedDepth = 0.90f;
        private const float TargetSpacingPadding = 18f;
        private const float TargetCenterGap = 0.14f;

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI remainingLabel;
        private Button restartButton;
        private Button playfieldButton;
        private GoldMinerRuntimeProbe runtimeProbe;
        private RectTransform playfieldRoot;
        private RectTransform minerRoot;
        private RectTransform pivotRect;
        private RectTransform ropeRect;
        private RectTransform hookRect;
        private RectTransform targetLayer;
        private readonly List<TargetNode> targets = new List<TargetNode>();
        private bool pendingTargetRefresh = true;
        private MiniGameSettlement pendingSettlement;
        private HookState hookState;
        private float swingAngle;
        private float swingDirection = 1f;
        private float travelAngle;
        private float ropeLength;
        private float maxRopeLength;
        private float mineLeft;
        private float mineRight;
        private float mineTop;
        private float mineBottom;
        private float layoutWidth;
        private float layoutHeight;
        private int score;
        private int coinCount;
        private int chestCount;
        private Vector2 hookOrigin;
        private Vector2 travelDirection = Vector2.down;
        private TargetNode grabbedTarget;

        public GameGoldMinerView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameGoldMinerView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            if (!EnsurePlayfieldLayout())
            {
                return;
            }

            UpdateHook(deltaTime);
            UpdateHookVisuals();
        }

        protected override void BuildOrBindSections()
        {
            var topConfig = MiniGameShellTopBarBuilder.CreateDefaultConfig("GoldMinerTop");
            topConfig.RootAnchoredPosition = new Vector2(0f, -TopSectionNudgeDown);
            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(Shell.TopHost, topConfig);
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("GoldMinerBottom"));
            var bottomRoot = bottomContainerRefs.Root.gameObject;
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomContainerRefs.ActionBar).Button;

            if (titleLabel == null || scoreLabel == null || restartButton == null)
            {
                throw new InvalidOperationException("GoldMiner prefab structure is incomplete.");
            }

            remainingLabel = EnsureRemainingLabel(scoreLabel);
            restartButton.gameObject.name = "RestartButton";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);

            BuildPlayfield();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();

            score = 0;
            coinCount = 0;
            chestCount = 0;
            pendingSettlement = null;
            hookState = HookState.Swinging;
            swingAngle = -30f;
            swingDirection = 1f;
            travelAngle = swingAngle;
            ropeLength = 0f;
            grabbedTarget = null;
            travelDirection = AngleToDirection(travelAngle);

            ResetTargets();
            RefreshTargetsIfNeeded();
            RefreshHud();
            UpdateHookVisuals();
        }

        protected override void OnPauseRequested()
        {
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (playfieldButton != null)
            {
                playfieldButton.onClick.RemoveListener(OnPlayfieldPressed);
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.goldminer.help", null);
        }

        private void BuildPlayfield()
        {
            playfieldRoot = CreateRect("GoldMinerPlayfield", Shell.ContentHost, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            runtimeProbe = playfieldRoot.gameObject.AddComponent<GoldMinerRuntimeProbe>();

            var board = CreateRoundedRect("BoardSurface", playfieldRoot, new Color(0.97f, 0.95f, 0.89f, 0.70f), 32f);
            Stretch(board.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -30f));
            board.raycastTarget = true;

            playfieldButton = board.gameObject.AddComponent<Button>();
            playfieldButton.transition = Selectable.Transition.None;
            playfieldButton.targetGraphic = board;
            playfieldButton.onClick.AddListener(OnPlayfieldPressed);

            minerRoot = CreateRect("MinerRig", playfieldRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            CreateMinerVisuals();

            targetLayer = CreateRect("TargetLayer", playfieldRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateHookVisuals();
            CreateTargets();

            runtimeProbe.Bind(
                GetHookStateName,
                () => score,
                () => coinCount,
                () => chestCount,
                GetRemainingTargetCount,
                () => hookState == HookState.Settled,
                LaunchAtAngleForTest,
                GetSuggestedLaunchAnglesForTest,
                () => swingAngle,
                ForceClearBoardForTest);
        }

        private void CreateMinerVisuals()
        {
            var beam = CreateImage("Beam", minerRoot, new Color(0.40f, 0.28f, 0.16f, 1f));
            beam.rectTransform.sizeDelta = new Vector2(196f, 18f);
            beam.rectTransform.pivot = new Vector2(0.5f, 1f);
            beam.raycastTarget = false;

            var body = CreateImage("MinerBody", minerRoot, new Color(0.31f, 0.38f, 0.52f, 1f));
            body.rectTransform.sizeDelta = new Vector2(86f, 56f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            body.raycastTarget = false;

            var helmet = CreateImage("MinerHelmet", minerRoot, new Color(0.95f, 0.76f, 0.22f, 1f));
            helmet.rectTransform.sizeDelta = new Vector2(70f, 22f);
            helmet.rectTransform.pivot = new Vector2(0.5f, 1f);
            helmet.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            helmet.raycastTarget = false;

            pivotRect = CreateRect("HookPivot", playfieldRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        }

        private void CreateHookVisuals()
        {
            ropeRect = CreateImage("HookRope", playfieldRoot, new Color(0.22f, 0.16f, 0.12f, 1f)).rectTransform;
            ropeRect.anchorMin = new Vector2(0.5f, 0.5f);
            ropeRect.anchorMax = new Vector2(0.5f, 0.5f);
            ropeRect.pivot = new Vector2(0.5f, 1f);
            ropeRect.sizeDelta = new Vector2(6f, IdleRopeLength);
            ropeRect.GetComponent<Image>().raycastTarget = false;

            hookRect = CreateImage("HookHead", playfieldRoot, new Color(0.89f, 0.75f, 0.32f, 1f)).rectTransform;
            hookRect.anchorMin = new Vector2(0.5f, 0.5f);
            hookRect.anchorMax = new Vector2(0.5f, 0.5f);
            hookRect.pivot = new Vector2(0.5f, 1f);
            hookRect.sizeDelta = new Vector2(HookSize, HookSize);
            hookRect.GetComponent<Image>().raycastTarget = false;

            var leftClaw = CreateImage("LeftClaw", hookRect, new Color(0.70f, 0.57f, 0.20f, 1f));
            leftClaw.rectTransform.anchorMin = new Vector2(0f, 0f);
            leftClaw.rectTransform.anchorMax = new Vector2(0f, 0f);
            leftClaw.rectTransform.pivot = new Vector2(0.5f, 1f);
            leftClaw.rectTransform.sizeDelta = new Vector2(8f, 18f);
            leftClaw.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            leftClaw.rectTransform.localEulerAngles = new Vector3(0f, 0f, 24f);
            leftClaw.raycastTarget = false;

            var rightClaw = CreateImage("RightClaw", hookRect, new Color(0.70f, 0.57f, 0.20f, 1f));
            rightClaw.rectTransform.anchorMin = new Vector2(1f, 0f);
            rightClaw.rectTransform.anchorMax = new Vector2(1f, 0f);
            rightClaw.rectTransform.pivot = new Vector2(0.5f, 1f);
            rightClaw.rectTransform.sizeDelta = new Vector2(8f, 18f);
            rightClaw.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
            rightClaw.rectTransform.localEulerAngles = new Vector3(0f, 0f, -24f);
            rightClaw.raycastTarget = false;
        }

        private void CreateTargets()
        {
            targets.Clear();
            for (var index = 0; index < MaxTargetCount; index++)
            {
                var image = CreateImage("Target_" + index, targetLayer, Color.white);
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.gameObject.SetActive(false);
                targets.Add(new TargetNode(image, image.rectTransform));
            }
        }

        private void ResetTargets()
        {
            pendingTargetRefresh = true;
            for (var index = 0; index < targets.Count; index++)
            {
                var node = targets[index];
                node.isActive = false;
                node.isAttached = false;
                node.currentPosition = Vector2.zero;
                node.rect.gameObject.SetActive(false);
            }
        }

        private void RefreshTargetsIfNeeded()
        {
            if (!pendingTargetRefresh || !EnsurePlayfieldLayout())
            {
                return;
            }

            var templates = BuildRandomTargetTemplates();
            for (var index = 0; index < targets.Count; index++)
            {
                var node = targets[index];
                if (index >= templates.Count)
                {
                    node.isActive = false;
                    node.isAttached = false;
                    node.rect.gameObject.SetActive(false);
                    continue;
                }

                var template = templates[index];
                ApplyTargetTemplate(node, template);
            }

            pendingTargetRefresh = false;
            RefreshHud();
        }

        private List<TargetTemplate> BuildRandomTargetTemplates()
        {
            var count = UnityEngine.Random.Range(MinTargetCount, MaxTargetCount + 1);
            var templates = new List<TargetTemplate>(count);
            for (var index = 0; index < count; index++)
            {
                var type = GetRandomTargetType();
                var config = BuildTargetConfig(type);
                var placement = FindRandomPlacement(templates, config);
                templates.Add(new TargetTemplate("Target_" + index + "_" + type, type, placement.x, placement.y));
            }

            EnsureGuaranteedChestTarget(templates);

            return templates;
        }

        private static void EnsureGuaranteedChestTarget(List<TargetTemplate> templates)
        {
            if (templates == null || templates.Count == 0)
            {
                return;
            }

            for (var index = 0; index < templates.Count; index++)
            {
                if (BuildTargetConfig(templates[index].type).chestReward > 0)
                {
                    return;
                }
            }

            var first = templates[0];
            templates[0] = new TargetTemplate(first.name, GoldMinerTargetType.RareGem, first.normalizedX, first.normalizedDepth);
        }

        private Vector2 FindRandomPlacement(IReadOnlyList<TargetTemplate> existingTemplates, TargetConfig candidateConfig)
        {
            for (var attempt = 0; attempt < TargetPlacementAttempts; attempt++)
            {
                var normalizedX = GenerateRandomNormalizedX();
                var normalizedDepth = UnityEngine.Random.Range(TargetMinNormalizedDepth, TargetMaxNormalizedDepth);
                var candidatePosition = CalculateTargetPosition(normalizedX, normalizedDepth);
                if (IsPlacementClear(candidatePosition, candidateConfig, existingTemplates))
                {
                    return new Vector2(normalizedX, normalizedDepth);
                }
            }

            return new Vector2(
                GenerateRandomNormalizedX(),
                UnityEngine.Random.Range(TargetMinNormalizedDepth, TargetMaxNormalizedDepth));
        }

        private static GoldMinerTargetType GetRandomTargetType()
        {
            var roll = UnityEngine.Random.value;
            if (roll < 0.10f)
            {
                return GoldMinerTargetType.SpecialBigGold;
            }

            if (roll < 0.35f)
            {
                return GoldMinerTargetType.RareGem;
            }

            if (roll < 0.55f)
            {
                return GoldMinerTargetType.BigGold;
            }

            if (roll < 0.80f)
            {
                return GoldMinerTargetType.SmallGold;
            }

            return GoldMinerTargetType.Stone;
        }

        private static float GenerateRandomNormalizedX()
        {
            float normalizedX;
            do
            {
                normalizedX = UnityEngine.Random.Range(TargetMinNormalizedX, TargetMaxNormalizedX);
            }
            while (Mathf.Abs(normalizedX) < TargetCenterGap);

            return normalizedX;
        }

        private bool IsPlacementClear(Vector2 candidatePosition, TargetConfig candidateConfig, IReadOnlyList<TargetTemplate> existingTemplates)
        {
            for (var index = 0; index < existingTemplates.Count; index++)
            {
                var existingTemplate = existingTemplates[index];
                var existingConfig = BuildTargetConfig(existingTemplate.type);
                var existingPosition = CalculateTargetPosition(existingTemplate);
                var minDistance = candidateConfig.radius + existingConfig.radius + TargetSpacingPadding;
                if (Vector2.Distance(candidatePosition, existingPosition) < minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyTargetTemplate(TargetNode node, TargetTemplate template)
        {
            node.template = template;
            node.type = template.type;
            node.config = BuildTargetConfig(template.type);
            node.isActive = true;
            node.isAttached = false;
            node.rect.gameObject.SetActive(true);
            node.rect.gameObject.name = template.name;
            node.image.sprite = GetTargetSprite(template.type);
            node.image.color = node.config.color;
            node.currentPosition = CalculateTargetPosition(template);
            node.rect.anchoredPosition = node.currentPosition;
            node.rect.sizeDelta = Vector2.one * node.config.diameter;
            node.rect.localScale = Vector3.one;
        }

        private void UpdateHook(float deltaTime)
        {
            switch (hookState)
            {
                case HookState.Swinging:
                    UpdateSwing(deltaTime);
                    break;
                case HookState.Firing:
                    UpdateFiring(deltaTime);
                    break;
                case HookState.RetractingEmpty:
                    UpdateRetracting(deltaTime, false);
                    break;
                case HookState.RetractingWithTarget:
                    UpdateRetracting(deltaTime, true);
                    break;
            }
        }

        private void UpdateSwing(float deltaTime)
        {
            swingAngle += swingDirection * SwingSpeed * deltaTime;
            if (swingAngle >= SwingMaxAngle)
            {
                swingAngle = SwingMaxAngle;
                swingDirection = -1f;
            }
            else if (swingAngle <= SwingMinAngle)
            {
                swingAngle = SwingMinAngle;
                swingDirection = 1f;
            }

            travelAngle = swingAngle;
            travelDirection = AngleToDirection(travelAngle);
        }

        private void UpdateFiring(float deltaTime)
        {
            var previousTip = GetHookTipPosition();
            ropeLength += LaunchSpeed * deltaTime;
            var nextTip = GetHookTipPosition();

            TargetNode hitTarget;
            if (TryFindHitTarget(previousTip, nextTip, out hitTarget))
            {
                grabbedTarget = hitTarget;
                grabbedTarget.isAttached = true;
                grabbedTarget.currentPosition = nextTip;
                ropeLength = Vector2.Distance(hookOrigin, grabbedTarget.currentPosition);
                hookState = HookState.RetractingWithTarget;
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.42f, 1.12f);
                return;
            }

            if (ropeLength >= maxRopeLength || nextTip.x <= mineLeft || nextTip.x >= mineRight || nextTip.y <= mineBottom)
            {
                hookState = HookState.RetractingEmpty;
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.22f, 1.04f);
            }
        }

        private void UpdateRetracting(float deltaTime, bool carryingTarget)
        {
            var speed = carryingTarget && grabbedTarget != null
                ? RetractSpeed / grabbedTarget.config.retractWeight
                : RetractSpeed;

            ropeLength = Mathf.Max(0f, ropeLength - (speed * deltaTime));

            if (carryingTarget && grabbedTarget != null)
            {
                grabbedTarget.currentPosition = GetHookTipPosition();
            }

            if (ropeLength > 0.01f)
            {
                return;
            }

            ropeLength = 0f;
            if (!carryingTarget || grabbedTarget == null)
            {
                hookState = HookState.Swinging;
                return;
            }

            score += grabbedTarget.config.score;
            coinCount += grabbedTarget.config.coinReward;
            chestCount += grabbedTarget.config.chestReward;
            grabbedTarget.isActive = false;
            grabbedTarget.isAttached = false;
            grabbedTarget.rect.gameObject.SetActive(false);
            grabbedTarget = null;
            RefreshHud();

            if (GetRemainingTargetCount() == 0)
            {
                ShowSettlement(false);
                return;
            }

            hookState = HookState.Swinging;
            travelAngle = swingAngle;
            travelDirection = AngleToDirection(travelAngle);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Combo, 0.25f, 1.05f);
        }

        private void UpdateHookVisuals()
        {
            if (playfieldRoot == null || ropeRect == null || hookRect == null)
            {
                return;
            }

            var displayAngle = hookState == HookState.Swinging ? swingAngle : travelAngle;
            var displayLength = hookState == HookState.Swinging ? IdleRopeLength : ropeLength;
            var tipPosition = hookState == HookState.Swinging
                ? hookOrigin + (AngleToDirection(displayAngle) * IdleRopeLength)
                : GetHookTipPosition();

            ropeRect.anchoredPosition = hookOrigin;
            ropeRect.localEulerAngles = new Vector3(0f, 0f, displayAngle);
            ropeRect.sizeDelta = new Vector2(6f, Mathf.Max(6f, displayLength));

            hookRect.anchoredPosition = tipPosition;
            hookRect.localEulerAngles = new Vector3(0f, 0f, displayAngle);

            if (pivotRect != null)
            {
                pivotRect.anchoredPosition = hookOrigin;
            }

            if (grabbedTarget != null && grabbedTarget.isAttached)
            {
                grabbedTarget.rect.anchoredPosition = tipPosition;
            }
        }

        private void RefreshHud()
        {
            titleLabel.text = UiTextCatalog.Get("game.goldminer.name");
            scoreLabel.text = BuildMetricText("goldminer.hud.coin", coinCount);
            remainingLabel.text = BuildMetricText("goldminer.hud.remaining", GetRemainingTargetCount());
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            ShowSettlement(true);
        }

        private void OnPlayfieldPressed()
        {
            TryLaunchHook();
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private bool LaunchAtAngleForTest(float angleDegrees)
        {
            if (hookState != HookState.Swinging)
            {
                return false;
            }

            swingAngle = Mathf.Clamp(angleDegrees, SwingMinAngle, SwingMaxAngle);
            travelAngle = swingAngle;
            travelDirection = AngleToDirection(travelAngle);
            UpdateHookVisuals();
            return TryLaunchHook();
        }

        private bool TryLaunchHook()
        {
            if (hookState != HookState.Swinging)
            {
                return false;
            }

            hookState = HookState.Firing;
            ropeLength = IdleRopeLength;
            travelAngle = swingAngle;
            travelDirection = AngleToDirection(travelAngle);
            grabbedTarget = null;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.32f, 1.1f);
            return true;
        }

        private void ShowSettlement(bool isExit)
        {
            hookState = HookState.Settled;
            pendingSettlement = new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = BuildSettlementSummary(isExit),
            };

            if (pendingSettlement.Summary == "?")
            {
                pendingSettlement.Summary = BuildSettlementSummary(isExit);
            }

            if (isExit)
            {
                ShowBackHallRewardSettlementPanel(
                    pendingSettlement,
                    "GoldMinerSettlementPanel",
                    new MiniGameSettlementInfoRow(UiTextCatalog.Get("goldminer.hud.score"), score.ToString()),
                    new MiniGameSettlementInfoRow(UiTextCatalog.Get("goldminer.settlement.targets"), targets.Count.ToString()),
                    CompleteSettlement);
            }
            else
            {
                ShowRewardSettlementPanel(
                    pendingSettlement,
                    new MiniGameRewardSettlementPanelParams
                    {
                        RootName = "GoldMinerSettlementPanel",
                        Style = MiniGameRewardSettlementPanelStyle.Success,
                        PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                        Title = UiTextCatalog.Get("goldminer.settlement.win_title"),
                        PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("goldminer.hud.score"), score.ToString()),
                        SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("goldminer.settlement.targets"), targets.Count.ToString()),
                        RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                        CoinCount = pendingSettlement.CoinCount,
                        ChestCount = pendingSettlement.ChestCount
                    },
                    ResetGame,
                    CompleteSettlement,
                    true);
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.72f, 1f);
        }

        private void ForceClearBoardForTest()
        {
            if (hookState == HookState.Settled)
            {
                return;
            }

            grabbedTarget = null;
            for (var index = 0; index < targets.Count; index++)
            {
                var node = targets[index];
                if (!node.isActive)
                {
                    continue;
                }

                score += node.config.score;
                coinCount += node.config.coinReward;
                chestCount += node.config.chestReward;
                node.isActive = false;
                node.isAttached = false;
                node.rect.gameObject.SetActive(false);
            }

            RefreshHud();
            ShowSettlement(false);
        }

        private void CompleteSettlement()
        {
            Shell.ClosePopup();
            CompleteGame?.Invoke(pendingSettlement ?? new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = BuildSettlementSummary(false),
            });
        }

        private string BuildSettlementSummary(bool isExit)
        {
            var textKey = isExit ? "goldminer.summary.exit" : "goldminer.summary.finished";
            return UiTextCatalog.Format(textKey, score, coinCount, chestCount);
        }

        private string GetHookStateName()
        {
            return hookState.ToString();
        }

        private float[] GetSuggestedLaunchAnglesForTest()
        {
            var angles = new List<float>();
            for (var index = 0; index < targets.Count; index++)
            {
                var node = targets[index];
                if (!node.isActive || node.isAttached)
                {
                    continue;
                }

                var delta = node.currentPosition - hookOrigin;
                if (delta.sqrMagnitude <= 0.01f)
                {
                    continue;
                }

                angles.Add(Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg);
            }

            return angles.ToArray();
        }

        private int GetRemainingTargetCount()
        {
            var count = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                if (targets[index].isActive)
                {
                    count++;
                }
            }

            return count;
        }

        private bool EnsurePlayfieldLayout()
        {
            if (playfieldRoot == null)
            {
                return false;
            }

            var rect = playfieldRoot.rect;
            if (rect.width < 10f || rect.height < 10f)
            {
                return false;
            }

            if (Mathf.Abs(layoutWidth - rect.width) < 0.01f && Mathf.Abs(layoutHeight - rect.height) < 0.01f)
            {
                return true;
            }

            layoutWidth = rect.width;
            layoutHeight = rect.height;
            hookOrigin = new Vector2(0f, (rect.height * 0.5f) - LayoutTopOffset);
            mineLeft = -rect.width * MineWidthFactor;
            mineRight = rect.width * MineWidthFactor;
            mineTop = hookOrigin.y - OriginToMineTop;
            mineBottom = (-rect.height * 0.5f) + LayoutBottomPadding;
            maxRopeLength = Vector2.Distance(hookOrigin, new Vector2(0f, mineBottom)) + 48f;

            if (minerRoot != null)
            {
                minerRoot.anchoredPosition = new Vector2(0f, (rect.height * 0.5f) - 28f);
            }

            for (var index = 0; index < targets.Count; index++)
            {
                var node = targets[index];
                if (!node.isActive)
                {
                    continue;
                }

                var position = CalculateTargetPosition(node.template);
                if (!node.isAttached)
                {
                    node.currentPosition = position;
                }

                if (!node.isAttached)
                {
                    node.rect.anchoredPosition = position;
                }
            }

            RefreshTargetsIfNeeded();

            return true;
        }

        private Vector2 CalculateTargetPosition(TargetTemplate template)
        {
            return CalculateTargetPosition(template.normalizedX, template.normalizedDepth);
        }

        private Vector2 CalculateTargetPosition(float normalizedX, float normalizedDepth)
        {
            var x = Mathf.Lerp(mineLeft, mineRight, (normalizedX + 1f) * 0.5f);
            var y = Mathf.Lerp(hookOrigin.y - OriginToMineTop, mineBottom, normalizedDepth);
            return new Vector2(x, y);
        }

        private Vector2 GetHookTipPosition()
        {
            return hookOrigin + (travelDirection * ropeLength);
        }

        private bool TryFindHitTarget(Vector2 start, Vector2 end, out TargetNode hitTarget)
        {
            hitTarget = null;
            var bestDistance = float.MaxValue;

            for (var index = 0; index < targets.Count; index++)
            {
                var node = targets[index];
                if (!node.isActive || node.isAttached)
                {
                    continue;
                }

                float segmentFactor;
                var distance = DistancePointToSegment(node.currentPosition, start, end, out segmentFactor);
                var captureRadius = node.config.radius + HookHitRadius;
                if (distance > captureRadius)
                {
                    continue;
                }

                var pathDistance = Vector2.Distance(start, Vector2.Lerp(start, end, segmentFactor));
                if (pathDistance >= bestDistance)
                {
                    continue;
                }

                bestDistance = pathDistance;
                hitTarget = node;
            }

            return hitTarget != null;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end, out float segmentFactor)
        {
            var segment = end - start;
            var sqrMagnitude = segment.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
            {
                segmentFactor = 0f;
                return Vector2.Distance(point, start);
            }

            segmentFactor = Mathf.Clamp01(Vector2.Dot(point - start, segment) / sqrMagnitude);
            var closestPoint = start + (segment * segmentFactor);
            return Vector2.Distance(point, closestPoint);
        }

        private static TargetConfig BuildTargetConfig(GoldMinerTargetType type)
        {
            switch (type)
            {
                case GoldMinerTargetType.BigGold:
                    return new TargetConfig(160, 20, 0, 82f, 1.9f, new Color(1f, 0.92f, 0.45f, 1f));
                case GoldMinerTargetType.SmallGold:
                    return new TargetConfig(80, 5, 0, 58f, 1.15f, new Color(1f, 0.88f, 0.30f, 1f));
                case GoldMinerTargetType.RareGem:
                    return new TargetConfig(120, 10, 1, 52f, 1.05f, new Color(0.46f, 0.82f, 1f, 1f));
                case GoldMinerTargetType.SpecialBigGold:
                    return new TargetConfig(220, 20, 1, 92f, 1.65f, new Color(1f, 0.80f, 0.18f, 1f));
                default:
                    return new TargetConfig(35, 10, 0, 70f, 2.35f, new Color(0.88f, 0.90f, 0.95f, 1f));
            }
        }

        private static Sprite GetTargetSprite(GoldMinerTargetType type)
        {
            switch (type)
            {
                case GoldMinerTargetType.BigGold:
                    return Resources.Load<Sprite>("GameIcons/coin");
                case GoldMinerTargetType.SmallGold:
                    return Resources.Load<Sprite>("GameIcons/coin");
                case GoldMinerTargetType.RareGem:
                    return Resources.Load<Sprite>("GameIcons/diamond");
                case GoldMinerTargetType.SpecialBigGold:
                    return Resources.Load<Sprite>("GameIcons/chest");
                default:
                    return Resources.Load<Sprite>("GameIcons/shield");
            }
        }

        private static TextMeshProUGUI EnsureRemainingLabel(TextMeshProUGUI template)
        {
            var existing = template.transform.parent.Find("Remaining")?.GetComponent<TextMeshProUGUI>();
            if (existing != null)
            {
                return existing;
            }

            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false);
            clone.name = "Remaining";
            clone.transform.SetAsLastSibling();
            return clone.GetComponent<TextMeshProUGUI>();
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            Stretch(rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            return rectTransform;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, float cornerRadius)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            var graphic = gameObject.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            return graphic;
        }

        private static string BuildMetricText(string key, int value)
        {
            return UiTextCatalog.Get(key) + " " + value;
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
        }

        private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.localScale = Vector3.one;
            rectTransform.localEulerAngles = Vector3.zero;
        }

        private enum HookState
        {
            Swinging,
            Firing,
            RetractingEmpty,
            RetractingWithTarget,
            Settled,
        }

        private enum GoldMinerTargetType
        {
            BigGold,
            SmallGold,
            Stone,
            RareGem,
            SpecialBigGold,
        }

        private readonly struct TargetTemplate
        {
            public readonly string name;
            public readonly GoldMinerTargetType type;
            public readonly float normalizedX;
            public readonly float normalizedDepth;

            public TargetTemplate(string name, GoldMinerTargetType type, float normalizedX, float normalizedDepth)
            {
                this.name = name;
                this.type = type;
                this.normalizedX = normalizedX;
                this.normalizedDepth = normalizedDepth;
            }
        }

        private readonly struct TargetConfig
        {
            public readonly int score;
            public readonly int coinReward;
            public readonly int chestReward;
            public readonly float diameter;
            public readonly float retractWeight;
            public readonly Color color;

            public float radius
            {
                get { return diameter * 0.5f; }
            }

            public TargetConfig(int score, int coinReward, int chestReward, float diameter, float retractWeight, Color color)
            {
                this.score = score;
                this.coinReward = coinReward;
                this.chestReward = chestReward;
                this.diameter = diameter;
                this.retractWeight = retractWeight;
                this.color = color;
            }
        }

        private sealed class TargetNode
        {
            public TargetTemplate template;
            public GoldMinerTargetType type;
            public TargetConfig config;
            public readonly Image image;
            public readonly RectTransform rect;
            public bool isActive;
            public bool isAttached;
            public Vector2 currentPosition;

            public TargetNode(Image image, RectTransform rect)
            {
                this.image = image;
                this.rect = rect;
                isActive = false;
            }
        }
    }
}
