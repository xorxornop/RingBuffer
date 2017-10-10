namespace RingBuffer
{
    /// <summary>
    ///     Interface for a simple ring/wrapping data buffer ("ringbuffer")
    ///     with asynchronous I/O capability.
    /// </summary>
    public partial interface IRingBuffer<T>
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
        /// <remarks>
        ///     Overwriting inherently causes data loss. Whether this is acceptable will vary by use-case.
        /// </remarks>
        /// <value><c>true</c> if overwritable; otherwise, <c>false</c>.</value>
        bool CanOverwrite { get; }

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
