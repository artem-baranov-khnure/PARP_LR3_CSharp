using System.Globalization;
using System.Runtime.Intrinsics.X86;

namespace PARP_LR3_CSharp
{
    internal static class Program
    {
        private const string Separator = "-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=";

        static void Main()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Console.WriteLine($"SSE: {YesNo(Sse.IsSupported)}, SSE2: {YesNo(Sse2.IsSupported)}, " +
                              $"AVX: {YesNo(Avx.IsSupported)}, AVX2: {YesNo(Avx2.IsSupported)}\n");

            Console.WriteLine($"Tasks 6, 7 {Separator}\n");
            SqrtTask.Run();

            Console.WriteLine($"\n\nTask 8 {Separator}\n");
            ComplexMultiplyTask.Run();
        }

        private static string YesNo(bool value) => value ? "yes" : "no";
    }
}
