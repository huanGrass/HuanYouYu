using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class WatermelonMergeGameView : MiniGameBase
    {
        public const string GameIdConstant = "watermelon-merge";

        private const float Gravity = 1380f;
        private const float PositionSolverIterations = 3f;
        private const float WallBounce = 0.18f;
        private const float FruitBounce = 0.08f;
        private const float GroundFriction = 0.86f;
        private const float AirDamping = 0.995f;
        private const float DropCooldownSeconds = 0.38f;
        private const float OverflowGraceSeconds = 1.35f;
        private const float OverflowVelocityLimit = 110f;
        private const int MaxFruitCount = 70;
        private const int MaxLevel = 10;

        private static readonly float[] FruitRadii =
        {
            26f,
            32f,
            38f,
            45f,
            53f,
            62f,
            72f,
            84f,
            96f,
            110f,
            126f
        };

        private static readonly int[] MergeScores =
        {
            0,
            4,
            8,
            16,
            32,
            64,
            128,
            256,
            512,
            1024,
            2048
        };

        private static readonly string[] FruitIconPaths =
        {
            "GameIcons/strawberry",
            "GameIcons/grapes",
            "GameIcons/apple",
            "GameIcons/orange",
            "GameIcons/peach",
            "GameIcons/tomato",
            "GameIcons/eggplant",
            "GameIcons/corn",
            "GameIcons/pineapple",
            "GameIcons/pumpkin",
            "GameIcons/watermelon"
        };

        private readonly List<FruitNode> fruits = new List<FruitNode>();
        private readonly List<Sprite> fruitSprites = new List<Sprite>();

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI previewLabel;
        private TMP_FontAsset fontAsset;
        private Button restartButton;
        private RectTransform contentRoot;
        private RectTransform boardRoot;
        private RectTransform fruitLayer;
        private RectTransform dangerLine;
        private RectTransform previewRoot;
        private Image previewIcon;
        private MiniGameSettlement pendingSettlement;
        private WatermelonMergeState state;
        private float boardWidth;
        private float boardHeight;
        private float boardLeft;
        private float boardRight;
        private float boardBottom;
        private float boardTop;
        private float dangerLineY;
        private float dropCooldown;
        private float overflowTimer;
        private int currentFruitLevel;
        private int nextFruitLevel;
        private int score;
        private int coinCount;
        private int chestCount;
        private int fruitSequence;

        private enum WatermelonMergeState
        {
            Running,
            Paused,
            Settled,
            Disposed
        }

        private sealed class FruitNode
        {
            public int Id;
            public int Level;
            public float Radius;
            public RectTransform Root;
            public Image Icon;
            public Vector2 Position;
            public Vector2 Velocity;
            public bool IsMerging;
        }

        public WatermelonMergeGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "WatermelonMergeView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (state != WatermelonMergeState.Running)
            {
                return;
            }

            EnsureBoardLayout();
            dropCooldown = Mathf.Max(0f, dropCooldown - deltaTime);
            StepSimulation(Mathf.Min(deltaTime, 0.033f));
            RefreshFruitViews();
            CheckOverflow(deltaTime);
        }

        protected override void BuildOrBindSections()
        {
            fontAsset = MiniGameFontProvider.DefaultFont;

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("WatermelonMergeTop"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContentSection();
            BuildBottomSection();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            EnsureSpritesLoaded();
            ClearFruits();

            state = WatermelonMergeState.Running;
            pendingSettlement = null;
            score = 0;
            coinCount = 0;
            chestCount = 0;
            fruitSequence = 0;
            overflowTimer = 0f;
            dropCooldown = 0f;
            currentFruitLevel = RollNextFruitLevel();
            nextFruitLevel = RollNextFruitLevel();

            EnsureBoardLayout();
            RefreshHud();
            RefreshPreview();
        }

        protected override void OnPauseRequested()
        {
            if (state != WatermelonMergeState.Running)
            {
                return;
            }

            state = WatermelonMergeState.Paused;
            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            state = WatermelonMergeState.Disposed;
            Shell.ClosePopup();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            var relay = boardRoot != null ? boardRoot.GetComponent<WatermelonMergeInputRelay>() : null;
            if (relay != null)
            {
                relay.Clicked -= OnBoardClicked;
            }

            ClearFruits();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.watermelon_merge.help", null);
        }

        private void BuildContentSection()
        {
            var contentObject = CreateRectObject("WatermelonMergeContent", Shell.ContentHost);
            contentRoot = contentObject.GetComponent<RectTransform>();
            Stretch(contentRoot, Vector2.zero, Vector2.one, new Vector2(24f, 16f), new Vector2(-24f, -16f));

            var surface = EnsureRoundedRectGraphic(contentObject, new Color32(246, 240, 224, 230), 34f, false);
            surface.raycastTarget = false;

            var boardObject = CreateRectObject("WatermelonMergeBoard", contentRoot);
            boardRoot = boardObject.GetComponent<RectTransform>();
            Stretch(boardRoot, Vector2.zero, Vector2.one, new Vector2(24f, 18f), new Vector2(-24f, -24f));
            var boardGraphic = EnsureRoundedRectGraphic(boardObject, new Color32(255, 250, 238, 255), 30f, true);
            boardGraphic.raycastTarget = true;

            var relay = boardObject.AddComponent<WatermelonMergeInputRelay>();
            relay.Clicked += OnBoardClicked;

            var lineObject = CreateRectObject("DangerLine", boardRoot);
            dangerLine = lineObject.GetComponent<RectTransform>();
            dangerLine.anchorMin = new Vector2(0f, 1f);
            dangerLine.anchorMax = new Vector2(1f, 1f);
            dangerLine.pivot = new Vector2(0.5f, 0.5f);
            dangerLine.sizeDelta = new Vector2(0f, 4f);
            dangerLine.anchoredPosition = new Vector2(0f, -138f);
            var dangerGraphic = lineObject.AddComponent<Image>();
            dangerGraphic.color = new Color32(225, 96, 78, 150);
            dangerGraphic.raycastTarget = false;

            var layerObject = CreateRectObject("FruitLayer", boardRoot);
            fruitLayer = layerObject.GetComponent<RectTransform>();
            Stretch(fruitLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildBottomSection()
        {
            var bottomContainerRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("WatermelonMergeBottom"));

            var actionBar = bottomContainerRefs.ActionBar;
            actionBar.sizeDelta = new Vector2(360f, 88f);
            var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 24f;
            }

            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(actionBar).Button;
            restartButton.onClick.AddListener(OnRestartClicked);
            MiniGameSfxPlayer.Attach(restartButton, MiniGameSfxType.UiTap, 0.95f);

            previewRoot = CreatePreview(actionBar);
        }

        private RectTransform CreatePreview(Transform parent)
        {
            var rootObject = CreateRectObject("NextFruitPreview", parent);
            var root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(156f, 76f);

            var layoutElement = rootObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 156f;
            layoutElement.preferredHeight = 76f;

            var background = EnsureRoundedRectGraphic(rootObject, new Color32(255, 246, 226, 255), 24f, false);
            background.raycastTarget = false;

            var labelObject = CreateRectObject("Label", root);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(10f, 0f), new Vector2(-2f, 0f));
            previewLabel = labelObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                previewLabel.font = fontAsset;
            }

            previewLabel.fontSize = 20f;
            previewLabel.fontStyle = FontStyles.Bold;
            previewLabel.alignment = TextAlignmentOptions.Center;
            previewLabel.enableWordWrapping = false;
            previewLabel.raycastTarget = false;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(root, false);
            Stretch(iconRect, new Vector2(0.58f, 0.08f), new Vector2(0.98f, 0.92f), Vector2.zero, Vector2.zero);
            previewIcon = iconObject.GetComponent<Image>();
            previewIcon.preserveAspect = true;
            previewIcon.raycastTarget = false;
            return root;
        }

        private void OnBoardClicked(Vector2 localPosition)
        {
            if (state != WatermelonMergeState.Running || dropCooldown > 0f || fruits.Count >= MaxFruitCount)
            {
                return;
            }

            EnsureBoardLayout();
            var radius = FruitRadii[currentFruitLevel];
            var x = Mathf.Clamp(localPosition.x, boardLeft + radius, boardRight - radius);
            SpawnFruit(x, currentFruitLevel);
            currentFruitLevel = nextFruitLevel;
            nextFruitLevel = RollNextFruitLevel();
            dropCooldown = DropCooldownSeconds;
            RefreshPreview();
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.72f, 1.05f);
        }

        private FruitNode SpawnFruit(float x, int level)
        {
            var node = CreateFruitNode(level);
            node.Position = new Vector2(x, boardTop - node.Radius - 8f);
            node.Velocity = new Vector2(0f, -40f);
            fruits.Add(node);
            RefreshFruitView(node);
            return node;
        }

        private FruitNode CreateFruitNode(int level)
        {
            var rootObject = CreateRectObject("Fruit_" + fruitSequence, fruitLayer);
            var root = rootObject.GetComponent<RectTransform>();
            var size = FruitRadii[level] * 2f;
            root.sizeDelta = new Vector2(size, size);

            var backgroundObject = CreateRectObject("Background", root);
            Stretch(backgroundObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var background = EnsureRoundedRectGraphic(backgroundObject, GetFruitColor(level), FruitRadii[level], false);
            background.raycastTarget = false;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(root, false);
            Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = fruitSprites[level];
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            return new FruitNode
            {
                Id = fruitSequence++,
                Level = level,
                Radius = FruitRadii[level],
                Root = root,
                Icon = icon,
                Position = Vector2.zero,
                Velocity = Vector2.zero,
                IsMerging = false
            };
        }

        private void StepSimulation(float deltaTime)
        {
            for (var i = 0; i < fruits.Count; i++)
            {
                var node = fruits[i];
                node.Velocity += Vector2.down * Gravity * deltaTime;
                node.Velocity *= AirDamping;
                node.Position += node.Velocity * deltaTime;
                ResolveBounds(node);
            }

            for (var iteration = 0; iteration < PositionSolverIterations; iteration++)
            {
                if (ResolveFruitCollisions())
                {
                    break;
                }
            }
        }

        private void ResolveBounds(FruitNode node)
        {
            if (node.Position.x - node.Radius < boardLeft)
            {
                node.Position.x = boardLeft + node.Radius;
                node.Velocity.x = Mathf.Abs(node.Velocity.x) * WallBounce;
            }
            else if (node.Position.x + node.Radius > boardRight)
            {
                node.Position.x = boardRight - node.Radius;
                node.Velocity.x = -Mathf.Abs(node.Velocity.x) * WallBounce;
            }

            if (node.Position.y - node.Radius < boardBottom)
            {
                node.Position.y = boardBottom + node.Radius;
                node.Velocity.y = Mathf.Abs(node.Velocity.y) * WallBounce;
                node.Velocity.x *= GroundFriction;
            }
        }

        private bool ResolveFruitCollisions()
        {
            for (var i = 0; i < fruits.Count; i++)
            {
                for (var j = i + 1; j < fruits.Count; j++)
                {
                    var left = fruits[i];
                    var right = fruits[j];
                    var delta = right.Position - left.Position;
                    var distance = delta.magnitude;
                    var minDistance = left.Radius + right.Radius;
                    if (distance >= minDistance || minDistance <= 0f)
                    {
                        continue;
                    }

                    if (left.Level == right.Level && left.Level < MaxLevel)
                    {
                        MergeFruits(i, j);
                        return true;
                    }

                    var normal = distance > 0.001f ? delta / distance : Vector2.right;
                    var overlap = minDistance - distance;
                    left.Position -= normal * (overlap * 0.5f);
                    right.Position += normal * (overlap * 0.5f);

                    var relativeVelocity = Vector2.Dot(right.Velocity - left.Velocity, normal);
                    if (relativeVelocity < 0f)
                    {
                        var impulse = normal * (-relativeVelocity * FruitBounce);
                        left.Velocity -= impulse;
                        right.Velocity += impulse;
                    }

                    ResolveBounds(left);
                    ResolveBounds(right);
                }
            }

            return false;
        }

        private void MergeFruits(int leftIndex, int rightIndex)
        {
            var left = fruits[leftIndex];
            var right = fruits[rightIndex];
            var newLevel = left.Level + 1;
            var position = (left.Position + right.Position) * 0.5f;
            var velocity = ((left.Velocity + right.Velocity) * 0.5f) + (Vector2.up * 120f);

            RemoveFruitAt(Mathf.Max(leftIndex, rightIndex));
            RemoveFruitAt(Mathf.Min(leftIndex, rightIndex));

            var merged = CreateFruitNode(newLevel);
            merged.Position = position;
            merged.Velocity = velocity;
            fruits.Add(merged);
            ResolveBounds(merged);
            RefreshFruitView(merged);

            score += MergeScores[newLevel];
            coinCount = score;
            if (newLevel == MaxLevel)
            {
                chestCount += 1;
            }

            RefreshHud();
            MiniGameSfxPlayer.Play(newLevel == MaxLevel ? MiniGameSfxType.Combo : MiniGameSfxType.MatchSuccess, 0.88f, 1f);
        }

        private void RemoveFruitAt(int index)
        {
            var node = fruits[index];
            fruits.RemoveAt(index);
            if (node.Root != null)
            {
                UnityEngine.Object.Destroy(node.Root.gameObject);
            }
        }

        private void RefreshFruitViews()
        {
            for (var i = 0; i < fruits.Count; i++)
            {
                RefreshFruitView(fruits[i]);
            }
        }

        private void RefreshFruitView(FruitNode node)
        {
            if (node.Root == null)
            {
                return;
            }

            node.Root.anchoredPosition = node.Position;
            var size = node.Radius * 2f;
            node.Root.sizeDelta = new Vector2(size, size);
        }

        private void CheckOverflow(float deltaTime)
        {
            var overLine = false;
            for (var i = 0; i < fruits.Count; i++)
            {
                var node = fruits[i];
                if (node.Position.y + node.Radius > dangerLineY && Mathf.Abs(node.Velocity.y) < OverflowVelocityLimit)
                {
                    overLine = true;
                    break;
                }
            }

            overflowTimer = overLine ? overflowTimer + deltaTime : Mathf.Max(0f, overflowTimer - (deltaTime * 2f));
            if (overflowTimer >= OverflowGraceSeconds)
            {
                ShowSettlement(false);
            }
        }

        private void ShowSettlement(bool isExit)
        {
            if (state == WatermelonMergeState.Settled)
            {
                return;
            }

            state = WatermelonMergeState.Settled;
            pendingSettlement = new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = UiTextCatalog.Format(
                    isExit ? "watermelon_merge.settlement.exit" : "watermelon_merge.settlement.end",
                    score,
                    coinCount,
                    chestCount)
            };

            if (isExit)
            {
                ShowBackHallRewardSettlementPanel(
                    pendingSettlement,
                    "WatermelonMergeSettlementPanel",
                    new MiniGameSettlementInfoRow(UiTextCatalog.Get("watermelon_merge.settlement.score"), score.ToString()),
                    new MiniGameSettlementInfoRow(UiTextCatalog.Get("watermelon_merge.settlement.fruits"), fruits.Count.ToString()),
                    CompleteSettlement);
            }
            else
            {
                ShowRewardSettlementPanel(
                    pendingSettlement,
                    new MiniGameRewardSettlementPanelParams
                    {
                        RootName = "WatermelonMergeSettlementPanel",
                        Style = MiniGameRewardSettlementPanelStyle.Failure,
                        PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                        Title = UiTextCatalog.Get("watermelon_merge.settlement.failure_title"),
                        PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("watermelon_merge.settlement.score"), score.ToString()),
                        SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("watermelon_merge.settlement.fruits"), fruits.Count.ToString()),
                        RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                        CoinCount = pendingSettlement.CoinCount,
                        ChestCount = pendingSettlement.ChestCount
                    },
                    ResetGame,
                    CompleteSettlement,
                    true);
            }

            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.95f, 1f);
        }

        private void CompleteSettlement()
        {
            Shell.ClosePopup();
            CompleteGame?.Invoke(pendingSettlement ?? CreateCurrentSettlement(true));
        }

        private MiniGameSettlement CreateCurrentSettlement(bool isExit)
        {
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = coinCount,
                ChestCount = chestCount,
                Summary = UiTextCatalog.Format(
                    isExit ? "watermelon_merge.settlement.exit" : "watermelon_merge.settlement.end",
                    score,
                    coinCount,
                    chestCount)
            };
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
            state = WatermelonMergeState.Running;
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            ShowSettlement(true);
        }

        private void OnRestartClicked()
        {
            ResetGame();
        }

        private void RefreshHud()
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.watermelon_merge.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("watermelon_merge.hud.score", score, coinCount, chestCount);
            }
        }

        private void RefreshPreview()
        {
            if (previewLabel != null)
            {
                previewLabel.text = UiTextCatalog.Get("watermelon_merge.hud.next");
            }

            if (previewIcon != null && fruitSprites.Count > currentFruitLevel)
            {
                previewIcon.sprite = fruitSprites[currentFruitLevel];
                previewIcon.color = Color.white;
            }
        }

        private void EnsureBoardLayout()
        {
            if (boardRoot == null)
            {
                return;
            }

            var rect = boardRoot.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            boardWidth = rect.width;
            boardHeight = rect.height;
            boardLeft = (-boardWidth * 0.5f) + 18f;
            boardRight = (boardWidth * 0.5f) - 18f;
            boardBottom = (-boardHeight * 0.5f) + 18f;
            boardTop = (boardHeight * 0.5f) - 18f;
            dangerLineY = boardTop - 138f;
        }

        private void EnsureSpritesLoaded()
        {
            if (fruitSprites.Count > 0)
            {
                return;
            }

            for (var i = 0; i < FruitIconPaths.Length; i++)
            {
                var sprite = Resources.Load<Sprite>(FruitIconPaths[i]);
                if (sprite == null)
                {
                    throw new InvalidOperationException("Missing fruit icon: Resources/" + FruitIconPaths[i]);
                }

                fruitSprites.Add(sprite);
            }
        }

        private void ClearFruits()
        {
            for (var i = 0; i < fruits.Count; i++)
            {
                if (fruits[i].Root != null)
                {
                    UnityEngine.Object.Destroy(fruits[i].Root.gameObject);
                }
            }

            fruits.Clear();
        }

        private static int RollNextFruitLevel()
        {
            return UnityEngine.Random.Range(0, 3);
        }

        private static Color GetFruitColor(int level)
        {
            switch (level)
            {
                case 0:
                    return new Color32(248, 109, 118, 255);
                case 1:
                    return new Color32(153, 103, 201, 255);
                case 2:
                    return new Color32(245, 148, 85, 255);
                case 3:
                    return new Color32(250, 190, 86, 255);
                case 4:
                    return new Color32(246, 159, 118, 255);
                case 5:
                    return new Color32(232, 93, 75, 255);
                case 6:
                    return new Color32(135, 96, 168, 255);
                case 7:
                    return new Color32(239, 204, 77, 255);
                case 8:
                    return new Color32(240, 203, 71, 255);
                case 9:
                    return new Color32(235, 158, 70, 255);
                default:
                    return new Color32(73, 151, 87, 255);
            }
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
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

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private sealed class WatermelonMergeInputRelay : MonoBehaviour, IPointerClickHandler
        {
            public event Action<Vector2> Clicked;

            public void OnPointerClick(PointerEventData eventData)
            {
                var rect = transform as RectTransform;
                if (rect == null)
                {
                    return;
                }

                Vector2 localPoint;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPoint))
                {
                    return;
                }

                Clicked?.Invoke(localPoint);
            }
        }
    }
}
