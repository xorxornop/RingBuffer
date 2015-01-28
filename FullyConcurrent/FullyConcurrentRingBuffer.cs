#region License

//  	Copyright 2013-2014 Matthew Ducker
//  	
//  	Licensed under the Apache License, Version 2.0 (the "License");
//  	you may not use this file except in compliance with the License.
//  	
//  	You may obtain a copy of the License at
//  		
//  		http://www.apache.org/licenses/LICENSE-2.0
//  	
//  	Unless required by applicable law or agreed to in writing, software
//  	distributed under the License is distributed on an "AS IS" BASIS,
//  	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  	See the License for the specific language governing permissions and 
//  	limitations under the License.

#endregion

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PerfCopy;
using RingByteBuffer.FullyConcurrent.AsyncHelpers;

namespace RingByteBuffer.FullyConcurrent
{
    /// <summary>
    ///     Concurrent <see cref="IRingBuffer"/> implementation allowing for concurrent (parallel) I/O - 
    ///     operations can be concurrent (depending on pattern of use), and are optionally asynchronous. 
    ///     This implementation allows an arbitrary number of concurrent reads and writes (it is suggested 
    ///     to use the number of physical CPUs or less).
    /// </summary>
    public class FullyConcurrentRingBuffer : IRingBuffer
    {
        protected readonly int Capacity;
        protected byte[] Buffer;
        protected int BufferHeadOffsetDirty, BufferTailOffsetDirty;
        protected int ContentLength, ContentLengthDirty;

        protected SemaphoreSlim OperationBeginLock = new SemaphoreSlim(0, 1); // Put/take
        protected SemaphoreSlim OperationEndLock = new SemaphoreSlim(0, 1); // Put/take

        protected int PendingPutSequenceIdentity = 0, PendingTakeSequenceIdentity = 0;  
        protected AsyncManualResetEvent PutCompletedEvent = new AsyncManualResetEvent();
        protected AsyncManualResetEvent TakeCompletedEvent = new AsyncManualResetEvent();
        protected int LatestPutSequenceIdentity = 0, LatestTakeSequenceIdentity = 0;

        protected SemaphoreSlim StateController; // Controls # of concurrent puts/takes - set up in constructor
        protected SpinLock StateModificationLock = new SpinLock();


        /// <summary>
        ///     Initialises a new instance of a <see cref="RingBuffer" />.
        /// </summary>
        /// <param name="maximumCapacity">Maximum required storage capability of the ringbuffer.</param>
        /// <param name="buffer">Data to initialise the ringbuffer with.</param>
        /// <param name="maxOperations">
        ///     Maximum number of concurrent I/O operations (read, write). Should probably be based on
        ///     physical CPU count (equal or less).
        /// </param>
        /// <exception cref="ArgumentNullException">Supplied data array is null.</exception>
        /// <exception cref="ArgumentException">
        ///     Capacity is less than 2 bytes, or <paramref name="buffer" /> length exceeds <paramref name="maximumCapacity" />.
        /// </exception>
        public FullyConcurrentRingBuffer(int maximumCapacity, byte[] buffer = null, int? maxOperations = null)
        {
            Capacity = maximumCapacity;
            Buffer = new byte[RingBuffer.CeilingNextPowerOfTwo(maximumCapacity)];

            if (buffer != null) {
                buffer.CopyBytes_NoChecks(0, Buffer, 0, buffer.Length);
                BufferTailOffsetDirty = buffer.Length;
                ContentLength = BufferTailOffsetDirty;
                ContentLengthDirty = BufferTailOffsetDirty;
            }

            StateController = new SemaphoreSlim(0, maxOperations ?? Environment.ProcessorCount);
        }

        #region Properties

        /// <summary>
        ///     Length of data stored.
        /// </summary>
        public int CurrentLengthInFlight
        {
            get
            {
                int localValue;
                bool lockTaken = false;
                try {
                    StateModificationLock.Enter(ref lockTaken);
                    // Read shared state
                    localValue = ContentLengthDirty;
                }
                finally {
                    if (lockTaken) {
                        StateModificationLock.Exit(false);
                    }
                }
                return localValue;
            }
        }

        /// <summary>
        ///     Capacity not filled with data.
        /// </summary>
        public int SpareLengthInFlight
        {
            get
            {
                int localValue;
                bool lockTaken = false;
                try {
                    StateModificationLock.Enter(ref lockTaken);
                    // Read shared state
                    localValue = Capacity - ContentLengthDirty;
                }
                finally {
                    if (lockTaken) {
                        StateModificationLock.Exit(false);
                    }
                }
                return localValue;
            }
        }

        /// <summary>
        ///     Length of data stored.
        /// </summary>
        public int CurrentLength
        {
            get
            {
                int localValue;
                bool lockTaken = false;
                try {
                    StateModificationLock.Enter(ref lockTaken);
                    // Read shared state
                    localValue = ContentLength;
                }
                finally {
                    if (lockTaken) {
                        StateModificationLock.Exit(false);
                    }
                }
                return localValue;
            }
        }

        /// <summary>
        ///     Capacity not filled with data.
        /// </summary>
        public int SpareLength
        {
            get
            {
                int localValue;
                bool lockTaken = false;
                try {
                    StateModificationLock.Enter(ref lockTaken);
                    // Read shared state
                    localValue = Capacity - ContentLength;
                }
                finally {
                    if (lockTaken) {
                        StateModificationLock.Exit(false);
                    }
                }
                return localValue;
            }
        }

        public int MaximumCapacity
        {
            get { return Capacity; }
        }

        public bool Overwritable
        {
            get { return false; }
        }

        #endregion

        #region IRingBuffer Members

        /// <inheritdoc />
        public void Put(byte input)
        {
            StateController.Wait();
            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                // Read and update shared state
                if (ContentLengthDirty + 1 > Capacity) {
                    throw new InvalidOperationException("Buffer capacity insufficient for write operation.");
                }
                // Write shared state
                Buffer[BufferTailOffsetDirty] = input;
                Interlocked.Increment(ref BufferTailOffsetDirty);
                if (BufferTailOffsetDirty == Capacity) {
                    Interlocked.Exchange(ref BufferTailOffsetDirty, 0);
                }
                Interlocked.Increment(ref ContentLength);
                Interlocked.Increment(ref ContentLengthDirty);
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                StateController.Release();
            }
        }

        public void Put(byte[] buffer)
        {
            Put(buffer, 0, buffer.Length);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is negative.</exception>
        public void Put(byte[] buffer, int offset, int count)
        {
            Put(buffer, offset, count, CancellationToken.None).Wait();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is negative.</exception>
        public void PutExactlyFrom(Stream source, int count)
        {
            PutExactlyFromAsync(source, count, CancellationToken.None).Wait();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is negative.</exception>
        public async Task PutExactlyFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }

            var putFunc = new Func<int, int, Task>(async (tailOffset, length) => {
                while (length > 0) {
                    int chunk = Math.Min(Capacity - tailOffset, length);
                    int chunkIn = 0;
                    while (chunkIn < chunk) {
                        int iterIn =
                            await source.ReadAsync(Buffer, tailOffset, chunk - chunkIn);
                        if (iterIn < 1) {
                            throw new EndOfStreamException();
                        }
                        chunkIn += iterIn;
                    }
                    tailOffset = (tailOffset + chunk == Capacity) ? 0 : tailOffset + chunk;
                    length -= chunk;
                }
                PutPublish(tailOffset, count);
            });

            await Put(count, putFunc, cancellationToken);
        }

        /// <inheritdoc />
        public int PutFrom(Stream source, int count)
        {
            throw new InvalidOperationException("Indeterminate length operations not supported.");
        }

        /// <inheritdoc />
        public Task<int> PutFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Indeterminate length operations not supported.");
        }

        /// <inheritdoc />
        public byte Take()
        {
            StateController.Wait();
            byte output;
            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                if (ContentLength < 1) {
                    throw new InvalidOperationException("Ringbuffer contents insufficient for take/read operation.");
                }
                // Read and update shared state
                output = Buffer[BufferHeadOffsetDirty];
                Interlocked.Increment(ref BufferHeadOffsetDirty);
                if (BufferHeadOffsetDirty == Capacity) {
                    BufferHeadOffsetDirty = 0;
                    Interlocked.Exchange(ref BufferHeadOffsetDirty, 0);
                }
                Interlocked.Decrement(ref ContentLength);
                Interlocked.Decrement(ref ContentLengthDirty);
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                StateController.Release();
            }

            return output;
        }

        public byte[] Take(int count)
        {
            var buf = new byte[count];
            Take(buf, 0, count);
            return buf;
        }

        public void Take(byte[] buffer)
        {
            Take(buffer, 0, buffer.Length);
        }

        /// <inheritdoc />
        public void Take(byte[] buffer, int offset, int count)
        {
            Take(buffer, offset, count, CancellationToken.None).Wait();
        }

        /// <inheritdoc />
        public void TakeTo(Stream destination, int count)
        {
            TakeToAsync(destination, count, CancellationToken.None).Wait();
        }

        /// <inheritdoc />
        public async Task TakeToAsync(Stream destination, int count, CancellationToken cancellationToken)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }

            var takeFunc = new Func<int, int, Task>(async (headOffset, length) => {
                while (length > 0) {
                    int chunk = Math.Min(Capacity - headOffset, length);
                    await destination.WriteAsync(Buffer, headOffset, chunk, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) {
                        return;
                    }
                    headOffset = (headOffset + chunk == Capacity) ? 0 : headOffset + chunk;
                    length -= chunk;
                }
            });

            await Take(count, takeFunc, cancellationToken);
        }

        /// <inheritdoc />
        public void Skip(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }

            try {
                OperationBeginLock.Wait();
                OperationEndLock.Wait();

                if (count > ContentLength) {
                    throw new ArgumentException("Ringbuffer contents insufficient for operation.", "count");
                }

                bool lockTaken = false;
                try {
                    StateModificationLock.Enter(ref lockTaken);
                    // Update shared state
                    Interlocked.Exchange(ref BufferHeadOffsetDirty, (BufferHeadOffsetDirty + count) % Capacity);
                    Interlocked.Add(ref ContentLength, -count);
                }
                finally {
                    if (lockTaken) {
                        StateModificationLock.Exit(false);
                    }
                }
            }
            finally {
                OperationBeginLock.Release();
                OperationEndLock.Release();
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            OperationBeginLock.Wait();
            OperationEndLock.Wait();
            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                // Write shared state
                Array.Clear(Buffer, 0, Buffer.Length);
                BufferHeadOffsetDirty = 0;
                BufferTailOffsetDirty = 0;
                ContentLength = 0;
                ContentLengthDirty = 0;
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                OperationBeginLock.Release();
                OperationEndLock.Release();
            }
        }


        public byte[] ToArray()
        {
            throw new NotImplementedException();
        }

        #endregion

        public async Task Put(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offset must be positive.");
            }
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (buffer.Length < offset + count) {
                throw new ArgumentException("Source array too small for requested input.");
            }

            var putFunc = new Func<int, int, Task>((tailOffset, length) => {
                while (length > 0) {
                    int chunk = Math.Min(Capacity - tailOffset, length);
                    buffer.CopyBytes_NoChecks(offset, Buffer, tailOffset, chunk);
                    tailOffset = (tailOffset + chunk == Capacity) ? 0 : tailOffset + chunk;
                    offset += chunk;
                    length -= chunk;
                }
                return null;
            });

            await Put(count, putFunc, cancellationToken);
        }

        protected async Task Put(int length, Func<int, int, Task> putFunc, CancellationToken cancellationToken)
        {
            // Wait for concurrent operations to drop low enough to proceed, where necessary (controlled by semaphore)
            await StateController.WaitAsync(cancellationToken);
            try {
                cancellationToken.ThrowIfCancellationRequested();

                int sequenceId;
                // Local state (shadow of shared) for operation
                int startBufferTailOffset, endBufferTailOffset;
                // Read current shared state into locals, determine post-op state, and update shared state to it         
                PutAllocate(length, out startBufferTailOffset, out endBufferTailOffset, out sequenceId);

                // Run and wait for operation to complete
                await putFunc(startBufferTailOffset, length);
                PutCompletedEvent.Set();

                // Loop and wait for this operation to be the pending operation sequence (may be immediate)               
                while (PendingPutSequenceIdentity > sequenceId) {
                    // Wait for a put to complete (it might be this one)
                    await PutCompletedEvent.WaitAsync();
                }
                OperationEndLock.Wait();
                Interlocked.Increment(ref PendingPutSequenceIdentity);
                    // successor operations should wait on semaphore rather than loop on event async wait
                PutPublish(endBufferTailOffset, length);
                PutCompletedEvent.Reset();
            }
            finally {
                StateController.Release();
            }
        }

        /// <summary>
        ///     Allocates space in ringbuffer for a put operation.
        /// </summary>
        /// <param name="count">Number of bytes to take/read.</param>
        /// <param name="startTailOffset">Ringbuffer tail offset at start of put operation.</param>
        /// <param name="endTailOffset">Ringbuffer tail offset at end of put operation.</param>
        /// <param name="sequenceId">Operation sequence identity.</param>
        protected void PutAllocate(int count, out int startTailOffset, out int endTailOffset, out int sequenceId)
        {
            bool resourceConstrained = false;
            // Check operation viability
            if (ContentLengthDirty + count > Capacity) {
                resourceConstrained = true;
                OperationBeginLock.Wait();
                while (LatestTakeSequenceIdentity >= PendingTakeSequenceIdentity) {
                    TakeCompletedEvent.Wait();
                    OperationEndLock.Wait();
                }
                if (ContentLength + count > Capacity) {
                    throw new ArgumentException("Ringbuffer capacity insufficient for put/write operation.", "count");
                }
            }

            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                // Read shared state
                startTailOffset = BufferTailOffsetDirty;
                // Determine and write shared state
                endTailOffset = (startTailOffset + count) % Capacity;
                Interlocked.Exchange(ref BufferTailOffsetDirty, endTailOffset);
                Interlocked.Add(ref ContentLengthDirty, count);
                sequenceId = Interlocked.Increment(ref LatestPutSequenceIdentity);
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                if (resourceConstrained) {
                    OperationBeginLock.Release();
                    OperationEndLock.Release();
                }
            }
        }

        /// <summary>
        ///     Indicates that the buffer alteration made as part of a put operation has finished,
        ///     so the content should be made available.
        /// </summary>
        /// <param name="endTailOffset"></param>
        /// <param name="count"></param>
        protected void PutPublish(int endTailOffset, int count)
        {
            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                Interlocked.Exchange(ref BufferTailOffsetDirty, endTailOffset);
                Interlocked.Add(ref ContentLength, count);
                //Interlocked.Increment(ref PendingPutSequenceIdentity);
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                OperationEndLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task Take(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offsets must be positive.");
            }

            if (buffer.Length < offset + count) {
                throw new ArgumentException("Destination array too small for requested output.");
            }

            var takeFunc = new Func<int, int, Task>((headOffset, length) => {
                while (length > 0) {
                    int chunk = Math.Min(Capacity - headOffset, length);
                    Buffer.CopyBytes_NoChecks(headOffset, buffer, offset, chunk);
                    headOffset = (headOffset + chunk == Capacity) ? 0 : headOffset + chunk;
                    offset += chunk;
                    length -= chunk;
                }
                return null;
            });

            await Take(count, takeFunc, cancellationToken);
        }

        protected async Task Take(int length, Func<int, int, Task> takeFunc, CancellationToken cancellationToken)
        {
            // Wait for concurrent operations to drop low enough to proceed, where necessary (controlled by semaphore)
            await StateController.WaitAsync(cancellationToken);
            try {
                cancellationToken.ThrowIfCancellationRequested();

                int sequenceNumber;
                // Local state (shadow of shared) for operation
                int startBufferHeadOffset, endBufferHeadOffset;
                // Read current shared state into locals, determine post-op state, and update shared state to it
                TakeDeallocate(length, out startBufferHeadOffset, out endBufferHeadOffset, out sequenceNumber);

                // Run and wait for operation to complete
                await takeFunc(startBufferHeadOffset, length);
                TakeCompletedEvent.Set();

                // Loop and wait for this operation to be the pending operation sequence (may be immediate)
                while (PendingTakeSequenceIdentity > sequenceNumber) {
                    // Wait for a put to complete (it might be this one)
                    await TakeCompletedEvent.WaitAsync();
                }
                OperationEndLock.Wait();
                Interlocked.Increment(ref PendingTakeSequenceIdentity);
                    // successor operations should wait on semaphore rather than loop on event async wait
                TakePublish(endBufferHeadOffset, length);
                TakeCompletedEvent.Reset();
            }
            finally {
                StateController.Release();
            }
        }

        /// <summary>
        ///     Reads and updates shared buffer state for a take operation,
        ///     and verifies validity of <paramref name="count" /> parameter value.
        /// </summary>
        /// <param name="count">Number of bytes to take/read.</param>
        /// <param name="startHeadOffset">Reference to ringbuffer head offset (start of live content).</param>
        /// <param name="endHeadOffset"></param>
        /// <param name="sequenceNumber"></param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        protected void TakeDeallocate(int count, out int startHeadOffset, out int endHeadOffset, out int sequenceNumber)
        {
            bool resourceConstrained = false;
            // Check operation viability
            if (count > ContentLength) {
                resourceConstrained = true;
                OperationBeginLock.Wait();
                while (LatestPutSequenceIdentity >= PendingPutSequenceIdentity) {
                    // Exhaust all pending operations
                    PutCompletedEvent.Wait();
                    OperationEndLock.Wait();
                }
                if (count > ContentLength) {
                    throw new ArgumentException("Ringbuffer contents insufficient for take/read operation.", "count");
                }
            }

            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                // Read shared state
                startHeadOffset = BufferHeadOffsetDirty;
                // Determine and write shared state
                endHeadOffset = (startHeadOffset + count) % Capacity;
                Interlocked.Exchange(ref BufferHeadOffsetDirty, endHeadOffset);
                Interlocked.Add(ref ContentLengthDirty, -count);
                sequenceNumber = Interlocked.Increment(ref LatestTakeSequenceIdentity);
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                if (resourceConstrained) {
                    OperationBeginLock.Release();
                    OperationEndLock.Release();
                }
            }
        }

        /// <summary>
        ///     Indicates that the buffer alteration(s) made as part of a take operation have finished,
        ///     so the changes should be made available.
        /// </summary>
        /// <param name="headOffset">Ending offset for the take operation.</param>
        /// <param name="count">Number of bytes taken.</param>
        protected void TakePublish(int headOffset, int count)
        {
            bool lockTaken = false;
            try {
                StateModificationLock.Enter(ref lockTaken);
                Interlocked.Add(ref ContentLength, -count);
            }
            finally {
                if (lockTaken) {
                    StateModificationLock.Exit(false);
                }
                OperationEndLock.Release();
            }
        }
    }
}
