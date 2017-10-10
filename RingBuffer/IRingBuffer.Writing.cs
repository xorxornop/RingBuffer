using System;

namespace RingBuffer
{
    partial interface IRingBuffer<T>
    {
        /// <summary>
        ///     Write/put a single <typeparamref name="T"/> with a value from <paramref name="input" /> into the buffer.
        /// </summary>
        /// <param name="input">Single <typeparamref name="T"/> to write into the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Quantity of unallocated capacity (<see cref="Unallocated"/>) is insufficient for performing a write.</exception>
        void Write(T item);

        /// <summary>
        ///     Write/put this <paramref name="input" /> data into the buffer.
        /// </summary>
        /// <param name="input">Array of <typeparamref name="T"/>s to write into the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="InvalidOperationException">Quantity of unallocated capacity (<see cref="Unallocated"/>) is insufficient for performing a write.</exception>
        void Write(T[] input);

        /// <summary>
        ///     Write/put this <paramref name="arraySegment" /> data into the buffer.
        /// </summary>
        /// <param name="arraySegment"><see cref="ArraySegment{T}"/> to write into the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Quantity of unallocated capacity (<see cref="Unallocated"/>) is insufficient for performing a write.</exception>
        void Write(ArraySegment<T> arraySegment);
    }
}
