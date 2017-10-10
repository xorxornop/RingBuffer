using System;

namespace RingBuffer
{
    partial class RingBuffer<T>
    {
        /// <inheritdoc />
        public T Read()
        {
            if (Reading) {
                throw new NotSupportedException("Buffer cannot perform parallel/concurrent reads.");
            }
            if (Allocated == 0) {
                throw new InvalidOperationException("Buffer allocation insufficient for read operation.");
            }

            try {
                Reading = true;

                if (BufferSpanHead == Capacity) {
                    BufferSpanHead = 0;
                }

                T output = BufferSpan[BufferSpanHead++];
                return output;
            }
            finally {
                Reading = false;
            }
        }

        /// <inheritdoc />
        public T[] Read(int length)
        {
            T[] array;
            try {
                Reading = true;



                array = RentArray(length);
                var destSpan = new Span<T>(array);
                ReadFromBufferInto(destSpan);
                return array;
            }
            finally {
                Reading = false;
            }
        }

        /// <inheritdoc />
        public void ReadInto(T[] array) => ReadFromBufferInto(new Span<T>(array));

        /// <inheritdoc />
        public void ReadInto(ArraySegment<T> arraySegment) => ReadFromBufferInto(arraySegment);

        /// <inheritdoc />
        void ISpanCapableRingBuffer<T>.ReadIntoSpan(Span<T> destSpan) => ReadFromBufferInto(destSpan);

        /// <inheritdoc />
        SpanReadLock ISpanCapableRingBuffer<T>.ReadSpanExact(int length)
        {
            var readLock = new ReadLock(this);

            // If allocated span can be read and returned as a true slice (no wrapping), 
            // return that, otherwise allocate an array of correct size, and return it as a span.

            if (Capacity - BufferSpanHead > length) {
                // Wrapping read; prepare array:
                T[] array = RentArray(length);
                ReadFromBufferInto(new Span<T>(array));

                return new SpanReadLock(readLock, array, length);
            } else {
                return new SpanReadLock(readLock, BufferSpan.Slice(BufferSpanHead, length));
            }
        }

        /// <inheritdoc />
        SpanReadLock ISpanCapableRingBuffer<T>.ReadSpan(ref int length)
        {
            var readLock = new ReadLock(this);

            if (BufferSpanHead == Capacity) {
                BufferSpanHead = 0;
            }

            // Return span until the wrapping point, where applicable (ensures it is a true slice), 
            // returning the remaining length, whatever it might be (perhaps zero) via the ref var.

            ReadOnlySpan<T> readSlice = BufferSpan.Slice(BufferSpanHead, Math.Min(Capacity - BufferSpanHead, length));
            length -= readSlice.Length;

            return new SpanReadLock(readLock, readSlice);
        }

        /// <inheritdoc />
        (SpanReadLock readLock, int remainingLength) ISpanCapableRingBuffer<T>.ReadSpan(int length) =>
            (((ISpanCapableRingBuffer<T>)this).ReadSpan(ref length), length);

        /*
         * SpanReadLock rl = ((ISpanCapableRingBuffer<T>)this).ReadSpan(ref length);
         *   return (rl, length);
         */

        /// <inheritdoc />
        SpanReadLock ISpanCapableRingBuffer<T>.ReadAllAsSpan() => ((ISpanCapableRingBuffer<T>)this).ReadSpanExact(Allocated);

        /// <summary>
        /// Read buffer content into <paramref name="destination"/> until full.
        /// </summary>
        /// <param name="destination">Destination span.</param>
        void ReadFromBufferInto(Span<T> destination)
        {
            using (new ReadLock(this)) {
                var start = 0;
                var length = destination.Length;

                while (length > 0) {
                    if (BufferSpanHead == Capacity) {
                        BufferSpanHead = 0;
                    }
                    Span<T> srcSlice = BufferSpan.Slice(BufferSpanHead, Math.Min(Capacity - BufferSpanHead, length));
                    Span<T> dstSlice = destination.Slice(start, srcSlice.Length);
                    srcSlice.CopyTo(dstSlice);

                    BufferSpanHead = BufferSpanHead + dstSlice.Length;
                    start += dstSlice.Length;
                    length -= dstSlice.Length;
                }
            }
        }
    }
}
