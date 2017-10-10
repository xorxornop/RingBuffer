using System;
using System.Buffers;
using System.Threading;

namespace RingBuffer
{
    /// <summary>
    ///     Simple ring/wrapping data buffer ("ringbuffer") with asynchronous I/O capability.
    /// </summary>
    /// <inheritdoc />
    public partial class RingBuffer<T> : IRingBuffer<T>, ISpanCapableRingBuffer<T>
        where T : struct
    {
        public RingBuffer(T[] buffer = null, bool allowOverwrite = false) : this(new Span<T>(buffer), allowOverwrite)
        {
        }

        /// <summary>
        /// Create a new ringbuffer instance using an existing backing <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="allowOverwrite">If <c>true</c>, allow the overwriting of existing, unread data by a new write operation (thus incurring the loss of this unread data).</param>
        public RingBuffer(Span<T> buffer, bool allowOverwrite = false)
        {
            if (buffer.Length < 2) {
                throw new ArgumentOutOfRangeException(nameof(buffer), "Capacity of buffer must be at least 2.");
            }

            CanOverwrite = allowOverwrite;
            BufferSpan = buffer;
        }

#if ALLOW_UNSAFE
        public unsafe RingBuffer(void* bufferPointer, int bufferLength, bool allowOverwrite = false) : this(new Span<T>(bufferPointer, bufferLength), allowOverwrite)
        {
        }
#endif

        public RingBuffer(int minimumCapacity, bool allowOverwrite = false)
        {
            if (minimumCapacity < 2) {
                throw new ArgumentOutOfRangeException(nameof(minimumCapacity), "Capacity must be at least 2.");
            }

            CanOverwrite = allowOverwrite;

            // Calculate the next power of 2, greater than or equal to minimumCapacity, inclusive.
            var capacity = 2;
            while (capacity < minimumCapacity) {
                capacity <<= 1;
            }

            BufferSpan = new Span<T>(new T[capacity]);
        }

        /// <inheritdoc />
        public bool CanOverwrite { get; }

        /// <inheritdoc />
        public int Capacity => BufferSpan.Length;

        /// <inheritdoc />
        public int Allocated
        {
            get {
                using (new ReadLock(this)) {
                    using (new WriteLock(this)) {
                        if (BufferSpanTail > BufferSpanHead) {
                            // Allocated span does not wrap to reach tail
                            // Example:
                            // |________|
                            // |___#####| Length == 5/8
                            //
                            return BufferSpanTail - BufferSpanHead;
                        }

                        // Allocated span of buffer wraps to reach tail
                        // Example:
                        // |________|
                        // |##___###| Length == 5/8
                        //
                        return (Capacity - BufferSpanHead) + BufferSpanTail;
                    }
                }
            }
        }

        /// <inheritdoc />
        public int Unallocated => Capacity - Allocated;

        /// <summary>
        /// Backing buffer
        /// </summary>
        public ReadOnlySpan<T> Buffer => BufferSpan;

        protected Span<T> BufferSpan;



        /// <summary>
        /// The buffer span's head offset, where reading begins.
        /// </summary>
        protected volatile int BufferSpanHead;

        /// <summary>
        /// The buffer span's tail offset, where writing begins. Can be equal to <see cref="Capacity"/> to denote the case when 
        /// <c><see cref="BufferSpanHead"/> == 0 &amp; &amp; <see cref="Allocated"/> == <see cref="Capacity"/></c>
        /// </summary>
        /// <remarks>
        /// Can be equal to <see cref="Capacity"/> to denote the case when 
        /// <c><see cref="BufferSpanHead"/> == 0 &amp;&amp; <see cref="Allocated"/> == <see cref="Capacity"/></c>
        /// </remarks>
        protected volatile int BufferSpanTail;

        protected internal volatile bool Reading;
        protected internal volatile bool Writing;

        protected virtual ArrayPool<T> GetArrayPool() => ArrayPool<T>.Shared;
        protected virtual T[] RentArray(int minimumLength) => GetArrayPool().Rent(minimumLength);
        protected virtual void ReturnArray(T[] rentedArray) => GetArrayPool().Return(rentedArray);


        /// <inheritdoc />
        public void Reset()
        {
            // Lock until any read and/or write has completed
            using (new ReadLock(this)) {
                using (new WriteLock(this)) {
                    BufferSpan.Clear();
                }
            }
        }

        /// <inheritdoc />
        public void Skip(int length) => SkipInternal(length);

        protected void SkipInternal(int length, ReadLock? readLock = null)
        {
            if (length > Capacity) {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    "Ringbuffer capacity insufficient for skip operation: cannot proceed (requested length exceeds capacity).");
            }

            using (readLock ?? new ReadLock(this)) {
                if (length > Allocated) {
                    throw new ArgumentException(
                        "Ringbuffer allocation insufficient for skip operation: cannot proceed (requested length exceeds allocated length).",
                        nameof(length));
                }

                while (length > 0) {
                    if (BufferSpanHead == Capacity) {
                        BufferSpanHead = 0;
                    }
                    BufferSpanHead += Math.Min(Capacity - BufferSpanHead, length);
                }
            }
        }

        /// <inheritdoc />
        public T[] ToArray() => BufferSpan.ToArray();
    }
}
