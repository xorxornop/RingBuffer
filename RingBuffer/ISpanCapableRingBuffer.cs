using System;

namespace RingBuffer
{
    /// <summary>
    ///     Interface for a simple ring/wrapping data buffer ("ringbuffer") with 
    ///     support for I/O operations with/using <see cref="Span{T}"/>s.
    /// </summary>
    /// <typeparam name="T">Type of items stored in the buffer.</typeparam>
    public interface ISpanCapableRingBuffer<T> : IRingBuffer<T>
        where T : struct
    {
        #region Reading

        /// <summary>
        /// Read <typeparamref name="T"/>s from the ringbuffer into <paramref name="destSpan"/>.
        /// </summary>
        /// <param name="destSpan"></param>
        void ReadIntoSpan(Span<T> destSpan);

        /// <summary>
        ///     Read <paramref name="length" /> of <typeparamref name="T"/>s from the ringbuffer into 
        ///     a <see cref="RingBuffer{T}.SpanReadLock"/> containing a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="length">Quantity of <typeparamref name="T"/>s to read/take from the ringbuffer.</param>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> of the <typeparamref name="T"/>s to read from the ringbuffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative, or greater than <see cref="IRingBuffer{T}.Capacity"/>.</exception>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="IRingBuffer{T}.Allocated"/>) is insufficient for performing a read.</exception>
        RingBuffer<T>.SpanReadLock ReadSpanExact(int length);

        /// <summary>
        ///     Read <typeparamref name="T"/>s from the ringbuffer - until it reaches the wrapping point, or the <paramref name="length"/> is reached - 
        ///     into a <see cref="RingBuffer{T}.SpanReadLock"/> containing a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        RingBuffer<T>.SpanReadLock ReadSpan(ref int length);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        (RingBuffer<T>.SpanReadLock readLock, int remainingLength) ReadSpan(int length);

        /// <summary>
        ///     Read/take all <typeparamref name="T"/>s from the ringbuffer into 
        ///     a <see cref="RingBuffer{T}.SpanReadLock"/> containing a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <returns>A <see cref="Span{T}"/> of the <typeparamref name="T"/>s to read from the ringbuffer.</returns>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="IRingBuffer{T}.Allocated"/>) is insufficient for performing a read.</exception>
        RingBuffer<T>.SpanReadLock ReadAllAsSpan();

        #endregion

        #region Writing

        /// <summary>
        ///     Write <typeparamref name="T"/>s from <paramref name="input" /> into the ringbuffer.
        /// </summary>
        /// <param name="input"><see cref="Span{T}"/> for the <typeparamref name="T"/>s to write into the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Quantity of unallocated capacity (<see cref="base.Unallocated"/>) is insufficient for performing a write.</exception>
        void WriteSpan(Span<T> input);

        #endregion
    }
}
