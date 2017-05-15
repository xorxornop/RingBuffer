using System;

namespace RingBuffer
{
    /// <summary>
    ///     Interface for a simple ring/cyclic-byte-data buffer ("ringbuffer") with 
    ///     support for I/O operations [to/from/with/using] <see cref="Span{T}"/>s.
    /// </summary>
    public interface ISpanCapableRingBuffer<T> : IRingBuffer<T>
        where T : struct
    {
        void ReadInto(Span<T> destSpan);

        /// <summary>
        ///     Read/take <paramref name="length" /> of <typeparamref name="T"/>s from the ringbuffer into a <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="length">Quantity of <typeparamref name="T"/>s to read/take from the ringbuffer.</param>
        /// <returns>A <see cref="Span{T}"/> representation of the <typeparamref name="T"/>s that have been read from the ringbuffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative, or greater than <see cref="IRingBuffer{T}.Capacity"/>.</exception>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="IRingBuffer{T}.Allocated"/>) is insufficient for performing a read.</exception>
        Span<T> ReadAsSpan(int length);

        Span<T> ReadAsSpan(ref int length);

        /// <summary>
        ///     Read/take all <typeparamref name="T"/>s from the ringbuffer into a <see cref="Span{T}"/>.
        /// </summary>
        /// <returns>A <see cref="Span{T}"/> representation of the <typeparamref name="T"/>s that have been read from the ringbuffer.</returns>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="IRingBuffer{T}.Allocated"/>) is insufficient for performing a read.</exception>
        Span<T> ReadAllAsSpan();

        /// <summary>
        ///     Write/put this <paramref name="input" /> data into the ringbuffer.
        /// </summary>
        /// <param name="input"><see cref="Span{T}"/> of <typeparamref name="T"/>s to write into the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Quantity of unallocated capacity (<see cref="base.Unallocated"/>) is insufficient for performing a write.</exception>
        void Write(Span<T> input);
    }
}
