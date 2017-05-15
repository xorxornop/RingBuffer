using System;

namespace RingBuffer
{
    /// <summary>
    ///     Interface for a simple ring/cyclic-byte-data buffer ("ringbuffer")
    ///     with asynchronous I/O capability.
    /// </summary>
    public interface IRingBuffer<T>
        where T : struct
    {
        /// <summary>
        ///     Maximum/total quantity of data that can be allocated/stored.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        ///     Quantity of data that is currently allocated/stored.
        /// </summary>
        int Allocated { get; }

        /// <summary>
        ///     Quantity of data from the <see cref="Capacity"/> that is reserved 
        ///     for future allocation, and currently unallocated/empty.
        /// </summary>
        /// <remarks>
        /// 
        /// <para>
        ///     Value will and must at all times must be [at most] equal to, 
        ///     but likely lesser than, that of <see cref="Capacity"/>, 
        ///     irrespective of the internal state of the buffer and its representations.
        /// </para>
        /// <code><see cref="Unallocated"/> = <see cref="Capacity"/> - <see cref="Allocated"/></code>
        /// </remarks>
        int Unallocated { get; }

        /// <summary>
        ///     Whether currently allocated data within the buffer can/may be overwritten.
        /// </summary>
        /// <value><c>true</c> if overwritable; otherwise, <c>false</c>.</value>
        bool CanOverwrite { get; }



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

        void ReadInto(T[] array, int offset, int count);

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

        void WriteFrom(T[] array, int offset, int count);

        /// <summary>
        ///     Skips/discards <paramref name="quantity" /> <typeparamref name="T"/>s number of bytes from the ringbuffer.
        /// </summary>
        /// <remarks>
        ///     Skipped bytes are not actually discarded until overwritten; 
        ///     skipped data is are represented identically to data that has been read, 
        ///     the only difference being in that skipping data really just skips the associated read operation; 
        ///     all other logic involved is identical. 
        ///     Note: having skipped data remain unmodified in the backing buffer may be unacceptable 
        ///     in certain use-cases (such as security applications); 
        ///     <see cref="Reset" /> may be useful to ensure the erasure of any and all data possibly retained in backing buffer.
        /// </remarks>
        /// <param name="quantity">Quantity of <typeparamref name="T"/>s to skip/discard.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        void Skip(int quantity);

        /// <summary>
        ///     Resets the ringbuffer to an empty state, and sets every byte to zero.
        /// </summary>
        void Reset();

        /// <summary>
        ///     Emits the entire length of the buffer in use. Ringbuffer will be empty after use.
        /// </summary>
        /// <returns>Ringbuffer data.</returns>
        T[] ToArray();
    }
}
