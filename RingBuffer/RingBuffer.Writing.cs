using System;
using System.Threading;

namespace RingBuffer
{
    partial class RingBuffer<T>
    {
        /// <inheritdoc />
        public void Write(T item)
        {
            using (new WriteLock(this)) {
                if (Allocated + 1 > Capacity) {
                    if (CanOverwrite) {
                        Skip(1);
                    } else {
                        throw new InvalidOperationException("Ringbuffer capacity insufficient for write operation: cannot proceed (overwriting disabled).");
                    }
                }

                if (BufferSpanTail == Capacity) {

                    BufferSpan[0] = item; // Zero because this is the tail's real start/position
                    BufferSpanTail = 1; // Tail now corrected for position, post-wrap
                } else {
                    BufferSpan[BufferSpanTail++] = item;
                }
            }
        }

        /// <inheritdoc />
        public void Write(T[] array) => WriteIntoBufferFrom(new Span<T>(array));

        /// <inheritdoc />
        public void Write(ArraySegment<T> arraySegment) => WriteIntoBufferFrom(arraySegment);

        /// <inheritdoc />
        void ISpanCapableRingBuffer<T>.WriteSpan(Span<T> srcSpan) => WriteIntoBufferFrom(srcSpan);

        /// <summary>
        /// Write contents of <paramref name="srcSpan"/> into the ringbuffer.
        /// </summary>
        /// <param name="srcSpan">Source span.</param>
        void WriteIntoBufferFrom(Span<T> srcSpan)
        {
            if (Allocated + srcSpan.Length > Capacity) {
                if (CanOverwrite) {
                    var skip = Capacity - (Allocated + srcSpan.Length);
                    Skip(skip);
                } else {
                    throw new ArgumentException("Ringbuffer capacity insufficient for write operation: cannot proceed (overwriting disabled). " +
                                                "Performing this operation would be inevitably lossy, as it would result in overwriting some preceding data from this same operation .");
                }
            }

            var start = 0;
            var length = srcSpan.Length;

            while (length > 0) {
                if (BufferSpanTail == Capacity) {
                    BufferSpanTail = 0;
                }
                Span<T> destSlice = BufferSpan.Slice(BufferSpanTail, Math.Min(Capacity - BufferSpanTail, length));
                Span<T> srcSlice = srcSpan.Slice(start, destSlice.Length);
                srcSlice.CopyTo(destSlice);
                BufferSpanTail = BufferSpanTail + srcSlice.Length;
                start += srcSlice.Length;
                length -= srcSlice.Length;
            }
        }
    }
}
