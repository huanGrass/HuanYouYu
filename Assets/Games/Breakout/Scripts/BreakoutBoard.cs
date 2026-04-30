using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class BreakoutBoard : IDisposable
    {
        private const float DesignBoardWidth = 900f;
        private const float DesignBoardHeight = 1200f;
        private const float WallPadding = 34f;
        private const float TopPadding = 54f;
        private const float BottomPadding = 40f;
        private const float PaddleWidth = 150f;
        private const float PaddleHeight = 22f;
        private const float PaddleY = -470f;
        private const float BallRadius = 14f;
        private const float InitialBallSpeed = 760f;
        private const int BrickRows = 15;
        private const int BrickColumns = 11;
        private const float BrickWidth = 60f;
        private const float BrickHeight = 24f;
        private const float BrickSpacingX = 7f;
        private const float BrickSpacingY = 7f;
        private const float BrickTopY = 400f;
        private const float SubstepDuration = 1f / 120f;
        private const float MinimumBounceHorizontal = 0.2f;
        private const float BrickBurstDuration = 0.24f;
        private const float BrickFlashDuration = 0.16f;
        private const float PaddlePulseDuration = 0.16f;
        private const float BallPulseDuration = 0.14f;
        private const float BoardPulseDuration = 0.28f;
        private const float PowerUpDropChance = 0.14f;
        private const float PowerUpFallSpeed = 260f;
        private const float PowerUpSize = 54f;
        private const int MaxActiveBalls = 9;

        private static readonly Color BoardColor = new Color32(15, 27, 44, 255);
        private static readonly Color BoardBorderColor = new Color32(57, 89, 128, 255);
        private static readonly Color PaddleColor = new Color32(242, 244, 247, 255);
        private static readonly Color BallColor = new Color32(250, 208, 87, 255);
        private static readonly Color BoardPulseColor = new Color32(48, 80, 120, 255);
        private static readonly Color BoardBorderPulseColor = new Color32(173, 221, 255, 255);
        private static readonly Color BrickFlashColor = new Color(1f, 0.97f, 0.86f, 0.95f);
        private static readonly Color BrickDamageColor = new Color(1f, 1f, 1f, 0.62f);
        private static readonly Color SplitPowerUpColor = new Color32(255, 184, 108, 255);
        private static readonly Color ExtraServePowerUpColor = new Color32(72, 211, 221, 255);
        private static readonly Color[] BrickColors =
        {
            new Color32(255, 126, 95, 255),
            new Color32(255, 184, 108, 255),
            new Color32(72, 211, 173, 255),
            new Color32(99, 179, 237, 255),
            new Color32(167, 139, 250, 255)
        };

        private readonly RectTransform root;
        private readonly RectTransform boardRect;
        private readonly RectTransform bricksRoot;
        private readonly RectTransform effectsRoot;
        private readonly RectTransform paddleRect;
        private readonly RectTransform ballRect;
        private readonly List<BrickView> bricks = new List<BrickView>(BrickRows * BrickColumns);
        private readonly List<TransientVisual> transientEffects = new List<TransientVisual>(12);
        private readonly List<PowerUpView> powerUps = new List<PowerUpView>(6);
        private readonly List<BallView> extraBalls = new List<BallView>(MaxActiveBalls - 1);
        private readonly float layoutScale;
        private readonly Image boardImage;
        private readonly Image paddleImage;
        private readonly CircleGraphic ballImage;
        private readonly BoardBorderGraphic borderGraphic;
        private BreakoutLevelDefinition currentLevel;

        private Vector2 ballVelocity;
        private bool ballAttached;
        private float paddleX;
        private float paddlePulseTimer;
        private float ballPulseTimer;
        private float boardPulseTimer;

        public BreakoutBoard(RectTransform parent)
        {
            layoutScale = CalculateLayoutScale(parent);

            root = CreateRect("BreakoutBoardRoot", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            boardRect = CreateRect(
                "Board",
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(Scaled(DesignBoardWidth), Scaled(DesignBoardHeight)));
            boardImage = boardRect.gameObject.AddComponent<Image>();
            boardImage.color = BoardColor;

            var border = CreateRect("Border", boardRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            borderGraphic = border.gameObject.AddComponent<BoardBorderGraphic>();
            borderGraphic.Initialize(BoardBorderColor, Scaled(6f));

            bricksRoot = CreateRect("Bricks", boardRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            effectsRoot = CreateRect("Effects", boardRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            effectsRoot.SetAsLastSibling();

            paddleRect = CreateRect(
                "Paddle",
                boardRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, Scaled(PaddleY)),
                new Vector2(Scaled(PaddleWidth), Scaled(PaddleHeight)));
            paddleImage = paddleRect.gameObject.AddComponent<Image>();
            paddleImage.color = PaddleColor;

            ballRect = CreateRect(
                "Ball",
                boardRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(Scaled(BallRadius * 2f), Scaled(BallRadius * 2f)));
            ballImage = ballRect.gameObject.AddComponent<CircleGraphic>();
            ballImage.color = BallColor;
            ballRect.SetAsLastSibling();

            CreateBricks();
        }

        public event Action BrickBroken;

        public event Action BallLost;

        public event Action BoardCleared;

        public RectTransform BoardRect
        {
            get { return boardRect; }
        }

        internal int ActiveTransientEffectCount
        {
            get { return transientEffects.Count; }
        }

        internal bool IsPaddlePulseActive
        {
            get { return paddlePulseTimer > 0f; }
        }

        internal bool IsBallPulseActive
        {
            get { return ballPulseTimer > 0f; }
        }

        internal bool IsBoardPulseActive
        {
            get { return boardPulseTimer > 0f; }
        }

        internal int ActivePowerUpCount
        {
            get { return powerUps.Count; }
        }

        internal int ActiveBallCount
        {
            get { return ballAttached ? 1 : 1 + extraBalls.Count; }
        }

        public void ResetBoard()
        {
            paddleX = 0f;
            ApplyPaddlePosition();
            ballVelocity = Vector2.zero;
            ballAttached = true;
            paddlePulseTimer = 0f;
            ballPulseTimer = 0f;
            boardPulseTimer = 0f;
            ClearTransientEffects();
            ClearPowerUps();
            ClearExtraBalls();
            ApplyVisualPulseState();
            ApplyCurrentLevel();
            SyncAttachedBall();
        }

        public void SetLevel(BreakoutLevelDefinition level)
        {
            currentLevel = level;
            ApplyCurrentLevel();
        }

        public void Tick(float deltaTime)
        {
            TickVisualEffects(deltaTime);
            if (ballAttached || deltaTime <= 0f)
            {
                return;
            }

            UpdatePowerUps(deltaTime);
            var remaining = deltaTime;
            while (remaining > 0f)
            {
                var step = Mathf.Min(SubstepDuration, remaining);
                remaining -= step;
                SimulateStep(step);
                if (ballAttached)
                {
                    return;
                }
            }
        }

        public void TickVisualEffects(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            UpdateTransientEffects(deltaTime);
            UpdatePulseTimers(deltaTime);
        }

        public void SetPaddlePosition(float boardX)
        {
            paddleX = Mathf.Clamp(boardX, MinPaddleX, MaxPaddleX);
            ApplyPaddlePosition();
            if (ballAttached)
            {
                SyncAttachedBall();
            }
        }

        public void MovePaddle(float deltaX)
        {
            SetPaddlePosition(paddleX + deltaX);
        }

        public void AttachBallToPaddle()
        {
            ballAttached = true;
            ballVelocity = Vector2.zero;
        }

        public void SyncAttachedBall()
        {
            if (!ballAttached)
            {
                return;
            }

            ballRect.anchoredPosition = new Vector2(paddleX, Scaled(PaddleY + (PaddleHeight * 0.5f) + BallRadius + 8f));
        }

        public void LaunchBall()
        {
            if (!ballAttached)
            {
                return;
            }

            ballAttached = false;
            ballVelocity = new Vector2(0.32f, 1f).normalized * Scaled(InitialBallSpeed);
            ClearExtraBalls();
        }

        public void Dispose()
        {
            ClearTransientEffects();
            ClearPowerUps();
            ClearExtraBalls();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        private float MinBallX
        {
            get { return (-Scaled(DesignBoardWidth) * 0.5f) + Scaled(WallPadding) + Scaled(BallRadius); }
        }

        private float MaxBallX
        {
            get { return (Scaled(DesignBoardWidth) * 0.5f) - Scaled(WallPadding) - Scaled(BallRadius); }
        }

        private float MaxBallY
        {
            get { return (Scaled(DesignBoardHeight) * 0.5f) - Scaled(TopPadding) - Scaled(BallRadius); }
        }

        private float MinBallY
        {
            get { return (-Scaled(DesignBoardHeight) * 0.5f) + Scaled(BottomPadding) - Scaled(BallRadius); }
        }

        private float MinPaddleX
        {
            get { return (-Scaled(DesignBoardWidth) * 0.5f) + Scaled(WallPadding) + (Scaled(PaddleWidth) * 0.5f); }
        }

        private float MaxPaddleX
        {
            get { return (Scaled(DesignBoardWidth) * 0.5f) - Scaled(WallPadding) - (Scaled(PaddleWidth) * 0.5f); }
        }

        private void SimulateStep(float deltaTime)
        {
            var primaryLost = SimulateBallStep(ballRect, ref ballVelocity, deltaTime);
            for (var i = extraBalls.Count - 1; i >= 0; i--)
            {
                var extraBall = extraBalls[i];
                var velocity = extraBall.Velocity;
                if (SimulateBallStep(extraBall.Rect, ref velocity, deltaTime))
                {
                    DestroyExtraBallAt(i);
                    continue;
                }

                extraBall.Velocity = velocity;
            }

            if (!primaryLost)
            {
                return;
            }

            if (extraBalls.Count > 0)
            {
                PromoteExtraBallToPrimary();
                return;
            }

            ballAttached = true;
            ballVelocity = Vector2.zero;
            BallLost?.Invoke();
        }

        private bool SimulateBallStep(RectTransform activeBallRect, ref Vector2 activeBallVelocity, float deltaTime)
        {
            var position = activeBallRect.anchoredPosition;
            position += activeBallVelocity * deltaTime;

            if (position.x <= MinBallX)
            {
                position.x = MinBallX;
                activeBallVelocity.x = Mathf.Abs(activeBallVelocity.x);
            }
            else if (position.x >= MaxBallX)
            {
                position.x = MaxBallX;
                activeBallVelocity.x = -Mathf.Abs(activeBallVelocity.x);
            }

            if (position.y >= MaxBallY)
            {
                position.y = MaxBallY;
                activeBallVelocity.y = -Mathf.Abs(activeBallVelocity.y);
            }

            if (activeBallVelocity.y < 0f && CheckPaddleCollisionForBall(ref position, ref activeBallVelocity))
            {
                activeBallRect.anchoredPosition = position;
                return false;
            }

            if (CheckBrickCollisionForBall(ref position, ref activeBallVelocity))
            {
                activeBallRect.anchoredPosition = position;
                return false;
            }

            if (position.y <= MinBallY)
            {
                activeBallRect.anchoredPosition = position;
                return true;
            }

            activeBallRect.anchoredPosition = position;
            return false;
        }

        private bool CheckPaddleCollision(ref Vector2 position)
        {
            return CheckPaddleCollisionForBall(ref position, ref ballVelocity);
        }

        private bool CheckPaddleCollisionForBall(ref Vector2 position, ref Vector2 activeBallVelocity)
        {
            var paddleHalfWidth = GetCurrentPaddleWidth() * 0.5f;
            var paddleLeft = paddleX - paddleHalfWidth - Scaled(BallRadius);
            var paddleRight = paddleX + paddleHalfWidth + Scaled(BallRadius);
            var paddleTop = Scaled(PaddleY + (PaddleHeight * 0.5f) + BallRadius);
            var paddleBottom = Scaled(PaddleY - (PaddleHeight * 0.5f) - BallRadius);
            if (position.x < paddleLeft || position.x > paddleRight || position.y > paddleTop || position.y < paddleBottom)
            {
                return false;
            }

            position.y = Scaled(PaddleY + (PaddleHeight * 0.5f) + BallRadius);
            var hitFactor = Mathf.Clamp((position.x - paddleX) / paddleHalfWidth, -1f, 1f);
            if (Mathf.Abs(hitFactor) < MinimumBounceHorizontal)
            {
                hitFactor = MinimumBounceHorizontal * (hitFactor >= 0f ? 1f : -1f);
            }

            var bounceDirection = new Vector2(hitFactor, 1f).normalized;
            activeBallVelocity = bounceDirection * Mathf.Max(Scaled(InitialBallSpeed), activeBallVelocity.magnitude);
            paddlePulseTimer = PaddlePulseDuration;
            ballPulseTimer = BallPulseDuration;
            ApplyVisualPulseState();
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.85f);
            return true;
        }

        private bool CheckBrickCollision(ref Vector2 position)
        {
            return CheckBrickCollisionForBall(ref position, ref ballVelocity);
        }

        private bool CheckBrickCollisionForBall(ref Vector2 position, ref Vector2 activeBallVelocity)
        {
            for (var i = 0; i < bricks.Count; i++)
            {
                var brick = bricks[i];
                if (!brick.Active)
                {
                    continue;
                }

                var brickCenter = brick.Rect.anchoredPosition;
                var delta = position - brickCenter;
                var brickWidth = Scaled(BrickWidth);
                var brickHeight = Scaled(BrickHeight);
                var ballRadius = Scaled(BallRadius);
                var overlapX = (brickWidth * 0.5f) + ballRadius - Mathf.Abs(delta.x);
                var overlapY = (brickHeight * 0.5f) + ballRadius - Mathf.Abs(delta.y);
                if (overlapX <= 0f || overlapY <= 0f)
                {
                    continue;
                }

                if (overlapX < overlapY)
                {
                    var sign = delta.x >= 0f ? 1f : -1f;
                    position.x = brickCenter.x + sign * ((brickWidth * 0.5f) + ballRadius);
                    activeBallVelocity.x *= -1f;
                }
                else
                {
                    var sign = delta.y >= 0f ? 1f : -1f;
                    position.y = brickCenter.y + sign * ((brickHeight * 0.5f) + ballRadius);
                    activeBallVelocity.y *= -1f;
                }

                if (!brick.ApplyHit())
                {
                    CreateBrickHitEffect(brick);
                    MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.78f);
                    return true;
                }

                CreateBrickBreakEffect(brick);
                TrySpawnPowerUp(brick.Rect.anchoredPosition);
                BrickBroken?.Invoke();
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, 0.9f);

                if (RemainingBrickCount() == 0)
                {
                    boardPulseTimer = BoardPulseDuration;
                    ApplyVisualPulseState();
                    BoardCleared?.Invoke();
                }

                return true;
            }

            return false;
        }

        private int RemainingBrickCount()
        {
            var count = 0;
            for (var i = 0; i < bricks.Count; i++)
            {
                if (bricks[i].Active)
                {
                    count += 1;
                }
            }

            return count;
        }

        private void ApplyCurrentLevel()
        {
            var rows = currentLevel != null ? currentLevel.Rows : null;
            for (var i = 0; i < bricks.Count; i++)
            {
                var row = i / BrickColumns;
                var column = i % BrickColumns;
                var hitPoints = 0;
                var active = rows != null &&
                    row < rows.Length &&
                    !string.IsNullOrEmpty(rows[row]) &&
                    column < rows[row].Length &&
                    rows[row][column] != '0';
                if (active)
                {
                    hitPoints = GetBrickHitPoints(rows[row][column], row, column);
                }

                bricks[i].SetActive(active, hitPoints);
            }
        }

        private static int GetBrickHitPoints(char levelMark, int row, int column)
        {
            if (levelMark >= '2' && levelMark <= '3')
            {
                return levelMark - '0';
            }

            if (row < 3 && column % 2 == 0)
            {
                return 3;
            }

            if (row < 8 && (row + column) % 3 != 0)
            {
                return 2;
            }

            return 1;
        }

        private void ApplyPaddlePosition()
        {
            paddleRect.anchoredPosition = new Vector2(paddleX, Scaled(PaddleY));
            paddleRect.sizeDelta = new Vector2(GetCurrentPaddleWidth(), Scaled(PaddleHeight));
        }

        private void CreateBricks()
        {
            var brickWidth = Scaled(BrickWidth);
            var brickHeight = Scaled(BrickHeight);
            var brickSpacingX = Scaled(BrickSpacingX);
            var brickSpacingY = Scaled(BrickSpacingY);
            var brickTopY = Scaled(BrickTopY);
            var totalWidth = (BrickColumns * brickWidth) + ((BrickColumns - 1) * brickSpacingX);
            var startX = -(totalWidth * 0.5f) + (brickWidth * 0.5f);

            for (var row = 0; row < BrickRows; row++)
            {
                for (var column = 0; column < BrickColumns; column++)
                {
                    var brickRect = CreateRect(
                        "Brick_" + row + "_" + column,
                        bricksRoot,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(
                            startX + (column * (brickWidth + brickSpacingX)),
                            brickTopY - (row * (brickHeight + brickSpacingY))),
                        new Vector2(brickWidth, brickHeight));
                    var image = brickRect.gameObject.AddComponent<Image>();
                    image.color = BrickColors[row % BrickColors.Length];
                    bricks.Add(new BrickView(brickRect, image, image.color));
                }
            }
        }

        private void CreateBrickBreakEffect(BrickView brick)
        {
            if (brick == null || brick.Rect == null)
            {
                return;
            }

            CreateTransientQuad(
                "BreakoutBrickBurst",
                brick.Rect.anchoredPosition,
                brick.Rect.sizeDelta,
                brick.BaseColor,
                BrickBurstDuration,
                new Vector2(1f, 1f),
                new Vector2(1.18f, 0.82f));

            CreateTransientQuad(
                "BreakoutBrickFlash",
                brick.Rect.anchoredPosition,
                new Vector2(brick.Rect.sizeDelta.x * 0.9f, brick.Rect.sizeDelta.y * 0.7f),
                BrickFlashColor,
                BrickFlashDuration,
                new Vector2(0.72f, 0.72f),
                new Vector2(1.2f, 1.2f));
        }

        private void CreateBrickHitEffect(BrickView brick)
        {
            if (brick == null || brick.Rect == null)
            {
                return;
            }

            CreateTransientQuad(
                "BreakoutBrickDamage",
                brick.Rect.anchoredPosition,
                new Vector2(brick.Rect.sizeDelta.x * 0.86f, brick.Rect.sizeDelta.y * 0.66f),
                BrickDamageColor,
                BrickFlashDuration,
                new Vector2(0.82f, 0.82f),
                new Vector2(1.08f, 1.08f));
        }

        private void CreateTransientQuad(
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            float duration,
            Vector2 startScale,
            Vector2 endScale)
        {
            var effectRect = CreateRect(
                name,
                effectsRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                size);
            var image = effectRect.gameObject.AddComponent<Image>();
            image.color = color;
            effectRect.localScale = new Vector3(startScale.x, startScale.y, 1f);
            transientEffects.Add(new TransientVisual(effectRect, image, color, duration, startScale, endScale));
            effectRect.SetAsLastSibling();
            SetBallsAsLastSiblings();
        }

        private void UpdateTransientEffects(float deltaTime)
        {
            for (var i = transientEffects.Count - 1; i >= 0; i--)
            {
                var effect = transientEffects[i];
                effect.Elapsed += deltaTime;
                var progress = Mathf.Clamp01(effect.Duration > 0f ? effect.Elapsed / effect.Duration : 1f);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                effect.Rect.localScale = new Vector3(
                    Mathf.Lerp(effect.StartScale.x, effect.EndScale.x, eased),
                    Mathf.Lerp(effect.StartScale.y, effect.EndScale.y, eased),
                    1f);
                effect.Image.color = new Color(
                    effect.BaseColor.r,
                    effect.BaseColor.g,
                    effect.BaseColor.b,
                    Mathf.Lerp(effect.BaseColor.a, 0f, eased));

                if (progress < 1f)
                {
                    continue;
                }

                if (effect.Rect != null)
                {
                    UnityEngine.Object.Destroy(effect.Rect.gameObject);
                }

                transientEffects.RemoveAt(i);
            }
        }

        private void UpdatePulseTimers(float deltaTime)
        {
            paddlePulseTimer = Mathf.Max(0f, paddlePulseTimer - deltaTime);
            ballPulseTimer = Mathf.Max(0f, ballPulseTimer - deltaTime);
            boardPulseTimer = Mathf.Max(0f, boardPulseTimer - deltaTime);
            ApplyVisualPulseState();
        }

        private void ApplyVisualPulseState()
        {
            var paddlePulse = EvaluatePulse01(paddlePulseTimer, PaddlePulseDuration);
            var ballPulse = EvaluatePulse01(ballPulseTimer, BallPulseDuration);
            var boardPulse = EvaluatePulse01(boardPulseTimer, BoardPulseDuration);

            paddleRect.localScale = new Vector3(
                Mathf.Lerp(1f, 1.1f, paddlePulse),
                Mathf.Lerp(1f, 0.84f, paddlePulse),
                1f);
            paddleImage.color = Color.Lerp(PaddleColor, Color.white, paddlePulse * 0.28f);
            ballRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, ballPulse);
            ballImage.color = Color.Lerp(BallColor, Color.white, ballPulse * 0.72f);
            for (var i = 0; i < extraBalls.Count; i++)
            {
                extraBalls[i].Rect.localScale = ballRect.localScale;
                extraBalls[i].Graphic.color = ballImage.color;
            }

            boardImage.color = Color.Lerp(BoardColor, BoardPulseColor, boardPulse * 0.88f);
            borderGraphic.SetColor(Color.Lerp(BoardBorderColor, BoardBorderPulseColor, boardPulse));
            ApplyPaddlePosition();
        }

        private void TrySpawnPowerUp(Vector2 anchoredPosition)
        {
            if (UnityEngine.Random.value > PowerUpDropChance)
            {
                return;
            }

            SpawnPowerUp(anchoredPosition, (BreakoutPowerUpType)UnityEngine.Random.Range(0, 2));
        }

        private void SpawnPowerUp(Vector2 anchoredPosition, BreakoutPowerUpType type)
        {
            var rect = CreateRect(
                "PowerUp_" + type,
                effectsRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                new Vector2(Scaled(PowerUpSize), Scaled(PowerUpSize)));
            var icon = rect.gameObject.AddComponent<PowerUpIconGraphic>();
            icon.Initialize(type, GetPowerUpColor(type), BallColor, PaddleColor);
            powerUps.Add(new PowerUpView(rect, icon, type));
            rect.SetAsLastSibling();
            SetBallsAsLastSiblings();
        }

        private void UpdatePowerUps(float deltaTime)
        {
            for (var i = powerUps.Count - 1; i >= 0; i--)
            {
                var powerUp = powerUps[i];
                var position = powerUp.Rect.anchoredPosition;
                position.y -= Scaled(PowerUpFallSpeed) * deltaTime;
                powerUp.Rect.anchoredPosition = position;
                powerUp.Rect.Rotate(0f, 0f, 120f * deltaTime);

                if (CheckPowerUpPaddleCollision(powerUp))
                {
                    CollectPowerUp(powerUp);
                    powerUps.RemoveAt(i);
                    continue;
                }

                if (position.y < MinBallY - Scaled(PowerUpSize))
                {
                    if (powerUp.Rect != null)
                    {
                        UnityEngine.Object.Destroy(powerUp.Rect.gameObject);
                    }

                    powerUps.RemoveAt(i);
                }
            }
        }

        private bool CheckPowerUpPaddleCollision(PowerUpView powerUp)
        {
            var position = powerUp.Rect.anchoredPosition;
            var paddleHalfWidth = GetCurrentPaddleWidth() * 0.5f;
            var powerUpHalfSize = Scaled(PowerUpSize) * 0.5f;
            var paddleTop = Scaled(PaddleY + (PaddleHeight * 0.5f)) + powerUpHalfSize;
            var paddleBottom = Scaled(PaddleY - (PaddleHeight * 0.5f)) - powerUpHalfSize;
            return position.x >= paddleX - paddleHalfWidth - powerUpHalfSize &&
                position.x <= paddleX + paddleHalfWidth + powerUpHalfSize &&
                position.y >= paddleBottom &&
                position.y <= paddleTop;
        }

        private void CollectPowerUp(PowerUpView powerUp)
        {
            ApplyPowerUp(powerUp.Type, powerUp.Rect.anchoredPosition);

            CreateTransientQuad(
                "BreakoutPowerUpCollect",
                powerUp.Rect.anchoredPosition,
                powerUp.Rect.sizeDelta,
                powerUp.Graphic.color,
                BrickBurstDuration,
                new Vector2(1f, 1f),
                new Vector2(1.35f, 1.35f));

            UnityEngine.Object.Destroy(powerUp.Rect.gameObject);
            ApplyVisualPulseState();
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 1f);
        }

        private float GetCurrentPaddleWidth()
        {
            return Scaled(PaddleWidth);
        }

        private void ApplyPowerUp(BreakoutPowerUpType type, Vector2 collectPosition)
        {
            if (type == BreakoutPowerUpType.ExtraServeBalls)
            {
                AddServedBalls();
                return;
            }

            SplitCurrentBalls(collectPosition);
        }

        private void SplitCurrentBalls(Vector2 fallbackPosition)
        {
            if (ballAttached)
            {
                AddBall(fallbackPosition, new Vector2(-0.42f, 1f).normalized * Scaled(InitialBallSpeed));
                AddBall(fallbackPosition, new Vector2(0f, 1f).normalized * Scaled(InitialBallSpeed));
                AddBall(fallbackPosition, new Vector2(0.42f, 1f).normalized * Scaled(InitialBallSpeed));
                ballAttached = false;
                return;
            }

            var origin = ballRect.anchoredPosition;
            var direction = ballVelocity;
            var speed = Mathf.Max(Scaled(InitialBallSpeed), ballVelocity.magnitude);
            ballVelocity = RotateDirection(direction, -24f) * speed;
            AddBall(origin, RotateDirection(direction, 0f) * speed);
            AddBall(origin, RotateDirection(direction, 24f) * speed);
            ballPulseTimer = BallPulseDuration;
        }

        private void AddServedBalls()
        {
            var origin = new Vector2(paddleX, Scaled(PaddleY + (PaddleHeight * 0.5f) + BallRadius + 8f));
            AddBall(origin, new Vector2(-0.42f, 1f).normalized * Scaled(InitialBallSpeed));
            AddBall(origin, new Vector2(0f, 1f).normalized * Scaled(InitialBallSpeed));
            AddBall(origin, new Vector2(0.42f, 1f).normalized * Scaled(InitialBallSpeed));
            ballPulseTimer = BallPulseDuration;
        }

        private void AddBall(Vector2 anchoredPosition, Vector2 velocity)
        {
            if (ballAttached)
            {
                ballRect.anchoredPosition = anchoredPosition;
                ballVelocity = velocity;
                ballAttached = false;
                return;
            }

            if (ActiveBallCount >= MaxActiveBalls)
            {
                return;
            }

            var rect = CreateRect(
                "ExtraBall",
                boardRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                new Vector2(Scaled(BallRadius * 2f), Scaled(BallRadius * 2f)));
            var graphic = rect.gameObject.AddComponent<CircleGraphic>();
            graphic.color = BallColor;
            extraBalls.Add(new BallView(rect, graphic, velocity));
            SetBallsAsLastSiblings();
        }

        private void PromoteExtraBallToPrimary()
        {
            var promoted = extraBalls[extraBalls.Count - 1];
            ballRect.anchoredPosition = promoted.Rect.anchoredPosition;
            ballVelocity = promoted.Velocity;
            DestroyExtraBallAt(extraBalls.Count - 1);
        }

        private void DestroyExtraBallAt(int index)
        {
            var extraBall = extraBalls[index];
            if (extraBall.Rect != null)
            {
                UnityEngine.Object.Destroy(extraBall.Rect.gameObject);
            }

            extraBalls.RemoveAt(index);
        }

        private void SetBallsAsLastSiblings()
        {
            for (var i = 0; i < extraBalls.Count; i++)
            {
                extraBalls[i].Rect.SetAsLastSibling();
            }

            ballRect.SetAsLastSibling();
        }

        private static Vector2 RotateDirection(Vector2 velocity, float degrees)
        {
            var direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector2.up;
            var radians = degrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector2(
                (direction.x * cos) - (direction.y * sin),
                (direction.x * sin) + (direction.y * cos)).normalized;
        }

        private static Color GetPowerUpColor(BreakoutPowerUpType type)
        {
            switch (type)
            {
                case BreakoutPowerUpType.ExtraServeBalls:
                    return ExtraServePowerUpColor;
                default:
                    return SplitPowerUpColor;
            }
        }

        private void ClearTransientEffects()
        {
            for (var i = 0; i < transientEffects.Count; i++)
            {
                if (transientEffects[i].Rect != null)
                {
                    UnityEngine.Object.Destroy(transientEffects[i].Rect.gameObject);
                }
            }

            transientEffects.Clear();
        }

        private void ClearPowerUps()
        {
            for (var i = 0; i < powerUps.Count; i++)
            {
                if (powerUps[i].Rect != null)
                {
                    UnityEngine.Object.Destroy(powerUps[i].Rect.gameObject);
                }
            }

            powerUps.Clear();
        }

        private void ClearExtraBalls()
        {
            for (var i = 0; i < extraBalls.Count; i++)
            {
                if (extraBalls[i].Rect != null)
                {
                    UnityEngine.Object.Destroy(extraBalls[i].Rect.gameObject);
                }
            }

            extraBalls.Clear();
        }

        private static float EvaluatePulse01(float timer, float duration)
        {
            if (timer <= 0f || duration <= 0f)
            {
                return 0f;
            }

            var normalized = 1f - Mathf.Clamp01(timer / duration);
            return Mathf.Sin(normalized * Mathf.PI);
        }

        private float Scaled(float value)
        {
            return value * layoutScale;
        }

        private static float CalculateLayoutScale(RectTransform parent)
        {
            if (parent == null)
            {
                return 0.68f;
            }

            var rect = parent.rect;
            if (rect.width <= 0.01f || rect.height <= 0.01f)
            {
                return 0.68f;
            }

            return Mathf.Min(Mathf.Min(rect.width / DesignBoardWidth, rect.height / DesignBoardHeight), 0.72f);
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private sealed class BrickView
        {
            public BrickView(RectTransform rect, Image image, Color baseColor)
            {
                Rect = rect;
                Image = image;
                BaseColor = baseColor;
                Active = true;
                MaxHitPoints = 1;
                HitPoints = 1;
            }

            public RectTransform Rect { get; }

            public Image Image { get; }

            public Color BaseColor { get; }

            public bool Active { get; private set; }

            public int HitPoints { get; private set; }

            public int MaxHitPoints { get; private set; }

            public void SetActive(bool active, int hitPoints)
            {
                Active = active;
                MaxHitPoints = Mathf.Max(1, hitPoints);
                HitPoints = active ? MaxHitPoints : 0;
                Rect.gameObject.SetActive(active);
                ApplyColor();
            }

            public bool ApplyHit()
            {
                if (!Active)
                {
                    return false;
                }

                HitPoints = Mathf.Max(0, HitPoints - 1);
                if (HitPoints > 0)
                {
                    ApplyColor();
                    return false;
                }

                Active = false;
                Rect.gameObject.SetActive(false);
                return true;
            }

            private void ApplyColor()
            {
                if (!Active)
                {
                    Image.color = BaseColor;
                    return;
                }

                var strength = Mathf.Clamp01((MaxHitPoints - 1) / 2f);
                var damage = Mathf.Clamp01((MaxHitPoints - HitPoints) / 2f);
                var toughColor = Color.Lerp(BaseColor, Color.white, strength * 0.28f);
                Image.color = Color.Lerp(toughColor, BaseColor, damage * 0.72f);
            }
        }

        private sealed class TransientVisual
        {
            public TransientVisual(
                RectTransform rect,
                Image image,
                Color baseColor,
                float duration,
                Vector2 startScale,
                Vector2 endScale)
            {
                Rect = rect;
                Image = image;
                BaseColor = baseColor;
                Duration = duration;
                StartScale = startScale;
                EndScale = endScale;
            }

            public RectTransform Rect { get; }

            public Image Image { get; }

            public Color BaseColor { get; }

            public float Duration { get; }

            public Vector2 StartScale { get; }

            public Vector2 EndScale { get; }

            public float Elapsed { get; set; }
        }

        private sealed class PowerUpView
        {
            public PowerUpView(RectTransform rect, PowerUpIconGraphic graphic, BreakoutPowerUpType type)
            {
                Rect = rect;
                Graphic = graphic;
                Type = type;
            }

            public RectTransform Rect { get; }

            public PowerUpIconGraphic Graphic { get; }

            public BreakoutPowerUpType Type { get; }
        }

        private sealed class BallView
        {
            public BallView(RectTransform rect, CircleGraphic graphic, Vector2 velocity)
            {
                Rect = rect;
                Graphic = graphic;
                Velocity = velocity;
            }

            public RectTransform Rect { get; }

            public CircleGraphic Graphic { get; }

            public Vector2 Velocity { get; set; }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class CircleGraphic : MaskableGraphic
        {
            private const int SegmentCount = 28;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
                var center = rect.center;
                vh.AddVert(center, color, new Vector2(0.5f, 0.5f));
                for (var i = 0; i <= SegmentCount; i++)
                {
                    var angle = (Mathf.PI * 2f * i) / SegmentCount;
                    var point = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    vh.AddVert(point, color, Vector2.zero);
                    if (i > 0)
                    {
                        vh.AddTriangle(0, i, i + 1);
                    }
                }
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class PowerUpIconGraphic : MaskableGraphic
        {
            private BreakoutPowerUpType type;
            private Color ballColor;
            private Color paddleColor;

            public void Initialize(BreakoutPowerUpType powerUpType, Color backgroundColor, Color iconBallColor, Color iconPaddleColor)
            {
                type = powerUpType;
                color = backgroundColor;
                ballColor = iconBallColor;
                paddleColor = iconPaddleColor;
                SetAllDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
                AddCircle(vh, rect.center, radius, color, 28);

                if (type == BreakoutPowerUpType.ExtraServeBalls)
                {
                    AddQuad(vh, new Rect(rect.center.x - (radius * 0.52f), rect.yMin + (radius * 0.32f), radius * 1.04f, radius * 0.22f), paddleColor);
                    AddCircle(vh, rect.center + new Vector2(-radius * 0.42f, radius * 0.18f), radius * 0.18f, ballColor, 18);
                    AddCircle(vh, rect.center + new Vector2(0f, radius * 0.42f), radius * 0.18f, ballColor, 18);
                    AddCircle(vh, rect.center + new Vector2(radius * 0.42f, radius * 0.18f), radius * 0.18f, ballColor, 18);
                    return;
                }

                AddCircle(vh, rect.center, radius * 0.18f, ballColor, 18);
                AddCircle(vh, rect.center + new Vector2(-radius * 0.42f, radius * 0.34f), radius * 0.18f, ballColor, 18);
                AddCircle(vh, rect.center + new Vector2(radius * 0.42f, radius * 0.34f), radius * 0.18f, ballColor, 18);
                AddQuad(vh, new Rect(rect.center.x - (radius * 0.48f), rect.center.y + (radius * 0.09f), radius * 0.4f, radius * 0.08f), ballColor);
                AddQuad(vh, new Rect(rect.center.x + (radius * 0.08f), rect.center.y + (radius * 0.09f), radius * 0.4f, radius * 0.08f), ballColor);
            }

            private static void AddCircle(VertexHelper vh, Vector2 center, float radius, Color circleColor, int segments)
            {
                var startIndex = vh.currentVertCount;
                vh.AddVert(center, circleColor, new Vector2(0.5f, 0.5f));
                for (var i = 0; i <= segments; i++)
                {
                    var angle = (Mathf.PI * 2f * i) / segments;
                    var point = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    vh.AddVert(point, circleColor, Vector2.zero);
                    if (i > 0)
                    {
                        vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
                    }
                }
            }

            private static void AddQuad(VertexHelper vh, Rect rect, Color quadColor)
            {
                var startIndex = vh.currentVertCount;
                vh.AddVert(new Vector3(rect.xMin, rect.yMin), quadColor, Vector2.zero);
                vh.AddVert(new Vector3(rect.xMin, rect.yMax), quadColor, Vector2.up);
                vh.AddVert(new Vector3(rect.xMax, rect.yMax), quadColor, Vector2.one);
                vh.AddVert(new Vector3(rect.xMax, rect.yMin), quadColor, Vector2.right);
                vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
            }
        }

        [RequireComponent(typeof(CanvasRenderer))]
        private sealed class BoardBorderGraphic : MaskableGraphic
        {
            private Color borderColor;
            private float borderThickness;

            public void Initialize(Color color, float thickness)
            {
                borderColor = color;
                borderThickness = thickness;
                SetAllDirty();
            }

            public void SetColor(Color color)
            {
                if (borderColor == color)
                {
                    return;
                }

                borderColor = color;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                var rect = rectTransform.rect;
                var thickness = Mathf.Clamp(borderThickness, 1f, Mathf.Min(rect.width, rect.height) * 0.5f);
                AddQuad(vh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), borderColor);
                AddQuad(vh, new Rect(rect.xMin, rect.yMin, rect.width, thickness), borderColor);
                AddQuad(vh, new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - (thickness * 2f)), borderColor);
                AddQuad(vh, new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - (thickness * 2f)), borderColor);
            }

            private static void AddQuad(VertexHelper vh, Rect rect, Color color)
            {
                var startIndex = vh.currentVertCount;
                vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
                vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
                vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
                vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
                vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
            }
        }
    }
}
