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
        private const float MinPointDistance = 128f;
        private const float MinPointX = -285f;
        private const float MaxPointX = 285f;
        private const float MinPointY = -305f;
        private const float MaxPointY = 245f;
        private const float PointSize = 132f;
        private const float LevelOneProduceInterval = 1f;
        private const float LevelTwoProduceInterval = 0.8f;
        private const float LevelThreeProduceInterval = 0.6f;
        private const float TransferInterval = 0.55f;
        private const float EnemyThinkInterval = 1.2f;
        private const float LineThickness = 18f;
        private const float LineEndpointInset = 58f;
        private const float ArrowWidth = 34f;
        private const float ArrowHeight = 44f;
        private const float SoldierSize = 30f;
        private const float SoldierTravelSpeed = 318f;
        private const float SoldierDestinationEpsilon = 0.5f;
        private const float CutLinePadding = 20f;

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
        private readonly List<RectTransform> cutGestureLines = new List<RectTransform>();
        private readonly float[] enemyThinkTimers = new float[EnemyOwners.Length];

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private Button levelSelectButton;
        private RectTransform contentRect;
        private RectTransform lineLayer;
        private RectTransform pointLayer;
        private RectTransform previewLine;
        private MiniGameLevelProgressController levelProgress;
        private MiniGameLevelSelectView levelSelectView;
        private MiniGameWinSettlementView winSettlementView;
        private MiniGameSettlement activeWinSettlement;
        private int currentLevelIndex;
        private int dragSourceIndex = -1;
        private bool isCuttingGesture;
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
            if (winSettlementView != null)
            {
                winSettlementView.Tick(deltaTime);
            }

            if (isSettled)
            {
                return;
            }

            var clampedDelta = Mathf.Max(0f, deltaTime);
            TickProduction(clampedDelta);
            TickConnections(clampedDelta);
            TickDetachedMovingUnits(clampedDelta);
            TickEnemyAi(clampedDelta);
            RefreshHud();
            RefreshPointViews();
            CheckRoundEnd();
        }

        protected override void BuildOrBindSections()
        {
            Shell.SetPauseButtonVisible(true);

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
            CloseWinSettlementView();
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
            CloseWinSettlementView();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.control_point.help", "game.control_point.credits");
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

        }

        private ControlPointViewRefs CreatePointView(int index, Transform parent, Vector2 position)
        {
            var root = CreateRectObject("ControlPoint_" + index, parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PointSize, PointSize);
            rect.anchoredPosition = position;

            var graphic = root.AddComponent<RoundedRectGraphic>();
            graphic.color = NeutralColor;
            graphic.CornerRadius = PointSize * 0.5f;

            var label = CreateText("Units", rect, 48f, FontStyles.Bold);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var levelLabel = CreateText("Level", rect, 18f, FontStyles.Bold);
            var levelRect = levelLabel.rectTransform;
            levelRect.anchorMin = new Vector2(0.5f, 1f);
            levelRect.anchorMax = new Vector2(0.5f, 1f);
            levelRect.pivot = new Vector2(0.5f, 1f);
            levelRect.anchoredPosition = new Vector2(0f, -16f);
            levelRect.sizeDelta = new Vector2(74f, 24f);

            var trigger = root.AddComponent<EventTrigger>();
            AddPointTrigger(trigger, EventTriggerType.PointerDown, index);
            AddPointTrigger(trigger, EventTriggerType.BeginDrag, index);
            AddPointTrigger(trigger, EventTriggerType.Drag, index);
            AddPointTrigger(trigger, EventTriggerType.EndDrag, index);
            AddPointTrigger(trigger, EventTriggerType.PointerUp, index);

            return new ControlPointViewRefs(rect, graphic, label, levelLabel);
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

                point.ProduceTimer += deltaTime;
                while (true)
                {
                    var interval = GetProduceInterval(point.UnitCount);
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

                connection.TransferTimer += deltaTime;
                while (connection.TransferTimer >= TransferInterval)
                {
                    connection.TransferTimer -= TransferInterval;
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
            var source = points[connection.SourceIndex];
            if (source.UnitCount <= 1)
            {
                return;
            }

            source.UnitCount--;
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
                    continue;
                }

                if (IsContestedConnection(connection))
                {
                    ResolveFrontlineArrival(connection, soldier);
                    if (connection.MovingUnits.Contains(soldier))
                    {
                        soldier.WaitingAtFront = true;
                    }
                    else
                    {
                        i = Mathf.Min(i, connection.MovingUnits.Count - 1);
                    }
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

        private void ResolveFrontlineArrival(ControlPointConnection connection, MovingUnitView soldier)
        {
            var opposing = FindOpposingConnection(connection);
            if (opposing == null)
            {
                return;
            }

            for (var i = opposing.MovingUnits.Count - 1; i >= 0; i--)
            {
                var opposingSoldier = opposing.MovingUnits[i];
                if (!opposingSoldier.WaitingAtFront)
                {
                    continue;
                }

                DestroyMovingSoldier(opposingSoldier);
                opposing.MovingUnits.RemoveAt(i);
                DestroyMovingSoldier(soldier);
                connection.MovingUnits.Remove(soldier);
                return;
            }
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

                var targetIndex = SelectEnemyTarget(sourceIndex, owner);
                if (targetIndex >= 0)
                {
                    EstablishConnection(sourceIndex, targetIndex, owner);
                }
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
            var neutralTarget = FindFirstTarget(sourceIndex, ControlPointOwner.Neutral);
            if (neutralTarget >= 0)
            {
                return neutralTarget;
            }

            var playerTarget = FindFirstTarget(sourceIndex, ControlPointOwner.Player);
            if (playerTarget >= 0)
            {
                return playerTarget;
            }

            return FindFirstEnemyTarget(sourceIndex, side);
        }

        private int FindFirstTarget(int sourceIndex, ControlPointOwner owner)
        {
            for (var i = 0; i < points.Length; i++)
            {
                if (i != sourceIndex && points[i].Owner == owner)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindFirstEnemyTarget(int sourceIndex, ControlPointOwner side)
        {
            for (var i = 0; i < points.Length; i++)
            {
                if (i != sourceIndex && IsEnemyOwner(points[i].Owner) && points[i].Owner != side)
                {
                    return i;
                }
            }

            return -1;
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

            AddCutGestureLineSegment(lastCutLocalPoint, localPoint);
            cutGesturePoints.Add(localPoint);
            lastCutLocalPoint = localPoint;
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
                points[sourceIndex].Owner == side;
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
            if (IsContestedConnection(connection))
            {
                return (GetPointPosition(connection.SourceIndex) + GetPointPosition(connection.TargetIndex)) * 0.5f;
            }

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
                PositionLine(connection.Line, start, (start + end) * 0.5f, true, false);
                return;
            }

            PositionLine(connection.Line, start, end, true, true);
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
                ApplyLineInset(ref start, ref end, true, false);
                return;
            }

            ApplyLineInset(ref start, ref end, true, true);
        }

        private static void ApplyLineInset(ref Vector2 start, ref Vector2 end, bool insetStart, bool insetEnd)
        {
            var delta = end - start;
            var length = delta.magnitude;
            var inset = (insetStart ? LineEndpointInset : 0f) + (insetEnd ? LineEndpointInset : 0f);
            if (length <= inset)
            {
                return;
            }

            var direction = delta / length;
            if (insetStart)
            {
                start += direction * LineEndpointInset;
            }

            if (insetEnd)
            {
                end -= direction * LineEndpointInset;
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
            PositionLine(previewLine, start, end, true, true);
        }

        private void HidePreviewLine()
        {
            if (previewLine != null)
            {
                UnityEngine.Object.Destroy(previewLine.gameObject);
                previewLine = null;
            }
        }

        private void AddCutGestureLineSegment(Vector2 start, Vector2 end)
        {
            if ((end - start).sqrMagnitude <= 4f)
            {
                return;
            }

            var segment = CreatePlainLine("CutGestureLine", CutGestureLineColor);
            PositionLine(segment, start, end, false, false);
            segment.transform.SetAsLastSibling();
            cutGestureLines.Add(segment);
        }

        private void HideCutGestureLine()
        {
            for (var i = cutGestureLines.Count - 1; i >= 0; i--)
            {
                DestroyLine(cutGestureLines[i]);
            }

            cutGestureLines.Clear();
            cutGesturePoints.Clear();
        }

        private static void PositionLine(RectTransform line, Vector2 start, Vector2 end, bool insetStart, bool insetEnd)
        {
            if (line == null)
            {
                return;
            }

            var delta = end - start;
            var length = delta.magnitude;
            var inset = (insetStart ? LineEndpointInset : 0f) + (insetEnd ? LineEndpointInset : 0f);
            if (length > inset)
            {
                var direction = delta / length;
                if (insetStart)
                {
                    start += direction * LineEndpointInset;
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
                view.UnitLabel.text = point.UnitCount.ToString();
                view.LevelLabel.text = "Lv" + GetPointLevel(point.UnitCount);
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
            ShowSettlementAndComplete(BuildSettlement());
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
            CloseWinSettlementView();
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

        private void LoadNextLevel()
        {
            EnsureLevelProgress();
            if (!levelProgress.GoNext())
            {
                CompleteWinSettlement();
                return;
            }

            CloseWinSettlementView();
            ResetGame();
        }

        private void ShowWinSettlement(MiniGameSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            Shell.ClosePopup();
            CloseWinSettlementView();
            activeWinSettlement = settlement;
            winSettlementView = MiniGameWinSettlementView.Create(
                Shell.PopupHost,
                titleLabel == null ? null : titleLabel.font,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "ControlPointSettlementPanel",
                    Title = UiTextCatalog.Get("control_point.settlement.title"),
                    PrimaryInfo = MiniGameSettlementInfoRow.CreateLevel(currentLevelIndex + 1),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("control_point.settlement.owned"), CountOwned(ControlPointOwner.Player) + "/" + points.Length),
                    RewardLabel = UiTextCatalog.Get("control_point.settlement.reward"),
                    NextButtonText = UiTextCatalog.Get("control_point.action.next_level"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                LoadNextLevel,
                CompleteWinSettlement);
        }

        private void CompleteWinSettlement()
        {
            if (activeWinSettlement == null)
            {
                return;
            }

            var settlement = activeWinSettlement;
            CloseWinSettlementView();
            CompleteGame?.Invoke(settlement);
        }

        private void CloseLevelSelectView()
        {
            if (levelSelectView != null)
            {
                levelSelectView.Dispose();
                levelSelectView = null;
            }
        }

        private void CloseWinSettlementView()
        {
            if (winSettlementView != null)
            {
                winSettlementView.Dispose();
                winSettlementView = null;
            }

            activeWinSettlement = null;
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

        private static int GetConnectionCapacity(int unitCount)
        {
            return GetPointLevel(unitCount);
        }

        private static float GetProduceInterval(int unitCount)
        {
            switch (GetPointLevel(unitCount))
            {
                case 3:
                    return LevelThreeProduceInterval;
                case 2:
                    return LevelTwoProduceInterval;
                default:
                    return LevelOneProduceInterval;
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
            text.font = TMP_Settings.defaultFontAsset;
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
            public ControlPointViewRefs(RectTransform root, RoundedRectGraphic background, TextMeshProUGUI unitLabel, TextMeshProUGUI levelLabel)
            {
                Root = root;
                Background = background;
                UnitLabel = unitLabel;
                LevelLabel = levelLabel;
            }

            public RectTransform Root { get; }
            public RoundedRectGraphic Background { get; }
            public TextMeshProUGUI UnitLabel { get; }
            public TextMeshProUGUI LevelLabel { get; }
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
