using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PerfCopy;

namespace RingByteBuffer
{
    /// <summary>
    ///     <see cref="RingBuffer"/> implementation allowing for concurrent (parallel) I/O.
    /// </summary>
    /// <remarks>
    ///     Concurrent I/O is handled by reading shared state, determining what shared state 
    ///     <emphasis>would be</emphasis> after the operation had terminated (if it were updating 
    ///     state as it proceeded, as is traditional), and then in a concurrency-safe manner, 
    ///     updating the state to this before actually operating on the ringbuffer contents.
    /// 
    ///     All further actions within the operation use only this derived local state. 
    ///     Although the buffer is shared, operations do not overlap because of this 
    ///     preemptive state synchronisation. Any operation called afterward observes the same state 
    ///     that would be observed as if the first operation had terminated, 
    ///     although in actuality it may not have yet.
    /// </remarks>
    public class ConcurrentRingBuffer : RingBuffer
    {
        protected SpinLock Lock = new SpinLock();

        protected bool PendingPut = false, PendingTake = false;

        protected int ContentLengthDirty, BufferHeadOffsetDirty = 0, BufferTailOffsetDirty;

        public ConcurrentRingBuffer(int maximumCapacity, byte[] buffer = null, bool allowOverwrite = false)
            : base(maximumCapacity, buffer, allowOverwrite)
        {
            // Init may have loaded buffer (ctor parameter) contents into ringbuffer, need to sync tail
            BufferTailOffsetDirty = BufferTailOffset;
            ContentLengthDirty = ContentLength;
            
        }

        /// <summary>
        ///     Length of data stored.
        /// </summary>
        public override int CurrentLength
        {
            get
            {
                int localValue;
                bool lockTaken = false;
                try {
                    Lock.Enter(ref lockTaken);
                    // Read shared state
                    localValue = ContentLength;
                }
                finally {
                    if (lockTaken) 
                        Lock.Exit(false);
                }
                return localValue;
            }
        }

        /// <summary>
        ///     Capacity not filled with data.
        /// </summary>
        public override int Spare
        {
            get
            {
                int localValue;
                bool lockTaken = false;
                try {
                    Lock.Enter(ref lockTaken);
                    // Read shared state
                    localValue = Capacity - ContentLength;
                }
                finally {
                    if (lockTaken) 
                        Lock.Exit(false);
                }
                return localValue;
            }
        }

        /// <inheritdoc />
        public override void Put(byte input)
        {
            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                // Read and update shared state
                if (ContentLength + 1 > Capacity) {
                    if (CanOverwrite) {
                        if (BufferHeadOffset + 1 == Capacity) {
                            BufferHeadOffset = 0;
                            ContentLength--;
                        }
                    } else {
                        throw new InvalidOperationException("Buffer capacity insufficient for write operation.");
                    }
                }
                // Write shared state
                Buffer[BufferTailOffset++] = input;
                if (BufferTailOffset == Capacity) {
                    BufferTailOffset = 0;
                }
                ContentLength++;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }
        }

        /// <inheritdoc />
        public override void Put(byte[] buffer, int offset, int count)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offset must be positive.");
            }
            // Local state (shadow of shared) for operation
            int localBufferTailOffset;
            // Read current shared state into locals, determine post-op state, and update shared state to it
            PutAllocate(out localBufferTailOffset, count);
            if (buffer.Length < offset + count) {
                throw new ArgumentException("Source array too small for requested input.");
            }
            
            int length = count;
            while (length > 0) {
                int chunk = Math.Min(Capacity - localBufferTailOffset, length);
                buffer.CopyBytes_NoChecks(offset, Buffer, localBufferTailOffset, chunk);
                localBufferTailOffset = (localBufferTailOffset + chunk == Capacity) ? 0 : localBufferTailOffset + chunk;
                offset += chunk;
                length -= chunk;
            }
            PutPublish(localBufferTailOffset, count);
        }

        /// <inheritdoc />
        public override int PutFrom(Stream source, int count)
        {
            // Local state (shadow of shared) for operation
            int localBufferTailOffset;
            // Read current shared state into locals, determine post-op state, and update shared state to it
            PutAllocate(out localBufferTailOffset, count);
            int remaining = count;
            bool earlyFinish = false;
            while (remaining > 0 || !earlyFinish) {
                int chunk = Math.Min(Capacity - localBufferTailOffset, remaining);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    int iterIn = source.Read(Buffer, localBufferTailOffset, chunk - chunkIn);
                    if (iterIn < 1) {
                        earlyFinish = true;
                    }
                    chunkIn += iterIn;
                }
                if (earlyFinish) 
                    continue;
                localBufferTailOffset = (localBufferTailOffset + chunk == Capacity) ? 0 : localBufferTailOffset + chunk;
                remaining -= chunk;
            }
            PutPublish(localBufferTailOffset, count - remaining);

            return count - remaining;
        }

        /// <inheritdoc />
        public override void PutExactlyFrom(Stream source, int count)
        {
            // Local state (shadow of shared) for operation
            int localBufferTailOffset;
            // Read current shared state into locals, determine post-op state, and update shared state to it
            PutAllocate(out localBufferTailOffset, count);
            int length = count;
            while (length > 0) {
                int chunk = Math.Min(Capacity - localBufferTailOffset, length);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    var iterIn = source.Read(Buffer, localBufferTailOffset, chunk - chunkIn);
                    if (iterIn < 1) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                localBufferTailOffset = (localBufferTailOffset + chunk == Capacity) ? 0 : localBufferTailOffset + chunk;
                length -= chunk;
            }
            PutPublish(localBufferTailOffset, count);
        }

        /// <inheritdoc />
        public override async Task<int> PutFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            // Local state (shadow of shared) for operation
            int localBufferTailOffset;
            // Read current shared state into locals, determine post-op state, and update shared state to it
            PutAllocate(out localBufferTailOffset, count);
            int remaining = count;
            bool earlyFinish = false;
            while (remaining > 0 || !earlyFinish) {
                int chunk = Math.Min(Capacity - localBufferTailOffset, remaining);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    int iterIn = await source.ReadAsync(Buffer, localBufferTailOffset, chunk - chunkIn, cancellationToken);
                    if (iterIn < 1 || cancellationToken.IsCancellationRequested) {
                        earlyFinish = true;
                    }
                    chunkIn += iterIn;
                }
                if (earlyFinish) 
                    continue;
                localBufferTailOffset = (localBufferTailOffset + chunk == Capacity) ? 0 : localBufferTailOffset + chunk;
                remaining -= chunk;
            }
            PutPublish(localBufferTailOffset, count - remaining);

            return count - remaining;
        }

        /// <inheritdoc />
        public override async Task PutExactlyFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            // Local state (shadow of shared) for operation
            int localBufferTailOffset;
            // Read current shared state into locals, determine post-op state, and update shared state to it
            PutAllocate(out localBufferTailOffset, count);
            int length = count;
            while (length > 0) {
                int chunk = Math.Min(Capacity - localBufferTailOffset, length);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    int iterIn = await source.ReadAsync(Buffer, localBufferTailOffset, chunk - chunkIn, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) {
                        return;
                    }
                    if (iterIn < 1) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                localBufferTailOffset = (localBufferTailOffset + chunk == Capacity) ? 0 : localBufferTailOffset + chunk;
                length -= chunk;
            }
            PutPublish(localBufferTailOffset, count);
        }

        /// <summary>
        ///     Allocates space in ringbuffer for a put operation.
        /// </summary>
        /// <param name="tailOffset">Reference to ringbuffer tail offset (end of live content).</param>
        /// <param name="count">Number of bytes to take/read.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer has too much in it.</exception>
        protected void PutAllocate(out int tailOffset, int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }

            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                // Read shared state
                if (PendingPut) {
                    throw new InvalidOperationException();
                }
                tailOffset = BufferTailOffset;
                // Check operation viability
                if (ContentLength + count > Capacity) {
                    if (CanOverwrite && !PendingTake) {
                        int skip = Capacity - (ContentLength + count);
                        // Update shared state
                        SkipLocal(ref BufferHeadOffsetDirty, skip);
                    } else {
                        throw new ArgumentException("Ringbuffer capacity insufficient for put/write operation.", "count");
                    }
                }
                // Determine and write shared state
                BufferTailOffsetDirty = (tailOffset + count) % Capacity;
                ContentLengthDirty = ContentLength + count;
                PendingPut = true;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }
        }

        /// <summary>
        ///     Indicates that the buffer alteration made as part of a put operation has finished, 
        ///     so the content should be made available.
        /// </summary>
        /// <param name="tailOffset"></param>
        /// <param name="count"></param>
        protected void PutPublish(int tailOffset, int count)
        {
            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                BufferTailOffset = tailOffset;
                ContentLength += count;
                PendingPut = false;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }
        }

        /// <inheritdoc />
        public override byte Take()
        {
            byte output;
            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                if (ContentLength == 0) {
                    throw new InvalidOperationException("Ringbuffer contents insufficient for take/read operation.");
                }
                // Read and update shared state
                output = Buffer[BufferHeadOffset++];
                if (BufferHeadOffset == Capacity) {
                    BufferHeadOffset = 0;
                }
                ContentLength--;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }

            return output;
        }

        /// <inheritdoc />
        public override void Take(byte[] buffer, int offset, int count)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offsets must be positive.");
            }
            int localBufferHeadOffset;
            TakeInitial(out localBufferHeadOffset, count);
            if (buffer.Length < offset + count) {
                throw new ArgumentException("Destination array too small for requested output.");
            }

            int length = count;
            while (length > 0) {
                int chunk = Math.Min(Capacity - localBufferHeadOffset, length);
                Buffer.CopyBytes_NoChecks(localBufferHeadOffset, buffer, offset, chunk);
                localBufferHeadOffset = (localBufferHeadOffset + chunk == Capacity) ? 0 : localBufferHeadOffset + chunk;
                offset += chunk;
                length -= chunk;
            }
            TakePublish(localBufferHeadOffset, count);
        }

        /// <inheritdoc />
        public override void TakeTo(Stream destination, int count)
        {
            int localBufferHeadOffset;
            TakeInitial(out localBufferHeadOffset, count);
            int length = count;
            while (length > 0) {
                int chunk = Math.Min(Capacity - localBufferHeadOffset, length);
                destination.Write(Buffer, localBufferHeadOffset, chunk);
                localBufferHeadOffset = (localBufferHeadOffset + chunk == Capacity) ? 0 : localBufferHeadOffset + chunk;
                length -= chunk;
            }
            TakePublish(localBufferHeadOffset, count);
        }

        /// <inheritdoc />
        public override async Task TakeToAsync(Stream destination, int count, CancellationToken cancellationToken)
        {
            int localBufferHeadOffset;
            TakeInitial(out localBufferHeadOffset, count);
            int length = count;
            while (length > 0) {
                int chunk = Math.Min(Capacity - localBufferHeadOffset, length);
                await destination.WriteAsync(Buffer, localBufferHeadOffset, chunk, cancellationToken);
                if (cancellationToken.IsCancellationRequested) {
                    return;
                }
                localBufferHeadOffset = (localBufferHeadOffset + chunk == Capacity) ? 0 : localBufferHeadOffset + chunk;
                length -= chunk;
            }
            TakePublish(localBufferHeadOffset, count);
        }

        /// <summary>
        ///     Reads and updates shared buffer state for a take operation, 
        ///     and verifies validity of <paramref name="count"/> parameter value.
        /// </summary>
        /// <param name="headOffset">Reference to ringbuffer head offset (start of live content).</param>
        /// <param name="count">Number of bytes to take/read.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        protected void TakeInitial(out int headOffset, int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }

            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                // Read shared state
                if (PendingTake) {
                    throw new InvalidOperationException();
                }
                headOffset = BufferHeadOffset;
                // Check operation viability
                if (count > ContentLength) {
                    throw new ArgumentException("Ringbuffer contents insufficient for take/read operation.", "count");
                }
                // Determine and write shared state
                BufferHeadOffsetDirty = (headOffset + count) % Capacity;
                ContentLengthDirty = ContentLength - count;
                PendingTake = true;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }
        }

        /// <summary>
        ///     Indicates that the buffer alteration(s) made as part of a take operation have finished, 
        ///     so the content should be made available.
        /// </summary>
        /// <param name="headOffset">Ending offset for the take operation.</param>
        /// <param name="count">Number of bytes taken.</param>
        protected void TakePublish(int headOffset, int count)
        {
            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                BufferHeadOffset = headOffset;
                ContentLength -= count;
                PendingTake = false;
            }
            finally {
                if (lockTaken) Lock.Exit(false);
            }
        }

        /// <inheritdoc />
        public override void Skip(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }

            bool lockTaken = false;
            try {
                if (PendingTake) {
                    throw new InvalidOperationException("Ringbuffer is already executing a take operation - cannot do skip concurrently.");
                }
                if (count > ContentLength) {
                    throw new ArgumentException("Ringbuffer contents insufficient for operation.", "count");
                }
                Lock.Enter(ref lockTaken);
                // Update shared state
                SkipLocal(ref BufferHeadOffset, count);
                ContentLength -= count;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }
        }

        /// <summary>
        ///     Skips ringbuffer content by moving offset and substracting stored length. 
        ///     Must be called from within memory-safe context! 
        /// </summary>
        /// <remarks>
        ///     Does not perform error-checking on arguments - callee should do this.
        /// </remarks>
        /// <param name="headOffset">Reference to ringbuffer head offset (start of live content).</param>
        /// <param name="count">Number of bytes to skip.</param>
        protected void SkipLocal(ref int headOffset, int count)
        {
            // Modular division gives new offset position
            headOffset = (headOffset + count) % Capacity;
        }

        /// <inheritdoc />
        public override void Reset()
        {
            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                // Write shared state
                Array.Clear(Buffer, 0, Buffer.Length);
                BufferHeadOffset = 0;
                BufferTailOffset = 0;
                ContentLength = 0;
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }
        }

        /// <summary>
        ///     Emits the entire length of the buffer in use. Ringbuffer will be empty after use.
        /// </summary>
        /// <returns>Ringbuffer data.</returns>
        public override byte[] ToArray()
        {
            byte[] buffer;
            bool lockTaken = false;
            try {
                Lock.Enter(ref lockTaken);
                buffer = Take(ContentLength);
                Reset();
            }
            finally {
                if (lockTaken) 
                    Lock.Exit(false);
            }

            return buffer;
        }
    }
}
