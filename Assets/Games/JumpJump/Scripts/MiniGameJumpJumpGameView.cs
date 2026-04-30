using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class MiniGameJumpJumpGameView : MiniGameBase
    {
        public const string GameIdConstant = "jumpjump";

        private const float MinChargeDuration = 0.08f;
        private const float MaxChargeDuration = 1.45f;
        private const float MinJumpDistance = 2.2f;
        private const float MaxJumpDistance = 6.4f;
        private const float JumpDuration = 0.64f;
        private const float JumpHeight = 3.8f;
        private const float FallAcceleration = 38f;
        private const float StartFailVelocity = -1.2f;
        private const float FailureY = -8.5f;
        private const float CameraFollowSharpness = 6.2f;
        private const float CameraFieldOfView = 46f;
        private const float CameraFocusHeight = 0.85f;
        private static readonly Vector3 CameraFollowOffset = new Vector3(-11.6f, 13.8f, -11.6f);
        private const float PlatformHalfWidth = 1.25f;
        private const float PlatformHeight = 0.9f;
        private const float PlatformHalfDepth = 1.25f;
        private const float MinVisiblePlatformCenterDistance = 3.15f;
        private const float LandingEdgeTolerance = 0.18f;
        private const float PlayerScaleX = 0.58f;
        private const float PlayerScaleY = 0.82f;
        private const float PlayerScaleZ = 0.58f;
        private const float PlayerShadowBaseScale = 0.54f;
        private const int PlayerJumpRollTurns = 2;
        private const int CoinCountPerPlatform = 5;
        private const int ChestPlatformStep = 20;
        private const float PlayerJumpRollDegrees = 360f * PlayerJumpRollTurns;

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private Button restartButton;
        private Camera worldCamera;
        private bool ownsCamera;
        private JumpJumpWorld world;
        private JumpJumpRunState runState;
        private JumpJumpPlatformState currentPlatform;
        private JumpJumpPlatformState targetPlatform;
        private Vector3 cameraTargetPosition;
        private Quaternion cameraTargetRotation;
        private bool settlementPopupShown;

        public MiniGameJumpJumpGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "MiniGameJumpJumpView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || world == null || runState == null)
            {
                return;
            }

            HandlePointerInput(deltaTime);
            UpdateJump(deltaTime);
            UpdateFailing(deltaTime);
            UpdateCamera(deltaTime);
            world.RemovePastPlatformsOutsideView(worldCamera);
            ApplyVisualState();
        }

        protected override void BuildOrBindSections()
        {
            Shell.SetBackgroundVisible(false);

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("JumpJumpHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            var bottomRefs = MiniGameShellBottomBarBuilder.CreateBottomContainer(
                Shell.BottomHost,
                MiniGameShellBottomBarBuilder.CreateDefaultContainerConfig("JumpJumpActions"));
            restartButton = MiniGameShellBottomBarBuilder.CreateRestartButton(bottomRefs.ActionBar).Button;
            restartButton.gameObject.name = "RestartButton";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);

            EnsureWorldCamera();
            world = JumpJumpWorld.Create();

            if (titleLabel == null || scoreLabel == null || restartButton == null || world == null || worldCamera == null)
            {
                throw new InvalidOperationException("JumpJump 3D runtime structure is incomplete.");
            }
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            settlementPopupShown = false;

            runState = new JumpJumpRunState();
            currentPlatform = new JumpJumpPlatformState(Vector3.zero, Vector3.one);
            targetPlatform = new JumpJumpPlatformState(CreateNextPlatformPosition(currentPlatform.Position, true, 0), GetNextPlatformScale(0));
            runState.PlayerGroundPosition = GetPlayerStandingPosition(currentPlatform);
            runState.PlayerWorldPosition = runState.PlayerGroundPosition;
            runState.PlayerForward = (targetPlatform.Position - currentPlatform.Position).normalized;
            if (runState.PlayerForward.sqrMagnitude <= 0.0001f)
            {
                runState.PlayerForward = new Vector3(1f, 0f, 0.72f).normalized;
            }
            world.ClearPastPlatforms();

            ApplyPlatformTransforms();
            BuildCameraPose(instant: true);
            RefreshHud();
            ApplyVisualState();
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.jumpjump.help", null);
        }

        protected override void OnPauseRequested()
        {
            if (settlementPopupShown)
            {
                return;
            }

            Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (world != null)
            {
                world.Dispose();
                world = null;
            }

            if (worldCamera != null)
            {
                if (ownsCamera)
                {
                    UnityEngine.Object.Destroy(worldCamera.gameObject);
                }
                else
                {
                    worldCamera.clearFlags = CameraClearFlags.SolidColor;
                    worldCamera.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
                    worldCamera.orthographic = true;
                    worldCamera.orthographicSize = 5f;
                    worldCamera.transform.position = new Vector3(0f, 0f, -10f);
                    worldCamera.transform.rotation = Quaternion.identity;
                }
            }
        }

        private void EnsureWorldCamera()
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                var cameraObject = new GameObject("JumpJumpWorldCamera", typeof(Camera), typeof(AudioListener));
                worldCamera = cameraObject.GetComponent<Camera>();
                worldCamera.tag = "MainCamera";
                ownsCamera = true;
            }
            else
            {
                ownsCamera = false;
                worldCamera.name = "Main Camera";
            }

            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color32(214, 233, 249, 255);
            worldCamera.orthographic = false;
            worldCamera.fieldOfView = CameraFieldOfView;
            worldCamera.nearClipPlane = 0.1f;
            worldCamera.farClipPlane = 200f;
            worldCamera.allowMSAA = false;
            worldCamera.allowHDR = true;
        }

        private void HandlePointerInput(float deltaTime)
        {
            if (runState.IsJumping || runState.IsFailing || HasBlockingPopup())
            {
                if (runState.IsCharging)
                {
                    ReleaseCharge();
                }
                return;
            }

            if (TryGetPrimaryPointerDown(out _) && !IsPointerOverUi())
            {
                runState.IsCharging = true;
                runState.ChargeDuration = 0f;
            }

            if (runState.IsCharging)
            {
                runState.ChargeDuration += deltaTime;
            }

            if (runState.IsCharging && TryGetPrimaryPointerUp(out _))
            {
                ReleaseCharge();
            }
        }

        private void ReleaseCharge()
        {
            var chargeRatio = GetNormalizedCharge(runState.ChargeDuration);
            runState.IsCharging = false;
            runState.ChargeDuration = 0f;
            StartJump(chargeRatio);
        }

        private void StartJump(float chargeRatio)
        {
            runState.IsJumping = true;
            runState.JumpElapsed = 0f;
            runState.JumpDistance = Mathf.Lerp(MinJumpDistance, MaxJumpDistance, Mathf.Clamp01(chargeRatio));
            runState.JumpStartPosition = runState.PlayerGroundPosition;
            runState.PlayerGroundPosition = runState.JumpStartPosition;
            runState.PlayerWorldPosition = runState.JumpStartPosition;
            runState.PlayerForward = (targetPlatform.Position - currentPlatform.Position);
            runState.PlayerForward.y = 0f;
            if (runState.PlayerForward.sqrMagnitude <= 0.0001f)
            {
                runState.PlayerForward = new Vector3(1f, 0f, 0.72f);
            }
            runState.PlayerForward.Normalize();
        }

        private void UpdateJump(float deltaTime)
        {
            if (!runState.IsJumping)
            {
                return;
            }

            runState.JumpElapsed += deltaTime;
            var progress = Mathf.Clamp01(runState.JumpElapsed / JumpDuration);
            var horizontalPosition = runState.JumpStartPosition + runState.PlayerForward * (runState.JumpDistance * progress);
            var height = Mathf.Sin(progress * Mathf.PI) * JumpHeight;
            runState.PlayerGroundPosition = horizontalPosition;
            runState.PlayerWorldPosition = horizontalPosition + Vector3.up * height;

            if (runState.JumpElapsed < JumpDuration)
            {
                BuildCameraPose(false);
                return;
            }

            runState.IsJumping = false;
            runState.PlayerWorldPosition = horizontalPosition;
            runState.PlayerGroundPosition = horizontalPosition;

            if (CheckLanding(horizontalPosition))
            {
                HandleSuccessfulLanding();
                return;
            }

            BeginFailure();
        }

        private bool CheckLanding(Vector3 landingPosition)
        {
            var localOffset = landingPosition - GetPlayerStandingPosition(targetPlatform);
            var halfWidth = targetPlatform.Scale.x * 0.52f + LandingEdgeTolerance;
            var halfDepth = targetPlatform.Scale.z * 0.52f + LandingEdgeTolerance;
            return Mathf.Abs(localOffset.x) <= halfWidth &&
                Mathf.Abs(localOffset.z) <= halfDepth;
        }

        private void HandleSuccessfulLanding()
        {
            world.AddPastPlatform(currentPlatform);
            currentPlatform = targetPlatform;
            runState.Score += 1;
            targetPlatform = new JumpJumpPlatformState(
                CreateNextPlatformPosition(currentPlatform.Position, false, runState.Score),
                GetNextPlatformScale(runState.Score));
            runState.PlayerForward = (targetPlatform.Position - currentPlatform.Position).normalized;

            ApplyPlatformTransforms();
            BuildCameraPose(false);
            RefreshHud();
        }

        private void BeginFailure()
        {
            runState.IsFailing = true;
            runState.FallVelocity = StartFailVelocity;
            BuildCameraPose(false);
        }

        private void UpdateFailing(float deltaTime)
        {
            if (!runState.IsFailing)
            {
                return;
            }

            runState.FallVelocity -= FallAcceleration * deltaTime;
            runState.PlayerWorldPosition += Vector3.up * (runState.FallVelocity * deltaTime);
            if (!settlementPopupShown && runState.PlayerWorldPosition.y <= FailureY)
            {
                ShowSettlementPopup();
            }
        }

        private void ShowSettlementPopup()
        {
            settlementPopupShown = true;
            var settlement = BuildSettlement();

            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "JumpJumpSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Failure,
                    PrimaryAction = MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("jumpjump.settlement.failure_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("jumpjump.settlement.platforms"), runState.Score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("jumpjump.settlement.best"), runState.Score.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                ResetGame,
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private static int GetCoinCount(int platformCount)
        {
            return Mathf.Max(0, platformCount) * CoinCountPerPlatform;
        }

        private static int GetChestCount(int platformCount)
        {
            return Mathf.Max(0, platformCount) / ChestPlatformStep;
        }

        private MiniGameSettlement BuildSettlement()
        {
            return new MiniGameSettlement
            {
                Score = runState.Score,
                CoinCount = GetCoinCount(runState.Score),
                ChestCount = GetChestCount(runState.Score),
                Summary = UiTextCatalog.Format(
                    "jumpjump.settlement.summary",
                    runState.Score,
                    GetCoinCount(runState.Score),
                    GetChestCount(runState.Score))
            };
        }

        private void BuildCameraPose(bool instant)
        {
            var focus = Vector3.Lerp(currentPlatform.Position, targetPlatform.Position, 0.5f);
            focus.y = CameraFocusHeight;

            cameraTargetPosition = focus + CameraFollowOffset;
            cameraTargetRotation = Quaternion.LookRotation(focus - cameraTargetPosition, Vector3.up);

            if (instant)
            {
                worldCamera.transform.SetPositionAndRotation(cameraTargetPosition, cameraTargetRotation);
            }
        }

        private void UpdateCamera(float deltaTime)
        {
            if (worldCamera == null)
            {
                return;
            }

            worldCamera.transform.position = Vector3.Lerp(
                worldCamera.transform.position,
                cameraTargetPosition,
                1f - Mathf.Exp(-CameraFollowSharpness * deltaTime));
            worldCamera.transform.rotation = Quaternion.Slerp(
                worldCamera.transform.rotation,
                cameraTargetRotation,
                1f - Mathf.Exp(-CameraFollowSharpness * deltaTime));
        }

        private void ApplyPlatformTransforms()
        {
            world.CurrentPlatform.SetPlatform(currentPlatform.Position, currentPlatform.Scale);
            world.TargetPlatform.SetPlatform(targetPlatform.Position, targetPlatform.Scale);
            world.CurrentPlatform.SetPalette(new Color32(240, 236, 226, 255), new Color32(185, 173, 153, 255));
            world.TargetPlatform.SetPalette(new Color32(255, 212, 155, 255), new Color32(226, 153, 89, 255));

            var focus = (currentPlatform.Position + targetPlatform.Position) * 0.5f;
            focus.y = -1.6f;
            world.BackPlane.transform.position = focus + new Vector3(0f, -3.5f, 0f);
        }

        private void ApplyVisualState()
        {
            if (world == null || runState == null)
            {
                return;
            }

            var chargeRatio = runState.IsCharging ? GetNormalizedCharge(runState.ChargeDuration) : 0f;
            var squashY = Mathf.Lerp(1f, 0.72f, chargeRatio);
            var squashXZ = Mathf.Lerp(1f, 1.16f, chargeRatio);
            var playerScale = new Vector3(PlayerScaleX * squashXZ, PlayerScaleY * squashY, PlayerScaleZ * squashXZ);
            var playerPosition = runState.PlayerWorldPosition;
            if (!runState.IsJumping && !runState.IsFailing)
            {
                playerPosition.y += playerScale.y - PlayerScaleY;
            }

            world.Player.transform.position = playerPosition;
            world.Player.transform.localScale = playerScale;

            var lookDirection = runState.PlayerForward.sqrMagnitude > 0.0001f ? runState.PlayerForward : new Vector3(1f, 0f, 0.72f);
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                var playerRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                if (runState.IsJumping)
                {
                    var jumpProgress = Mathf.Clamp01(runState.JumpElapsed / JumpDuration);
                    playerRotation *= Quaternion.AngleAxis(PlayerJumpRollDegrees * jumpProgress, Vector3.right);
                }

                world.Player.transform.rotation = playerRotation;
            }

            var shadowPosition = runState.PlayerGroundPosition;
            shadowPosition.y = 0.08f;
            world.PlayerShadow.transform.position = shadowPosition;
            var shadowScale = Mathf.Lerp(0.98f, 0.72f, Mathf.Clamp01(runState.PlayerWorldPosition.y / JumpHeight));
            world.PlayerShadow.transform.localScale = new Vector3(
                PlayerShadowBaseScale * shadowScale,
                1f,
                PlayerShadowBaseScale * shadowScale);
        }

        private void RefreshHud()
        {
            titleLabel.text = UiTextCatalog.GetOrFallback("game.jumpjump.name", "跳一跳");
            scoreLabel.text = UiTextCatalog.Format("jumpjump.hud.score", runState != null ? runState.Score : 0);
        }

        private Vector3 CreateNextPlatformPosition(Vector3 from, bool first, int score)
        {
            var previousForward = runState != null ? runState.PlayerForward : new Vector3(1f, 0f, 0.72f).normalized;
            return CreateNextPlatformPosition(from, first, score, previousForward);
        }

        private Vector3 CreateNextPlatformPosition(Vector3 from, bool first, int score, Vector3 previousForward)
        {
            previousForward.y = 0f;
            if (previousForward.sqrMagnitude <= 0.0001f)
            {
                previousForward = new Vector3(1f, 0f, 0.72f);
            }
            previousForward.Normalize();

            var direction = first
                ? new Vector3(1f, 0f, 0.72f).normalized
                : new Vector3(previousForward.z, 0f, -previousForward.x).normalized;
            var startsPositive = UnityEngine.Random.value > 0.5f;
            var maxDistance = 4.6f + Mathf.Min(1.35f, score * 0.09f);
            var lastCandidate = from + direction * UnityEngine.Random.Range(3.65f, maxDistance);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var signedDirection = direction;
                if (!first && (attempt % 2 == 0) != startsPositive)
                {
                    signedDirection = -signedDirection;
                }

                var extraDistance = Mathf.Floor(attempt * 0.5f) * 0.35f;
                var distance = UnityEngine.Random.Range(3.65f, maxDistance) + extraDistance;
                var candidate = from + signedDirection * distance;
                lastCandidate = candidate;
                if (first || IsVisiblePlatformPositionClear(candidate))
                {
                    return candidate;
                }
            }

            return lastCandidate;
        }

        private bool IsVisiblePlatformPositionClear(Vector3 candidate)
        {
            if (IsPlatformTooClose(candidate, currentPlatform) || IsPlatformTooClose(candidate, targetPlatform))
            {
                return false;
            }

            return world == null || !world.IsTooCloseToPastPlatform(candidate, MinVisiblePlatformCenterDistance);
        }

        private static bool IsPlatformTooClose(Vector3 candidate, JumpJumpPlatformState platform)
        {
            if (platform == null)
            {
                return false;
            }

            var delta = candidate - platform.Position;
            delta.y = 0f;
            return delta.sqrMagnitude < MinVisiblePlatformCenterDistance * MinVisiblePlatformCenterDistance;
        }

        private static Vector3 GetNextPlatformScale(int score)
        {
            var width = UnityEngine.Random.Range(1.1f, Mathf.Max(1.5f, 2.15f - score * 0.012f));
            var depth = UnityEngine.Random.Range(1.1f, Mathf.Max(1.5f, 2.15f - score * 0.012f));
            return new Vector3(width, PlatformHeight, depth);
        }

        private static Vector3 GetPlayerStandingPosition(JumpJumpPlatformState platform)
        {
            return platform.Position + Vector3.up * (platform.Scale.y * 0.5f + PlayerScaleY);
        }

        private bool HasBlockingPopup()
        {
            return Shell.PopupHost != null && Shell.PopupHost.childCount > 0;
        }

        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (Input.touchCount > 0)
            {
                return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            settlementPopupShown = true;
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 0.92f);
            var settlement = BuildSettlement();
            ShowBackHallRewardSettlementPanel(
                settlement,
                "JumpJumpSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("jumpjump.settlement.platforms"), runState.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("jumpjump.settlement.best"), runState.Score.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private float DebugGetIdealChargeNormalized()
        {
            var distance = Vector3.Distance(currentPlatform.Position, targetPlatform.Position);
            return Mathf.InverseLerp(MinJumpDistance, MaxJumpDistance, distance);
        }

        private void DebugSimulateJump(float normalizedCharge)
        {
            if (runState == null || runState.IsJumping || runState.IsFailing)
            {
                return;
            }

            StartJump(Mathf.Clamp01(normalizedCharge));
        }

        private int DebugGetScore()
        {
            return runState != null ? runState.Score : 0;
        }

        private bool DebugIsFailing()
        {
            return runState != null && runState.IsFailing;
        }

        private float DebugGetPlayerJumpRollDegrees()
        {
            return PlayerJumpRollDegrees;
        }

        private Vector3 DebugGetPlayerForward()
        {
            return runState != null ? runState.PlayerForward : Vector3.forward;
        }

        private static bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            screenPosition = default;
            return false;
        }

        private static bool TryGetPrimaryPointerUp(out Vector2 screenPosition)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }

            screenPosition = default;
            return false;
        }

        private float GetNormalizedCharge(float chargeDuration)
        {
            return Mathf.Clamp01((chargeDuration - MinChargeDuration) / (MaxChargeDuration - MinChargeDuration));
        }

        private sealed class JumpJumpRunState
        {
            public int Score;
            public bool IsCharging;
            public bool IsJumping;
            public bool IsFailing;
            public float ChargeDuration;
            public float JumpElapsed;
            public float JumpDistance;
            public float FallVelocity;
            public Vector3 JumpStartPosition;
            public Vector3 PlayerForward;
            public Vector3 PlayerGroundPosition;
            public Vector3 PlayerWorldPosition;
        }

        private sealed class JumpJumpPlatformState
        {
            public JumpJumpPlatformState(Vector3 position, Vector3 scale)
            {
                Position = position;
                Scale = scale;
            }

            public Vector3 Position;
            public Vector3 Scale;
        }

        private sealed class JumpJumpWorld : IDisposable
        {
            private const string ColorShaderResourcePath = "Shaders/JumpJumpUnlitColor";
            private const string ColorShaderName = "HuanYouYu/JumpJump/UnlitColor";

            private JumpJumpWorld(
                GameObject root,
                PlatformVisual currentPlatform,
                PlatformVisual targetPlatform,
                GameObject player,
                GameObject playerShadow,
                GameObject backPlane,
                Light keyLight)
            {
                Root = root;
                CurrentPlatform = currentPlatform;
                TargetPlatform = targetPlatform;
                Player = player;
                PlayerShadow = playerShadow;
                BackPlane = backPlane;
                KeyLight = keyLight;
                PastPlatforms = new List<PlatformVisual>();
            }

            public GameObject Root { get; }

            public PlatformVisual CurrentPlatform { get; }

            public PlatformVisual TargetPlatform { get; }

            public List<PlatformVisual> PastPlatforms { get; }

            public GameObject Player { get; }

            public GameObject PlayerShadow { get; }

            public GameObject BackPlane { get; }

            public Light KeyLight { get; }

            public static JumpJumpWorld Create()
            {
                var root = new GameObject("JumpJumpWorldRoot");

                var backPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                backPlane.name = "BackPlane";
                backPlane.transform.SetParent(root.transform, false);
                backPlane.transform.localScale = new Vector3(2.7f, 1f, 2.7f);
                AssignColorMaterial(backPlane.GetComponent<Renderer>(), new Color32(228, 242, 255, 255));
                UnityEngine.Object.Destroy(backPlane.GetComponent<Collider>());

                var currentPlatform = PlatformVisual.Create("CurrentPlatform", root.transform);
                var targetPlatform = PlatformVisual.Create("TargetPlatform", root.transform);

                var player = new GameObject("Player");
                player.name = "Player";
                player.transform.SetParent(root.transform, false);

                var playerBody = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                playerBody.name = "PlayerBody";
                playerBody.transform.SetParent(player.transform, false);
                playerBody.transform.localPosition = new Vector3(0f, -0.48f, 0f);
                playerBody.transform.localScale = new Vector3(0.78f, 1.04f, 0.78f);
                AssignColorMaterial(playerBody.GetComponent<Renderer>(), new Color32(54, 65, 86, 255));
                UnityEngine.Object.Destroy(playerBody.GetComponent<Collider>());

                var playerHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                playerHead.name = "PlayerHead";
                playerHead.transform.SetParent(player.transform, false);
                playerHead.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                playerHead.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
                AssignColorMaterial(playerHead.GetComponent<Renderer>(), new Color32(255, 168, 94, 255));
                UnityEngine.Object.Destroy(playerHead.GetComponent<Collider>());

                var playerMark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                playerMark.name = "PlayerMark";
                playerMark.transform.SetParent(player.transform, false);
                playerMark.transform.localPosition = new Vector3(0f, 0.32f, 0.31f);
                playerMark.transform.localScale = new Vector3(0.22f, 0.13f, 0.08f);
                AssignColorMaterial(playerMark.GetComponent<Renderer>(), new Color32(255, 236, 190, 255));
                UnityEngine.Object.Destroy(playerMark.GetComponent<Collider>());

                var shadow = new GameObject("PlayerShadow", typeof(MeshFilter), typeof(MeshRenderer));
                shadow.name = "PlayerShadow";
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.localScale = new Vector3(PlayerShadowBaseScale, 1f, PlayerShadowBaseScale);
                shadow.GetComponent<MeshFilter>().sharedMesh = CreateFlatCircleMesh();
                AssignColorMaterial(shadow.GetComponent<MeshRenderer>(), new Color32(63, 81, 96, 180));

                var keyLightObject = new GameObject("JumpJumpKeyLight", typeof(Light));
                keyLightObject.transform.SetParent(root.transform, false);
                keyLightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                var keyLight = keyLightObject.GetComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.color = new Color32(255, 245, 230, 255);
                keyLight.intensity = 1.15f;
                keyLight.shadows = LightShadows.None;

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color32(198, 212, 224, 255);

                return new JumpJumpWorld(root, currentPlatform, targetPlatform, player, shadow, backPlane, keyLight);
            }

            private static Mesh CreateFlatCircleMesh()
            {
                const int segmentCount = 32;
                var vertices = new Vector3[segmentCount + 1];
                var triangles = new int[segmentCount * 3];
                vertices[0] = Vector3.zero;

                for (var i = 0; i < segmentCount; i++)
                {
                    var angle = i * Mathf.PI * 2f / segmentCount;
                    vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
                }

                for (var i = 0; i < segmentCount; i++)
                {
                    var triangleIndex = i * 3;
                    triangles[triangleIndex] = 0;
                    triangles[triangleIndex + 1] = i + 1;
                    triangles[triangleIndex + 2] = i == segmentCount - 1 ? 1 : i + 2;
                }

                var mesh = new Mesh
                {
                    name = "PlayerShadowMesh",
                    vertices = vertices,
                    triangles = triangles
                };
                mesh.RecalculateBounds();
                return mesh;
            }

            public static void AssignColorMaterial(Renderer renderer, Color color)
            {
                if (renderer == null)
                {
                    return;
                }

                var shader = Resources.Load<Shader>(ColorShaderResourcePath);
                if (shader == null)
                {
                    shader = Shader.Find(ColorShaderName);
                }

                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                var material = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }

            public void Dispose()
            {
                if (Root != null)
                {
                    UnityEngine.Object.Destroy(Root);
                }
            }

            public void AddPastPlatform(JumpJumpPlatformState platform)
            {
                var visual = PlatformVisual.Create("PastPlatform" + (PastPlatforms.Count + 1), Root.transform);
                visual.SetPlatform(platform.Position, platform.Scale);
                visual.SetPalette(new Color32(224, 215, 198, 255), new Color32(166, 153, 132, 255));
                PastPlatforms.Add(visual);
            }

            public void RemovePastPlatformsOutsideView(Camera camera)
            {
                if (camera == null)
                {
                    return;
                }

                for (var i = PastPlatforms.Count - 1; i >= 0; i--)
                {
                    if (!PastPlatforms[i].IsVisibleInCamera(camera))
                    {
                        PastPlatforms[i].Destroy();
                        PastPlatforms.RemoveAt(i);
                    }
                }
            }

            public void ClearPastPlatforms()
            {
                for (var i = PastPlatforms.Count - 1; i >= 0; i--)
                {
                    PastPlatforms[i].Destroy();
                }

                PastPlatforms.Clear();
            }

            public bool IsTooCloseToPastPlatform(Vector3 position, float minDistance)
            {
                var minSqrDistance = minDistance * minDistance;
                for (var i = 0; i < PastPlatforms.Count; i++)
                {
                    var delta = position - PastPlatforms[i].Position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude < minSqrDistance)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class PlatformVisual
        {
            private readonly Renderer topRenderer;
            private readonly Renderer bodyRenderer;
            private readonly GameObject top;
            private readonly GameObject body;

            private PlatformVisual(GameObject root, GameObject topPart, GameObject bodyPart)
            {
                Root = root;
                top = topPart;
                body = bodyPart;
                topRenderer = top.GetComponent<Renderer>();
                bodyRenderer = body.GetComponent<Renderer>();
            }

            public GameObject Root { get; }

            public Vector3 Position
            {
                get { return Root.transform.position; }
            }

            public static PlatformVisual Create(string name, Transform parent)
            {
                var root = new GameObject(name);
                root.transform.SetParent(parent, false);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                JumpJumpWorld.AssignColorMaterial(body.GetComponent<Renderer>(), Color.white);
                UnityEngine.Object.Destroy(body.GetComponent<Collider>());

                var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                top.name = "Top";
                top.transform.SetParent(root.transform, false);
                JumpJumpWorld.AssignColorMaterial(top.GetComponent<Renderer>(), Color.white);
                UnityEngine.Object.Destroy(top.GetComponent<Collider>());

                return new PlatformVisual(root, top, body);
            }

            public void SetPlatform(Vector3 position, Vector3 scale)
            {
                Root.transform.position = position;
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = scale;
                top.transform.localPosition = new Vector3(0f, scale.y * 0.32f, 0f);
                top.transform.localScale = new Vector3(scale.x * 1.04f, scale.y * 0.22f, scale.z * 1.04f);
            }

            public void SetPalette(Color topColor, Color bodyColor)
            {
                topRenderer.sharedMaterial.color = topColor;
                bodyRenderer.sharedMaterial.color = bodyColor;
            }

            public bool IsVisibleInCamera(Camera camera)
            {
                var bounds = topRenderer.bounds;
                bounds.Encapsulate(bodyRenderer.bounds);
                var min = bounds.min;
                var max = bounds.max;
                return IsPointVisible(camera, new Vector3(min.x, min.y, min.z)) ||
                    IsPointVisible(camera, new Vector3(min.x, min.y, max.z)) ||
                    IsPointVisible(camera, new Vector3(min.x, max.y, min.z)) ||
                    IsPointVisible(camera, new Vector3(min.x, max.y, max.z)) ||
                    IsPointVisible(camera, new Vector3(max.x, min.y, min.z)) ||
                    IsPointVisible(camera, new Vector3(max.x, min.y, max.z)) ||
                    IsPointVisible(camera, new Vector3(max.x, max.y, min.z)) ||
                    IsPointVisible(camera, new Vector3(max.x, max.y, max.z));
            }

            public void Destroy()
            {
                if (Root != null)
                {
                    UnityEngine.Object.Destroy(Root);
                }
            }

            private static bool IsPointVisible(Camera camera, Vector3 point)
            {
                var viewport = camera.WorldToViewportPoint(point);
                return viewport.z > 0f &&
                    viewport.x >= 0f && viewport.x <= 1f &&
                    viewport.y >= 0f && viewport.y <= 1f;
            }
        }
    }
}
