using System;
using UnityEngine;
using Random = System.Random;

namespace HuanYouYu.MiniGameHall
{
    internal sealed class LightsOutPuzzle
    {
        public int QuestionNumber;
        public int GridSize;
        public int[] Numbers;
        public bool[] InitialLights;
        public bool[] TargetLights;
        public int TargetAnswer;
        public string Expression;
        public int ReferenceSteps;
        public int[] SolutionClickIndices;
    }

    internal static class LightsOutPuzzleGenerator
    {
        private const int MaxAttempts = 2000;
        private const int MaxAnswer = 20;

        internal static LightsOutPuzzle Generate(int questionNumber, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var safeQuestionNumber = Mathf.Max(1, questionNumber);
            var gridSize = ResolveGridSize(safeQuestionNumber);
            var cellCount = gridSize * gridSize;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var numbers = CreateNumbers(cellCount, random);
                var targetLights = CreateTargetLights(numbers, random);
                var answer = SumLit(numbers, targetLights);
                if (answer <= 0 || answer > MaxAnswer)
                {
                    continue;
                }

                var solution = CreateSolutionClicks(cellCount, gridSize, safeQuestionNumber, random);
                var initialLights = CopyLights(targetLights);
                for (var i = 0; i < solution.Length; i++)
                {
                    ToggleCross(initialLights, gridSize, solution[i]);
                }

                if (SumLit(numbers, initialLights) == answer)
                {
                    continue;
                }

                if (SolutionHasEarlyAnswer(numbers, initialLights, gridSize, solution, answer))
                {
                    continue;
                }

                return new LightsOutPuzzle
                {
                    QuestionNumber = safeQuestionNumber,
                    GridSize = gridSize,
                    Numbers = numbers,
                    InitialLights = initialLights,
                    TargetLights = targetLights,
                    TargetAnswer = answer,
                    Expression = BuildExpression(answer, random),
                    ReferenceSteps = solution.Length,
                    SolutionClickIndices = solution
                };
            }

            throw new InvalidOperationException("Unable to generate a valid LightsOut puzzle.");
        }

        internal static int ResolveGridSize(int questionNumber)
        {
            if (questionNumber <= 4)
            {
                return 3;
            }

            if (questionNumber <= 12)
            {
                return 4;
            }

            return 5;
        }

        internal static int SumLit(int[] numbers, bool[] lights)
        {
            if (numbers == null || lights == null || numbers.Length != lights.Length)
            {
                return 0;
            }

            var sum = 0;
            for (var i = 0; i < numbers.Length; i++)
            {
                if (lights[i])
                {
                    sum += numbers[i];
                }
            }

            return sum;
        }

        internal static void ToggleCross(bool[] lights, int gridSize, int index)
        {
            if (lights == null || gridSize <= 0 || index < 0 || index >= lights.Length)
            {
                return;
            }

            Toggle(lights, index);

            var row = index / gridSize;
            var column = index % gridSize;
            if (row > 0)
            {
                Toggle(lights, index - gridSize);
            }

            if (row < gridSize - 1)
            {
                Toggle(lights, index + gridSize);
            }

            if (column > 0)
            {
                Toggle(lights, index - 1);
            }

            if (column < gridSize - 1)
            {
                Toggle(lights, index + 1);
            }
        }

        internal static bool[] CopyLights(bool[] source)
        {
            if (source == null)
            {
                return Array.Empty<bool>();
            }

            var copy = new bool[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static int[] CreateNumbers(int cellCount, Random random)
        {
            var numbers = new int[cellCount];
            for (var i = 0; i < numbers.Length; i++)
            {
                numbers[i] = random.Next(1, 10);
            }

            return numbers;
        }

        private static bool[] CreateTargetLights(int[] numbers, Random random)
        {
            var targetLights = new bool[numbers.Length];
            var indices = CreateShuffledIndices(numbers.Length, random);
            var sum = 0;
            var targetCellCount = random.Next(1, Mathf.Min(5, numbers.Length) + 1);
            for (var i = 0; i < indices.Length; i++)
            {
                var index = indices[i];
                if (sum + numbers[index] > MaxAnswer)
                {
                    continue;
                }

                if (sum > 0 && CountLit(targetLights) >= targetCellCount && random.NextDouble() < 0.75d)
                {
                    continue;
                }

                targetLights[index] = true;
                sum += numbers[index];
                if (sum >= MaxAnswer || CountLit(targetLights) >= targetCellCount + 2)
                {
                    break;
                }
            }

            if (sum == 0)
            {
                var bestIndex = 0;
                for (var i = 1; i < numbers.Length; i++)
                {
                    if (numbers[i] < numbers[bestIndex])
                    {
                        bestIndex = i;
                    }
                }

                targetLights[bestIndex] = true;
            }

            return targetLights;
        }

        private static int[] CreateSolutionClicks(int cellCount, int gridSize, int questionNumber, Random random)
        {
            var minSteps = gridSize == 3 ? 2 : gridSize == 4 ? 3 : 4;
            var maxSteps = gridSize == 3 ? 3 : gridSize == 4 ? 5 : 7;
            if (questionNumber > 18)
            {
                maxSteps += 1;
            }

            var stepCount = random.Next(minSteps, Mathf.Min(maxSteps, cellCount) + 1);
            var indices = CreateShuffledIndices(cellCount, random);
            var solution = new int[stepCount];
            Array.Copy(indices, solution, stepCount);
            return solution;
        }

        private static bool SolutionHasEarlyAnswer(
            int[] numbers,
            bool[] initialLights,
            int gridSize,
            int[] solution,
            int answer)
        {
            var lights = CopyLights(initialLights);
            for (var i = 0; i < solution.Length; i++)
            {
                ToggleCross(lights, gridSize, solution[i]);
                var solved = SumLit(numbers, lights) == answer;
                if (i < solution.Length - 1 && solved)
                {
                    return true;
                }

                if (i == solution.Length - 1 && !solved)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildExpression(int answer, Random random)
        {
            if (answer == 20)
            {
                var left = random.Next(11, 20);
                return left + " + " + (answer - left) + " = ?";
            }

            if (answer > 1 && answer <= 18 && random.NextDouble() < 0.55d)
            {
                var minLeft = Mathf.Max(1, answer - 9);
                var maxLeft = Mathf.Min(9, answer - 1);
                var left = random.Next(minLeft, maxLeft + 1);
                var right = answer - left;
                return left + " + " + right + " = ?";
            }

            var subtrahend = random.Next(1, Mathf.Max(2, 21 - answer));
            var minuend = answer + subtrahend;
            return minuend + " - " + subtrahend + " = ?";
        }

        private static int[] CreateShuffledIndices(int count, Random random)
        {
            var indices = new int[count];
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            for (var i = indices.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(0, i + 1);
                var value = indices[i];
                indices[i] = indices[swapIndex];
                indices[swapIndex] = value;
            }

            return indices;
        }

        private static int CountLit(bool[] lights)
        {
            var count = 0;
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i])
                {
                    count++;
                }
            }

            return count;
        }

        private static void Toggle(bool[] lights, int index)
        {
            lights[index] = !lights[index];
        }
    }
}
