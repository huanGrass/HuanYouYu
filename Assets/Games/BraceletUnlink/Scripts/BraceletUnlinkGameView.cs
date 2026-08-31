using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class BraceletUnlinkGameView : MiniGameBase
    {
        public const string GameIdConstant = "bracelet-unlink";

        public static int LevelCount
        {
            get { return BraceletUnlinkLevelData.CreateBuiltInLevels().Length; }
        }

        public static int[] LevelIds
        {
            get { return new[] { 1, 2, 4, 5, 7, 8 }; }
        }

        private const float RingDiameter = 184f;
        private const float RingPathRadius = 67f;
        private const float RingOuterRadius = 82f;
        private const float RingInnerRadius = 52f;
        private const float GoldLoopInteriorReachRadius = 46f;
        private const float AttachedLoopHeight = 40f;
        private const float AttachedLoopOwnerOverlap = 6f;
        private const float AttachedLoopFrameThickness = 8f;
        private const float ReleaseTolerance = 12f;
        private const float SnapTolerance = 12f;
        private const float CollisionAllowance = 0.35f;
        private const float CollisionFeedbackDuration = 0.16f;
        private const float CollisionFeedbackPeak = 3.2f;
        private const float ReleaseAnimationDuration = 0.62f;
        private const float SettlementDelay = 0.72f;
        private const float MaxGameplayBoardScale = 1.45f;

        private static readonly Color[] RingColors =
        {
            new Color32(47, 151, 135, 255),
            new Color32(66, 171, 151, 255),
            new Color32(49, 157, 139, 255),
            new Color32(218, 112, 123, 255),
            new Color32(39, 139, 125, 255),
            new Color32(220, 225, 211, 255),
            new Color32(43, 148, 132, 255),
            new Color32(37, 143, 126, 255),
            new Color32(56, 163, 143, 255),
            new Color32(42, 153, 132, 255),
            new Color32(61, 168, 147, 255),
            new Color32(38, 145, 127, 255)
        };

        private readonly List<RingState> rings = new List<RingState>();
        private readonly List<MapBuckleState> mapBuckles = new List<MapBuckleState>();
        private readonly List<AttachedBuckleState> attachedBuckles = new List<AttachedBuckleState>();
        private readonly Dictionary<int, RingState> ringsBySlot = new Dictionary<int, RingState>();
        private static BraceletUnlinkLevelData[] levelDefinitionsOverride;
        private BraceletUnlinkLevelData[] levelDefinitions;
        private BraceletUnlinkLevelData levelData;

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI summaryLabel;
        private TextMeshProUGUI hintLabel;
        private Button levelSelectButton;
        private Button restartButton;
        private MiniGameLevelSelectView levelSelectView;
        private MiniGameLevelProgressController levelProgress;
        private RectTransform boardRoot;
        private RectTransform ringLayer;
        private RectTransform mapBuckleFrontLayer;
        private int gestureCount;
        private bool hasGameplayInteraction;
        private int clearedRingCount;
        private int clearedMapBuckleCount;
        private int currentLevelIndex;
        private int unlockedLevelCount = 1;
        private bool isCompleted;
        private float settlementTimer;
        private float gameplayBoardScale = 1f;

        public BraceletUnlinkGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "BraceletUnlinkView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        protected override void BuildOrBindSections()
        {
            var topBar = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("BraceletUnlinkHeader"));
            titleLabel = topBar.TitleText;
            summaryLabel = topBar.ScoreText;
            levelDefinitions = levelDefinitionsOverride ?? ResolveInitialLevelDefinitions();
            EnsureLevelProgress();
            levelData = levelDefinitions[currentLevelIndex].Clone();
            BuildBoard();

            var bottom = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("BraceletUnlinkActions"));
            levelSelectButton = MiniGameShellBottomBarBuilder.CreateLevelSelectButton(bottom.ActionBar).Button;
            levelSelectButton.gameObject.name = "BraceletLevelSelectButton";
            levelSelectButton.onClick.RemoveAllListeners();
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
            InitializeRandomMode(bottom.ActionBar);
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottom.ActionBar).Button;
            restartButton.gameObject.name = "RestartButton";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
            BuildEditorEntry(bottom.ActionBar);

            if (titleLabel == null || summaryLabel == null || hintLabel == null || restartButton == null || boardRoot == null)
            {
                throw new InvalidOperationException("BraceletUnlink view structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            gestureCount = 0;
            hasGameplayInteraction = false;
            clearedRingCount = 0;
            clearedMapBuckleCount = 0;
            isCompleted = false;
            settlementTimer = 0f;

            for (var i = 0; i < mapBuckles.Count; i++)
            {
                var buckle = mapBuckles[i];
                buckle.IsActive = true;
                buckle.RingAThreaded = true;
                buckle.RingBThreaded = true;
                buckle.RingADocked = false;
                buckle.RingBDocked = false;
                buckle.ReleaseProgress = 0f;
                buckle.Root.gameObject.SetActive(true);
                buckle.Root.anchoredPosition = buckle.InitialPosition;
                buckle.Root.localScale = Vector3.one;
                var mapColor = buckle.Root.GetComponent<GoldLoopGraphic>().color;
                mapColor.a = 1f;
                buckle.Root.GetComponent<GoldLoopGraphic>().color = mapColor;
                SetCollidersEnabled(buckle.RailColliders, true);
            }

            for (var i = 0; i < attachedBuckles.Count; i++)
            {
                attachedBuckles[i].IsActive = true;
                attachedBuckles[i].IsThreadingTarget = true;
                attachedBuckles[i].Visual.gameObject.SetActive(true);
                attachedBuckles[i].Visual.localScale = Vector3.one;
                var loopColor = attachedBuckles[i].Visual.GetComponent<GoldLoopGraphic>().color;
                loopColor.a = 1f;
                attachedBuckles[i].Visual.GetComponent<GoldLoopGraphic>().color = loopColor;
                SetCollidersEnabled(attachedBuckles[i].RailColliders, true);
            }

            for (var i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                ring.GapAngle = ring.InitialGapAngle;
                ring.IsDragging = false;
                ring.GestureCounted = false;
                ring.DragRotation = 0f;
                ring.BlockedDirection = 0f;
                ring.CollisionFeedbackTime = 0f;
                ring.IsCleared = false;
                ring.ReleaseProgress = 0f;
                ring.Rect.gameObject.SetActive(true);
                ring.Rect.anchoredPosition = ring.InitialPosition;
                ring.Rect.localEulerAngles = new Vector3(0f, 0f, ring.GapAngle);
                ring.Rect.localScale = Vector3.one;
                SyncAttachedBuckleVisuals(ring);
                SetRingCollidersEnabled(ring, true);
                var color = ring.Graphic.color;
                color.a = 1f;
                ring.Graphic.color = color;
            }

            Physics2D.SyncTransforms();
            RefreshHud();
            SetHint("bracelet-unlink.hint.rotate");
        }

        public override void Tick(float deltaTime)
        {
            var safeDelta = Mathf.Max(0f, deltaTime);
            for (var i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                TickReleaseAnimation(ring, safeDelta);
                if (!ring.IsCleared)
                {
                    TickCollisionFeedback(ring, safeDelta);
                }
            }
            for (var i = 0; i < mapBuckles.Count; i++)
            {
                TickMapBuckleReleaseAnimation(mapBuckles[i], safeDelta);
            }

            if (!isCompleted || settlementTimer <= 0f)
            {
                return;
            }

            settlementTimer -= safeDelta;
            if (settlementTimer <= 0f)
            {
                ShowWinSettlement();
            }
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.bracelet-unlink.help", null);
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
            if (levelSelectButton != null)
            {
                levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            }
            if (randomModeButton != null)
            {
                randomModeButton.onClick.RemoveListener(OnRandomModeClicked);
            }
            CloseLevelSelectView();
        }

        private void BuildBoard()
        {
            var panelObject = new GameObject(
                "BraceletUnlinkBoardPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RoundedRectGraphic));
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(Shell.ContentHost, false);
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680f, 750f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelGraphic = panelObject.GetComponent<RoundedRectGraphic>();
            panelGraphic.color = new Color32(239, 245, 199, 248);
            panelGraphic.CornerRadius = 44f;
            panelGraphic.raycastTarget = false;

            hintLabel = CreateText("BraceletUnlinkHint", panelRect, 25f, new Color32(103, 80, 66, 255));
            hintLabel.alignment = TextAlignmentOptions.Center;
            hintLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            hintLabel.rectTransform.anchorMax = Vector2.one;
            hintLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            hintLabel.rectTransform.offsetMin = new Vector2(30f, -76f);
            hintLabel.rectTransform.offsetMax = new Vector2(-30f, -20f);

            boardRoot = CreateLayer("BraceletUnlinkBoard", panelRect);
            boardRoot.anchorMin = boardRoot.anchorMax = boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.sizeDelta = new Vector2(680f, 620f);
            boardRoot.anchoredPosition = new Vector2(0f, -48f);
            ringLayer = CreateLayer("RingLayer", boardRoot);
            Stretch(ringLayer);
            mapBuckleFrontLayer = CreateLayer("MapBuckleFrontLayer", boardRoot);
            Stretch(mapBuckleFrontLayer);
            RebuildLevelObjects();
        }

        private void RebuildLevelObjects()
        {
            ClearLayer(ringLayer);
            ClearLayer(mapBuckleFrontLayer);
            BuildRings();
            BuildAttachedBuckleLinks();
            BuildMapBuckles();
            RefreshGameplayBoardScale();
        }

        private void BuildRings()
        {
            rings.Clear();
            ringsBySlot.Clear();
            for (var slot = 0; slot < levelData.SlotCount; slot++)
            {
                var slotData = levelData.Rings[slot];
                if (slotData.Kind == BraceletRingKind.Empty)
                {
                    continue;
                }

                var i = rings.Count;
                var position = levelData.GetSlotPosition(slot);
                var ringObject = new GameObject(
                    "Bracelet_" + i,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(BraceletRingGraphic),
                    typeof(Rigidbody2D));
                var rect = ringObject.GetComponent<RectTransform>();
                rect.SetParent(ringLayer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(RingDiameter, RingDiameter);
                rect.anchoredPosition = position;
                var body = ringObject.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;

                var graphic = ringObject.GetComponent<BraceletRingGraphic>();
                graphic.color = RingColors[Mathf.Abs(slotData.ColorIndex) % RingColors.Length];
                graphic.IsClosed = slotData.Kind == BraceletRingKind.Closed;
                graphic.raycastTarget = true;
                var arcColliders = CreateRingArcColliders(ringObject, graphic.IsClosed);
                var state = new RingState
                {
                    Id = i,
                    SlotId = slot,
                    InitialPosition = position,
                    InitialGapAngle = slotData.GapAngle,
                    GapAngle = slotData.GapAngle,
                    Rect = rect,
                    Graphic = graphic,
                    Body = body,
                    ArcColliders = arcColliders,
                    IsClosed = graphic.IsClosed
                };
                rings.Add(state);
                ringsBySlot.Add(slot, state);
                ringObject.AddComponent<RingDragTarget>().Bind(this, i);
            }
        }

        private void BuildAttachedBuckleLinks()
        {
            attachedBuckles.Clear();
            for (var edgeIndex = 0; edgeIndex < levelData.Edges.Length; edgeIndex++)
            {
                var edge = levelData.Edges[edgeIndex];
                if (edge.Kind != BraceletEdgeKind.AttachedToA && edge.Kind != BraceletEdgeKind.AttachedToB)
                {
                    continue;
                }
                RingState first;
                RingState second;
                if (!ringsBySlot.TryGetValue(edge.SlotA, out first) || !ringsBySlot.TryGetValue(edge.SlotB, out second))
                {
                    continue;
                }
                var ownerState = edge.Kind == BraceletEdgeKind.AttachedToA ? first : second;
                var targetState = edge.Kind == BraceletEdgeKind.AttachedToA ? second : first;
                var owner = ownerState.Id;
                var target = targetState.Id;
                var i = attachedBuckles.Count;
                var localAngle = DirectionAngle(targetState.InitialPosition - ownerState.InitialPosition) - ownerState.InitialGapAngle;
                var centerDistance = Vector2.Distance(ownerState.InitialPosition, targetState.InitialPosition);
                RectTransform visual;
                var railColliders = CreateAttachedBuckle(ownerState.Rect, i, localAngle, centerDistance, out visual);
                var state = new AttachedBuckleState
                {
                    Id = i,
                    Owner = owner,
                    Target = target,
                    IsActive = true,
                    IsThreadingTarget = true,
                    LocalAngle = localAngle,
                    Visual = visual,
                    RailColliders = railColliders
                };
                attachedBuckles.Add(state);
                rings[owner].OwnedAttachedBuckleIds.Add(i);
                rings[owner].AttachedLinkIds.Add(i);
                rings[target].AttachedLinkIds.Add(i);
                SyncAttachedBuckleVisual(state);
            }
        }

        private void BuildMapBuckles()
        {
            mapBuckles.Clear();
            for (var edgeIndex = 0; edgeIndex < levelData.Edges.Length; edgeIndex++)
            {
                var edge = levelData.Edges[edgeIndex];
                if (edge.Kind != BraceletEdgeKind.Map)
                {
                    continue;
                }
                RingState stateA;
                RingState stateB;
                if (!ringsBySlot.TryGetValue(edge.SlotA, out stateA) || !ringsBySlot.TryGetValue(edge.SlotB, out stateB))
                {
                    continue;
                }
                var ringA = stateA.Id;
                var ringB = stateB.Id;
                var i = mapBuckles.Count;
                var position = (rings[ringA].InitialPosition + rings[ringB].InitialPosition) * 0.5f;
                var rootObject = new GameObject(
                    "MapBuckle_" + i,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(GoldLoopGraphic));
                var root = rootObject.GetComponent<RectTransform>();
                root.SetParent(mapBuckleFrontLayer, false);
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
                root.sizeDelta = new Vector2(58f, 42f);
                root.anchoredPosition = position;
                var edgeDirection = rings[ringB].InitialPosition - rings[ringA].InitialPosition;
                root.localEulerAngles = new Vector3(0f, 0f, DirectionAngle(edgeDirection));
                var mapLoopLength = Mathf.Max(60f, edgeDirection.magnitude - GoldLoopInteriorReachRadius * 2f);
                var railColliders = CreateGoldLoop(root, true, false, mapLoopLength);

                var buckle = new MapBuckleState
                {
                    Id = i,
                    RingA = ringA,
                    RingB = ringB,
                    RingAAngle = DirectionAngle(position - rings[ringA].InitialPosition),
                    RingBAngle = DirectionAngle(position - rings[ringB].InitialPosition),
                    RingAThreaded = true,
                    RingBThreaded = true,
                    Root = root,
                    InitialPosition = position,
                    RailColliders = railColliders,
                    IsActive = true
                };
                mapBuckles.Add(buckle);
                rings[ringA].LinkedBuckleIds.Add(i);
                rings[ringB].LinkedBuckleIds.Add(i);
            }
        }

        private CircleCollider2D[] CreateRingArcColliders(GameObject ringObject, bool isClosed)
        {
            var count = isClosed ? 22 : 18;
            const float startAngle = 34f;
            var endAngle = isClosed ? 394f : 326f;
            var result = new CircleCollider2D[count];
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.Lerp(startAngle, endAngle, i / (float)(count - 1));
                var collider = ringObject.AddComponent<CircleCollider2D>();
                collider.radius = 14f;
                collider.offset = Direction(angle) * RingPathRadius;
                result[i] = collider;
            }

            return result;
        }

        private BoxCollider2D[] CreateAttachedBuckle(RectTransform ringRect, int buckleId, float localAngle, float centerDistance, out RectTransform visual)
        {
            var rootObject = new GameObject(
                "AttachedBuckle_" + buckleId,
                typeof(RectTransform),
                typeof(CanvasRenderer));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(ringRect, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            var loopLength = Mathf.Max(
                56f,
                centerDistance - (RingOuterRadius - AttachedLoopOwnerOverlap) - GoldLoopInteriorReachRadius);
            root.sizeDelta = new Vector2(loopLength, AttachedLoopHeight);
            root.anchoredPosition = Direction(localAngle)
                * (RingOuterRadius - AttachedLoopOwnerOverlap + loopLength * 0.5f);
            root.localEulerAngles = new Vector3(0f, 0f, localAngle);
            var rails = CreateGoldLoopColliders(root, loopLength);

            var visualObject = new GameObject("AttachedBuckleVisual_" + buckleId, typeof(RectTransform), typeof(CanvasRenderer), typeof(GoldLoopGraphic));
            visual = visualObject.GetComponent<RectTransform>();
            visual.SetParent(mapBuckleFrontLayer, false);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(loopLength, AttachedLoopHeight);
            visual.GetComponent<GoldLoopGraphic>().IsAttachedLoop = true;
            ConfigureGoldLoopGraphic(visual.GetComponent<GoldLoopGraphic>(), null);
            return rails;
        }

        private BoxCollider2D[] CreateGoldLoopColliders(RectTransform root, float length)
        {
            const float frameThickness = AttachedLoopFrameThickness;
            var outerSize = new Vector2(length, AttachedLoopHeight);
            root.sizeDelta = outerSize;
            var horizontalSize = new Vector2(outerSize.x - frameThickness * 2f, frameThickness);
            var horizontalOffset = (outerSize.y - frameThickness) * 0.5f;
            var verticalSize = new Vector2(frameThickness, outerSize.y - frameThickness * 2f);
            var verticalOffset = (outerSize.x - frameThickness) * 0.5f;
            return new[]
            {
                CreateGoldRailCollider("TopRail", root, horizontalSize, new Vector2(0f, horizontalOffset)),
                CreateGoldRailCollider("BottomRail", root, horizontalSize, new Vector2(0f, -horizontalOffset)),
                CreateGoldRailCollider("LeftRail", root, verticalSize, new Vector2(-verticalOffset, 0f)),
                CreateGoldRailCollider("RightRail", root, verticalSize, new Vector2(verticalOffset, 0f))
            };
        }

        private BoxCollider2D[] CreateGoldLoop(RectTransform root, bool mapBuckle, bool frontHalfOnly, float requestedLength)
        {
            var outerSize = new Vector2(requestedLength, 50f);
            const float frameThickness = 10f;
            root.sizeDelta = outerSize;
            var graphic = root.GetComponent<GoldLoopGraphic>();
            graphic.FrameThickness = frameThickness;
            ConfigureGoldLoopGraphic(graphic, frontHalfOnly ? (bool?)true : null);
            var horizontalSize = new Vector2(outerSize.x - frameThickness * 2f, frameThickness);
            var horizontalOffset = (outerSize.y - frameThickness) * 0.5f;
            var verticalSize = new Vector2(frameThickness, outerSize.y - frameThickness * 2f);
            var verticalOffset = (outerSize.x - frameThickness) * 0.5f;

            return new[]
            {
                CreateGoldRailCollider("TopRail", root, horizontalSize, new Vector2(0f, horizontalOffset)),
                CreateGoldRailCollider("BottomRail", root, horizontalSize, new Vector2(0f, -horizontalOffset)),
                CreateGoldRailCollider("LeftRail", root, verticalSize, new Vector2(-verticalOffset, 0f)),
                CreateGoldRailCollider("RightRail", root, verticalSize, new Vector2(verticalOffset, 0f))
            };
        }

        private static void ConfigureGoldLoopGraphic(GoldLoopGraphic graphic, bool? drawUpperHalf)
        {
            graphic.color = Color.white;
            graphic.FrameThickness = 10f;
            graphic.raycastTarget = false;
            if (drawUpperHalf.HasValue)
            {
                graphic.DrawUpperHalf = drawUpperHalf.Value;
            }
        }

        private static BoxCollider2D CreateGoldRailCollider(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var railObject = new GameObject(name, typeof(RectTransform), typeof(BoxCollider2D));
            var rail = railObject.GetComponent<RectTransform>();
            rail.SetParent(parent, false);
            rail.anchorMin = rail.anchorMax = rail.pivot = new Vector2(0.5f, 0.5f);
            rail.sizeDelta = size;
            rail.anchoredPosition = position;
            var collider = railObject.GetComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        private void HandleBeginDrag(int ringId, PointerEventData eventData)
        {
            var ring = GetRing(ringId);
            if (ring == null || ring.IsCleared || isCompleted || isEditing || eventData == null)
            {
                return;
            }

            Vector2 pointer;
            if (!TryGetBoardPointer(eventData, out pointer))
            {
                return;
            }

            var direction = pointer - ring.InitialPosition;
            if (direction.sqrMagnitude < 100f)
            {
                return;
            }

            ring.IsDragging = true;
            hasGameplayInteraction = true;
            ring.GestureCounted = false;
            ring.LastPointerAngle = DirectionAngle(direction);
            ring.DragRotation = 0f;
            SetHint("bracelet-unlink.hint.physics");
        }

        private void HandleDrag(int ringId, PointerEventData eventData)
        {
            var ring = GetRing(ringId);
            if (ring == null || !ring.IsDragging || ring.IsCleared || eventData == null)
            {
                return;
            }

            Vector2 pointer;
            if (!TryGetBoardPointer(eventData, out pointer))
            {
                return;
            }

            var direction = pointer - ring.InitialPosition;
            if (direction.sqrMagnitude < 100f)
            {
                return;
            }

            var pointerAngle = DirectionAngle(direction);
            var delta = Mathf.DeltaAngle(ring.LastPointerAngle, pointerAngle);
            ring.LastPointerAngle = pointerAngle;
            var applied = ApplyRingRotation(ringId, delta);
            ring.DragRotation += Mathf.Abs(applied);
            var isBlocked = Mathf.Abs(applied) + 0.1f < Mathf.Abs(delta);
            UpdateCollisionFeedback(ring, delta, isBlocked);
            if (isBlocked)
            {
                SetHint("bracelet-unlink.hint.blocked");
            }
        }

        private void HandleEndDrag(int ringId)
        {
            var ring = GetRing(ringId);
            if (ring == null || !ring.IsDragging)
            {
                return;
            }

            ring.IsDragging = false;
            CountGesture(ring);
            if (!isCompleted && !ring.IsCleared)
            {
                SetHint("bracelet-unlink.hint.rotate");
            }
        }

        private float ApplyRingRotation(int ringId, float requestedDelta)
        {
            var ring = GetRing(ringId);
            if (ring == null || ring.IsCleared || isCompleted || Mathf.Abs(requestedDelta) < 0.001f)
            {
                return 0f;
            }

            RestoreCollisionFeedbackVisual(ring);
            var startAngle = ring.GapAngle;
            var constraints = BuildRotationConstraints(ring);
            var appliedDelta = FindAllowedRotation(ring, requestedDelta, constraints);
            SetRingAngle(ring, startAngle + appliedDelta);
            if (Mathf.Abs(appliedDelta - requestedDelta) <= 0.1f)
            {
                appliedDelta += TryApplyGapSnap(ring, startAngle);
            }

            CheckMapBuckleMatches(ring, startAngle, appliedDelta);
            CheckAttachedBuckleMatches(ring, startAngle, appliedDelta);

            return appliedDelta;
        }

        private void UpdateCollisionFeedback(RingState ring, float requestedDelta, bool isBlocked)
        {
            if (ring == null || ring.IsCleared)
            {
                return;
            }

            var direction = Mathf.Sign(requestedDelta);
            if (!isBlocked)
            {
                ring.BlockedDirection = 0f;
                ring.CollisionFeedbackTime = 0f;
                RestoreCollisionFeedbackVisual(ring);
                return;
            }

            if (Mathf.Approximately(direction, ring.BlockedDirection))
            {
                return;
            }

            ring.BlockedDirection = direction;
            ring.CollisionFeedbackTime = CollisionFeedbackDuration;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Collision, 0.55f, 0.96f + ring.Id % 3 * 0.035f);
        }

        private void TickCollisionFeedback(RingState ring, float deltaTime)
        {
            if (ring.CollisionFeedbackTime <= 0f)
            {
                return;
            }

            ring.CollisionFeedbackTime = Mathf.Max(0f, ring.CollisionFeedbackTime - deltaTime);
            var progress = 1f - ring.CollisionFeedbackTime / CollisionFeedbackDuration;
            var pulse = progress < 0.32f
                ? progress / 0.32f
                : 1f - (progress - 0.32f) / 0.68f;
            ApplyRingVisualAngle(ring, ring.GapAngle + ring.BlockedDirection * CollisionFeedbackPeak * Mathf.Max(0f, pulse));
        }

        private void RestoreCollisionFeedbackVisual(RingState ring)
        {
            ApplyRingVisualAngle(ring, ring.GapAngle);
        }

        private void ApplyRingVisualAngle(RingState ring, float angle)
        {
            ring.Rect.localEulerAngles = new Vector3(0f, 0f, NormalizeAngle(angle));
            SyncAttachedBuckleVisuals(ring);
        }

        private float TryApplyGapSnap(RingState ring, float rotationStartAngle)
        {
            var snapDelta = FindGapSnapDelta(ring, rotationStartAngle);
            if (Mathf.Abs(snapDelta) < 0.001f)
            {
                return 0f;
            }

            var currentAngle = ring.GapAngle;
            var constraints = BuildRotationConstraints(ring);
            var allowed = FindAllowedRotation(ring, snapDelta, constraints);
            if (Mathf.Abs(allowed - snapDelta) > 0.1f)
            {
                SetRingAngle(ring, currentAngle);
                return 0f;
            }

            SetRingAngle(ring, currentAngle + snapDelta);
            return snapDelta;
        }

        private float FindGapSnapDelta(RingState ring, float rotationStartAngle)
        {
            if (ring.IsClosed)
            {
                return 0f;
            }

            var bestDelta = 0f;
            var bestDistance = SnapTolerance + 0.001f;
            for (var i = 0; i < ring.LinkedBuckleIds.Count; i++)
            {
                var buckle = mapBuckles[ring.LinkedBuckleIds[i]];
                if (!buckle.IsActive)
                {
                    continue;
                }

                if ((buckle.RingA == ring.Id && !buckle.RingAThreaded)
                    || (buckle.RingB == ring.Id && !buckle.RingBThreaded))
                {
                    continue;
                }

                var targetAngle = buckle.RingA == ring.Id ? buckle.RingAAngle : buckle.RingBAngle;
                TrySelectGapSnapTarget(ring, rotationStartAngle, targetAngle, ref bestDelta, ref bestDistance);
            }

            for (var i = 0; i < ring.AttachedLinkIds.Count; i++)
            {
                var link = attachedBuckles[ring.AttachedLinkIds[i]];
                if (!link.IsActive || !link.IsThreadingTarget || link.Target != ring.Id)
                {
                    continue;
                }

                var owner = rings[link.Owner];
                var ownerToTarget = DirectionAngle(ring.InitialPosition - owner.InitialPosition);
                var ownerLoopAngle = owner.GapAngle + link.LocalAngle;
                if (Mathf.Abs(Mathf.DeltaAngle(ownerLoopAngle, ownerToTarget)) > ReleaseTolerance)
                {
                    continue;
                }

                var targetAngle = DirectionAngle(owner.InitialPosition - ring.InitialPosition);
                TrySelectGapSnapTarget(ring, rotationStartAngle, targetAngle, ref bestDelta, ref bestDistance);
            }

            return bestDelta;
        }

        private static void TrySelectGapSnapTarget(
            RingState ring,
            float rotationStartAngle,
            float targetAngle,
            ref float bestDelta,
            ref float bestDistance)
        {
            var delta = Mathf.DeltaAngle(ring.GapAngle, targetAngle);
            var distance = Mathf.Abs(delta);
            var startDistance = Mathf.Abs(Mathf.DeltaAngle(rotationStartAngle, targetAngle));
            if (distance > SnapTolerance || distance >= startDistance - 0.01f || distance >= bestDistance)
            {
                return;
            }

            bestDelta = delta;
            bestDistance = distance;
        }

        private float FindAllowedRotation(
            RingState ring,
            float requestedDelta,
            List<RotationCollisionConstraint> constraints)
        {
            var startAngle = ring.GapAngle;
            var direction = Mathf.Sign(requestedDelta);
            var remaining = Mathf.Abs(requestedDelta);
            var applied = 0f;
            while (remaining > 0.001f)
            {
                var step = direction * Mathf.Min(4f, remaining);
                SetRingAngle(ring, startAngle + applied + step);
                if (TryAcceptRotationConstraints(constraints, true))
                {
                    applied += step;
                    remaining -= Mathf.Abs(step);
                    continue;
                }

                var low = 0f;
                var high = 1f;
                for (var i = 0; i < 7; i++)
                {
                    var middle = (low + high) * 0.5f;
                    SetRingAngle(ring, startAngle + applied + step * middle);
                    if (TryAcceptRotationConstraints(constraints, false))
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                return applied + step * low;
            }

            return applied;
        }

        private List<RotationCollisionConstraint> BuildRotationConstraints(RingState moving)
        {
            Physics2D.SyncTransforms();
            var constraints = new List<RotationCollisionConstraint>();

            for (var ownedIndex = 0; ownedIndex < moving.OwnedAttachedBuckleIds.Count; ownedIndex++)
            {
                var owned = attachedBuckles[moving.OwnedAttachedBuckleIds[ownedIndex]];
                if (!owned.IsActive)
                {
                    continue;
                }

                for (var ringIndex = 0; ringIndex < rings.Count; ringIndex++)
                {
                    var other = rings[ringIndex];
                    if (other == moving || other.IsCleared)
                    {
                        continue;
                    }
                    AddRotationConstraint(constraints, owned.RailColliders, other.ArcColliders);
                }
            }

            for (var attachedIndex = 0; attachedIndex < attachedBuckles.Count; attachedIndex++)
            {
                var otherAttached = attachedBuckles[attachedIndex];
                if (!otherAttached.IsActive
                    || otherAttached.Owner == moving.Id
                    || otherAttached.Target == moving.Id)
                {
                    continue;
                }
                AddRotationConstraint(constraints, otherAttached.RailColliders, moving.ArcColliders);
            }

            for (var ownedIndex = 0; ownedIndex < moving.OwnedAttachedBuckleIds.Count; ownedIndex++)
            {
                var owned = attachedBuckles[moving.OwnedAttachedBuckleIds[ownedIndex]];
                if (!owned.IsActive)
                {
                    continue;
                }
                for (var attachedIndex = 0; attachedIndex < attachedBuckles.Count; attachedIndex++)
                {
                    var otherAttached = attachedBuckles[attachedIndex];
                    if (!otherAttached.IsActive || otherAttached == owned || otherAttached.Owner == moving.Id)
                    {
                        continue;
                    }
                    AddRotationConstraint(constraints, owned.RailColliders, otherAttached.RailColliders);
                }
            }

            for (var mapIndex = 0; mapIndex < mapBuckles.Count; mapIndex++)
            {
                var map = mapBuckles[mapIndex];
                if (!map.IsActive)
                {
                    continue;
                }

                if (map.RingA != moving.Id && map.RingB != moving.Id)
                {
                    AddRotationConstraint(constraints, map.RailColliders, moving.ArcColliders);
                }
                for (var ownedIndex = 0; ownedIndex < moving.OwnedAttachedBuckleIds.Count; ownedIndex++)
                {
                    var owned = attachedBuckles[moving.OwnedAttachedBuckleIds[ownedIndex]];
                    if (owned.IsActive)
                    {
                        AddRotationConstraint(constraints, map.RailColliders, owned.RailColliders);
                    }
                }
            }

            return constraints;
        }

        private static void AddRotationConstraint(
            ICollection<RotationCollisionConstraint> constraints,
            Collider2D[] first,
            Collider2D[] second)
        {
            if (first == null || second == null)
            {
                return;
            }

            constraints.Add(new RotationCollisionConstraint
            {
                First = first,
                Second = second,
                AcceptedPenetration = GetPenetration(first, second)
            });
        }

        private static bool TryAcceptRotationConstraints(
            IList<RotationCollisionConstraint> constraints,
            bool commit)
        {
            for (var i = 0; i < constraints.Count; i++)
            {
                var constraint = constraints[i];
                constraint.CandidatePenetration = GetPenetration(constraint.First, constraint.Second);
                if (!IsAllowedPenetration(constraint.AcceptedPenetration, constraint.CandidatePenetration))
                {
                    return false;
                }
            }

            if (commit)
            {
                for (var i = 0; i < constraints.Count; i++)
                {
                    constraints[i].AcceptedPenetration = constraints[i].CandidatePenetration;
                }
            }
            return true;
        }

        private static bool IsAllowedPenetration(float startPenetration, float candidatePenetration)
        {
            if (startPenetration <= CollisionAllowance)
            {
                return candidatePenetration <= CollisionAllowance;
            }

            return candidatePenetration < startPenetration - 0.001f;
        }

        private static bool RingIsLinkedToBuckle(RingState ring, int buckleId)
        {
            for (var i = 0; i < ring.LinkedBuckleIds.Count; i++)
            {
                if (ring.LinkedBuckleIds[i] == buckleId)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetPenetration(Collider2D first, Collider2D second)
        {
            if (first == null || second == null || !first.enabled || !second.enabled)
            {
                return 0f;
            }

            var distance = first.Distance(second);
            return distance.isOverlapped ? Mathf.Max(0f, -distance.distance) : 0f;
        }

        private static float GetPenetration(Collider2D first, Collider2D[] others)
        {
            var worst = 0f;
            if (others == null)
            {
                return worst;
            }

            for (var i = 0; i < others.Length; i++)
            {
                worst = Mathf.Max(worst, GetPenetration(first, others[i]));
            }

            return worst;
        }

        private static float GetPenetration(Collider2D[] first, Collider2D[] second)
        {
            var worst = 0f;
            if (first == null || second == null)
            {
                return worst;
            }

            for (var i = 0; i < first.Length; i++)
            {
                worst = Mathf.Max(worst, GetPenetration(first[i], second));
            }

            return worst;
        }

        private void CheckMapBuckleMatches(RingState movingRing, float startAngle, float appliedDelta)
        {
            if (movingRing.IsClosed)
            {
                RefreshDockingStates();
                return;
            }

            RefreshDockingStates();
            for (var i = 0; i < movingRing.LinkedBuckleIds.Count; i++)
            {
                var buckle = mapBuckles[movingRing.LinkedBuckleIds[i]];
                if (!buckle.IsActive)
                {
                    continue;
                }

                var movingTarget = buckle.RingA == movingRing.Id ? buckle.RingAAngle : buckle.RingBAngle;
                if (IsGapInsideBuckle(movingRing, movingTarget))
                {
                    SetMapBuckleSideDocked(buckle, movingRing.Id, true);
                }
                var otherRing = rings[buckle.RingA == movingRing.Id ? buckle.RingB : buckle.RingA];
                if (AreBothMapBuckleSidesOpen(buckle))
                {
                    EliminateMapBuckle(buckle);
                    continue;
                }

                TryReleaseUnlinkedRing(movingRing);
                TryReleaseUnlinkedRing(otherRing);
            }

            RefreshDockingStates();
        }

        private void RefreshDockingStates()
        {
            for (var i = 0; i < mapBuckles.Count; i++)
            {
                var buckle = mapBuckles[i];
                buckle.RingADocked = buckle.RingADocked
                    && buckle.IsActive
                    && buckle.RingAThreaded
                    && IsGapInsideBuckle(rings[buckle.RingA], buckle.RingAAngle);
                buckle.RingBDocked = buckle.RingBDocked
                    && buckle.IsActive
                    && buckle.RingBThreaded
                    && IsGapInsideBuckle(rings[buckle.RingB], buckle.RingBAngle);
            }
        }

        private bool AreBothMapBuckleSidesOpen(MapBuckleState buckle)
        {
            return IsMapBuckleSideOpen(buckle, buckle.RingA, buckle.RingAAngle)
                && IsMapBuckleSideOpen(buckle, buckle.RingB, buckle.RingBAngle);
        }

        private bool IsMapBuckleSideOpen(MapBuckleState buckle, int ringId, float targetAngle)
        {
            return !IsMapBuckleThreadingRing(buckle, ringId)
                || IsGapInsideBuckle(rings[ringId], targetAngle);
        }

        private static bool IsMapBuckleThreadingRing(MapBuckleState buckle, int ringId)
        {
            return buckle != null
                && buckle.IsActive
                && ((buckle.RingA == ringId && buckle.RingAThreaded)
                    || (buckle.RingB == ringId && buckle.RingBThreaded));
        }

        private static bool IsMapBuckleSideDocked(MapBuckleState buckle, int ringId)
        {
            return buckle != null
                && ((buckle.RingA == ringId && buckle.RingADocked)
                    || (buckle.RingB == ringId && buckle.RingBDocked));
        }

        private static void SetMapBuckleSideDocked(MapBuckleState buckle, int ringId, bool docked)
        {
            if (buckle.RingA == ringId)
            {
                buckle.RingADocked = docked;
            }
            else if (buckle.RingB == ringId)
            {
                buckle.RingBDocked = docked;
            }
        }

        private void CheckAttachedBuckleMatches(RingState movingRing, float startAngle, float appliedDelta)
        {
            for (var i = 0; i < movingRing.AttachedLinkIds.Count; i++)
            {
                var link = attachedBuckles[movingRing.AttachedLinkIds[i]];
                if (!link.IsActive)
                {
                    continue;
                }

                var owner = rings[link.Owner];
                var target = rings[link.Target];
                if (movingRing == owner && link.IsThreadingTarget)
                {
                    var ownerToTarget = DirectionAngle(target.InitialPosition - owner.InitialPosition);
                    var targetToOwner = DirectionAngle(owner.InitialPosition - target.InitialPosition);
                    var loopStart = startAngle + link.LocalAngle;
                    if (IsGapInsideBuckle(target, targetToOwner)
                        && DidAngleSweepAcross(loopStart, appliedDelta, ownerToTarget))
                    {
                        link.IsThreadingTarget = false;
                        MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.66f);
                    }
                }
                TryReleaseUnlinkedRing(target);
                TryReleaseUnlinkedRing(owner);
            }
        }

        private static bool IsGapInsideBuckle(RingState ring, float targetAngle)
        {
            return !ring.IsClosed
                && !ring.IsCleared
                && Mathf.Abs(Mathf.DeltaAngle(ring.GapAngle, targetAngle)) <= ReleaseTolerance;
        }

        private void EliminateMapBuckle(MapBuckleState buckle)
        {
            if (buckle == null || !buckle.IsActive)
            {
                return;
            }

            buckle.RingAThreaded = false;
            buckle.RingBThreaded = false;
            buckle.IsActive = false;
            buckle.ReleaseProgress = 0f;
            SetCollidersEnabled(buckle.RailColliders, false);
            clearedMapBuckleCount += 1;

            var ringA = rings[buckle.RingA];
            var ringB = rings[buckle.RingB];
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.72f);
            TryReleaseUnlinkedRing(ringA);
            TryReleaseUnlinkedRing(ringB);
            if (clearedMapBuckleCount >= mapBuckles.Count)
            {
                for (var i = 0; i < rings.Count; i++)
                {
                    TryReleaseUnlinkedRing(rings[i]);
                }
            }
            RefreshHud();
            if (!isCompleted)
            {
                SetHint("bracelet-unlink.hint.map_buckle_cleared");
            }
        }

        private void TryReleaseUnlinkedRing(RingState ring)
        {
            if (ring.IsCleared
                || CountBlockingMapBuckles(ring) > 0
                || CountActiveAttachedBuckles(ring) > 0
                || CountBlockingOwnedAttachedBuckles(ring) > 0)
            {
                return;
            }

            ring.IsCleared = true;
            ring.IsDragging = false;
            ring.ReleaseProgress = 0f;
            SetRingCollidersEnabled(ring, false);
            clearedRingCount += 1;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.58f, 0.96f + ring.Id % 4 * 0.025f);

            for (var i = 0; i < ring.AttachedLinkIds.Count; i++)
            {
                var incoming = attachedBuckles[ring.AttachedLinkIds[i]];
                if (!incoming.IsActive || !incoming.IsThreadingTarget || incoming.Target != ring.Id)
                {
                    continue;
                }

                incoming.IsThreadingTarget = false;
                TryReleaseUnlinkedRing(rings[incoming.Owner]);
            }

            for (var i = 0; i < ring.LinkedBuckleIds.Count; i++)
            {
                var buckle = mapBuckles[ring.LinkedBuckleIds[i]];
                if (!IsMapBuckleThreadingRing(buckle, ring.Id))
                {
                    continue;
                }

                if (buckle.RingA == ring.Id)
                {
                    buckle.RingAThreaded = false;
                }
                else
                {
                    buckle.RingBThreaded = false;
                }
                var otherRing = rings[buckle.RingA == ring.Id ? buckle.RingB : buckle.RingA];
                if (AreBothMapBuckleSidesOpen(buckle))
                {
                    EliminateMapBuckle(buckle);
                }
            }

            for (var i = 0; i < ring.OwnedAttachedBuckleIds.Count; i++)
            {
                var owned = attachedBuckles[ring.OwnedAttachedBuckleIds[i]];
                if (!owned.IsActive)
                {
                    continue;
                }

                owned.IsThreadingTarget = false;
                SetCollidersEnabled(owned.RailColliders, false);
                TryReleaseUnlinkedRing(rings[owned.Target]);
            }

            if (clearedRingCount < rings.Count)
            {
                return;
            }

            isCompleted = true;
            if (isRandomMode)
            {
                CompleteRandomRound();
            }
            else
            {
                EnsureLevelProgress();
                levelProgress.CompleteCurrentLevel(CreateSettlement().Score);
                SyncLevelProgressFields();
                UnlockRandomModeIfCampaignFinished();
            }
            settlementTimer = SettlementDelay;
            SetHint("bracelet-unlink.hint.completed");
        }

        private int CountBlockingMapBuckles(RingState ring)
        {
            var count = 0;
            for (var i = 0; i < ring.LinkedBuckleIds.Count; i++)
            {
                var buckle = mapBuckles[ring.LinkedBuckleIds[i]];
                if (!IsMapBuckleThreadingRing(buckle, ring.Id))
                {
                    continue;
                }

                var targetAngle = buckle.RingA == ring.Id ? buckle.RingAAngle : buckle.RingBAngle;
                if (!ring.IsClosed)
                {
                    if (!IsGapInsideBuckle(ring, targetAngle))
                    {
                        count += 1;
                    }
                    continue;
                }

                if (buckle.RingAThreaded && buckle.RingBThreaded)
                {
                    var otherRing = rings[buckle.RingA == ring.Id ? buckle.RingB : buckle.RingA];
                    count += IsMapBuckleSideDocked(buckle, otherRing.Id) ? 0 : 1;
                }
            }

            return count;
        }

        private int CountActiveAttachedBuckles(RingState ring)
        {
            var count = 0;
            for (var i = 0; i < ring.AttachedLinkIds.Count; i++)
            {
                var link = attachedBuckles[ring.AttachedLinkIds[i]];
                if (link.IsActive
                    && link.IsThreadingTarget
                    && link.Target == ring.Id
                    && !IsAttachedBuckleDocked(link))
                {
                    count += 1;
                }
            }

            return count;
        }

        private int CountBlockingOwnedAttachedBuckles(RingState ring)
        {
            var count = 0;
            for (var i = 0; i < ring.OwnedAttachedBuckleIds.Count; i++)
            {
                var link = attachedBuckles[ring.OwnedAttachedBuckleIds[i]];
                if (link.IsActive && link.IsThreadingTarget && !IsOwnedAttachedBuckleDocked(link))
                {
                    count += 1;
                }
            }

            return count;
        }

        private bool IsOwnedAttachedBuckleDocked(AttachedBuckleState link)
        {
            return IsAttachedBuckleDocked(link);
        }

        private bool IsAttachedBuckleDocked(AttachedBuckleState link)
        {
            var owner = rings[link.Owner];
            var target = rings[link.Target];
            if (target.IsCleared)
            {
                return true;
            }

            var ownerToTarget = DirectionAngle(target.InitialPosition - owner.InitialPosition);
            var ownerLoopAngle = owner.GapAngle + link.LocalAngle;
            var targetToOwner = DirectionAngle(owner.InitialPosition - target.InitialPosition);
            return Mathf.Abs(Mathf.DeltaAngle(ownerLoopAngle, ownerToTarget)) <= ReleaseTolerance
                && IsGapInsideBuckle(target, targetToOwner);
        }

        private void CountGesture(RingState ring)
        {
            if (ring.GestureCounted || ring.DragRotation < 4f)
            {
                return;
            }

            ring.GestureCounted = true;
            gestureCount += 1;
            RefreshHud();
        }

        private void TickReleaseAnimation(RingState ring, float deltaTime)
        {
            if (!ring.IsCleared || !ring.Rect.gameObject.activeSelf || ring.ReleaseProgress >= 1f)
            {
                return;
            }

            ring.ReleaseProgress = AdvanceReleaseProgress(ring.ReleaseProgress, deltaTime);
            var eased = EaseReleaseProgress(ring.ReleaseProgress);
            ring.Rect.anchoredPosition = GetReleasePosition(ring.InitialPosition, eased);
            ring.Rect.localEulerAngles = new Vector3(0f, 0f, ring.GapAngle + 250f * eased);
            ring.Rect.localScale = GetReleaseScale(eased);
            var color = ring.Graphic.color;
            color.a = GetReleaseAlpha(eased);
            ring.Graphic.color = color;
            SyncAttachedBuckleVisuals(ring);
            if (ring.ReleaseProgress >= 1f)
            {
                ring.Rect.gameObject.SetActive(false);
                for (var i = 0; i < ring.OwnedAttachedBuckleIds.Count; i++)
                {
                    HideAttachedBuckleWithOwner(attachedBuckles[ring.OwnedAttachedBuckleIds[i]]);
                }
            }
        }

        private static void TickMapBuckleReleaseAnimation(MapBuckleState buckle, float deltaTime)
        {
            if (buckle == null
                || buckle.IsActive
                || !buckle.Root.gameObject.activeSelf
                || buckle.ReleaseProgress >= 1f)
            {
                return;
            }

            buckle.ReleaseProgress = AdvanceReleaseProgress(buckle.ReleaseProgress, deltaTime);
            var eased = EaseReleaseProgress(buckle.ReleaseProgress);
            buckle.Root.anchoredPosition = GetReleasePosition(buckle.InitialPosition, eased);
            buckle.Root.localScale = GetReleaseScale(eased);
            var graphic = buckle.Root.GetComponent<GoldLoopGraphic>();
            var color = graphic.color;
            color.a = GetReleaseAlpha(eased);
            graphic.color = color;
            if (buckle.ReleaseProgress >= 1f)
            {
                buckle.Root.gameObject.SetActive(false);
            }
        }

        private static float AdvanceReleaseProgress(float progress, float deltaTime)
        {
            return Mathf.Clamp01(progress + deltaTime / ReleaseAnimationDuration);
        }

        private static float EaseReleaseProgress(float progress)
        {
            return 1f - Mathf.Pow(1f - progress, 3f);
        }

        private static Vector2 GetReleasePosition(Vector2 initialPosition, float eased)
        {
            var horizontal = Mathf.Sign(initialPosition.x + 0.01f) * 45f * eased;
            return initialPosition + new Vector2(horizontal, 205f * eased);
        }

        private static Vector3 GetReleaseScale(float eased)
        {
            return Vector3.one * Mathf.Lerp(1f, 1.08f, eased);
        }

        private static float GetReleaseAlpha(float eased)
        {
            return 1f - eased;
        }

        private static void HideAttachedBuckleWithOwner(AttachedBuckleState link)
        {
            if (link == null || !link.IsActive)
            {
                return;
            }

            link.IsActive = false;
            link.IsThreadingTarget = false;
            link.Visual.gameObject.SetActive(false);
            SetCollidersEnabled(link.RailColliders, false);
        }

        private bool TryGetBoardPointer(PointerEventData eventData, out Vector2 pointer)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardRoot,
                eventData.position,
                eventData.pressEventCamera,
                out pointer);
        }

        private void SetRingAngle(RingState ring, float angle)
        {
            ring.GapAngle = NormalizeAngle(angle);
            ring.Rect.localEulerAngles = new Vector3(0f, 0f, ring.GapAngle);
            SyncAttachedBuckleVisuals(ring);
            Physics2D.SyncTransforms();
        }

        private void SyncAttachedBuckleVisuals(RingState ring)
        {
            for (var i = 0; i < ring.OwnedAttachedBuckleIds.Count; i++)
            {
                SyncAttachedBuckleVisual(attachedBuckles[ring.OwnedAttachedBuckleIds[i]]);
            }
        }

        private void SyncAttachedBuckleVisual(AttachedBuckleState buckle)
        {
            var owner = rings[buckle.Owner];
            var worldAngle = owner.Rect.localEulerAngles.z + buckle.LocalAngle;
            var centerRadius = RingOuterRadius - AttachedLoopOwnerOverlap + buckle.Visual.rect.width * 0.5f;
            buckle.Visual.anchoredPosition = owner.Rect.anchoredPosition
                + Direction(worldAngle) * (centerRadius * owner.Rect.localScale.x);
            buckle.Visual.localEulerAngles = new Vector3(0f, 0f, worldAngle);
            buckle.Visual.localScale = owner.Rect.localScale;
            var color = buckle.Visual.GetComponent<GoldLoopGraphic>().color;
            color.a = owner.Graphic.color.a;
            buckle.Visual.GetComponent<GoldLoopGraphic>().color = color;
        }

        private static void SetRingCollidersEnabled(RingState ring, bool enabled)
        {
            for (var i = 0; i < ring.ArcColliders.Length; i++)
            {
                ring.ArcColliders[i].enabled = enabled;
            }
        }

        private static void SetCollidersEnabled(Collider2D[] colliders, bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private void ShowWinSettlement()
        {
            settlementTimer = 0f;
            CloseLevelSelectView();
            var settlement = CreateSettlement();
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "BraceletUnlinkWinSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.NextLevel,
                    Title = UiTextCatalog.Get("bracelet-unlink.settlement.title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("bracelet-unlink.settlement.gestures"), gestureCount.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("bracelet-unlink.settlement.cleared"), clearedMapBuckleCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
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

        private MiniGameSettlement CreateSettlement()
        {
            return new MiniGameSettlement
            {
                Score = Mathf.Max(100, 1200 - gestureCount * 25),
                CoinCount = 0,
                ChestCount = 0,
                Summary = UiTextCatalog.Format("bracelet-unlink.settlement.summary", gestureCount)
            };
        }

        private void RefreshHud()
        {
            titleLabel.text = UiTextCatalog.Get("game.bracelet-unlink.name");
            summaryLabel.text = isRandomMode
                ? UiTextCatalog.Format(
                    "bracelet-unlink.random.hud.summary",
                    mapBuckles.Count - clearedMapBuckleCount,
                    gestureCount)
                : UiTextCatalog.Format(
                    "bracelet-unlink.hud.summary",
                    currentLevelIndex + 1,
                    mapBuckles.Count - clearedMapBuckleCount,
                    gestureCount);
        }

        private void SetHint(string key)
        {
            if (hintLabel != null)
            {
                hintLabel.text = UiTextCatalog.Get(key);
            }
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            if (hasGameplayInteraction)
            {
                ShowRestartConfirmation(ResetGame);
                return;
            }
            ResetGame();
        }

        private void ShowRestartConfirmation(Action onConfirm)
        {
            Shell.ShowConfirmPopup(
                UiTextCatalog.Get("bracelet-unlink.confirm_restart.title"),
                UiTextCatalog.Get("bracelet-unlink.confirm_restart.message"),
                UiTextCatalog.Get("bracelet-unlink.confirm_restart.confirm"),
                UiTextCatalog.Get("common.action.cancel"),
                ResumeFromPause,
                onConfirm);
        }

        private void OnLevelSelectClicked()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            CloseLevelSelectView();
            EnsureLevelProgress();
            levelSelectView = MiniGameLevelSelectView.Create(
                Shell.PopupHost,
                MiniGameFontProvider.DefaultFont,
                levelDefinitions.Length,
                currentLevelIndex,
                unlockedLevelCount,
                "BraceletUnlinkLevelSelectPanel",
                "BraceletUnlinkLevelButton_",
                SelectLevel,
                CloseLevelSelectView);
        }

        private void CloseLevelSelectView()
        {
            if (levelSelectView == null)
            {
                return;
            }
            levelSelectView.Dispose();
            levelSelectView = null;
        }

        private void SelectLevel(int index)
        {
            EnsureLevelProgress();
            if (!levelProgress.Select(index))
            {
                return;
            }
            ExitRandomModeForCampaign();
            SyncLevelProgressFields();
            levelData = levelDefinitions[currentLevelIndex].Clone();
            RebuildLevelObjects();
            ResetGame();
        }

        private void LoadNextLevel(MiniGameSettlement settlement)
        {
            if (isRandomMode)
            {
                GrantSettlementReward(settlement);
                StartRandomMode();
                return;
            }
            EnsureLevelProgress();
            if (!levelProgress.GoNext())
            {
                if (IsRandomModeUnlocked())
                {
                    GrantSettlementReward(settlement);
                    StartRandomMode();
                }
                else
                {
                    CompleteGame?.Invoke(settlement);
                }
                return;
            }
            GrantSettlementReward(settlement);
            SyncLevelProgressFields();
            levelData = levelDefinitions[currentLevelIndex].Clone();
            RebuildLevelObjects();
            ResetGame();
        }

        private void SaveNextLevelForReturn()
        {
            if (isRandomMode)
            {
                return;
            }
            EnsureLevelProgress();
            levelProgress.SaveNextAsCurrent();
            SyncLevelProgressFields();
        }

        private void EnsureLevelProgress()
        {
            if (levelProgress == null)
            {
                var levelIds = levelDefinitions != null && levelDefinitions.Length > 0
                    ? new int[levelDefinitions.Length]
                    : new[] { 0 };
                for (var index = 0; index < levelIds.Length && levelDefinitions != null; index++)
                {
                    levelIds[index] = levelDefinitions[index].LevelId;
                }
                levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, levelIds);
            }
            SyncLevelProgressFields();
        }

        private void SyncLevelProgressFields()
        {
            if (levelProgress == null)
            {
                currentLevelIndex = 0;
                unlockedLevelCount = 1;
                return;
            }
            currentLevelIndex = levelProgress.CurrentLevelIndex;
            unlockedLevelCount = levelProgress.UnlockedLevelCount;
        }

        private void RefreshGameplayBoardScale()
        {
            if (boardRoot == null || rings.Count == 0)
            {
                gameplayBoardScale = 1f;
                return;
            }
            var min = rings[0].InitialPosition;
            var max = rings[0].InitialPosition;
            for (var i = 1; i < rings.Count; i++)
            {
                min = Vector2.Min(min, rings[i].InitialPosition);
                max = Vector2.Max(max, rings[i].InitialPosition);
            }
            var required = max - min + Vector2.one * RingDiameter;
            gameplayBoardScale = Mathf.Min(MaxGameplayBoardScale, 610f / Mathf.Max(1f, required.x), 520f / Mathf.Max(1f, required.y));
            if (!isEditing)
            {
                boardRoot.localScale = Vector3.one * gameplayBoardScale;
            }
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            var settlement = CreateSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "BraceletUnlinkExitSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("bracelet-unlink.settlement.cleared"), clearedMapBuckleCount.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("bracelet-unlink.settlement.gestures"), gestureCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private RingState GetRing(int ringId)
        {
            return ringId >= 0 && ringId < rings.Count ? rings[ringId] : null;
        }

        private static bool DidAngleSweepAcross(float startAngle, float deltaAngle, float targetAngle)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(startAngle, targetAngle)) <= ReleaseTolerance
                || Mathf.Abs(Mathf.DeltaAngle(startAngle + deltaAngle, targetAngle)) <= ReleaseTolerance)
            {
                return true;
            }

            return deltaAngle >= 0f
                ? Mathf.Repeat(targetAngle - startAngle, 360f) <= deltaAngle + ReleaseTolerance
                : Mathf.Repeat(startAngle - targetAngle, 360f) <= -deltaAngle + ReleaseTolerance;
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        private static float DirectionAngle(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static RectTransform CreateLayer(string name, Transform parent)
        {
            var layerObject = new GameObject(name, typeof(RectTransform));
            var rect = layerObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RoundedRectGraphic CreateRoundedRect(string name, Transform parent, Color color, Vector2 size, float radius)
        {
            var graphicObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            var graphic = graphicObject.GetComponent<RoundedRectGraphic>();
            graphic.rectTransform.SetParent(parent, false);
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = graphic.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            graphic.rectTransform.sizeDelta = size;
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            text.font = MiniGameFontProvider.DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearLayer(RectTransform layer)
        {
            if (layer == null)
            {
                return;
            }
            for (var i = layer.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(layer.GetChild(i).gameObject);
            }
        }

        private sealed class RingState
        {
            public int Id;
            public int SlotId;
            public Vector2 InitialPosition;
            public float InitialGapAngle;
            public float GapAngle;
            public float LastPointerAngle;
            public float DragRotation;
            public float BlockedDirection;
            public float CollisionFeedbackTime;
            public float ReleaseProgress;
            public bool IsDragging;
            public bool GestureCounted;
            public bool IsCleared;
            public bool IsClosed;
            public RectTransform Rect;
            public BraceletRingGraphic Graphic;
            public Rigidbody2D Body;
            public CircleCollider2D[] ArcColliders;
            public readonly List<int> LinkedBuckleIds = new List<int>();
            public readonly List<int> AttachedLinkIds = new List<int>();
            public readonly List<int> OwnedAttachedBuckleIds = new List<int>();
        }

        private sealed class RotationCollisionConstraint
        {
            public Collider2D[] First;
            public Collider2D[] Second;
            public float AcceptedPenetration;
            public float CandidatePenetration;
        }

        private sealed class AttachedBuckleState
        {
            public int Id;
            public int Owner;
            public int Target;
            public bool IsActive;
            public bool IsThreadingTarget;
            public float LocalAngle;
            public RectTransform Visual;
            public BoxCollider2D[] RailColliders;
        }

        private sealed class MapBuckleState
        {
            public int Id;
            public int RingA;
            public int RingB;
            public float RingAAngle;
            public float RingBAngle;
            public bool RingADocked;
            public bool RingBDocked;
            public bool RingAThreaded;
            public bool RingBThreaded;
            public bool IsActive;
            public float ReleaseProgress;
            public Vector2 InitialPosition;
            public RectTransform Root;
            public BoxCollider2D[] RailColliders;
        }

        private sealed class RingDragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private BraceletUnlinkGameView owner;
            private int ringId;

            public void Bind(BraceletUnlinkGameView view, int id)
            {
                owner = view;
                ringId = id;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                owner?.HandleBeginDrag(ringId, eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                owner?.HandleDrag(ringId, eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                owner?.HandleEndDrag(ringId);
            }
        }
    }

    public sealed class BraceletRingGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        private const int ArcSegments = 56;
        private const int EndCornerSegments = 8;
        private const float GapHalfAngle = 28f;
        private bool isClosed;

        public bool IsClosed
        {
            get { return isClosed; }
            set
            {
                isClosed = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - 10f;
            var center = rect.center;
            var innerRadius = radius - 30f;
            var shadowColor = WithAlpha(Color.Lerp(color, new Color(0.05f, 0.16f, 0.14f), 0.32f), color.a * 0.18f);
            AddArc(vertexHelper, center + new Vector2(0f, -4f), innerRadius, radius, isClosed ? 0f : GapHalfAngle, isClosed ? 360f : 360f - GapHalfAngle, shadowColor, shadowColor);
            if (isClosed)
            {
                AddLitArc(vertexHelper, center, innerRadius, radius, 0f, 360f, color);
                return;
            }

            // 圆弧本身的径向截面就是缺口端面，避免再叠整圆端帽造成“两个圆球”的观感。
            AddLitArc(vertexHelper, center, innerRadius, radius, GapHalfAngle, 360f - GapHalfAngle, color);
            AddRoundedEnd(vertexHelper, center, innerRadius, radius, GapHalfAngle, color);
            AddRoundedEnd(vertexHelper, center, innerRadius, radius, -GapHalfAngle, color);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out localPoint))
            {
                return false;
            }

            var rect = GetPixelAdjustedRect();
            var distance = Vector2.Distance(localPoint, rect.center);
            var outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var innerRadius = outerRadius - 50f;
            if (distance < innerRadius || distance > outerRadius)
            {
                return false;
            }

            var localDirection = localPoint - rect.center;
            var localAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            return isClosed || Mathf.Abs(Mathf.DeltaAngle(0f, localAngle)) > GapHalfAngle;
        }

        private static void AddArc(VertexHelper helper, Vector2 center, float innerRadius, float outerRadius, float start, float end, Color32 innerColor, Color32 outerColor)
        {
            for (var i = 0; i < ArcSegments; i++)
            {
                var angle0 = Mathf.Lerp(start, end, i / (float)ArcSegments);
                var angle1 = Mathf.Lerp(start, end, (i + 1) / (float)ArcSegments);
                var direction0 = Direction(angle0);
                var direction1 = Direction(angle1);
                AddQuad(
                    helper,
                    center + direction0 * innerRadius,
                    center + direction0 * outerRadius,
                    center + direction1 * outerRadius,
                    center + direction1 * innerRadius,
                    innerColor,
                    outerColor,
                    outerColor,
                    innerColor);
            }
        }

        private static void AddLitArc(VertexHelper helper, Vector2 center, float innerRadius, float outerRadius, float start, float end, Color baseColor)
        {
            var lightDirection = Direction(125f);
            for (var i = 0; i < ArcSegments; i++)
            {
                var angle0 = Mathf.Lerp(start, end, i / (float)ArcSegments);
                var angle1 = Mathf.Lerp(start, end, (i + 1) / (float)ArcSegments);
                var direction0 = Direction(angle0);
                var direction1 = Direction(angle1);
                var light0 = Mathf.InverseLerp(-1f, 1f, Vector2.Dot(direction0, lightDirection));
                var light1 = Mathf.InverseLerp(-1f, 1f, Vector2.Dot(direction1, lightDirection));
                var inner0 = Color.Lerp(Color.Lerp(baseColor, Color.black, 0.24f), baseColor, light0 * 0.42f);
                var inner1 = Color.Lerp(Color.Lerp(baseColor, Color.black, 0.24f), baseColor, light1 * 0.42f);
                var outer0 = Color.Lerp(baseColor, Color.white, 0.08f + light0 * 0.22f);
                var outer1 = Color.Lerp(baseColor, Color.white, 0.08f + light1 * 0.22f);
                AddQuad(
                    helper,
                    center + direction0 * innerRadius,
                    center + direction0 * outerRadius,
                    center + direction1 * outerRadius,
                    center + direction1 * innerRadius,
                    inner0,
                    outer0,
                    outer1,
                    inner1);
            }
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }

        private static void AddRoundedEnd(VertexHelper helper, Vector2 center, float innerRadius, float outerRadius, float angle, Color baseColor)
        {
            const float cornerRadius = 3.5f;
            var radial = Direction(angle);
            var tangent = new Vector2(-radial.y, radial.x);
            var innerCenter = center + radial * (innerRadius + cornerRadius);
            var outerCenter = center + radial * (outerRadius - cornerRadius);
            var endColor = (Color32)Color.Lerp(baseColor, Color.white, 0.08f);
            AddQuad(
                helper,
                innerCenter - tangent * cornerRadius,
                outerCenter - tangent * cornerRadius,
                outerCenter + tangent * cornerRadius,
                innerCenter + tangent * cornerRadius,
                endColor,
                endColor,
                endColor,
                endColor);
            AddCircle(helper, innerCenter, cornerRadius, endColor);
            AddCircle(helper, outerCenter, cornerRadius, endColor);
        }

        private static void AddCircle(VertexHelper helper, Vector2 center, float radius, Color32 colorValue)
        {
            var centerIndex = helper.currentVertCount;
            helper.AddVert(center, colorValue, Vector2.zero);
            for (var i = 0; i <= EndCornerSegments; i++)
            {
                helper.AddVert(center + Direction(i * 360f / EndCornerSegments) * radius, colorValue, Vector2.zero);
            }

            for (var i = 0; i < EndCornerSegments; i++)
            {
                helper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private static void AddQuad(VertexHelper helper, Vector2 first, Vector2 second, Vector2 third, Vector2 fourth, Color32 firstColor, Color32 secondColor, Color32 thirdColor, Color32 fourthColor)
        {
            var start = helper.currentVertCount;
            helper.AddVert(first, firstColor, Vector2.zero);
            helper.AddVert(second, secondColor, Vector2.zero);
            helper.AddVert(third, thirdColor, Vector2.zero);
            helper.AddVert(fourth, fourthColor, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }

    public sealed class GoldLoopGraphic : MaskableGraphic
    {
        private const int CornerSegments = 10;
        private const float AttachedBodyStart = 8f;
        private float frameThickness = 6f;
        private bool drawUpperHalf;
        private bool splitLayer;
        private bool isAttachedLoop;

        public float FrameThickness
        {
            get { return frameThickness; }
            set
            {
                frameThickness = Mathf.Max(2f, value);
                SetVerticesDirty();
            }
        }

        public bool DrawUpperHalf
        {
            get { return drawUpperHalf; }
            set
            {
                drawUpperHalf = value;
                splitLayer = true;
                SetVerticesDirty();
            }
        }

        public bool IsAttachedLoop
        {
            get { return isAttachedLoop; }
            set
            {
                isAttachedLoop = value;
                SetVerticesDirty();
            }
        }

        public float AttachedBodyStartOffset
        {
            get { return AttachedBodyStart; }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var outerRadius = rect.height * 0.5f;
            var innerRadius = Mathf.Max(1f, outerRadius - frameThickness);
            var halfStraight = Mathf.Max(0f, rect.width * 0.5f - outerRadius);
            var tint = (Color32)color;
            var goldTop = MultiplyColor(new Color32(255, 207, 75, 255), tint);
            var goldBottom = MultiplyColor(new Color32(225, 142, 28, 255), tint);
            var shine = MultiplyColor(new Color32(255, 239, 166, 255), tint);
            var shadow = MultiplyColor(new Color32(169, 101, 22, 255), tint);

            // 视频采用侧视表现：金环的正面是一块厚实金扣，孔洞存在于碰撞结构中，
            // 并不是从镜头方向看到的橙色空心椭圆。
            if (splitLayer)
            {
                var visualHeight = Mathf.Min(30f, rect.height * 0.68f);
                var visualRect = new Rect(rect.center.x - rect.width * 0.5f, rect.center.y - visualHeight * 0.5f, rect.width, visualHeight);
                AddVerticalGradientCapsule(vertexHelper, visualRect, drawUpperHalf ? goldBottom : shadow, drawUpperHalf ? goldTop : shadow);
                if (drawUpperHalf)
                {
                    AddCapsule(vertexHelper, new Rect(visualRect.x + 7f, visualRect.yMax - 7f, visualRect.width - 14f, 3f), shine);
                }
                return;
            }

            var buckleHeight = Mathf.Min(32f, rect.height * 0.72f);
            var buckleRect = new Rect(rect.center.x - rect.width * 0.5f, rect.center.y - buckleHeight * 0.5f, rect.width, buckleHeight);
            if (isAttachedLoop)
            {
                var shadowRect = new Rect(buckleRect.x, buckleRect.y - 2f, buckleRect.width, buckleRect.height);
                AddAttachedBuckleSilhouette(vertexHelper, shadowRect, shadow, shadow);
                AddAttachedBuckleSilhouette(vertexHelper, buckleRect, goldBottom, goldTop);
                AddCapsule(vertexHelper, new Rect(buckleRect.x + 13f, buckleRect.yMax - 6f, buckleRect.width - 20f, 2.5f), shine);
                return;
            }

            AddCapsule(vertexHelper, new Rect(buckleRect.x, buckleRect.y - 2f, buckleRect.width, buckleRect.height), shadow);
            AddVerticalGradientCapsule(vertexHelper, buckleRect, goldBottom, goldTop);
            AddCapsule(vertexHelper, new Rect(buckleRect.x + 8f, buckleRect.yMax - 7f, buckleRect.width - 16f, 3f), shine);
        }

        private static Color32 MultiplyColor(Color32 source, Color32 tint)
        {
            return new Color32(
                (byte)(source.r * tint.r / 255),
                (byte)(source.g * tint.g / 255),
                (byte)(source.b * tint.b / 255),
                (byte)(source.a * tint.a / 255));
        }

        private static void AddUpperStraightBand(VertexHelper helper, Vector2 center, float halfStraight, float outerRadius, float innerRadius, Color32 colorValue)
        {
            if (halfStraight <= 0f)
            {
                return;
            }

            AddQuad(helper,
                center + new Vector2(-halfStraight, innerRadius),
                center + new Vector2(halfStraight, innerRadius),
                center + new Vector2(halfStraight, outerRadius),
                center + new Vector2(-halfStraight, outerRadius),
                colorValue);
        }

        private static void AddLowerStraightBand(VertexHelper helper, Vector2 center, float halfStraight, float outerRadius, float innerRadius, Color32 colorValue)
        {
            if (halfStraight <= 0f)
            {
                return;
            }

            AddQuad(helper,
                center + new Vector2(-halfStraight, -outerRadius),
                center + new Vector2(halfStraight, -outerRadius),
                center + new Vector2(halfStraight, -innerRadius),
                center + new Vector2(-halfStraight, -innerRadius),
                colorValue);
        }

        private static void AddHalfLoop(VertexHelper helper, Vector2 center, float halfStraight, float outerRadius, float innerRadius, float start, float end, Color32 innerColor, Color32 outerColor)
        {
            var side = Mathf.Cos((start + end) * 0.5f * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            var capCenter = center + Vector2.right * halfStraight * side;
            for (var i = 0; i < CornerSegments; i++)
            {
                var angle0 = Mathf.Lerp(start, end, i / (float)CornerSegments);
                var angle1 = Mathf.Lerp(start, end, (i + 1) / (float)CornerSegments);
                var direction0 = Direction(angle0);
                var direction1 = Direction(angle1);
                AddQuad(helper,
                    capCenter + direction0 * innerRadius,
                    capCenter + direction1 * innerRadius,
                    capCenter + direction1 * outerRadius,
                    capCenter + direction0 * outerRadius,
                    innerColor,
                    innerColor,
                    outerColor,
                    outerColor);
            }
        }

        private static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 colorValue)
        {
            AddQuad(helper, a, b, c, d, colorValue, colorValue, colorValue, colorValue);
        }

        private static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 colorA, Color32 colorB, Color32 colorC, Color32 colorD)
        {
            var start = helper.currentVertCount;
            helper.AddVert(a, colorA, Vector2.zero);
            helper.AddVert(b, colorB, Vector2.zero);
            helper.AddVert(c, colorC, Vector2.zero);
            helper.AddVert(d, colorD, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static void AddCapsule(VertexHelper helper, Rect rect, Color32 colorValue)
        {
            var radius = rect.height * 0.5f;
            var leftCenter = new Vector2(rect.xMin + radius, rect.center.y);
            var rightCenter = new Vector2(rect.xMax - radius, rect.center.y);
            AddQuad(helper,
                new Vector2(leftCenter.x, rect.yMin),
                new Vector2(rightCenter.x, rect.yMin),
                new Vector2(rightCenter.x, rect.yMax),
                new Vector2(leftCenter.x, rect.yMax),
                colorValue);
            AddFilledHalfCircle(helper, leftCenter, radius, 90f, 270f, colorValue);
            AddFilledHalfCircle(helper, rightCenter, radius, -90f, 90f, colorValue);
        }

        private static void AddVerticalGradientCapsule(VertexHelper helper, Rect rect, Color32 bottomColor, Color32 topColor)
        {
            var radius = rect.height * 0.5f;
            var leftCenter = new Vector2(rect.xMin + radius, rect.center.y);
            var rightCenter = new Vector2(rect.xMax - radius, rect.center.y);
            AddQuad(helper,
                new Vector2(leftCenter.x, rect.yMin),
                new Vector2(rightCenter.x, rect.yMin),
                new Vector2(rightCenter.x, rect.yMax),
                new Vector2(leftCenter.x, rect.yMax),
                bottomColor,
                bottomColor,
                topColor,
                topColor);
            AddGradientHalfCircle(helper, leftCenter, radius, 90f, 270f, rect, bottomColor, topColor);
            AddGradientHalfCircle(helper, rightCenter, radius, -90f, 90f, rect, bottomColor, topColor);
        }

        private static void AddAttachedBuckleSilhouette(
            VertexHelper helper,
            Rect rect,
            Color32 bottomColor,
            Color32 topColor)
        {
            const int capSegments = 6;
            var centerY = rect.center.y;
            var neckHalfHeight = 11.5f;
            var bodyHalfHeight = Mathf.Min(15f, rect.height * 0.5f);
            var neckEndX = rect.xMin + 5f;
            var shoulderEndX = rect.xMin + AttachedBodyStart;
            var targetCapRadius = bodyHalfHeight;
            var targetCapCenter = new Vector2(rect.xMax - targetCapRadius, centerY);
            var points = new List<Vector2>
            {
                new Vector2(rect.xMin, centerY - neckHalfHeight + 3f),
                new Vector2(rect.xMin + 3f, centerY - neckHalfHeight),
                new Vector2(neckEndX, centerY - neckHalfHeight),
                new Vector2(shoulderEndX, centerY - bodyHalfHeight),
                targetCapCenter + Vector2.down * targetCapRadius
            };
            for (var i = 1; i <= capSegments; i++)
            {
                points.Add(targetCapCenter + Direction(Mathf.Lerp(-90f, 90f, i / (float)capSegments)) * targetCapRadius);
            }
            points.Add(new Vector2(shoulderEndX, centerY + bodyHalfHeight));
            points.Add(new Vector2(neckEndX, centerY + neckHalfHeight));
            points.Add(new Vector2(rect.xMin + 3f, centerY + neckHalfHeight));
            points.Add(new Vector2(rect.xMin, centerY + neckHalfHeight - 3f));
            var centerIndex = helper.currentVertCount;
            helper.AddVert(rect.center, Color32.Lerp(bottomColor, topColor, 0.5f), Vector2.zero);
            for (var i = 0; i < points.Count; i++)
            {
                var gradient = Mathf.InverseLerp(rect.yMin, rect.yMax, points[i].y);
                helper.AddVert(points[i], Color32.Lerp(bottomColor, topColor, gradient), Vector2.zero);
            }
            for (var i = 0; i < points.Count; i++)
            {
                helper.AddTriangle(
                    centerIndex,
                    centerIndex + i + 1,
                    centerIndex + (i + 1) % points.Count + 1);
            }
        }

        private static void AddGradientHalfCircle(VertexHelper helper, Vector2 center, float radius, float start, float end, Rect rect, Color32 bottomColor, Color32 topColor)
        {
            var centerIndex = helper.currentVertCount;
            helper.AddVert(center, Color32.Lerp(bottomColor, topColor, 0.5f), Vector2.zero);
            for (var i = 0; i <= CornerSegments; i++)
            {
                var point = center + Direction(Mathf.Lerp(start, end, i / (float)CornerSegments)) * radius;
                helper.AddVert(point, Color32.Lerp(bottomColor, topColor, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)), Vector2.zero);
            }

            for (var i = 0; i < CornerSegments; i++)
            {
                helper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private static void AddFilledHalfCircle(VertexHelper helper, Vector2 center, float radius, float start, float end, Color32 colorValue)
        {
            var centerIndex = helper.currentVertCount;
            helper.AddVert(center, colorValue, Vector2.zero);
            for (var i = 0; i <= CornerSegments; i++)
            {
                helper.AddVert(center + Direction(Mathf.Lerp(start, end, i / (float)CornerSegments)) * radius, colorValue, Vector2.zero);
            }

            for (var i = 0; i < CornerSegments; i++)
            {
                helper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }
    }
}
