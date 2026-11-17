using System.Diagnostics;
using System.Numerics;

namespace PARP_LR3_CSharp
{
    internal record BenchmarkResult(string Type, double ScalarTime, double? SseTime, double? AvxTime);

    internal static class Benchmark
    {
        private const int SampleCount = 10;

        public static double MeasureMicroseconds(Action function, int arraySize)
        {
            int callsPerSample = Math.Clamp((1 << 25) / arraySize, 2, 200_000);

            function();

            double bestTime = double.MaxValue;
            for (int sample = 0; sample < SampleCount; sample++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int call = 0; call < callsPerSample; call++)
                {
                    function();
                }
                stopwatch.Stop();

                bestTime = Math.Min(bestTime, stopwatch.Elapsed.TotalMicroseconds / callsPerSample);
            }
            return bestTime;
        }

        public static bool AreClose<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual) where T : IFloatingPoint<T>
        {
            const double RelativeTolerance = 1e-5;

            for (int i = 0; i < expected.Length; i++)
            {
                double e = double.CreateTruncating(expected[i]);
                double a = double.CreateTruncating(actual[i]);
                if (Math.Abs(e - a) > RelativeTolerance * Math.Abs(e))
                {
                    return false;
                }
            }
            return true;
        }

        public static void PrintTable(int arraySize, IEnumerable<BenchmarkResult> results)
        {
            Console.WriteLine($"Array size = {arraySize}");
            Console.WriteLine($"{"Type",-8}{"Scalar, microsec",18}{"SSE, microsec",18}{"AVX, microsec",18}{"SSE speedup",14}{"AVX speedup",14}");

            foreach (BenchmarkResult r in results)
            {
                string sseSpeedup = FormatOrDash(r.ScalarTime / r.SseTime, "F2");
                string avxSpeedup = FormatOrDash(r.ScalarTime / r.AvxTime, "F2");

                Console.WriteLine($"{r.Type,-8}{r.ScalarTime,18:F3}{FormatOrDash(r.SseTime, "F3"),18}{FormatOrDash(r.AvxTime, "F3"),18}{sseSpeedup,14}{avxSpeedup,14}");
            }
            Console.WriteLine();
        }

        private static string FormatOrDash(double? value, string format)
        {
            return value.HasValue ? value.Value.ToString(format) : "-";
        }
    }
}
