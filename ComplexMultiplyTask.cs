using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace PARP_LR3_CSharp
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ComplexF
    {
        public readonly float Re;
        public readonly float Im;

        public ComplexF(float re, float im)
        {
            Re = re;
            Im = im;
        }

        // (a + bi)(c + di) = (ac - bd) + (ad + bc)i
        public static ComplexF operator *(ComplexF y, ComplexF z)
        {
            return new ComplexF(y.Re * z.Re - y.Im * z.Im,
                                y.Re * z.Im + y.Im * z.Re);
        }
    }

    internal static unsafe class ComplexMultiplyTask
    {
        private const int ArraySize = 4096;

        private const byte RealParts = 0b10_10_00_00; // lanes [0, 0, 2, 2]: [c0, c0, c1, c1]
        private const byte ImagParts = 0b11_11_01_01; // lanes [1, 1, 3, 3]: [d0, d0, d1, d1]
        private const byte SwapReIm  = 0b10_11_00_01; // lanes [1, 0, 3, 2]: [b0, a0, b1, a1]

        public static void Run()
        {
            Console.WriteLine("Task 8: x[i] = y[i] * z[i], complex float\n");
            Benchmark.PrintTable(ArraySize, new[] { Measure() });
        }

        private static void MultiplyScalar(ComplexF* y, ComplexF* z, ComplexF* x, int n)
        {
            for (int i = 0; i < n; i++)
            {
                x[i] = y[i] * z[i];
            }
        }


        //   y              = [a0,   b0,   a1,   b1  ]
        //   z              = [c0,   d0,   c1,   d1  ]
        //   zRe            = [c0,   c0,   c1,   c1  ]
        //   zIm            = [d0,   d0,   d1,   d1  ]
        //   ySwapped       = [b0,   a0,   b1,   a1  ]
        //   y * zRe        = [a0c0, b0c0, a1c1, b1c1]
        //   ySwapped * zIm = [b0d0, a0d0, b1d1, a1d1]  -> change sign of lanes 0 and 2
        //   sum            = [a0c0 - b0d0, b0c0 + a0d0, a1c1 - b1d1, b1c1 + a1d1]
        //                  = [Re0,         Im0,         Re1,         Im1        ]

        private static void MultiplySse(ComplexF* y, ComplexF* z, ComplexF* x, int n)
        {
            // XOR with -0.0f flips only the sign bit, so this negates lanes 0 and 2
            Vector128<float> negateReal = Vector128.Create(-0.0f, 0.0f, -0.0f, 0.0f);

            int i = 0;
            for (; i + 2 <= n; i += 2)
            {
                Vector128<float> yv = Sse.LoadAlignedVector128((float*)(y + i));
                Vector128<float> zv = Sse.LoadAlignedVector128((float*)(z + i));

                Vector128<float> zRe = Sse.Shuffle(zv, zv, RealParts);
                Vector128<float> zIm = Sse.Shuffle(zv, zv, ImagParts);
                Vector128<float> ySwapped = Sse.Shuffle(yv, yv, SwapReIm);

                Vector128<float> first = Sse.Multiply(yv, zRe);
                Vector128<float> second = Sse.Xor(Sse.Multiply(ySwapped, zIm), negateReal);

                Sse.StoreAligned((float*)(x + i), Sse.Add(first, second));
            }
            for (; i < n; i++)
            {
                x[i] = y[i] * z[i];
            }
        }

        private static void MultiplyAvx(ComplexF* y, ComplexF* z, ComplexF* x, int n)
        {
            Vector256<float> negateReal = Vector256.Create(-0.0f, 0.0f, -0.0f, 0.0f, -0.0f, 0.0f, -0.0f, 0.0f);

            int i = 0;
            for (; i + 4 <= n; i += 4)
            {
                Vector256<float> yv = Avx.LoadAlignedVector256((float*)(y + i));
                Vector256<float> zv = Avx.LoadAlignedVector256((float*)(z + i));

                Vector256<float> zRe = Avx.Shuffle(zv, zv, RealParts);
                Vector256<float> zIm = Avx.Shuffle(zv, zv, ImagParts);
                Vector256<float> ySwapped = Avx.Shuffle(yv, yv, SwapReIm);

                Vector256<float> first = Avx.Multiply(yv, zRe);
                Vector256<float> second = Avx.Xor(Avx.Multiply(ySwapped, zIm), negateReal);

                Avx.StoreAligned((float*)(x + i), Avx.Add(first, second));
            }
            for (; i < n; i++) // the last n % 4 elements
            {
                x[i] = y[i] * z[i];
            }
        }

        private static BenchmarkResult Measure()
        {
            int n = ArraySize;
            Random random = new Random(12345);

            using var input1 = new AlignedArray<ComplexF>(n);
            using var input2 = new AlignedArray<ComplexF>(n);
            using var expected = new AlignedArray<ComplexF>(n);
            using var output = new AlignedArray<ComplexF>(n);

            ComplexF* y = input1.Pointer;
            ComplexF* z = input2.Pointer;
            ComplexF* expectedX = expected.Pointer;
            ComplexF* x = output.Pointer;

            for (int i = 0; i < n; i++)
            {
                y[i] = RandomComplex(random);
                z[i] = RandomComplex(random);
            }

            double scalarTime = Benchmark.MeasureMicroseconds(() => MultiplyScalar(y, z, expectedX, n), n);

            double? MeasureSimd(string name, Action function, bool supported)
            {
                if (!supported)
                {
                    return null;
                }

                output.Clear();
                double time = Benchmark.MeasureMicroseconds(function, n);

                ReadOnlySpan<float> expectedParts = MemoryMarshal.Cast<ComplexF, float>(expected.AsSpan());
                ReadOnlySpan<float> actualParts = MemoryMarshal.Cast<ComplexF, float>(output.AsSpan());
                if (!Benchmark.AreClose(expectedParts, actualParts))
                {
                    Console.WriteLine($"Wrong result: complex, {name}");
                }
                return time;
            }

            double? sseTime = MeasureSimd("SSE", () => MultiplySse(y, z, x, n), Sse.IsSupported);
            double? avxTime = MeasureSimd("AVX", () => MultiplyAvx(y, z, x, n), Avx.IsSupported);

            return new BenchmarkResult("complex", scalarTime, sseTime, avxTime);
        }

        private static ComplexF RandomComplex(Random random)
        {
            return new ComplexF(random.NextSingle() * 200.0f - 100.0f,
                                random.NextSingle() * 200.0f - 100.0f);
        }
    }
}
