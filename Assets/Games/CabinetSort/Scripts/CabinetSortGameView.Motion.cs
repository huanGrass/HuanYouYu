using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed partial class CabinetSortGameView
    {
        private RectTransform motionRoot;
        private UiTweenRunner tweenRunner;
        private RawImage dragIcon;
        private bool[] busyShelves;
        private int activeMotions;
        private bool animating;
        private int dragged = -1;
        private int dragPointer;
        private int dropTarget = -1;
        private Vector3 dragOffset;

        public override void Tick(float deltaTime)
        {
            if (icons == null || locked || settled) return;
            for (var i = 0; i < icons.Length; i++)
            {
                if (busyShelves[i / 3]) continue;
                var scale = i == selected ? 1.1f + .025f * Mathf.Sin(Time.unscaledTime * 6) : 1f;
                if (i == hintFirst || i == hintSecond)
                    scale = 1.14f + .06f * Mathf.Sin(Time.unscaledTime * 6);
                icons[i].rectTransform.localScale = Vector3.Lerp(icons[i].rectTransform.localScale,
                    Vector3.one * scale, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            }
        }

        private void BeginDrag(int index, PointerEventData data)
        {
            if (locked || settled || busyShelves[index / 3] || dragged >= 0 || board[index] < 0 ||
                data.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(motionRoot, data.position,
                data.pressEventCamera, out var pointer)) return;
            selected = dragged = index;
            dragPointer = data.pointerId;
            if (index != hintFirst && index != hintSecond) hintFirst = hintSecond = -1;
            dropTarget = -1;
            dragIcon = CreateMovingIcon(index, "DraggedItem");
            dragIcon.rectTransform.localScale = Vector3.one * 1.12f;
            dragOffset = dragIcon.transform.position - pointer;
            data.eligibleForClick = false;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, .8f);
            Refresh();
        }

        private void UpdateDrag(int index, PointerEventData data)
        {
            if (dragged != index || dragPointer != data.pointerId || locked) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(motionRoot, data.position,
                data.pressEventCamera, out var pointer)) dragIcon.transform.position = pointer + dragOffset;
            dropTarget = FindDropTarget(data);
            Refresh();
        }

        private int FindDropTarget(PointerEventData data)
        {
            for (var i = 0; i < buttons.Length; i++)
                if (i != dragged && !busyShelves[i / 3] && board[i] >= 0 && RectTransformUtility.RectangleContainsScreenPoint(
                    slots[i].rectTransform, data.position, data.pressEventCamera)) return i;
            return -1;
        }

        private void EndDrag(int index, PointerEventData data)
        {
            if (dragged != index || dragPointer != data.pointerId) return;
            UpdateDrag(index, data);
            var target = dropTarget;
            dragged = dropTarget = selected = -1;
            data.eligibleForClick = false;
            if (target >= 0 && board[target] != board[index]) StartSwap(index, target);
            else
            {
                var moving = dragIcon;
                dragIcon = null;
                BeginMotion(index, index);
                Refresh();
                tweenRunner.Run(ReturnDraggedItem(index, moving));
            }
        }

        private IEnumerator ReturnDraggedItem(int index, RawImage moving)
        {
            var start = moving.transform.position;
            var end = icons[index].transform.position;
            yield return Animate(.18f, t =>
            {
                moving.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
                moving.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1, t);
            });
            DestroyMovingIcon(moving);
            FinishMotion(index, index);
        }

        private void StartSwap(int first, int second)
        {
            selected = hintFirst = hintSecond = -1;
            BeginMotion(first, second);
            var movingFirst = dragIcon != null ? dragIcon : CreateMovingIcon(first, "MovingFirst");
            dragIcon = null;
            var movingSecond = CreateMovingIcon(second, "MovingSecond");
            icons[first].enabled = icons[second].enabled = false;
            Refresh();
            tweenRunner.Run(SwapAndClear(first, second, movingFirst, movingSecond));
        }

        private IEnumerator SwapAndClear(int first, int second, RawImage movingFirst, RawImage movingSecond)
        {
            var firstShelf = first / 3 * 3;
            var secondShelf = second / 3 * 3;
            var affected = new int[firstShelf == secondShelf ? 3 : 6];
            for (var i = 0; i < affected.Length; i++) affected[i] = (i < 3 ? firstShelf : secondShelf) + i % 3;
            var firstStart = movingFirst.transform.position;
            var secondStart = movingSecond.transform.position;
            var firstEnd = icons[second].transform.position;
            var secondEnd = icons[first].transform.position;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, .85f);
            yield return Animate(.26f, t =>
            {
                var ease = Mathf.SmoothStep(0, 1, t);
                var arc = motionRoot.TransformVector(Vector3.up * (Mathf.Sin(t * Mathf.PI) * 12));
                movingFirst.transform.position = Vector3.Lerp(firstStart, firstEnd, ease) + arc;
                movingSecond.transform.position = Vector3.Lerp(secondStart, secondEnd, ease) - arc;
                movingFirst.rectTransform.localScale = movingSecond.rectTransform.localScale =
                    Vector3.one * (1 + .12f * Mathf.Sin(t * Mathf.PI));
            });

            var before = new int[board.Count];
            for (var i = 0; i < before.Length; i++) before[i] = board[i];
            var removed = board.Swap(first, second);
            var value = before[first];
            before[first] = before[second];
            before[second] = value;
            DestroyMovingIcon(movingFirst);
            DestroyMovingIcon(movingSecond);
            foreach (var i in affected)
            {
                icons[i].enabled = before[i] >= 0;
                icons[i].rectTransform.localScale = Vector3.one;
                if (before[i] >= 0) SetIconTexture(i, before[i]);
                slots[i].color = Color.clear;
            }
            while (removed > 0)
            {
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchSuccess, .95f);
                yield return Animate(.36f, t =>
                {
                    var scale = t < .3f ? Mathf.Lerp(1, 1.22f, t / .3f) :
                        Mathf.Lerp(1.22f, .15f, (t - .3f) / .7f);
                    var alpha = 1 - Mathf.Clamp01((t - .25f) / .75f);
                    foreach (var i in affected)
                    {
                        if (before[i] < 0 || board[i] >= 0) continue;
                        icons[i].rectTransform.localScale = Vector3.one * scale;
                        icons[i].color = new Color(1, 1, 1, alpha);
                        iconOutlines[i].enabled = true;
                        iconOutlines[i].effectColor = new Color(.86f, 1, .49f, .8f * (1 - t));
                    }
                });
                var revealed = new bool[board.Count];
                foreach (var i in affected)
                {
                    revealed[i] = board[i] < 0;
                    if (revealed[i]) icons[i].enabled = false;
                    iconOutlines[i].enabled = false;
                    slots[i].color = Color.clear;
                }
                var added = board.RefillShelf(firstShelf);
                if (secondShelf != firstShelf) added += board.RefillShelf(secondShelf);
                if (added > 0)
                {
                    foreach (var i in affected)
                    {
                        revealed[i] &= board[i] >= 0;
                        if (!revealed[i]) continue;
                        SetIconTexture(i, board[i]);
                        icons[i].enabled = true;
                        icons[i].color = new Color(1, 1, 1, 0);
                        icons[i].rectTransform.localScale = Vector3.one * .15f;
                    }
                    Refresh();
                    yield return Animate(.28f, t =>
                    {
                        var scale = t < .75f ? Mathf.Lerp(.15f, 1.12f, Mathf.SmoothStep(0, 1, t / .75f)) :
                            Mathf.Lerp(1.12f, 1, (t - .75f) / .25f);
                        foreach (var i in affected)
                        {
                            if (!revealed[i]) continue;
                            icons[i].rectTransform.localScale = Vector3.one * scale;
                            icons[i].color = new Color(1, 1, 1, Mathf.Clamp01(t * 3));
                        }
                    });
                }
                for (var i = 0; i < before.Length; i++) before[i] = board[i];
                // A newly revealed triple follows the same clear/reveal sequence.
                removed = board.ClearShelf(firstShelf);
                if (secondShelf != firstShelf) removed += board.ClearShelf(secondShelf);
            }
            FinishMotion(first, second);
            if (board.Remaining == 0 && !animating) ShowResult(true);
        }

        private IEnumerator Animate(float seconds, Action<float> update)
        {
            var elapsed = 0f;
            update(0);
            while (elapsed < seconds)
            {
                yield return null;
                if (locked) continue;
                elapsed += Time.unscaledDeltaTime;
                update(Mathf.Clamp01(elapsed / seconds));
            }
        }

        private RawImage CreateMovingIcon(int index, string name)
        {
            var source = icons[index].rectTransform;
            var rect = Rect(name, motionRoot, Vector2.one * .5f, Vector2.one * .5f, 0);
            rect.pivot = source.pivot;
            rect.sizeDelta = source.rect.size;
            rect.position = source.position;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = icons[index].texture;
            image.raycastTarget = false;
            return image;
        }

        private static void DestroyMovingIcon(RawImage image)
        {
            if (image == null) return;
            image.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(image.gameObject);
        }

        private void BeginMotion(int first, int second)
        {
            activeMotions++;
            animating = true;
            busyShelves[first / 3] = busyShelves[second / 3] = true;
            for (var i = 0; i < icons.Length; i++)
                if (i / 3 == first / 3 || i / 3 == second / 3) iconOutlines[i].enabled = false;
        }

        private void FinishMotion(int first, int second)
        {
            busyShelves[first / 3] = busyShelves[second / 3] = false;
            activeMotions--;
            animating = activeMotions > 0;
            for (var i = 0; i < icons.Length; i++)
                if (i / 3 == first / 3 || i / 3 == second / 3) icons[i].rectTransform.localScale = Vector3.one;
            Refresh();
        }

        private void CancelDrag()
        {
            DestroyMovingIcon(dragIcon);
            dragIcon = null;
            dragged = dropTarget = selected = -1;
        }

        private void StopMotion()
        {
            if (tweenRunner != null) tweenRunner.StopAllCoroutines();
            activeMotions = 0;
            animating = false;
            CancelDrag();
            if (busyShelves != null) Array.Clear(busyShelves, 0, busyShelves.Length);
            if (motionRoot != null)
                for (var i = motionRoot.childCount - 1; i >= 0; i--)
                    DestroyMovingIcon(motionRoot.GetChild(i).GetComponent<RawImage>());
            if (icons != null)
                for (var i = 0; i < icons.Length; i++) icons[i].rectTransform.localScale = Vector3.one;
        }

        private sealed class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public CabinetSortGameView Owner;
            public int Index;
            public void OnBeginDrag(PointerEventData data) => Owner.BeginDrag(Index, data);
            public void OnDrag(PointerEventData data) => Owner.UpdateDrag(Index, data);
            public void OnEndDrag(PointerEventData data) => Owner.EndDrag(Index, data);
            private void OnApplicationFocus(bool focus)
            {
                if (!focus && Owner != null && Owner.dragged == Index)
                {
                    Owner.CancelDrag();
                    Owner.Refresh();
                }
            }
        }
    }
}
