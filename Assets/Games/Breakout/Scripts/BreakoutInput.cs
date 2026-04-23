using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class BreakoutInput
    {
        private const float KeyboardMoveSpeed = 900f;

        private readonly RectTransform boardRect;
        private int activeTouchId = -1;
        private bool mouseDragging;

        public BreakoutInput(RectTransform boardRect)
        {
            this.boardRect = boardRect;
        }

        public BreakoutInputSnapshot Sample(float deltaTime)
        {
            var snapshot = new BreakoutInputSnapshot();

            if (TryGetPointerBoardX(out var boardX))
            {
                snapshot.HasPointer = true;
                snapshot.PointerBoardX = boardX;
            }

            snapshot.KeyboardDelta =
                (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? -1f : 0f) * KeyboardMoveSpeed * deltaTime +
                (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f : 0f) * KeyboardMoveSpeed * deltaTime;

            snapshot.LaunchRequested = Input.GetKeyDown(KeyCode.Space);
            return snapshot;
        }

        private bool TryGetPointerBoardX(out float boardX)
        {
            if (TryGetTouchBoardX(out boardX))
            {
                return true;
            }

            return TryGetMouseBoardX(out boardX);
        }

        private bool TryGetTouchBoardX(out float boardX)
        {
            boardX = 0f;
            if (Input.touchCount <= 0)
            {
                activeTouchId = -1;
                return false;
            }

            if (activeTouchId >= 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != activeTouchId)
                    {
                        continue;
                    }

                    if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
                    {
                        activeTouchId = -1;
                        return false;
                    }

                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, touch.position, null, out var point))
                    {
                        boardX = point.x;
                        return true;
                    }
                }

                activeTouchId = -1;
            }

            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (!RectTransformUtility.RectangleContainsScreenPoint(boardRect, touch.position, null))
                {
                    continue;
                }

                if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    activeTouchId = touch.fingerId;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, touch.position, null, out var point))
                    {
                        boardX = point.x;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetMouseBoardX(out float boardX)
        {
            boardX = 0f;
            if (Input.GetMouseButtonDown(0))
            {
                mouseDragging = RectTransformUtility.RectangleContainsScreenPoint(boardRect, Input.mousePosition, null);
            }

            if (!Input.GetMouseButton(0))
            {
                mouseDragging = false;
                return false;
            }

            if (!mouseDragging)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, Input.mousePosition, null, out var point))
            {
                return false;
            }

            boardX = point.x;
            return true;
        }
    }

    internal struct BreakoutInputSnapshot
    {
        public bool HasPointer;
        public float PointerBoardX;
        public float KeyboardDelta;
        public bool LaunchRequested;
    }
}
