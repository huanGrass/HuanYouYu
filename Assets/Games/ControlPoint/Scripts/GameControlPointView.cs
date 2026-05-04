using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class GameControlPointView : MiniGameBase
    {
        public const string GameIdConstant = "control-point";

        private const string LevelResourcePath = "Levels/control-point.levels";
        private const int MinPointCount = 5;
        private const int MaxPointCount = 10;
        private const float MinPointDistance = 168f;
        private const float MinPointX = -285f;
        private const float MaxPointX = 285f;
        private const float MinPointY = -305f;
        private const float MaxPointY = 245f;
        private const float LevelOnePointSize = 96f;
        private const float LevelTwoPointSize = 114f;
        private const float LevelThreePointSize = 132f;
        private const float LevelOneIdleProduceInterval = 1.4f;
        private const float LevelTwoIdleProduceInterval = 1.1f;
        private const float LevelThreeIdleProduceInterval = 0.85f;
        private const float LevelOneOneLineTransferInterval = 1f;
        private const float LevelTwoOneLineTransferInterval = 0.75f;
        private const float LevelTwoTwoLineTransferInterval = 1.05f;
        private const float LevelThreeOneLineTransferInterval = 0.55f;
        private const float LevelThreeTwoLineTransferInterval = 0.9f;
        private const float LevelThreeThreeLineTransferInterval = 1.25f;
        private const float EnemyThinkInterval = 1.2f;
        private const float LineThickness = 18f;
        private const float MaxLineEndpointInset = 58f;
        private const float ArrowWidth = 34f;
        private const float ArrowHeight = 44f;
        private const float SoldierSize = 30f;
        private const float CapacityDotSize = 10f;
        private const float CapacityDotInnerSize = 5.5f;
        private const float CapacityDotSpacing = 16f;
        private const float SoldierTravelSpeed = 260f;
        private const float SoldierDestinationEpsilon = 0.5f;
        private const float CutLinePadding = 20f;
        private const float CutTrailThickness = 28f;
        private const float CutTrailMinPointDistance = 10f;
        private const int MusterUnitGain = 8;
        private const float MusterCooldownSeconds = 20f;

        private static readonly Color NeutralColor = new Color32(238, 229, 198, 255);
        private static readonly Color PlayerColor = new Color32(70, 145, 106, 255);
        private static readonly Color EnemyColor = new Color32(195, 88, 72, 255);
        private static readonly Color EnemyTwoColor = new Color32(118, 105, 190, 255);
        private static readonly Color EnemyThreeColor = new Color32(196, 126, 55, 255);
        private static readonly Color PlayerLineColor = new Color(0.22f, 0.58f, 0.42f, 0.78f);
        private static readonly Color EnemyLineColor = new Color(0.78f, 0.30f, 0.24f, 0.72f);
        private static readonly Color EnemyTwoLineColor = new Color(0.44f, 0.36f, 0.78f, 0.72f);
        private static readonly Color EnemyThreeLineColor = new Color(0.78f, 0.47f, 0.20f, 0.72f);
        private static readonly Color PreviewLineColor = new Color(0.22f, 0.58f, 0.42f, 0.48f);
        private static readonly Color CutGestureLineColor = new Color(1f, 0.96f, 0.52f, 0.82f);
        private static readonly Color TextColor = new Color32(55, 66, 46, 255);
        private static readonly ControlPointOwner[] EnemyOwners =
        {
            ControlPointOwner.Enemy,
            ControlPointOwner.EnemyTwo,
            ControlPointOwner.EnemyThree
        };

        private static readonly ControlPointLevelDefinition[] LevelDefinitions = LoadLevelDefinitions();

        private ControlPointState[] points = new ControlPointState[0];
        private ControlPointViewRefs[] pointViews = new ControlPointViewRefs[0];
        private readonly List<ControlPointConnection> connections = new List<ControlPointConnection>();
        private readonly List<MovingUnitView> detachedMovingUnits = new List<MovingUnitView>();
        private readonly List<Vector2> cutGesturePoints = new List<Vector2>();
        private readonly float[] enemyThinkTimers = new float[EnemyOwners.Length];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private Button levelSelectButton;
        private Button musterButton;
        private TextMeshProUGUI musterButtonLabel;
        private RectTransform contentRect;
        private RectTransform lineLayer;
        private RectTransform pointLayer;
        private RectTransform previewLine;
        private CutGestureTrailGraphic cutGestureTrail;
        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private int currentLevelIndex;
        private int dragSourceIndex = -1;
        private bool isCuttingGesture;
        private bool isMusterSelecting;
        private float musterCooldownRemaining;
        private Vector2 lastCutLocalPoint;
        private int defeatedEnemyUnits;
        private bool isSettled;
        private ControlPointRoundResult roundResult;

        public static int LevelCount
        {
            get { return LevelDefinitions.Length; }
        }

        private ControlPointLevelDefinition CurrentLevel
        {
            get { return LevelDefinitions[currentLevelIndex]; }
        }

        public GameControlPointView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "GameControlPointView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (isSettled)
            {
                return;
            }

            var clampedDelta = Mathf.Max(0f, deltaTime);
            TickProduction(clampedDelta);
            TickConnections(clampedDelta);
            TickDetachedMovingUnits(clampedDelta);
            TickEnemyAi(clampedDelta);
            TickSkills(clampedDelta);
            RefreshHud();
            RefreshPointViews();
            CheckRoundEnd();
        }

        protected override void BuildOrBindSections()
        {
            var topRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("ControlPointTop"));
            titleLabel = topRefs.TitleText;
            scoreLabel = topRefs.ScoreText;

            BuildContent(Shell.ContentHost);
            BuildBottom(Shell.BottomHost);
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseLevelSelectView();
            CloseRewardSettlementPanel();
            EnsureLevelProgress();
            currentLevelIndex = levelProgress.CurrentLevelIndex;
            ClearConnections();
            ClearDetachedMovingUnits();
            HidePreviewLine();
            HideCutGestureLine();

            ApplyLevel(CurrentLevel);

            defeatedEnemyUnits = 0;
            for (var i = 0; i < enemyThinkTimers.Length; i++)
            {
                enemyThinkTimers[i] = EnemyThinkInterval + (i * 0.25f);
            }

            dragSourceIndex = -1;
            isCuttingGesture = false;
            isSettled = false;
            roundResult = ControlPointRoundResult.None;
            isMusterSelecting = false;
            musterCooldownRemaining = 0f;

            RefreshHud();
            RefreshPointViews();
        }

        protected override void OnPauseRequested()
        {
            if (!isSettled)
            {
                Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
            }
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            ClearConnections();
            ClearDetachedMovingUnits();
            HidePreviewLine();
            HideCutGestureLine();
            ClearPointViews();
            CloseLevelSelectView();
            CloseRewardSettlementPanel();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            }

            if (musterButton != null)
            {
                musterButton.onClick.RemoveListener(OnMusterClicked);
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.control_point.help", null);
        }

        private void BuildContent(Transform parent)
        {
            var content = CreateRectObject("ControlPointContent", parent);
            contentRect = content.GetComponent<RectTransform>();
            Stretch(contentRect, Vector2.zero, Vector2.one, new Vector2(22f, 20f), new Vector2(-22f, -20f));

            var background = content.AddComponent<RoundedRectGraphic>();
            background.color = new Color(0.86f, 0.92f, 0.82f, 0.55f);
            background.CornerRadius = 34f;
            background.raycastTarget = true;

            var contentTrigger = content.AddComponent<EventTrigger>();
            AddContentTrigger(contentTrigger, EventTriggerType.PointerDown);
            AddContentTrigger(contentTrigger, EventTriggerType.BeginDrag);
            AddContentTrigger(contentTrigger, EventTriggerType.Drag);
            AddContentTrigger(contentTrigger, EventTriggerType.EndDrag);
            AddContentTrigger(contentTrigger, EventTriggerType.PointerUp);

            lineLayer = CreateRectObject("LineLayer", contentRect).GetComponent<RectTransform>();
            Stretch(lineLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            pointLayer = CreateRectObject("PointLayer", contentRect).GetComponent<RectTransform>();
            Stretch(pointLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildBottom(Transform parent)
        {
            var bottomRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                parent,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("ControlPointBottom"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomRefs.ActionBar).Button;
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
            MiniGameSfxPlayer.Attach(restartButton, MiniGameSfxType.UiTap, 0.9f);

            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottomRefs.ActionBar, "ControlPointLevelSelectButton").Button;
            levelSelectButton.onClick.RemoveAllListeners();
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);

            var musterRefs = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottomRefs.ActionBar, "ControlPointMusterButton");
            musterButton = musterRefs.Button;
            musterButton.onClick.RemoveAllListeners();
            musterButton.onClick.AddListener(OnMusterClicked);
            musterButtonLabel = musterButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private ControlPointViewRefs CreatePointView(int index, Transform parent, Vector2 position)
        {
            var root = CreateRectObject("ControlPoint_" + index, parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(LevelOnePointSize, LevelOnePointSize);
            rect.anchoredPosition = position;

            var graphic = root.AddComponent<RoundedRectGraphic>();
            graphic.color = NeutralColor;
            graphic.CornerRadius = LevelOnePointSize * 0.5f;

            var label = CreateText("Units", rect, 40f, FontStyles.Bold);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var levelLabel = CreateText("Level", rect, 18f, FontStyles.Bold);
            var levelRect = levelLabel.rectTransform;
            levelRect.anchorMin = new Vector2(0.5f, 1f);
            levelRect.anchorMax = new Vector2(0.5f, 1f);
            levelRect.pivot = new Vector2(0.5f, 1f);
            levelRect.anchoredPosition = new Vector2(0f, -16f);
            levelRect.sizeDelta = new Vector2(74f, 24f);

            var capacityDots = CreateCapacityDots(rect);

            var trigger = root.AddComponent<EventTrigger>();
            AddPointTrigger(trigger, EventTriggerType.PointerDown, index);
            AddPointTrigger(trigger, EventTriggerType.BeginDrag, index);
            AddPointTrigger(trigger, EventTriggerType.Drag, index);
            AddPointTrigger(trigger, EventTriggerType.EndDrag, index);
            AddPointTrigger(trigger, EventTriggerType.PointerUp, index);

            return new ControlPointViewRefs(rect, graphic, label, levelLabel, capacityDots);
        }

        private CapacityDotView[] CreateCapacityDots(Transform parent)
        {
            var dots = new CapacityDotView[3];
            for (var i = 0; i < dots.Length; i++)
            {
                var dotRoot = CreateRectObject("ConnectionCapacityDot_" + i, parent);
                var dotRect = dotRoot.GetComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(CapacityDotSize, CapacityDotSize);

                var outer = dotRoot.AddComponent<RoundedRectGraphic>();
                outer.CornerRadius = CapacityDotSize * 0.5f;
                outer.raycastTarget = false;

                var innerObject = CreateRectObject("Inner", dotRect);
                var innerRect = innerObject.GetComponent<RectTransform>();
                innerRect.anchorMin = new Vector2(0.5f, 0.5f);
                innerRect.anchorMax = new Vector2(0.5f, 0.5f);
                innerRect.pivot = new Vector2(0.5f, 0.5f);
                innerRect.anchoredPosition = Vector2.zero;
                innerRect.sizeDelta = new Vector2(CapacityDotInnerSize, CapacityDotInnerSize);

                var inner = innerObject.AddComponent<RoundedRectGraphic>();
                inner.CornerRadius = CapacityDotInnerSize * 0.5f;
                inner.raycastTarget = false;

                dots[i] = new CapacityDotView(dotRect, outer, inner);
            }

            return dots;
        }

        private void AddPointTrigger(EventTrigger trigger, EventTriggerType type, int pointIndex)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(delegate(BaseEventData eventData)
            {
                HandlePointEvent(type, pointIndex, eventData as PointerEventData);
            });
            trigger.triggers.Add(entry);
        }

        private void AddContentTrigger(EventTrigger trigger, EventTriggerType type)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(delegate(BaseEventData eventData)
            {
                HandleContentEvent(type, eventData as PointerEventData);
            });
            trigger.triggers.Add(entry);
        }

        private void HandlePointEvent(EventTriggerType type, int pointIndex, PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (isMusterSelecting)
            {
                if (type == EventTriggerType.PointerDown || type == EventTriggerType.BeginDrag || type == EventTriggerType.PointerUp)
                {
                    TryApplyMuster(pointIndex);
                }

                return;
            }

            switch (type)
            {
                case EventTriggerType.PointerDown:
                case EventTriggerType.BeginDrag:
                    BeginPlayerDrag(pointIndex);
                    UpdatePlayerDrag(eventData.position, eventData.pressEventCamera);
                    break;
                case EventTriggerType.Drag:
                    UpdatePlayerDrag(eventData.position, eventData.pressEventCamera);
                    break;
                case EventTriggerType.EndDrag:
                case EventTriggerType.PointerUp:
                    EndPlayerDrag(eventData.position, eventData.pressEventCamera);
                    break;
            }
        }

        private void HandleContentEvent(EventTriggerType type, PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            switch (type)
            {
                case EventTriggerType.PointerDown:
                case EventTriggerType.BeginDrag:
                    BeginCutGesture(eventData.position, eventData.pressEventCamera);
                    break;
                case EventTriggerType.Drag:
                    UpdateCutGesture(eventData.position, eventData.pressEventCamera);
                    break;
                case EventTriggerType.EndDrag:
                case EventTriggerType.PointerUp:
                    UpdateCutGesture(eventData.position, eventData.pressEventCamera);
                    EndCutGesture();
                    break;
            }
        }

        private void TickProduction(float deltaTime)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point.Owner == ControlPointOwner.Neutral)
                {
                    point.ProduceTimer = 0f;
                    continue;
                }

                if (CountOutgoingConnections(i) > 0)
                {
                    point.ProduceTimer = 0f;
                    continue;
                }

                point.ProduceTimer += deltaTime;
                while (true)
                {
                    var interval = GetIdleProduceInterval(point.UnitCount);
                    if (point.ProduceTimer < interval)
                    {
                        break;
                    }

                    point.ProduceTimer -= interval;
                    point.UnitCount++;
                }
            }
        }

        private void TickConnections(float deltaTime)
        {
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                if (i >= connections.Count)
                {
                    continue;
                }

                var connection = connections[i];
                if (!IsConnectionStillValid(connection))
                {
                    RemoveConnection(connection);
                    continue;
                }

                RefreshConnectionVisual(connection);
                TickMovingUnits(connection, deltaTime);

                if (!connections.Contains(connection))
                {
                    continue;
                }

                var transferInterval = GetTransferInterval(points[connection.SourceIndex].UnitCount, CountOutgoingConnections(connection.SourceIndex));
                connection.TransferTimer += deltaTime;
                while (connection.TransferTimer >= transferInterval)
                {
                    connection.TransferTimer -= transferInterval;
                    LaunchOneUnit(connection);
                    if (!connections.Contains(connection))
                    {
                        break;
                    }

                    if (!IsConnectionStillValid(connection))
                    {
                        RemoveConnection(connection);
                        break;
                    }
                }
            }
        }

        private void LaunchOneUnit(ControlPointConnection connection)
        {
            var soldier = CreateMovingSoldier(
                connection,
                GetPointPosition(connection.SourceIndex),
                ResolveUnitDestination(connection));
            connection.MovingUnits.Add(soldier);
        }

        private void TickMovingUnits(ControlPointConnection connection, float deltaTime)
        {
            if (!IsContestedConnection(connection))
            {
                ReleaseWaitingUnits(connection);
            }

            for (var i = connection.MovingUnits.Count - 1; i >= 0; i--)
            {
                var soldier = connection.MovingUnits[i];
                if (soldier.WaitingAtFront)
                {
                    continue;
                }

                RedirectMovingSoldier(soldier, ResolveUnitDestination(connection));
                soldier.Elapsed += deltaTime;

                var travelDistance = Mathf.Max(1f, Vector2.Distance(soldier.Start, soldier.End));
                var progress = Mathf.Clamp01((soldier.Elapsed * SoldierTravelSpeed) / travelDistance);
                if (soldier.Root != null)
                {
                    soldier.Root.anchoredPosition = Vector2.Lerp(soldier.Start, soldier.End, progress);
                }

                if (progress < 1f)
                {
                    ResolveOpposingUnitCollision(connection, soldier);
                    continue;
                }

                if (IsContestedConnection(connection))
                {
                    if (!ResolveOpposingUnitCollision(connection, soldier))
                    {
                        DestroyMovingSoldier(soldier);
                        connection.MovingUnits.RemoveAt(i);
                        ApplyIncomingUnit(connection.TargetIndex, connection.Side);
                    }
                    i = Mathf.Min(i, connection.MovingUnits.Count - 1);
                }
                else
                {
                    DestroyMovingSoldier(soldier);
                    connection.MovingUnits.RemoveAt(i);
                    ApplyIncomingUnit(connection.TargetIndex, connection.Side);
                }
            }
        }

        private void TickDetachedMovingUnits(float deltaTime)
        {
            for (var i = detachedMovingUnits.Count - 1; i >= 0; i--)
            {
                var soldier = detachedMovingUnits[i];
                soldier.Elapsed += deltaTime;

                var travelDistance = Mathf.Max(1f, Vector2.Distance(soldier.Start, soldier.End));
                var progress = Mathf.Clamp01((soldier.Elapsed * SoldierTravelSpeed) / travelDistance);
                if (soldier.Root != null)
                {
                    soldier.Root.anchoredPosition = Vector2.Lerp(soldier.Start, soldier.End, progress);
                }

                if (progress < 1f)
                {
                    continue;
                }

                DestroyMovingSoldier(soldier);
                detachedMovingUnits.RemoveAt(i);
                ApplyIncomingUnit(soldier.TargetIndex, soldier.Side);
            }
        }

        private bool ResolveOpposingUnitCollision(ControlPointConnection connection, MovingUnitView soldier)
        {
            var opposing = FindOpposingConnection(connection);
            if (opposing == null)
            {
                return false;
            }

            for (var i = opposing.MovingUnits.Count - 1; i >= 0; i--)
            {
                var opposingSoldier = opposing.MovingUnits[i];
                if (opposingSoldier.WaitingAtFront || !HaveOpposingUnitsMet(connection, soldier, opposingSoldier))
                {
                    continue;
                }

                DestroyMovingSoldier(opposingSoldier);
                opposing.MovingUnits.RemoveAt(i);
                DestroyMovingSoldier(soldier);
                connection.MovingUnits.Remove(soldier);
                return true;
            }

            return false;
        }

        private bool HaveOpposingUnitsMet(ControlPointConnection connection, MovingUnitView soldier, MovingUnitView opposingSoldier)
        {
            if (connection == null || soldier == null || opposingSoldier == null || soldier.Root == null || opposingSoldier.Root == null)
            {
                return false;
            }

            var source = GetPointPosition(connection.SourceIndex);
            var target = GetPointPosition(connection.TargetIndex);
            var route = target - source;
            var lengthSquared = route.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return false;
            }

            var soldierProgress = Mathf.Clamp01(Vector2.Dot(soldier.Root.anchoredPosition - source, route) / lengthSquared);
            var opposingProgress = Mathf.Clamp01(Vector2.Dot(target - opposingSoldier.Root.anchoredPosition, route) / lengthSquared);
            return soldierProgress + opposingProgress >= 1f;
        }

        private void ReleaseWaitingUnits(ControlPointConnection connection)
        {
            for (var i = 0; i < connection.MovingUnits.Count; i++)
            {
                var soldier = connection.MovingUnits[i];
                if (!soldier.WaitingAtFront)
                {
                    continue;
                }

                soldier.WaitingAtFront = false;
                soldier.Start = soldier.Root != null ? soldier.Root.anchoredPosition : ResolveUnitDestination(connection);
                soldier.End = GetPointPosition(connection.TargetIndex);
                soldier.Elapsed = 0f;
            }
        }

        private static void RedirectMovingSoldier(MovingUnitView soldier, Vector2 nextEnd)
        {
            if (soldier == null || (soldier.End - nextEnd).sqrMagnitude <= SoldierDestinationEpsilon * SoldierDestinationEpsilon)
            {
                return;
            }

            soldier.Start = soldier.Root != null ? soldier.Root.anchoredPosition : soldier.Start;
            soldier.End = nextEnd;
            soldier.Elapsed = 0f;
        }

        private void ApplyIncomingUnit(int targetIndex, ControlPointOwner side)
        {
            var target = points[targetIndex];
            if (target.Owner == side)
            {
                target.UnitCount++;
                return;
            }

            target.UnitCount--;
            if (side == ControlPointOwner.Player && IsEnemyOwner(target.Owner))
            {
                defeatedEnemyUnits++;
            }

            if (target.UnitCount <= 0)
            {
                target.Owner = side;
                target.UnitCount = 1;
                target.ProduceTimer = 0f;
                RemoveOutgoingConnection(targetIndex);
            }
        }

        private void TickEnemyAi(float deltaTime)
        {
            for (var i = 0; i < EnemyOwners.Length; i++)
            {
                enemyThinkTimers[i] -= deltaTime;
                if (enemyThinkTimers[i] > 0f)
                {
                    continue;
                }

                enemyThinkTimers[i] = EnemyThinkInterval + (i * 0.15f);

                var owner = EnemyOwners[i];
                var sourceIndex = FindStrongestSource(owner);
                if (sourceIndex < 0)
                {
                    continue;
                }

                if (CountOutgoingConnections(sourceIndex) >= GetConnectionCapacity(points[sourceIndex].UnitCount))
                {
                    continue;
                }

                var targetIndex = SelectEnemyTarget(sourceIndex, owner);
                if (targetIndex >= 0)
                {
                    EstablishConnection(sourceIndex, targetIndex, owner);
                }
            }
        }

        private void TickSkills(float deltaTime)
        {
            if (musterCooldownRemaining <= 0f)
            {
                return;
            }

            musterCooldownRemaining = Mathf.Max(0f, musterCooldownRemaining - deltaTime);
            if (musterCooldownRemaining <= 0f)
            {
                RefreshMusterButton();
            }
        }

        private int FindStrongestSource(ControlPointOwner owner)
        {
            var bestIndex = -1;
            var bestUnits = 1;
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i].Owner != owner || points[i].UnitCount <= bestUnits)
                {
                    continue;
                }

                bestUnits = points[i].UnitCount;
                bestIndex = i;
            }

            return bestIndex;
        }

        private int SelectEnemyTarget(int sourceIndex, ControlPointOwner side)
        {
            var bestIndex = -1;
            var bestScore = float.MinValue;
            for (var i = 0; i < points.Length; i++)
            {
                if (i == sourceIndex || points[i].Owner == side || HasSameConnection(sourceIndex, i, side))
                {
                    continue;
                }

                var score = ScoreEnemyTarget(sourceIndex, i, side);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private float ScoreEnemyTarget(int sourceIndex, int targetIndex, ControlPointOwner side)
        {
            var target = points[targetIndex];
            var distance = Vector2.Distance(GetPointPosition(sourceIndex), GetPointPosition(targetIndex));
            var levelPressure = Mathf.Clamp01(currentLevelIndex / 99f);
            var sourceUnits = points[sourceIndex].UnitCount;
            var unitAdvantage = sourceUnits - target.UnitCount;
            var score = unitAdvantage * 1.4f - distance * 0.18f;

            if (target.Owner == ControlPointOwner.Player)
            {
                if (CountEnemyConnectionsTargetingPlayer() >= GetMaxEnemyPlayerPressureConnections())
                {
                    return float.MinValue;
                }

                return score + 55f + (levelPressure * 145f);
            }

            if (target.Owner == ControlPointOwner.Neutral)
            {
                return score + 80f - (levelPressure * 70f) - (target.UnitCount * 0.8f);
            }

            if (IsEnemyOwner(target.Owner) && target.Owner != side)
            {
                return score + 18f - (levelPressure * 35f);
            }

            return score;
        }

        private int CountEnemyConnectionsTargetingPlayer()
        {
            var count = 0;
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (IsEnemyOwner(connection.Side) && IsValidPointIndex(connection.TargetIndex) && points[connection.TargetIndex].Owner == ControlPointOwner.Player)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetMaxEnemyPlayerPressureConnections()
        {
            if (currentLevelIndex >= 70)
            {
                return 1;
            }

            return currentLevelIndex >= 30 ? 2 : 1;
        }

        private void BeginPlayerDrag(int pointIndex)
        {
            if (isSettled || !IsValidPointIndex(pointIndex) || points[pointIndex].Owner != ControlPointOwner.Player)
            {
                dragSourceIndex = -1;
                HidePreviewLine();
                return;
            }

            dragSourceIndex = pointIndex;
            ShowPreviewLine(GetPointPosition(pointIndex), GetPointPosition(pointIndex));
        }

        private void UpdatePlayerDrag(Vector2 screenPosition, Camera eventCamera)
        {
            if (dragSourceIndex < 0)
            {
                return;
            }

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, screenPosition, eventCamera, out localPoint))
            {
                ShowPreviewLine(GetPointPosition(dragSourceIndex), localPoint);
            }
        }

        private void EndPlayerDrag(Vector2 screenPosition, Camera eventCamera)
        {
            if (dragSourceIndex < 0)
            {
                HidePreviewLine();
                return;
            }

            var targetIndex = FindPointAtScreenPosition(screenPosition, eventCamera);
            if (targetIndex >= 0 && targetIndex != dragSourceIndex)
            {
                EstablishConnection(dragSourceIndex, targetIndex, ControlPointOwner.Player);
                MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.9f);
            }

            dragSourceIndex = -1;
            HidePreviewLine();
        }

        private void BeginCutGesture(Vector2 screenPosition, Camera eventCamera)
        {
            if (isCuttingGesture)
            {
                return;
            }

            if (isSettled || dragSourceIndex >= 0)
            {
                isCuttingGesture = false;
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, screenPosition, eventCamera, out localPoint))
            {
                isCuttingGesture = false;
                return;
            }

            isCuttingGesture = true;
            lastCutLocalPoint = localPoint;
            cutGesturePoints.Clear();
            cutGesturePoints.Add(localPoint);
            ShowCutGestureTrail();
        }

        private void UpdateCutGesture(Vector2 screenPosition, Camera eventCamera)
        {
            if (!isCuttingGesture || dragSourceIndex >= 0)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, screenPosition, eventCamera, out localPoint))
            {
                return;
            }

            cutGesturePoints.Add(localPoint);
            lastCutLocalPoint = localPoint;
            RefreshCutGestureTrail();
        }

        private void EndCutGesture()
        {
            if (isCuttingGesture)
            {
                CutPlayerConnectionsCrossingGesture();
            }

            isCuttingGesture = false;
            HideCutGestureLine();
        }

        private int FindPointAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
        {
            for (var i = 0; i < pointViews.Length; i++)
            {
                if (pointViews[i] != null && RectTransformUtility.RectangleContainsScreenPoint(pointViews[i].Root, screenPosition, eventCamera))
                {
                    return i;
                }
            }

            return -1;
        }

        private void EstablishConnection(int sourceIndex, int targetIndex, ControlPointOwner side)
        {
            if (!CanCreateConnection(sourceIndex, targetIndex, side))
            {
                return;
            }

            if (HasSameConnection(sourceIndex, targetIndex, side))
            {
                return;
            }

            if (CountOutgoingConnections(sourceIndex) >= GetConnectionCapacity(points[sourceIndex].UnitCount))
            {
                return;
            }

            var visual = CreateConnectionVisual("Connection_" + sourceIndex + "_" + targetIndex, GetLineColor(side));
            var connection = new ControlPointConnection(sourceIndex, targetIndex, side, visual.Line);
            connections.Add(connection);
            RefreshConnectionVisual(connection);

            var opposing = FindOpposingConnection(connection);
            if (opposing != null)
            {
                RefreshConnectionVisual(opposing);
            }
        }

        private bool HasSameConnection(int sourceIndex, int targetIndex, ControlPointOwner side)
        {
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (connection.SourceIndex == sourceIndex &&
                    connection.TargetIndex == targetIndex &&
                    connection.Side == side)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountOutgoingConnections(int sourceIndex)
        {
            var count = 0;
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].SourceIndex == sourceIndex)
                {
                    count++;
                }
            }

            return count;
        }

        private bool CanCreateConnection(int sourceIndex, int targetIndex, ControlPointOwner side)
        {
            return side != ControlPointOwner.Neutral &&
                IsValidPointIndex(sourceIndex) &&
                IsValidPointIndex(targetIndex) &&
                sourceIndex != targetIndex &&
                points[sourceIndex].Owner == side &&
                IsConnectionPathClear(sourceIndex, targetIndex);
        }

        private bool IsConnectionPathClear(int sourceIndex, int targetIndex)
        {
            var sourcePosition = GetPointPosition(sourceIndex);
            var targetPosition = GetPointPosition(targetIndex);
            for (var i = 0; i < points.Length; i++)
            {
                if (i == sourceIndex || i == targetIndex)
                {
                    continue;
                }

                var pointRadius = GetPointSize(points[i].UnitCount) * 0.5f;
                var pointPosition = GetPointPosition(i);
                if (DistancePointToSegmentSquared(pointPosition, sourcePosition, targetPosition) <= pointRadius * pointRadius)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsConnectionStillValid(ControlPointConnection connection)
        {
            return connection != null &&
                CanCreateConnection(connection.SourceIndex, connection.TargetIndex, connection.Side);
        }

        private bool IsContestedConnection(ControlPointConnection connection)
        {
            return FindOpposingConnection(connection) != null;
        }

        private ControlPointConnection FindOpposingConnection(ControlPointConnection connection)
        {
            if (connection == null)
            {
                return null;
            }

            for (var i = 0; i < connections.Count; i++)
            {
                var candidate = connections[i];
                if (candidate == connection)
                {
                    continue;
                }

                if (candidate.SourceIndex == connection.TargetIndex &&
                    candidate.TargetIndex == connection.SourceIndex &&
                    candidate.Side != connection.Side &&
                    IsConnectionStillValid(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private Vector2 ResolveUnitDestination(ControlPointConnection connection)
        {
            return GetPointPosition(connection.TargetIndex);
        }

        private void RefreshConnectionVisual(ControlPointConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            var start = GetPointPosition(connection.SourceIndex);
            var end = GetPointPosition(connection.TargetIndex);
            if (IsContestedConnection(connection))
            {
                PositionLine(connection.Line, start, (start + end) * 0.5f, GetLineEndpointInset(connection.SourceIndex), 0f);
                return;
            }

            PositionLine(connection.Line, start, end, GetLineEndpointInset(connection.SourceIndex), GetLineEndpointInset(connection.TargetIndex));
        }

        private void CutPlayerConnectionsCrossingSegment(Vector2 cutStart, Vector2 cutEnd)
        {
            if ((cutEnd - cutStart).sqrMagnitude <= 0.01f)
            {
                return;
            }

            var removedAny = false;
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                var connection = connections[i];
                if (connection.Side != ControlPointOwner.Player)
                {
                    continue;
                }

                Vector2 lineStart;
                Vector2 lineEnd;
                GetConnectionLineSegment(connection, out lineStart, out lineEnd);
                if (!SegmentsAreNear(cutStart, cutEnd, lineStart, lineEnd, (LineThickness * 0.5f) + CutLinePadding))
                {
                    continue;
                }

                RemoveConnectionAt(i, true);
                removedAny = true;
            }

            if (!removedAny)
            {
                return;
            }

            for (var i = 0; i < connections.Count; i++)
            {
                RefreshConnectionVisual(connections[i]);
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.85f);
        }

        private void CutPlayerConnectionsCrossingGesture()
        {
            for (var i = 1; i < cutGesturePoints.Count; i++)
            {
                CutPlayerConnectionsCrossingSegment(cutGesturePoints[i - 1], cutGesturePoints[i]);
            }
        }

        private void GetConnectionLineSegment(ControlPointConnection connection, out Vector2 start, out Vector2 end)
        {
            start = GetPointPosition(connection.SourceIndex);
            end = GetPointPosition(connection.TargetIndex);
            if (IsContestedConnection(connection))
            {
                end = (start + end) * 0.5f;
                ApplyLineInset(ref start, ref end, GetLineEndpointInset(connection.SourceIndex), 0f);
                return;
            }

            ApplyLineInset(ref start, ref end, GetLineEndpointInset(connection.SourceIndex), GetLineEndpointInset(connection.TargetIndex));
        }

        private static void ApplyLineInset(ref Vector2 start, ref Vector2 end, float startInset, float endInset)
        {
            var delta = end - start;
            var length = delta.magnitude;
            var inset = Mathf.Max(0f, startInset) + Mathf.Max(0f, endInset);
            if (length <= inset)
            {
                return;
            }

            var direction = delta / length;
            if (startInset > 0f)
            {
                start += direction * startInset;
            }

            if (endInset > 0f)
            {
                end -= direction * endInset;
            }
        }

        private void RemoveOutgoingConnection(int sourceIndex)
        {
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i].SourceIndex == sourceIndex)
                {
                    RemoveConnectionAt(i);
                }
            }
        }

        private void RemoveConnectionAt(int index, bool keepMovingUnits = false)
        {
            if (index < 0 || index >= connections.Count)
            {
                return;
            }

            DestroyConnectionVisual(connections[index], keepMovingUnits);
            connections.RemoveAt(index);
        }

        private void RemoveConnection(ControlPointConnection connection, bool keepMovingUnits = false)
        {
            if (connection == null)
            {
                return;
            }

            var index = connections.IndexOf(connection);
            if (index < 0)
            {
                return;
            }

            RemoveConnectionAt(index, keepMovingUnits);
        }

        private void ClearConnections()
        {
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                DestroyConnectionVisual(connections[i], false);
            }

            connections.Clear();
        }

        private ConnectionVisual CreateConnectionVisual(string name, Color color)
        {
            var rect = CreatePlainLine(name, color);
            rect.transform.SetAsFirstSibling();

            var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(DirectionTriangleGraphic));
            arrowObject.transform.SetParent(rect, false);
            var arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(ArrowHeight, ArrowWidth);
            arrowRect.anchoredPosition = new Vector2(-ArrowWidth * 0.18f, 0f);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, 270f);

            var arrowGraphic = arrowObject.GetComponent<DirectionTriangleGraphic>();
            arrowGraphic.color = color;
            arrowGraphic.raycastTarget = false;

            return new ConnectionVisual(rect);
        }

        private RectTransform CreatePlainLine(string name, Color color)
        {
            var lineObject = CreateRectObject(name, lineLayer);
            var rect = lineObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);

            var lineGraphic = lineObject.AddComponent<RoundedRectGraphic>();
            lineGraphic.color = color;
            lineGraphic.CornerRadius = LineThickness * 0.5f;
            lineGraphic.raycastTarget = false;
            return rect;
        }

        private void ShowPreviewLine(Vector2 start, Vector2 end)
        {
            if (previewLine == null)
            {
                previewLine = CreateConnectionVisual("PreviewConnection", PreviewLineColor).Line;
            }

            previewLine.gameObject.SetActive(true);
            var startInset = dragSourceIndex >= 0 ? GetLineEndpointInset(dragSourceIndex) : MaxLineEndpointInset;
            PositionLine(previewLine, start, end, startInset, 0f);
        }

        private void HidePreviewLine()
        {
            if (previewLine != null)
            {
                UnityEngine.Object.Destroy(previewLine.gameObject);
                previewLine = null;
            }
        }

        private void ShowCutGestureTrail()
        {
            if (cutGestureTrail == null)
            {
                var trailObject = CreateRectObject("CutGestureLine", lineLayer);
                var rect = trailObject.GetComponent<RectTransform>();
                Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                cutGestureTrail = trailObject.AddComponent<CutGestureTrailGraphic>();
                cutGestureTrail.color = CutGestureLineColor;
                cutGestureTrail.raycastTarget = false;
                cutGestureTrail.Thickness = CutTrailThickness;
                cutGestureTrail.MinPointDistance = CutTrailMinPointDistance;
            }

            cutGestureTrail.gameObject.SetActive(true);
            cutGestureTrail.transform.SetAsLastSibling();
            RefreshCutGestureTrail();
        }

        private void RefreshCutGestureTrail()
        {
            if (cutGestureTrail != null)
            {
                cutGestureTrail.SetPoints(cutGesturePoints);
            }
        }

        private void HideCutGestureLine()
        {
            if (cutGestureTrail != null)
            {
                UnityEngine.Object.Destroy(cutGestureTrail.gameObject);
                cutGestureTrail = null;
            }

            cutGesturePoints.Clear();
        }

        private static void PositionLine(RectTransform line, Vector2 start, Vector2 end, float startInset, float endInset)
        {
            if (line == null)
            {
                return;
            }

            var delta = end - start;
            var length = delta.magnitude;
            var inset = Mathf.Max(0f, startInset) + Mathf.Max(0f, endInset);
            if (length > inset)
            {
                var direction = delta / length;
                if (startInset > 0f)
                {
                    start += direction * startInset;
                }

                length -= inset;
            }

            line.anchoredPosition = start;
            line.sizeDelta = new Vector2(Mathf.Max(1f, length), LineThickness);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static bool SegmentsAreNear(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd, float maxDistance)
        {
            if (SegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd))
            {
                return true;
            }

            var maxDistanceSquared = maxDistance * maxDistance;
            return DistancePointToSegmentSquared(firstStart, secondStart, secondEnd) <= maxDistanceSquared ||
                DistancePointToSegmentSquared(firstEnd, secondStart, secondEnd) <= maxDistanceSquared ||
                DistancePointToSegmentSquared(secondStart, firstStart, firstEnd) <= maxDistanceSquared ||
                DistancePointToSegmentSquared(secondEnd, firstStart, firstEnd) <= maxDistanceSquared;
        }

        private static bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
        {
            var firstDirection = firstEnd - firstStart;
            var secondDirection = secondEnd - secondStart;
            var denominator = Cross(firstDirection, secondDirection);
            var difference = secondStart - firstStart;

            if (Mathf.Abs(denominator) <= 0.0001f)
            {
                return Mathf.Abs(Cross(difference, firstDirection)) <= 0.0001f &&
                    RangesOverlap(firstStart.x, firstEnd.x, secondStart.x, secondEnd.x) &&
                    RangesOverlap(firstStart.y, firstEnd.y, secondStart.y, secondEnd.y);
            }

            var firstAmount = Cross(difference, secondDirection) / denominator;
            var secondAmount = Cross(difference, firstDirection) / denominator;
            return firstAmount >= 0f && firstAmount <= 1f && secondAmount >= 0f && secondAmount <= 1f;
        }

        private static float DistancePointToSegmentSquared(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            var segment = segmentEnd - segmentStart;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return (point - segmentStart).sqrMagnitude;
            }

            var amount = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
            var projection = segmentStart + (segment * amount);
            return (point - projection).sqrMagnitude;
        }

        private static bool RangesOverlap(float firstStart, float firstEnd, float secondStart, float secondEnd)
        {
            return Mathf.Max(Mathf.Min(firstStart, firstEnd), Mathf.Min(secondStart, secondEnd)) <=
                Mathf.Min(Mathf.Max(firstStart, firstEnd), Mathf.Max(secondStart, secondEnd));
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return (first.x * second.y) - (first.y * second.x);
        }

        private MovingUnitView CreateMovingSoldier(ControlPointConnection connection, Vector2 start, Vector2 end)
        {
            var soldierObject = CreateRectObject("Soldier_" + connection.SourceIndex + "_" + connection.TargetIndex, lineLayer);
            soldierObject.transform.SetAsLastSibling();
            var rect = soldierObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(SoldierSize, SoldierSize);
            rect.anchoredPosition = start;

            var graphic = soldierObject.AddComponent<RoundedRectGraphic>();
            graphic.color = GetOwnerColor(connection.Side);
            graphic.CornerRadius = SoldierSize * 0.5f;
            graphic.raycastTarget = false;

            return new MovingUnitView(rect, start, end, connection.TargetIndex, connection.Side);
        }

        private static void DestroyMovingSoldier(MovingUnitView soldier)
        {
            if (soldier != null && soldier.Root != null)
            {
                UnityEngine.Object.Destroy(soldier.Root.gameObject);
            }
        }

        private void DestroyConnectionVisual(ControlPointConnection connection, bool keepMovingUnits)
        {
            if (connection == null)
            {
                return;
            }

            if (keepMovingUnits)
            {
                DetachMovingUnits(connection);
            }
            else
            {
                for (var i = connection.MovingUnits.Count - 1; i >= 0; i--)
                {
                    DestroyMovingSoldier(connection.MovingUnits[i]);
                }

                connection.MovingUnits.Clear();
            }

            DestroyLine(connection.Line);
        }

        private void DetachMovingUnits(ControlPointConnection connection)
        {
            for (var i = 0; i < connection.MovingUnits.Count; i++)
            {
                var soldier = connection.MovingUnits[i];
                soldier.WaitingAtFront = false;
                soldier.Start = soldier.Root != null ? soldier.Root.anchoredPosition : soldier.Start;
                soldier.End = GetPointPosition(connection.TargetIndex);
                soldier.Elapsed = 0f;
                detachedMovingUnits.Add(soldier);
            }

            connection.MovingUnits.Clear();
        }

        private void ClearDetachedMovingUnits()
        {
            for (var i = detachedMovingUnits.Count - 1; i >= 0; i--)
            {
                DestroyMovingSoldier(detachedMovingUnits[i]);
            }

            detachedMovingUnits.Clear();
        }

        private static void DestroyLine(RectTransform line)
        {
            if (line != null)
            {
                UnityEngine.Object.Destroy(line.gameObject);
            }
        }

        private void CheckRoundEnd()
        {
            if (isSettled)
            {
                return;
            }

            var playerOwned = CountOwned(ControlPointOwner.Player);
            if (playerOwned == points.Length)
            {
                Settle(ControlPointRoundResult.PlayerWin);
            }
            else if (AnyEnemyControlsAllPoints())
            {
                Settle(ControlPointRoundResult.EnemyWin);
            }
        }

        private void Settle(ControlPointRoundResult result)
        {
            if (isSettled)
            {
                return;
            }

            roundResult = result;
            isSettled = true;
            MiniGameSfxPlayer.Play(result == ControlPointRoundResult.PlayerWin ? MiniGameSfxType.MatchSuccess : MiniGameSfxType.MatchFail, 0.9f);
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = BuildSettlement();
            if (result == ControlPointRoundResult.PlayerWin)
            {
                EnsureLevelProgress();
                levelProgress.UnlockNext();
                ShowWinSettlement(settlement);
                return;
            }

            ShowSettlementAndComplete(settlement);
        }

        private MiniGameSettlement BuildSettlement()
        {
            var playerOwned = CountOwned(ControlPointOwner.Player);
            var playerUnits = CountUnits(ControlPointOwner.Player);
            if (roundResult == ControlPointRoundResult.PlayerWin)
            {
                var levelBonus = currentLevelIndex * 20;
                var score = (playerOwned * 100) + (playerUnits * 4) + (defeatedEnemyUnits * 10) + (currentLevelIndex * 50);
                var coinCount = 80 + levelBonus + (defeatedEnemyUnits * 3);
                return new MiniGameSettlement
                {
                    Score = score,
                    CoinCount = coinCount,
                    ChestCount = 1,
                    Summary = UiTextCatalog.Format("control_point.settlement.win", currentLevelIndex + 1, playerOwned, playerUnits, defeatedEnemyUnits, coinCount, 1)
                };
            }

            if (roundResult == ControlPointRoundResult.EnemyWin)
            {
                var coinCount = 12 + (playerOwned * 8) + (defeatedEnemyUnits * 2);
                return new MiniGameSettlement
                {
                    Score = (playerOwned * 60) + (playerUnits * 2) + (defeatedEnemyUnits * 6),
                    CoinCount = coinCount,
                    ChestCount = 0,
                    Summary = UiTextCatalog.Format("control_point.settlement.lose", playerOwned, defeatedEnemyUnits, coinCount)
                };
            }

            var exitCoins = (playerOwned * 10) + (defeatedEnemyUnits * 2);
            return new MiniGameSettlement
            {
                Score = (playerOwned * 50) + (playerUnits * 2) + (defeatedEnemyUnits * 5),
                CoinCount = exitCoins,
                ChestCount = 0,
                Summary = UiTextCatalog.Format("control_point.settlement.exit", playerOwned, defeatedEnemyUnits, exitCoins)
            };
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.control_point.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format(
                    "control_point.hud.score",
                    CountOwned(ControlPointOwner.Player),
                    points.Length,
                    CountEnemyOwned());
            }

            RefreshMusterButton();
        }

        private void RefreshPointViews()
        {
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var view = pointViews[i];
                if (view == null)
                {
                    continue;
                }

                view.Background.color = GetOwnerColor(point.Owner);
                var pointSize = GetPointSize(point.UnitCount);
                view.Root.sizeDelta = new Vector2(pointSize, pointSize);
                view.Background.CornerRadius = pointSize * 0.5f;
                view.UnitLabel.fontSize = pointSize >= LevelThreePointSize ? 48f : pointSize >= LevelTwoPointSize ? 44f : 40f;
                view.UnitLabel.text = point.UnitCount.ToString();
                view.LevelLabel.text = "Lv" + GetPointLevel(point.UnitCount);
                RefreshCapacityDots(view, point, i, pointSize);
            }
        }

        private void RefreshCapacityDots(ControlPointViewRefs view, ControlPointState point, int pointIndex, float pointSize)
        {
            if (view == null || view.CapacityDots == null || point == null)
            {
                return;
            }

            var capacity = GetConnectionCapacity(point.UnitCount);
            var connectedCount = CountOutgoingConnections(pointIndex);
            var startX = -((capacity - 1) * CapacityDotSpacing * 0.5f);
            var dotY = -(pointSize * 0.32f);
            var ownerColor = GetOwnerColor(point.Owner);
            for (var i = 0; i < view.CapacityDots.Length; i++)
            {
                var dot = view.CapacityDots[i];
                if (dot == null || dot.Root == null)
                {
                    continue;
                }

                var isVisible = i < capacity;
                dot.Root.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                dot.Root.anchoredPosition = new Vector2(startX + (i * CapacityDotSpacing), dotY);
                dot.Outer.color = TextColor;
                dot.Inner.color = i < connectedCount ? TextColor : ownerColor;
                dot.Inner.CornerRadius = CapacityDotInnerSize * 0.5f;
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            if (isSettled)
            {
                return;
            }

            roundResult = ControlPointRoundResult.Exit;
            isSettled = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = BuildSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "ControlPointSettlementPanel",
                MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("control_point.settlement.owned"), CountOwned(ControlPointOwner.Player) + "/" + points.Length),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
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
                "ControlPointLevelSelectPanel",
                "ControlPointLevelButton_",
                SelectLevel,
                CloseLevelSelectView);
        }

        private void OnMusterClicked()
        {
            if (isSettled || musterCooldownRemaining > 0f)
            {
                return;
            }

            isMusterSelecting = !isMusterSelecting;
            dragSourceIndex = -1;
            HidePreviewLine();
            RefreshMusterButton();
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
        }

        private bool TryApplyMuster(int pointIndex)
        {
            if (isSettled || musterCooldownRemaining > 0f || !IsValidPointIndex(pointIndex))
            {
                return false;
            }

            var point = points[pointIndex];
            if (point.Owner != ControlPointOwner.Player)
            {
                return false;
            }

            point.UnitCount += MusterUnitGain;
            point.ProduceTimer = 0f;
            isMusterSelecting = false;
            musterCooldownRemaining = MusterCooldownSeconds;
            RefreshMusterButton();
            RefreshPointViews();
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.9f);
            return true;
        }

        private void RefreshMusterButton()
        {
            if (musterButton == null)
            {
                return;
            }

            musterButton.interactable = !isSettled && musterCooldownRemaining <= 0f;
            if (musterButtonLabel == null)
            {
                return;
            }

            if (musterCooldownRemaining > 0f)
            {
                musterButtonLabel.text = UiTextCatalog.Format("control_point.skill.muster.cooldown", Mathf.CeilToInt(musterCooldownRemaining));
                return;
            }

            musterButtonLabel.text = UiTextCatalog.Get(isMusterSelecting ? "control_point.skill.muster.selecting" : "control_point.skill.muster");
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

            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "ControlPointSettlementPanel",
                    Title = UiTextCatalog.Get("control_point.settlement.title"),
                    PrimaryInfo = MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("control_point.settlement.owned"), CountOwned(ControlPointOwner.Player) + "/" + points.Length),
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

        private void ApplyLevel(ControlPointLevelDefinition level)
        {
            if (level == null)
            {
                return;
            }

            ClearPointViews();
            points = new ControlPointState[level.Points.Length];
            pointViews = new ControlPointViewRefs[level.Points.Length];

            for (var i = 0; i < points.Length; i++)
            {
                var setup = level.Points[i];
                points[i] = new ControlPointState(setup.Owner, setup.UnitCount);
                if (pointLayer != null)
                {
                    pointViews[i] = CreatePointView(i, pointLayer, level.Positions[i]);
                }
            }
        }

        private void ClearPointViews()
        {
            for (var i = 0; i < pointViews.Length; i++)
            {
                if (pointViews[i] != null && pointViews[i].Root != null)
                {
                    UnityEngine.Object.Destroy(pointViews[i].Root.gameObject);
                }
            }

            pointViews = new ControlPointViewRefs[0];
        }

        private int CountOwned(ControlPointOwner owner)
        {
            var count = 0;
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i].Owner == owner)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountUnits(ControlPointOwner owner)
        {
            var count = 0;
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i].Owner == owner)
                {
                    count += points[i].UnitCount;
                }
            }

            return count;
        }

        private int CountEnemyOwned()
        {
            var count = 0;
            for (var i = 0; i < points.Length; i++)
            {
                if (IsEnemyOwner(points[i].Owner))
                {
                    count++;
                }
            }

            return count;
        }

        private bool AnyEnemyControlsAllPoints()
        {
            for (var i = 0; i < EnemyOwners.Length; i++)
            {
                if (CountOwned(EnemyOwners[i]) == points.Length)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 GetPointPosition(int pointIndex)
        {
            if (!IsValidPointIndex(pointIndex) || pointViews[pointIndex] == null)
            {
                return Vector2.zero;
            }

            return pointViews[pointIndex].Root.anchoredPosition;
        }

        private bool IsValidPointIndex(int pointIndex)
        {
            return pointIndex >= 0 && pointIndex < points.Length;
        }

        private static int GetPointLevel(int unitCount)
        {
            if (unitCount >= 40)
            {
                return 3;
            }

            return unitCount >= 20 ? 2 : 1;
        }

        private static float GetPointSize(int unitCount)
        {
            switch (GetPointLevel(unitCount))
            {
                case 3:
                    return LevelThreePointSize;
                case 2:
                    return LevelTwoPointSize;
                default:
                    return LevelOnePointSize;
            }
        }

        private float GetLineEndpointInset(int pointIndex)
        {
            if (!IsValidPointIndex(pointIndex))
            {
                return MaxLineEndpointInset;
            }

            return Mathf.Min(MaxLineEndpointInset, Mathf.Max(0f, (GetPointSize(points[pointIndex].UnitCount) * 0.5f) - 8f));
        }

        private static int GetConnectionCapacity(int unitCount)
        {
            return GetPointLevel(unitCount);
        }

        private static float GetIdleProduceInterval(int unitCount)
        {
            switch (GetPointLevel(unitCount))
            {
                case 3:
                    return LevelThreeIdleProduceInterval;
                case 2:
                    return LevelTwoIdleProduceInterval;
                default:
                    return LevelOneIdleProduceInterval;
            }
        }

        private static float GetTransferInterval(int unitCount, int outgoingConnectionCount)
        {
            switch (GetPointLevel(unitCount))
            {
                case 3:
                    if (outgoingConnectionCount >= 3)
                    {
                        return LevelThreeThreeLineTransferInterval;
                    }

                    return outgoingConnectionCount >= 2
                        ? LevelThreeTwoLineTransferInterval
                        : LevelThreeOneLineTransferInterval;
                case 2:
                    return outgoingConnectionCount >= 2
                        ? LevelTwoTwoLineTransferInterval
                        : LevelTwoOneLineTransferInterval;
                default:
                    return LevelOneOneLineTransferInterval;
            }
        }

        private static Color GetOwnerColor(ControlPointOwner owner)
        {
            switch (owner)
            {
                case ControlPointOwner.Player:
                    return PlayerColor;
                case ControlPointOwner.Enemy:
                    return EnemyColor;
                case ControlPointOwner.EnemyTwo:
                    return EnemyTwoColor;
                case ControlPointOwner.EnemyThree:
                    return EnemyThreeColor;
                default:
                    return NeutralColor;
            }
        }

        private static Color GetLineColor(ControlPointOwner owner)
        {
            switch (owner)
            {
                case ControlPointOwner.Player:
                    return PlayerLineColor;
                case ControlPointOwner.EnemyTwo:
                    return EnemyTwoLineColor;
                case ControlPointOwner.EnemyThree:
                    return EnemyThreeLineColor;
                default:
                    return EnemyLineColor;
            }
        }

        private static bool IsEnemyOwner(ControlPointOwner owner)
        {
            return owner == ControlPointOwner.Enemy ||
                owner == ControlPointOwner.EnemyTwo ||
                owner == ControlPointOwner.EnemyThree;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle)
        {
            var textObject = CreateRectObject(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, fontSize, fontStyle, TextAlignmentOptions.Center);
            text.color = TextColor;
            return text;
        }

        private static void ConfigureText(TextMeshProUGUI text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            text.font = MiniGameFontProvider.DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class ControlPointState
        {
            public ControlPointState(ControlPointOwner owner, int unitCount)
            {
                Owner = owner;
                UnitCount = unitCount;
            }

            public ControlPointOwner Owner;
            public int UnitCount;
            public float ProduceTimer;
        }

        private sealed class ControlPointLevelDefinition
        {
            public ControlPointLevelDefinition(ControlPointPointSetup[] points, Vector2[] positions)
            {
                if (points == null || positions == null || points.Length != positions.Length)
                {
                    throw new ArgumentException("Control point level must define matching points and positions.", nameof(points));
                }

                Points = points;
                Positions = positions;
            }

            public readonly ControlPointPointSetup[] Points;
            public readonly Vector2[] Positions;
        }

        private sealed class ControlPointPointSetup
        {
            public ControlPointPointSetup(ControlPointOwner owner, int unitCount)
            {
                Owner = owner;
                UnitCount = Mathf.Max(1, unitCount);
            }

            public readonly ControlPointOwner Owner;
            public readonly int UnitCount;
        }

        private sealed class ControlPointViewRefs
        {
            public ControlPointViewRefs(
                RectTransform root,
                RoundedRectGraphic background,
                TextMeshProUGUI unitLabel,
                TextMeshProUGUI levelLabel,
                CapacityDotView[] capacityDots)
            {
                Root = root;
                Background = background;
                UnitLabel = unitLabel;
                LevelLabel = levelLabel;
                CapacityDots = capacityDots;
            }

            public RectTransform Root { get; }
            public RoundedRectGraphic Background { get; }
            public TextMeshProUGUI UnitLabel { get; }
            public TextMeshProUGUI LevelLabel { get; }
            public CapacityDotView[] CapacityDots { get; }
        }

        private sealed class CapacityDotView
        {
            public CapacityDotView(RectTransform root, RoundedRectGraphic outer, RoundedRectGraphic inner)
            {
                Root = root;
                Outer = outer;
                Inner = inner;
            }

            public RectTransform Root { get; }
            public RoundedRectGraphic Outer { get; }
            public RoundedRectGraphic Inner { get; }
        }

        private sealed class ControlPointConnection
        {
            public ControlPointConnection(int sourceIndex, int targetIndex, ControlPointOwner side, RectTransform line)
            {
                SourceIndex = sourceIndex;
                TargetIndex = targetIndex;
                Side = side;
                Line = line;
            }

            public readonly int SourceIndex;
            public readonly int TargetIndex;
            public readonly ControlPointOwner Side;
            public readonly RectTransform Line;
            public readonly List<MovingUnitView> MovingUnits = new List<MovingUnitView>();
            public float TransferTimer;
        }

        private sealed class ConnectionVisual
        {
            public ConnectionVisual(RectTransform line)
            {
                Line = line;
            }

            public RectTransform Line { get; }
        }

        private sealed class MovingUnitView
        {
            public MovingUnitView(RectTransform root, Vector2 start, Vector2 end, int targetIndex, ControlPointOwner side)
            {
                Root = root;
                Start = start;
                End = end;
                TargetIndex = targetIndex;
                Side = side;
            }

            public readonly RectTransform Root;
            public readonly int TargetIndex;
            public readonly ControlPointOwner Side;
            public Vector2 Start;
            public Vector2 End;
            public float Elapsed;
            public bool WaitingAtFront;
        }

        private sealed class CutGestureTrailGraphic : MaskableGraphic
        {
            [SerializeField] private float thickness = 28f;
            [SerializeField] private float minPointDistance = 10f;

            private readonly List<Vector2> trailPoints = new List<Vector2>();

            public float Thickness
            {
                get { return thickness; }
                set
                {
                    thickness = Mathf.Max(1f, value);
                    SetVerticesDirty();
                }
            }

            public float MinPointDistance
            {
                get { return minPointDistance; }
                set
                {
                    minPointDistance = Mathf.Max(0f, value);
                    SetVerticesDirty();
                }
            }

            public void SetPoints(IList<Vector2> points)
            {
                trailPoints.Clear();
                if (points != null)
                {
                    var minDistanceSquared = minPointDistance * minPointDistance;
                    for (var i = 0; i < points.Count; i++)
                    {
                        var point = points[i];
                        if (trailPoints.Count == 0 ||
                            (point - trailPoints[trailPoints.Count - 1]).sqrMagnitude >= minDistanceSquared ||
                            i == points.Count - 1)
                        {
                            trailPoints.Add(point);
                        }
                    }
                }

                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                if (trailPoints.Count < 2)
                {
                    return;
                }

                for (var i = 0; i < trailPoints.Count; i++)
                {
                    var tangent = GetTangent(i);
                    var normal = new Vector2(-tangent.y, tangent.x);
                    var amount = trailPoints.Count <= 1 ? 1f : i / (float)(trailPoints.Count - 1);
                    var width = thickness * GetWidthScale(amount, trailPoints.Count) * 0.5f;
                    var vertexColor = color;
                    vertexColor.a *= Mathf.Lerp(0.16f, 1f, amount);

                    AddVertex(vh, trailPoints[i] + (normal * width), vertexColor);
                    AddVertex(vh, trailPoints[i] - (normal * width), vertexColor);
                }

                for (var i = 0; i < trailPoints.Count - 1; i++)
                {
                    var left = i * 2;
                    var right = left + 1;
                    var nextLeft = left + 2;
                    var nextRight = left + 3;
                    vh.AddTriangle(left, right, nextLeft);
                    vh.AddTriangle(right, nextRight, nextLeft);
                }
            }

            private Vector2 GetTangent(int index)
            {
                Vector2 tangent;
                if (index <= 0)
                {
                    tangent = trailPoints[1] - trailPoints[0];
                }
                else if (index >= trailPoints.Count - 1)
                {
                    tangent = trailPoints[index] - trailPoints[index - 1];
                }
                else
                {
                    tangent = trailPoints[index + 1] - trailPoints[index - 1];
                }

                return tangent.sqrMagnitude <= 0.0001f ? Vector2.right : tangent.normalized;
            }

            private static float GetWidthScale(float amount, int pointCount)
            {
                if (pointCount <= 2)
                {
                    return 1f;
                }

                return Mathf.Max(0.34f, Mathf.Sin(amount * Mathf.PI));
            }

            private static void AddVertex(VertexHelper vh, Vector2 position, Color vertexColor)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = vertexColor;
                vh.AddVert(vertex);
            }
        }

        private enum ControlPointOwner
        {
            Neutral,
            Player,
            Enemy,
            EnemyTwo,
            EnemyThree
        }

        private enum ControlPointRoundResult
        {
            None,
            PlayerWin,
            EnemyWin,
            Exit
        }
    }
}
