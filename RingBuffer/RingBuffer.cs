using System;
using System.Buffers;

namespace RingBuffer
{
    public class RingBuffer<T> : IRingBuffer<T>, ISpanCapableRingBuffer<T>
        where T : struct
    {
        public RingBuffer(int minimumCapacity, bool allowOverwrite = false)
        {
            if (minimumCapacity < 2) {
                throw new ArgumentOutOfRangeException(nameof(minimumCapacity), "Capacity must be at least 2.");
            }

            CanOverwrite = allowOverwrite;
            BufferSpan = new Span<T>(new T[CeilingNextPowerOfTwo(minimumCapacity)]);
        }

        public RingBuffer(T[] buffer = null, bool allowOverwrite = false)
        {
            if (buffer.Length < 2) {
                throw new ArgumentOutOfRangeException(nameof(buffer), "Capacity of buffer must be at least 2.");
            }

            CanOverwrite = allowOverwrite;
            BufferSpan = new Span<T>(buffer);
        }

        /// <summary>
        ///     Calculate the next power of 2, greater than or equal to x.
        /// </summary>
        /// <param name="x">Value to round up to the next power.</param>
        /// <returns>The next power of 2 from <paramref name="x"/>, inclusive.</returns>
        protected static int CeilingNextPowerOfTwo(int x)
        {
            var result = 2;
            while (result < x) {
                result <<= 1;
            }

            return result;
        }

        public bool CanOverwrite { get; }

        public int Capacity => BufferSpan.Length;

        public int Allocated
        {
            get {
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

        public int Unallocated => Capacity - Allocated;

        object _lock = new object();

        protected Span<T> BufferSpan;

        /// <summary>
        /// The buffer span's head offset, where reading begins.
        /// </summary>
        protected int BufferSpanHead;

        /// <summary>
        /// The buffer span's tail offset, where writing begins. Can be equal to <see cref="Capacity"/> to denote the case when 
        /// <c><see cref="BufferSpanHead"/> == 0 &amp; &amp; <see cref="Allocated"/> == <see cref="Capacity"/></c>
        /// </summary>
        /// <remarks>
        /// Can be equal to <see cref="Capacity"/> to denote the case when 
        /// <c><see cref="BufferSpanHead"/> == 0 &amp;&amp; <see cref="Allocated"/> == <see cref="Capacity"/></c>
        /// </remarks>
        protected int BufferSpanTail;

        protected virtual ArrayPool<T> GetArrayPool() => ArrayPool<T>.Shared;
        protected virtual T[] RentArray(int minimumLength) => GetArrayPool().Rent(minimumLength);
        protected virtual void ReturnArray(T[] rentedArray) => GetArrayPool().Return(rentedArray);

        public T Read()
        {
            if (Allocated == 0) {
                throw new InvalidOperationException("Ringbuffer contents insufficient for read operation.");
            }

            if (BufferSpanHead == Capacity) {
                BufferSpanHead = 0;
            }

            T output = BufferSpan[BufferSpanHead++];
            return output;
        }

        /// <inheritdoc />
        public T[] Read(int quantity)
        {
            var array = new T[quantity];
            var destSpan = new Span<T>(array);
            ReadBufferInto(destSpan);
            return array;
        }

        /// <inheritdoc />
        public void ReadInto(ArraySegment<T> arraySegment)
        {
            ReadBufferInto(arraySegment);
        }

        /// <inheritdoc />
        public void ReadInto(T[] array)
        {
            var destSpan = new Span<T>(array);
            ReadBufferInto(destSpan);
        }

        /// <inheritdoc />
        public void ReadInto(T[] array, int offset, int count)
        {
            var destSpan = new Span<T>(array, offset, count);
            ReadBufferInto(destSpan);
        }

        protected void ReadBufferInto(Span<T> destSpan)
        {
            var start = 0;
            var length = destSpan.Length;

            while (length > 0) {
                var srcSlice = BufferSpan.Slice(BufferSpanHead, Math.Min(Capacity - BufferSpanHead, length));
                var destSlice = destSpan.Slice(start, srcSlice.Length);
                srcSlice.CopyTo(destSlice);

                BufferSpanHead = BufferSpanHead + destSlice.Length;
                start += destSlice.Length;
                length -= destSlice.Length;
            }
        }

        /// <inheritdoc />
        void ISpanCapableRingBuffer<T>.ReadInto(Span<T> destSpan)
        {
            ReadBufferInto(destSpan);
        }

        /// <inheritdoc />
        Span<T> ISpanCapableRingBuffer<T>.ReadAsSpan(int length)
        {
            // If allocated span can be read and returned as a true slice (no wrapping), 
            // return that, otherwise allocate an array of correct size, and return it as a span.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        Span<T> ISpanCapableRingBuffer<T>.ReadAsSpan(ref int length)
        {
            // Return span until the wrapping point, where applicable (ensures it is a true slice), 
            // returning the remaining length, whatever it might be (perhaps zero) via the ref var.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Write(T item)
        {
            if (Allocated + 1 > Capacity) {
                if (CanOverwrite) {
                    Skip(1);
                } else {
                    throw new InvalidOperationException("Ringbuffer capacity insufficient for put/write operation.");
                }
            }

            if (BufferSpanTail == Capacity) {

                BufferSpan[0] = item; // Zero because this is the tail's real start/position
                BufferSpanTail = 1; // Tail now corrected for position, post-wrap
            } else {
                BufferSpan[BufferSpanTail++] = item;
            }
        }

        /// <inheritdoc />
        public Span<T> ReadAllAsSpan()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Write(T[] array)
        {
            ((ISpanCapableRingBuffer<T>)this).Write(new Span<T>(array));
        }

        /// <inheritdoc />
        public void Write(ArraySegment<T> arraySegment)
        {
            ((ISpanCapableRingBuffer<T>)this).Write(arraySegment);
        }

        /// <inheritdoc />
        public void WriteFrom(T[] array, int offset, int count)
        {
            var srcSpan = new Span<T>(array, offset, count);
            ((ISpanCapableRingBuffer<T>)this).Write(new Span<T>(array, offset, count));
        }

        protected void WriteIntoBuffer(Span<T> srcSpan)
        {
            if (Allocated + srcSpan.Length > Capacity) {
                if (CanOverwrite) {
                    int skip = Capacity - (Allocated + srcSpan.Length);
                    Skip(skip);
                } else {
                    throw new ArgumentException("Ringbuffer capacity insufficient for write/put operation." +
                                                "To do so would be strictly lossy, resulting in overwriting of preceding data within the same operation.");
                }
            }

            var start = 0;
            var length = srcSpan.Length;

            while (length > 0) {
                if (BufferSpanTail == Capacity) {
                    BufferSpanTail = 0;
                }
                var destSlice = BufferSpan.Slice(BufferSpanTail, Math.Min(Capacity - BufferSpanTail, length));
                var srcSlice = srcSpan.Slice(start, destSlice.Length);
                srcSlice.CopyTo(destSlice);
                BufferSpanTail = BufferSpanTail + srcSlice.Length;
                start += srcSlice.Length;
                length -= srcSlice.Length;
            }
        }

        /// <inheritdoc />
        void ISpanCapableRingBuffer<T>.Write(Span<T> srcSpan)
        {
            WriteIntoBuffer(srcSpan);
        }

        /// <inheritdoc />
        public void Reset()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Skip(int length)
        {
            if (length > Capacity) {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    "Ringbuffer capacity insufficient for skip operation (requested length exceeds capacity).");
            } else if (length > Allocated) {
                throw new ArgumentException(
                    "Ringbuffer capacity insufficient for skip operation (requested length exceeds capacity).",
                    nameof(length));
            }

            if (BufferSpanTail > BufferSpanHead) {
                // Allocated span does not wrap to reach tail:
                BufferSpanHead += length;
            }

            // Allocated span of buffer wraps to reach tail:
            if (Capacity - BufferSpanHead >= length) {
                // Skip to before or to the wrapping point (end of buffer)
                BufferSpanHead += length;
            } else {
                // Skip to after the wrapping point (end of buffer)
                BufferSpanHead = length - (Capacity - BufferSpanHead);
            }
        }

        /// <inheritdoc />
        public T[] ToArray()
        {
            return BufferSpan.ToArray();
        }
    }
}
