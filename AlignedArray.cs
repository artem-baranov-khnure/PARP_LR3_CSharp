using System.Runtime.InteropServices;

namespace PARP_LR3_CSharp
{
    internal sealed unsafe class AlignedArray<T> : IDisposable where T : unmanaged
    {
        private const int Alignment = 64;

        public T* Pointer { get; private set; }
        public int Length { get; }

        public AlignedArray(int length)
        {
            Length = length;
            Pointer = (T*)NativeMemory.AlignedAlloc((nuint)(length * sizeof(T)), Alignment);
        }

        public Span<T> AsSpan() => new Span<T>(Pointer, Length);

        public void Clear() => AsSpan().Clear();

        public void Dispose()
        {
            NativeMemory.AlignedFree(Pointer);
            Pointer = null;
        }
    }
}
