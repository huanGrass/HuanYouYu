using System;
using UnityEngine;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public static class MiniGameDirectionPadBuilder
    {
        public struct Config
        {
            public string Name;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
            public Color RingColor;
            public Color ButtonColor;
            public Color ArrowColor;
            public float RingCornerRadius;
            public float ButtonSize;
            public float ButtonDistance;
            public Action UpAction;
            public Action DownAction;
            public Action LeftAction;
            public Action RightAction;

            public static Config Default
            {
                get
                {
                    return new Config
                    {
                        Name = "DirectionPad",
                        AnchorMin = new Vector2(0f, 0.5f),
                        AnchorMax = new Vector2(0f, 0.5f),
                        OffsetMin = new Vector2(18f, -106f),
                        OffsetMax = new Vector2(234f, 106f),
                        RingColor = new Color(1f, 1f, 1f, 0.36f),
                        ButtonColor = new Color(1f, 1f, 1f, 0.95f),
                        ArrowColor = new Color(0.31f, 0.42f, 0.26f, 1f),
                        RingCornerRadius = 82f,
                        ButtonSize = 92f,
                        ButtonDistance = 66f
                    };
                }
            }
        }

        public sealed class References
        {
            public RectTransform Root;
            public Button UpButton;
            public Button DownButton;
            public Button LeftButton;
            public Button RightButton;
        }

        public static References Create(Transform parent, Config config)
        {
            var root = new GameObject(string.IsNullOrEmpty(config.Name) ? "DirectionPad" : config.Name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, config.AnchorMin, config.AnchorMax, config.OffsetMin, config.OffsetMax);

            var padRing = new GameObject("PadRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            padRing.transform.SetParent(root.transform, false);
            var ringRect = padRing.GetComponent<RectTransform>();
            Stretch(ringRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var ringGraphic = padRing.GetComponent<RoundedRectGraphic>();
            ringGraphic.color = config.RingColor;
            ringGraphic.CornerRadius = config.RingCornerRadius;
            ringGraphic.raycastTarget = false;

            return new References
            {
                Root = rootRect,
                UpButton = CreateButton(root.transform, "UpButton", Vector2.up, 0f, config, config.UpAction),
                DownButton = CreateButton(root.transform, "DownButton", Vector2.down, 180f, config, config.DownAction),
                LeftButton = CreateButton(root.transform, "LeftButton", Vector2.left, 90f, config, config.LeftAction),
                RightButton = CreateButton(root.transform, "RightButton", Vector2.right, 270f, config, config.RightAction)
            };
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Vector2 anchoredDirection,
            float arrowRotation,
            Config config,
            Action action)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(config.ButtonSize, config.ButtonSize);
            rect.anchoredPosition = anchoredDirection * config.ButtonDistance;

            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.color = config.ButtonColor;
            graphic.CornerRadius = config.ButtonSize * 0.5f;

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = config.ButtonSize;
            layout.preferredHeight = config.ButtonSize;

            var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(DirectionTriangleGraphic));
            arrowObject.transform.SetParent(buttonObject.transform, false);
            var arrowRect = arrowObject.GetComponent<RectTransform>();
            Stretch(arrowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-17f, -15f), new Vector2(17f, 15f));
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, arrowRotation);

            var arrowGraphic = arrowObject.GetComponent<DirectionTriangleGraphic>();
            arrowGraphic.color = config.ArrowColor;
            arrowGraphic.raycastTarget = false;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(delegate { action(); });
            }

            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.9f);
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
