using System;

namespace RingBuffer
{
    partial interface IRingBuffer<T>
    {
        /// <summary>
        ///     Read/take a single <typeparamref name="T"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="Allocated"/>) is insufficient for performing a read.</exception>
        T Read();

        /// <summary>
        ///     Read/take <paramref name="quantity" /> <typeparamref name="T"/>s from the buffer into an array.
        /// </summary>
        /// <param name="quantity">Quantity of <typeparamref name="T"/>s to read/take from the buffer.</param>
        /// <returns>Array containing the data that was read/taken from the buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is negative, or greater than <see cref="Capacity"/>.</exception>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="Allocated"/>) is insufficient for performing a read.</exception>
        T[] Read(int quantity);

        void ReadInto(T[] output);

        /// <summary>
        ///     Read/take <see cref="ArraySegment{T}.Count"/> [of the <paramref name="arraySegment" />] <typeparamref name="T"/>s from the buffer into an array.
        /// </summary>
        /// <param name="arraySegment">
        /// Array segment that <c>N</c> <typeparamref name="T"/>s are read/taken into from the buffer. 
        /// N* : quantity to match length of the array segment (<see cref="ArraySegment{T}.Count"/>).
        /// </param>
        /// <exception cref="ArgumentException">Quantity of allocated data (<see cref="Allocated"/>) is insufficient for performing a read.</exception>
        void ReadInto(ArraySegment<T> arraySegment);
    }
}
