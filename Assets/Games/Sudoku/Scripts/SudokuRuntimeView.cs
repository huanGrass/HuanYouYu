using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class SudokuRuntimeView : IDisposable
    {
        private static readonly Color BoardVeilColor = new Color32(247, 241, 225, 176);
        private static readonly Color BoardShadowColor = new Color32(93, 109, 83, 24);
        private static readonly Color BoardPanelColor = new Color32(250, 245, 234, 255);
        private static readonly Color GridSurfaceColor = new Color32(245, 240, 229, 255);
        private static readonly Color CellBaseColor = new Color32(255, 252, 246, 255);
        private static readonly Color RelatedCellColor = new Color32(247, 236, 207, 255);
        private static readonly Color SelectedCellColor = new Color32(242, 210, 135, 255);
        private static readonly Color ConflictCellColor = new Color32(222, 136, 120, 255);
        private static readonly Color GivenTextColor = new Color32(78, 92, 68, 255);
        private static readonly Color PlayerTextColor = new Color32(55, 69, 49, 255);
        private static readonly Color HintTextColor = new Color32(38, 143, 116, 255);
        private static readonly Color ConflictTextColor = new Color32(255, 245, 240, 255);
        private static readonly Color CandidateTextColor = new Color32(120, 128, 111, 255);
        private static readonly Color DividerColor = new Color32(116, 108, 89, 255);
        private static readonly Color DividerEmphasisColor = new Color32(93, 84, 69, 255);
        private static readonly Color KeypadPanelColor = new Color32(235, 225, 205, 255);
        private static readonly Color KeypadButtonColor = new Color32(250, 246, 238, 255);
        private static readonly Color KeypadButtonTextColor = new Color32(66, 80, 63, 255);
        private const float BoardFrameSize = 668f;
        private const float GridSurfaceSize = 628f;
        private const float KeypadGridSize = 252f;

        private readonly RectTransform root;
        private readonly RectTransform keypadRoot;
        private readonly CellView[] cellViews;
        private readonly TMP_FontAsset fontAsset;
        private readonly Material fontMaterial;
        private readonly Action<int> onCellSelected;
        private readonly Action<int> onDigitInput;

        public SudokuRuntimeView(
            RectTransform contentParent,
            RectTransform keypadParent,
            TMP_FontAsset sharedFontAsset,
            Material sharedFontMaterial,
            Action<int> handleCellSelected,
            Action<int> handleDigitInput)
        {
            if (contentParent == null || keypadParent == null)
            {
                throw new ArgumentNullException(contentParent == null ? nameof(contentParent) : nameof(keypadParent));
            }

            fontAsset = sharedFontAsset;
            fontMaterial = sharedFontMaterial;
            onCellSelected = handleCellSelected;
            onDigitInput = handleDigitInput;

            root = CreateRect("SudokuRuntimeRoot", contentParent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            keypadRoot = BuildKeypad(keypadParent);
            cellViews = new CellView[SudokuBoardState.CellCount];

            BuildBoard(root);
        }

        public void Dispose()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }

            if (keypadRoot != null)
            {
                UnityEngine.Object.Destroy(keypadRoot.gameObject);
            }
        }

        public void Render(SudokuBoardState boardState, int selectedCellIndex)
        {
            if (boardState == null)
            {
                return;
            }

            var selectedValue = boardState.GetValue(selectedCellIndex);
            for (var cellIndex = 0; cellIndex < cellViews.Length; cellIndex++)
            {
                var cell = cellViews[cellIndex];
                if (cell == null)
                {
                    continue;
                }

                var value = boardState.GetValue(cellIndex);
                var isSelected = cellIndex == selectedCellIndex;
                var isRelated = selectedCellIndex >= 0 && boardState.IsRelated(selectedCellIndex, cellIndex);
                var hasSameValue = selectedValue != 0 && boardState.HasSameValue(selectedCellIndex, cellIndex);
                var hasConflict = boardState.HasConflict(cellIndex);

                cell.ValueLabel.gameObject.SetActive(value != 0);
                cell.CandidatesRoot.gameObject.SetActive(value == 0);
                cell.ValueLabel.text = value == 0 ? string.Empty : value.ToString();
                cell.ValueLabel.color = hasConflict
                    ? ConflictTextColor
                    : ResolveValueTextColor(boardState, cellIndex);
                RenderCandidates(cell, boardState.GetCandidateMask(cellIndex), value == 0);
                cell.Background.color = ResolveCellColor(isSelected, isRelated || hasSameValue, hasConflict);
            }
        }

        public bool TryGetCellCenterInRoot(int cellIndex, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            if (cellIndex < 0 || cellIndex >= cellViews.Length || cellViews[cellIndex] == null || root == null)
            {
                return false;
            }

            var cellRect = cellViews[cellIndex].Root;
            anchoredPosition = root.InverseTransformPoint(cellRect.TransformPoint(cellRect.rect.center));
            return true;
        }

        public RectTransform Root
        {
            get { return root; }
        }

        private void BuildBoard(RectTransform parent)
        {
            var boardVeil = CreateRect(
                "BoardVeil",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(BoardFrameSize + 40f, BoardFrameSize + 52f),
                new Vector2(0f, -10f));
            AddRoundedGraphic(boardVeil.gameObject, BoardVeilColor, 42f).raycastTarget = false;

            var boardShadow = CreateRect(
                "BoardShadow",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(BoardFrameSize, BoardFrameSize),
                new Vector2(0f, -14f));
            AddRoundedGraphic(boardShadow.gameObject, BoardShadowColor, 34f).raycastTarget = false;

            var boardPanel = CreateRect(
                "BoardPanel",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(BoardFrameSize, BoardFrameSize),
                new Vector2(0f, -8f));
            var boardBackground = AddRoundedGraphic(boardPanel.gameObject, BoardPanelColor, 34f);
            boardBackground.raycastTarget = false;

            var gridSurface = CreateRect(
                "GridSurface",
                boardPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(GridSurfaceSize, GridSurfaceSize),
                Vector2.zero);
            AddRoundedGraphic(gridSurface.gameObject, GridSurfaceColor, 24f).raycastTarget = false;

            const float cellSize = 68f;
            const float gap = 2f;
            const float cellStep = cellSize + gap;
            var boardSize = cellSize * SudokuBoardState.Size + gap * (SudokuBoardState.Size - 1);
            var cellsRoot = CreateRect("Cells", gridSurface, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(boardSize, boardSize), Vector2.zero);

            for (var row = 0; row < SudokuBoardState.Size; row++)
            {
                for (var column = 0; column < SudokuBoardState.Size; column++)
                {
                    var cellIndex = row * SudokuBoardState.Size + column;
                    var cellRect = CreateRect(
                        "Cell_" + row + "_" + column,
                        cellsRoot,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(cellSize, cellSize),
                        new Vector2(column * cellStep, -row * cellStep));

                    var button = cellRect.gameObject.AddComponent<Button>();
                    var background = AddRoundedGraphic(cellRect.gameObject, CellBaseColor, 10f);
                    button.targetGraphic = background;
                    var capturedIndex = cellIndex;
                    button.onClick.AddListener(delegate { onCellSelected?.Invoke(capturedIndex); });
                    MiniGameSfxPlayer.Attach(button, MiniGameSfxType.TileSelect, 0.82f);

                    var label = CreateText("Value", cellRect, string.Empty, 34f, FontStyles.Bold);
                    label.alignment = TextAlignmentOptions.Center;

                    var candidatesRoot = CreateRect("Candidates", cellRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    candidatesRoot.offsetMin = new Vector2(6f, 6f);
                    candidatesRoot.offsetMax = new Vector2(-6f, -6f);
                    var candidateLabels = BuildCandidateLabels(candidatesRoot);

                    cellViews[cellIndex] = new CellView(cellRect, background, label, candidatesRoot, candidateLabels);
                }
            }

            BuildGridDividers(gridSurface, boardSize, cellStep);
        }

        private static void RenderCandidates(CellView cell, int candidateMask, bool visible)
        {
            for (var i = 0; i < cell.CandidateLabels.Length; i++)
            {
                var label = cell.CandidateLabels[i];
                var shouldShow = visible && (candidateMask & (1 << i)) != 0;
                label.gameObject.SetActive(shouldShow);
            }
        }

        private TextMeshProUGUI[] BuildCandidateLabels(RectTransform parent)
        {
            var labels = new TextMeshProUGUI[SudokuBoardState.Size];
            const float slotSize = 18f;
            const float slotGap = 1f;
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var index = row * 3 + column;
                    var candidateRect = CreateRect(
                        "Candidate_" + (index + 1),
                        parent,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(slotSize, slotSize),
                        new Vector2(column * (slotSize + slotGap), -row * (slotSize + slotGap)));
                    var candidateLabel = CreateText("Label", candidateRect, (index + 1).ToString(), 16f, FontStyles.Normal);
                    candidateLabel.alignment = TextAlignmentOptions.Center;
                    candidateLabel.color = CandidateTextColor;
                    candidateLabel.gameObject.SetActive(false);
                    labels[index] = candidateLabel;
                }
            }

            return labels;
        }

        private void BuildGridDividers(RectTransform gridSurface, float boardSize, float cellStep)
        {
            var dividerRoot = CreateRect("Dividers", gridSurface, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(boardSize, boardSize), Vector2.zero);

            for (var i = 0; i <= SudokuBoardState.Size; i++)
            {
                var thickness = i % 3 == 0 ? 5f : 1.5f;
                var position = i == SudokuBoardState.Size ? boardSize : i * cellStep;
                var color = i % 3 == 0 ? DividerEmphasisColor : DividerColor;

                var horizontal = CreateRect(
                    "H_" + i,
                    dividerRoot,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(boardSize, thickness),
                    new Vector2(0f, -position + thickness * 0.5f));
                AddRoundedGraphic(horizontal.gameObject, color, 0f).raycastTarget = false;

                var vertical = CreateRect(
                    "V_" + i,
                    dividerRoot,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(thickness, boardSize),
                    new Vector2(position - thickness * 0.5f, 0f));
                AddRoundedGraphic(vertical.gameObject, color, 0f).raycastTarget = false;
            }
        }

        private RectTransform BuildKeypad(RectTransform parent)
        {
            var keypadRoot = CreateRect(
                "SudokuKeypad",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            keypadRoot.offsetMin = Vector2.zero;
            keypadRoot.offsetMax = Vector2.zero;

            var panelRect = CreateRect(
                "Panel",
                keypadRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(KeypadGridSize + 34f, KeypadGridSize + 34f),
                Vector2.zero);
            AddRoundedGraphic(panelRect.gameObject, KeypadPanelColor, 28f).raycastTarget = false;

            var gridRoot = CreateRect(
                "Grid",
                panelRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(KeypadGridSize, KeypadGridSize),
                Vector2.zero);

            const float buttonSize = 74f;
            const float horizontalGap = 15f;
            const float verticalGap = 15f;

            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var index = row * 3 + column;
                    var position = new Vector2(column * (buttonSize + horizontalGap), -row * (buttonSize + verticalGap));

                    var buttonRect = CreateRect(
                        "NumberButton_" + (index + 1),
                        gridRoot,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(buttonSize, buttonSize),
                        position);

                    var button = buttonRect.gameObject.AddComponent<Button>();
                    var background = AddRoundedGraphic(buttonRect.gameObject, KeypadButtonColor, 20f);
                    button.targetGraphic = background;
                    MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.88f);

                    var digit = index + 1;
                    button.onClick.AddListener(delegate { onDigitInput?.Invoke(digit); });
                    var label = CreateText("Label", buttonRect, digit.ToString(), 32f, FontStyles.Bold);
                    label.color = KeypadButtonTextColor;
                    label.alignment = TextAlignmentOptions.Center;
                }
            }

            return keypadRoot;
        }

        private static Color ResolveCellColor(bool isSelected, bool isRelated, bool hasConflict)
        {
            if (hasConflict)
            {
                return ConflictCellColor;
            }

            if (isSelected)
            {
                return SelectedCellColor;
            }

            if (isRelated)
            {
                return RelatedCellColor;
            }

            return CellBaseColor;
        }

        private static Color ResolveValueTextColor(SudokuBoardState boardState, int cellIndex)
        {
            if (boardState.IsGiven(cellIndex))
            {
                return GivenTextColor;
            }

            return boardState.IsHintRevealed(cellIndex) ? HintTextColor : PlayerTextColor;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float fontSize, FontStyles fontStyle)
        {
            var textRect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            EnsureCanvasRenderer(textRect.gameObject);
            var label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.enableWordWrapping = false;
            label.color = PlayerTextColor;
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            if (fontMaterial != null)
            {
                label.fontSharedMaterial = fontMaterial;
            }

            return label;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            if (anchorMin == anchorMax && Mathf.Approximately(anchorMin.x, 0f) && Mathf.Approximately(anchorMin.y, 1f))
            {
                rectTransform.pivot = new Vector2(0f, 1f);
            }

            return rectTransform;
        }

        private static RoundedRectGraphic AddRoundedGraphic(GameObject gameObject, Color color, float cornerRadius)
        {
            EnsureCanvasRenderer(gameObject);
            var graphic = gameObject.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = cornerRadius;
            return graphic;
        }

        private static void EnsureCanvasRenderer(GameObject gameObject)
        {
            if (gameObject.GetComponent<CanvasRenderer>() == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }
        }

        private sealed class CellView
        {
            public CellView(
                RectTransform root,
                RoundedRectGraphic background,
                TextMeshProUGUI valueLabel,
                RectTransform candidatesRoot,
                TextMeshProUGUI[] candidateLabels)
            {
                Root = root;
                Background = background;
                ValueLabel = valueLabel;
                CandidatesRoot = candidatesRoot;
                CandidateLabels = candidateLabels;
            }

            public RectTransform Root { get; }

            public RoundedRectGraphic Background { get; }

            public TextMeshProUGUI ValueLabel { get; }

            public RectTransform CandidatesRoot { get; }

            public TextMeshProUGUI[] CandidateLabels { get; }
        }
    }
}
