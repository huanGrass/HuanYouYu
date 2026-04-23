using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace HuanYouYu.Game2048
{
    public sealed class Game2048BoardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int BoardSize = 4;
        private const float BoardPixelSize = 820f;
        private const float TilePadding = 18f;
        private const float TileSpacing = 18f;
        private const float TileSize = 182f;
        private const float MoveAnimationDuration = 0.12f;

        private Game2048TileView[] tileViews = Array.Empty<Game2048TileView>();
        private readonly List<Game2048TileView> animatedTileViews = new List<Game2048TileView>();
        private bool inputEnabled = true;
        private bool pointerActive;
        private Vector2 pointerStartPosition;
        private float swipeThreshold = 72f;
        private RectTransform animationLayer;
        private Coroutine activeAnimation;
        private int[] lastRenderedValues = new int[BoardSize * BoardSize];
        private bool hasRenderedBoard;

        public event Action<Game2048MoveDirection> SwipePerformed;

        public void Initialize(Game2048TileView[] tiles, RectTransform tileAnimationLayer, float minSwipeDistance)
        {
            tileViews = tiles ?? Array.Empty<Game2048TileView>();
            animationLayer = tileAnimationLayer;
            swipeThreshold = Mathf.Max(24f, minSwipeDistance);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            pointerActive = false;
        }

        public void Refresh(Game2048Board board)
        {
            if (board == null)
            {
                return;
            }

            StopActiveAnimation();
            ApplyValues(board.Snapshot(), board.Size, !hasRenderedBoard);
        }

        public void PlayMoveAnimation(int[] previousValues, Game2048MoveResult result, Game2048Board board, Action onCompleted)
        {
            if (board == null)
            {
                onCompleted?.Invoke();
                return;
            }

            StopActiveAnimation();
            if (!isActiveAndEnabled || result.TileMotions == null || result.TileMotions.Length == 0)
            {
                ApplyValues(board.Snapshot(), board.Size, false);
                onCompleted?.Invoke();
                return;
            }

            activeAnimation = StartCoroutine(PlayMoveAnimationRoutine(previousValues, result.TileMotions, board, onCompleted));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!inputEnabled || eventData == null)
            {
                return;
            }

            pointerActive = true;
            pointerStartPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!inputEnabled || !pointerActive || eventData == null)
            {
                return;
            }

            pointerActive = false;
            var delta = eventData.position - pointerStartPosition;
            if (Mathf.Abs(delta.x) < swipeThreshold && Mathf.Abs(delta.y) < swipeThreshold)
            {
                return;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                SwipePerformed?.Invoke(delta.x >= 0f ? Game2048MoveDirection.Right : Game2048MoveDirection.Left);
                return;
            }

            SwipePerformed?.Invoke(delta.y >= 0f ? Game2048MoveDirection.Up : Game2048MoveDirection.Down);
        }

        public static Game2048BoardView Create(Transform parent, float layoutScale)
        {
            var root = new GameObject("Game2048Board", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Game2048BoardView));
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(BoardPixelSize, BoardPixelSize);
            rectTransform.localScale = Vector3.one * layoutScale;

            var background = root.GetComponent<RoundedRectGraphic>();
            background.color = new Color32(187, 173, 160, 255);
            background.CornerRadius = 32f;

            CreateSlotGrid(rectTransform);
            var tileLayer = CreateLayer(rectTransform, "TileLayer");

            var tiles = new Game2048TileView[BoardSize * BoardSize];
            for (var index = 0; index < tiles.Length; index++)
            {
                tiles[index] = CreateTile(tileLayer, index);
                SetTilePosition((RectTransform)tiles[index].transform, index / BoardSize, index % BoardSize);
            }

            var boardView = root.GetComponent<Game2048BoardView>();
            boardView.Initialize(tiles, tileLayer, 72f * layoutScale);
            return boardView;
        }

        private IEnumerator PlayMoveAnimationRoutine(IReadOnlyList<int> previousValues, IReadOnlyList<Game2048TileMotion> tileMotions, Game2048Board board, Action onCompleted)
        {
            ApplyValues(previousValues, board.Size, true);
            HideAnimatedSources(tileMotions, board.Size);

            for (var index = 0; index < tileMotions.Count; index++)
            {
                var motion = tileMotions[index];
                var tileView = CreateTile(animationLayer, -1);
                tileView.Bind(motion.Value);
                SetTilePosition((RectTransform)tileView.transform, motion.FromRow, motion.FromColumn);
                animatedTileViews.Add(tileView);
            }

            var elapsed = 0f;
            while (elapsed < MoveAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / MoveAnimationDuration);
                progress = progress * progress * (3f - (2f * progress));

                for (var index = 0; index < tileMotions.Count && index < animatedTileViews.Count; index++)
                {
                    var motion = tileMotions[index];
                    var tileTransform = (RectTransform)animatedTileViews[index].transform;
                    tileTransform.anchoredPosition = Vector2.LerpUnclamped(
                        GetCellPosition(motion.FromRow, motion.FromColumn),
                        GetCellPosition(motion.ToRow, motion.ToColumn),
                        progress);
                }

                yield return null;
            }

            activeAnimation = null;
            DestroyAnimatedTiles();
            ApplyValues(board.Snapshot(), board.Size, false);
            onCompleted?.Invoke();
        }

        private void ApplyValues(IReadOnlyList<int> values, int size, bool suppressPulse = false)
        {
            if (values == null)
            {
                return;
            }

            for (var row = 0; row < size; row++)
            {
                for (var column = 0; column < size; column++)
                {
                    var index = (row * size) + column;
                    if (index < tileViews.Length && tileViews[index] != null)
                    {
                        var previousValue = index < lastRenderedValues.Length ? lastRenderedValues[index] : 0;
                        tileViews[index].Bind(values[index]);
                        tileViews[index].ResetAnimationState();
                        tileViews[index].SetVisible(true);
                        SetTilePosition((RectTransform)tileViews[index].transform, row, column);
                        if (!suppressPulse && values[index] > 0)
                        {
                            if (previousValue <= 0)
                            {
                                tileViews[index].PlaySpawnPulse();
                            }
                            else if (values[index] > previousValue)
                            {
                                tileViews[index].PlayMergePulse();
                            }
                        }

                        if (index < lastRenderedValues.Length)
                        {
                            lastRenderedValues[index] = values[index];
                        }
                    }
                }
            }

            hasRenderedBoard = true;
        }

        private void HideAnimatedSources(IReadOnlyList<Game2048TileMotion> tileMotions, int size)
        {
            var hiddenFlags = new bool[tileViews.Length];
            for (var index = 0; index < tileMotions.Count; index++)
            {
                var motion = tileMotions[index];
                var sourceIndex = (motion.FromRow * size) + motion.FromColumn;
                if (sourceIndex < 0 || sourceIndex >= tileViews.Length || hiddenFlags[sourceIndex] || tileViews[sourceIndex] == null)
                {
                    continue;
                }

                hiddenFlags[sourceIndex] = true;
                tileViews[sourceIndex].SetVisible(false);
            }
        }

        private void StopActiveAnimation()
        {
            if (activeAnimation != null)
            {
                StopCoroutine(activeAnimation);
                activeAnimation = null;
            }

            DestroyAnimatedTiles();
            for (var index = 0; index < tileViews.Length; index++)
            {
                if (tileViews[index] != null)
                {
                    tileViews[index].ResetAnimationState();
                    tileViews[index].SetVisible(true);
                }
            }
        }

        private void DestroyAnimatedTiles()
        {
            for (var index = 0; index < animatedTileViews.Count; index++)
            {
                if (animatedTileViews[index] != null)
                {
                    Destroy(animatedTileViews[index].gameObject);
                }
            }

            animatedTileViews.Clear();
        }

        private static RectTransform CreateLayer(Transform parent, string name)
        {
            var layerObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = layerObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        private static void CreateSlotGrid(Transform parent)
        {
            var gridObject = new GameObject("SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            var rectTransform = gridObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = BoardSize;
            grid.spacing = new Vector2(TileSpacing, TileSpacing);
            grid.padding = new RectOffset((int)TilePadding, (int)TilePadding, (int)TilePadding, (int)TilePadding);
            grid.cellSize = new Vector2(TileSize, TileSize);
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (var index = 0; index < BoardSize * BoardSize; index++)
            {
                var slotObject = new GameObject("Slot_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
                var slotTransform = slotObject.GetComponent<RectTransform>();
                slotTransform.SetParent(rectTransform, false);

                var slotBackground = slotObject.GetComponent<RoundedRectGraphic>();
                slotBackground.CornerRadius = 24f;
                slotBackground.color = new Color32(205, 193, 180, 255);
            }
        }

        private static Game2048TileView CreateTile(Transform parent, int index)
        {
            var tileObject = new GameObject(index >= 0 ? "Tile_" + index : "AnimatedTile", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Game2048TileView));
            var tileTransform = tileObject.GetComponent<RectTransform>();
            tileTransform.SetParent(parent, false);
            tileTransform.anchorMin = new Vector2(0.5f, 0.5f);
            tileTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tileTransform.pivot = new Vector2(0.5f, 0.5f);
            tileTransform.sizeDelta = new Vector2(TileSize, TileSize);

            var tileBackground = tileObject.GetComponent<RoundedRectGraphic>();
            tileBackground.CornerRadius = 24f;

            var labelObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.SetParent(tileTransform, false);
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = Vector2.zero;
            labelTransform.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.fontSize = 54f;

            var tileView = tileObject.GetComponent<Game2048TileView>();
            tileView.Initialize(tileBackground, label);
            tileView.Bind(0);
            return tileView;
        }

        private static void SetTilePosition(RectTransform tileTransform, int row, int column)
        {
            if (tileTransform == null)
            {
                return;
            }

            tileTransform.anchoredPosition = GetCellPosition(row, column);
        }

        private static Vector2 GetCellPosition(int row, int column)
        {
            var startX = (-BoardPixelSize * 0.5f) + TilePadding + (TileSize * 0.5f);
            var startY = (BoardPixelSize * 0.5f) - TilePadding - (TileSize * 0.5f);
            return new Vector2(startX + (column * (TileSize + TileSpacing)), startY - (row * (TileSize + TileSpacing)));
        }
    }
}
