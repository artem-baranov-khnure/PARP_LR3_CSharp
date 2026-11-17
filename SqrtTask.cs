using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace PARP_LR3_CSharp
{
    internal static unsafe class SqrtTask
    {
        private const int ArraySize = 4096;

        private delegate void SqrtFunction<T>(T* y, T* x, int n) where T : unmanaged;

        public static void Run()
        {
            Console.WriteLine("Tasks 6, 7: x[i] = sqrt(y[i])\n");

            BenchmarkResult floatResult = Measure<float>("float", SqrtScalar, SqrtSse, Sse.IsSupported, SqrtAvx, Avx.IsSupported);
            BenchmarkResult doubleResult = Measure<double>("double", SqrtScalar, SqrtSse, Sse2.IsSupported, SqrtAvx, Avx.IsSupported);

            Benchmark.PrintTable(ArraySize, new[] { floatResult, doubleResult });
        }

        private static void SqrtScalar(float* y, float* x, int n)
        {
            for (int i = 0; i < n; i++)
            {
                x[i] = MathF.Sqrt(y[i]);
            }
        }

        private static void SqrtScalar(double* y, double* x, int n)
        {
            for (int i = 0; i < n; i++)
            {
                x[i] = Math.Sqrt(y[i]);
            }
        }

        private static void SqrtSse(float* y, float* x, int n)
        {
            int i = 0;
            for (; i + 4 <= n; i += 4)
            {
                Vector128<float> v = Sse.LoadAlignedVector128(y + i);
                Sse.StoreAligned(x + i, Sse.Sqrt(v));
            }
            for (; i < n; i++)
            {
                x[i] = MathF.Sqrt(y[i]);
            }
        }

        private static void SqrtSse(double* y, double* x, int n)
        {
            int i = 0;
            for (; i + 2 <= n; i += 2)
            {
                Vector128<double> v = Sse2.LoadAlignedVector128(y + i);
                Sse2.StoreAligned(x + i, Sse2.Sqrt(v));
            }
            for (; i < n; i++)
            {
                x[i] = Math.Sqrt(y[i]);
            }
        }

        private static void SqrtAvx(float* y, float* x, int n)
        {
            int i = 0;
            for (; i + 8 <= n; i += 8)
            {
                Vector256<float> v = Avx.LoadAlignedVector256(y + i);
                Avx.StoreAligned(x + i, Avx.Sqrt(v));
            }
            for (; i < n; i++)
            {
                x[i] = MathF.Sqrt(y[i]);
            }
        }

        private static void SqrtAvx(double* y, double* x, int n)
        {
            int i = 0;
            for (; i + 4 <= n; i += 4)
            {
                Vector256<double> v = Avx.LoadAlignedVector256(y + i);
                Avx.StoreAligned(x + i, Avx.Sqrt(v));
            }
            for (; i < n; i++)
            {
                x[i] = Math.Sqrt(y[i]);
            }
        }

        private static BenchmarkResult Measure<T>(string typeName,
            SqrtFunction<T> scalar,
            SqrtFunction<T> sse, bool sseSupported,
            SqrtFunction<T> avx, bool avxSupported)
            where T : unmanaged, IFloatingPoint<T>
        {
            int n = ArraySize;
            Random random = new Random(12345);

            using var input = new AlignedArray<T>(n);
            using var expected = new AlignedArray<T>(n);
            using var output = new AlignedArray<T>(n);

            T* y = input.Pointer;
            T* expectedX = expected.Pointer;
            T* x = output.Pointer;

            for (int i = 0; i < n; i++)
            {
                y[i] = T.CreateTruncating(random.NextSingle() * 1000.0f);
            }

            double scalarTime = Benchmark.MeasureMicroseconds(() => scalar(y, expectedX, n), n);

            double? MeasureSimd(string name, SqrtFunction<T> function, bool supported)
            {
                if (!supported)
                {
                    return null;
                }

                output.Clear();
                double time = Benchmark.MeasureMicroseconds(() => function(y, x, n), n);

                if (!Benchmark.AreClose<T>(expected.AsSpan(), output.AsSpan()))
                {
                    Console.WriteLine($"Wrong result: {typeName}, {name}");
                }
                return time;
            }

            double? sseTime = MeasureSimd("SSE", sse, sseSupported);
            double? avxTime = MeasureSimd("AVX", avx, avxSupported);

            return new BenchmarkResult(typeName, scalarTime, sseTime, avxTime);
        }
    }
}
