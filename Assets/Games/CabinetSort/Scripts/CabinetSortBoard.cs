using System;

namespace HuanYouYu.MiniGameHall
{
    public sealed class CabinetSortBoard
    {
        public const int ShelfColumns = 3;
        public const int ShelfRows = 9;
        public const int ItemTypeCount = 24;
        private readonly int[] items;
        private readonly int[] reserve;
        private int reserveIndex;
        public int Count => items.Length;
        public int TotalCount => items.Length + reserve.Length;
        public int HiddenCount => reserve.Length - reserveIndex;
        public int Remaining { get; private set; }
        public int Moves { get; private set; }
        public int this[int index] => items[index];

        public CabinetSortBoard(Random random)
        {

            if (random == null) throw new ArgumentNullException(nameof(random));
            items = new int[ShelfColumns * ShelfRows * 3];
            reserve = new int[ItemTypeCount * 6 - items.Length];
            // Six of every type across the front and the reserve. The interleaved front
            // is also a triple-free fallback if shuffle exhausts its attempts.
            for (var i = 0; i < items.Length; i++) items[i] = i % ItemTypeCount;
            for (var i = 0; i < reserve.Length; i++) reserve[i] = (Count + i) % ItemTypeCount;
            for (var i = reserve.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var value = reserve[i];
                reserve[i] = reserve[j];
                reserve[j] = value;
            }
            Remaining = TotalCount;
            Shuffle(random);
        }

        public int Swap(int first, int second)
        {
            if (first < 0 || second < 0 || first >= Count || second >= Count ||
                first == second || items[first] < 0 || items[second] < 0 || items[first] == items[second]) return 0;
            var value = items[first];
            items[first] = items[second];
            items[second] = value;
            Moves++;
            var removed = ClearShelf(first / 3 * 3);
            if (first / 3 != second / 3) removed += ClearShelf(second / 3 * 3);
            return removed;
        }

        public int ClearMatches()
        {
            var removed = 0;
            for (var start = 0; start < Count; start += 3)
            {
                removed += ClearShelf(start);
            }
            return removed;
        }

        public int ClearShelf(int start)
        {
            if (start < 0 || start >= Count || start % 3 != 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (!IsTriple(start)) return 0;
            items[start] = items[start + 1] = items[start + 2] = -1;
            Remaining -= 3;
            return 3;
        }

        public int RefillShelf(int start)
        {
            if (start < 0 || start >= Count || start % 3 != 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (items[start] >= 0 || HiddenCount == 0) return 0;
            for (var i = start; i < start + 3; i++) items[i] = reserve[reserveIndex++];
            return 3;
        }

        public int Refill()
        {
            var added = 0;
            for (var start = 0; start < Count && HiddenCount > 0; start += 3)
            {
                added += RefillShelf(start);
            }
            return added;
        }

        public bool TryGetHint(out int first, out int second)
        {
            first = second = -1;
            var counts = new int[ItemTypeCount];
            for (var i = 0; i < Count; i++) if (items[i] >= 0) counts[items[i]]++;
            for (var start = 0; start < Count; start += 3)
            {
                if (items[start] < 0) continue;
                var target = -1;
                var bestCount = 0;
                for (var i = start; i < start + 3; i++)
                {
                    var candidate = items[i];
                    if (counts[candidate] < 3) continue;
                    var localCount = 0;
                    for (var j = start; j < start + 3; j++) if (items[j] == candidate) localCount++;
                    if (localCount <= bestCount) continue;
                    target = candidate;
                    bestCount = localCount;
                }
                if (target < 0) continue;
                for (var i = start; i < start + 3; i++)
                {
                    if (items[i] == target) continue;
                    for (var j = 0; j < Count; j++)
                    {
                        if (j >= start && j < start + 3 || items[j] != target) continue;
                        first = i;
                        second = j;
                        return true;
                    }
                }
            }
            return false;
        }

        public void Shuffle(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var original = (int[])items.Clone();
            var occupied = new int[Count];
            var count = 0;
            for (var i = 0; i < Count; i++) if (items[i] >= 0) occupied[count++] = i;
            for (var attempt = 0; attempt < 64; attempt++)
            {
                for (var i = count - 1; i > 0; i--)
                {
                    var a = occupied[i];
                    var b = occupied[random.Next(i + 1)];
                    var value = items[a];
                    items[a] = items[b];
                    items[b] = value;
                }
                var hasTriple = false;
                for (var i = 0; i < Count; i += 3) hasTriple |= IsTriple(i);
                if (!hasTriple) return;
            }
            Array.Copy(original, items, Count);
        }

        private bool IsTriple(int start) => items[start] >= 0 &&
            items[start] == items[start + 1] && items[start] == items[start + 2];
    }
}
