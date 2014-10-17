#region License

// 	Copyright 2014-2014 Matthew Ducker
// 	
// 	Licensed under the Apache License, Version 2.0 (the "License");
// 	you may not use this file except in compliance with the License.
// 	
// 	You may obtain a copy of the License at
// 		
// 		http://www.apache.org/licenses/LICENSE-2.0
// 	
// 	Unless required by applicable law or agreed to in writing, software
// 	distributed under the License is distributed on an "AS IS" BASIS,
// 	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// 	See the License for the specific language governing permissions and 
// 	limitations under the License.

#endregion

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PerfCopy;

namespace RingByteBuffer
{
    /// <summary>
    ///     Simple ring/cyclic data buffer ("ringbuffer").
    /// </summary>
    /// <remarks>
    ///     Makes efficient use of memory.
    ///     Ensure initialised capacity can hold typical use case requirement with some overflow tolerance.
    /// </remarks>
    public abstract class RingBuffer
    {
        protected byte[] Buffer;
        protected readonly int Capacity;
        protected int BufferHeadOffset = 0, BufferTailOffset;
        protected int ContentLength;
        protected readonly bool CanOverwrite;

        /// <summary>
        ///     Maximum data possible to be stored.
        /// </summary>
        public int MaximumCapacity
        {
            get { return Capacity; }
        }

        /// <summary>
        ///     Length of data stored.
        /// </summary>
        public virtual int CurrentLength
        {
            get { return ContentLength; }
        }

        /// <summary>
        ///     Capacity not filled with data.
        /// </summary>
        public virtual int Spare
        {
            get { return Capacity - ContentLength; }
        }

        /// <summary>
        ///     Whether data is overwritable.
        /// </summary>
        /// <value><c>true</c> if overwritable; otherwise, <c>false</c>.</value>
        public bool Overwritable
        {
            get { return CanOverwrite; }
        }


        /// <summary>
        ///     Initialises a new instance of a <see cref="RingBuffer" />.
        /// </summary>
        /// <param name="maximumCapacity">Maximum required storage capability of the ringbuffer.</param>
        /// <param name="buffer">Data to initialise the ringbuffer with.</param>
        /// <param name="allowOverwrite">If set to <c>true</c> overwrite will be allowed, otherwise <c>false</c>.</param>
        /// <exception cref="ArgumentNullException">Supplied data array is null.</exception>
        /// <exception cref="ArgumentException">
        ///     Capacity is less than 2 bytes, or <paramref name="buffer"/> length exceeds <paramref name="maximumCapacity"/>.
        /// </exception>
        protected RingBuffer(int maximumCapacity, byte[] buffer = null, bool allowOverwrite = false)
        {
            if (maximumCapacity < 2) {
                throw new ArgumentException("Capacity must be at least 2 bytes.");
            }
            if (buffer != null && buffer.Length > maximumCapacity) {
                throw new ArgumentException("Initialisation data length exceeds allocated capacity.", "buffer");
            }

            Capacity = maximumCapacity;
            CanOverwrite = allowOverwrite;
            Buffer = new byte[CeilingNextPowerOfTwo(maximumCapacity)];

            if (buffer != null) {
                buffer.CopyBytes_NoChecks(0, Buffer, 0, buffer.Length);
                BufferTailOffset += buffer.Length;
            } else {
                BufferTailOffset = 0;
            }

            ContentLength = BufferTailOffset;
        }


        /// <summary>
        ///     Puts the single byte <paramref name="input"/> in the ringbuffer.
        /// </summary>
        /// <param name="input">Byte to write to the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public abstract void Put(byte input);

        /// <summary>
        ///     Put the data in <paramref name="buffer"/> in its entirety into the ringbuffer.
        /// </summary>
        /// <param name="buffer">Buffer to take input bytes from.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public void Put(byte[] buffer)
        {
            Put(buffer, 0, buffer.Length);
        }

        /// <summary>
        ///     Put <paramref name="count"/> bytes from <paramref name="buffer"/>, into the ringbuffer.
        /// </summary>
        /// <param name="buffer">Buffer to take input bytes from.</param>
        /// <param name="offset">Offset in <paramref name="buffer"/> to take bytes from.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public abstract void Put(byte[] buffer, int offset, int count);

        /// <summary>
        ///     Reads <paramref name="count"/> bytes from <paramref name="source"/>  
        ///     and puts them directly into the ringbuffer. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <returns>
        ///     Number of bytes actually put into the ringbuffer. 
        ///     If less than <paramref name="count"/>, end of <paramref name="source"/> was found.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public abstract int PutFrom(Stream source, int count);

        /// <summary>
        ///     Reads <paramref name="count"/> bytes from <paramref name="source"/>  
        ///     and puts them directly into the ringbuffer. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public abstract void PutExactlyFrom(Stream source, int count);

        /// <summary>
        ///     Reads up to <paramref name="count"/> bytes from <paramref name="source"/> asynchronously 
        ///     and puts them directly into the ringbuffer. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        ///     Number of bytes actually put into the ringbuffer. 
        ///     If less than <paramref name="count"/>, end of <paramref name="source"/> was found.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public abstract Task<int> PutFromAsync(Stream source, int count, CancellationToken cancellationToken);

        /// <summary>
        ///     Reads <paramref name="count"/> bytes from <paramref name="source"/> asynchronously 
        ///     and puts them directly into the ringbuffer. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it.</exception>
        public abstract Task PutExactlyFromAsync(Stream source, int count, CancellationToken cancellationToken);

        /// <summary>
        ///     Take a single byte from the ringbuffer.
        /// </summary>
        /// <exception cref="InvalidOperationException">Ringbuffer does not have enough in it.</exception>
        public abstract byte Take();

        /// <summary>
        ///     Takes <paramref name="count"/> bytes from the ringbuffer 
        ///     and returns a new buffer of them.
        /// </summary>
        /// <param name="count">Quantity of bytes to take.</param>
        /// <returns>Data from the ringbuffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        public byte[] Take(int count)
        {
            if (count < 0) 
                throw new ArgumentOutOfRangeException("count");
            if (count == 0) 
                return new byte[0]; 

            var output = new byte[count];
            Take(output, 0, count);
            return output;
        }

        /// <summary>
        ///     Takes bytes from the ringbuffer as necessary to fill <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">Buffer to fill with bytes taken from the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="ArgumentException">
        ///     Ringbuffer does not have enough in it, or the destination <paramref name="buffer"/> is too small.
        /// </exception>
        public void Take(byte[] buffer)
        {
            Take(buffer, 0, buffer.Length);
        }

        /// <summary>
        ///     Takes <paramref name="count"/> bytes from the ringbuffer and writes 
        ///     them to <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">Buffer to write bytes from the ringbuffer in.</param>
        /// <param name="offset">Offset in <paramref name="buffer"/> to write the bytes from the ringbuffer to.</param>
        /// <param name="count">Quantity of bytes to read into <paramref name="buffer"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="ArgumentException">
        ///     Ringbuffer does not have enough in it, or the destination <paramref name="buffer"/> is too small.
        /// </exception>
        public abstract void Take(byte[] buffer, int offset, int count);

        /// <summary>
        ///     Takes <paramref name="count"/> bytes directly from the 
        ///     ringbuffer and writes them into <paramref name="destination"/>. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="destination">Stream to write bytes to that are taken from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read into <paramref name="destination"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        public abstract void TakeTo(Stream destination, int count);

        /// <summary>
        ///     Takes <paramref name="count"/> bytes directly from the 
        ///     ringbuffer and writes them into <paramref name="destination"/> asynchronously. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="destination">Stream to write bytes to that are taken from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read into <paramref name="destination"/>.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        public abstract Task TakeToAsync(Stream destination, int count, CancellationToken cancellationToken);

        /// <summary>
        ///     Skips/discards <paramref name="count"/> number of bytes from the ringbuffer.
        /// </summary>
        /// <remarks>
        ///     Skipped bytes are not actually discarded until overwritten, 
        ///     so if security is needed, use <see cref="Reset"/>.
        /// </remarks>
        /// <param name="count">Number of bytes to skip ahead.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        public virtual void Skip(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (count > ContentLength) {
                throw new ArgumentException("Ringbuffer contents insufficient for operation.", "count");
            }

            // Modular division gives new offset position
            BufferHeadOffset = (BufferHeadOffset + count) % Capacity;
            ContentLength -= count;
        }

        /// <summary>
        ///     Resets the ringbuffer to an empty state, and sets every byte to zero.
        /// </summary>
        public virtual void Reset()
        {
            Array.Clear(Buffer, 0, Buffer.Length);
            BufferHeadOffset = 0;
            BufferTailOffset = 0;
            ContentLength = 0;
        }

        /// <summary>
        ///     Emits the entire length of the buffer in use. Ringbuffer will be empty after use.
        /// </summary>
        /// <returns>Ringbuffer data.</returns>
        public virtual byte[] ToArray()
        {
            return Take(ContentLength);
        }

        /// <summary>
        /// Calculate the next power of 2, greater than or equal to x.
        /// </summary>
        /// <param name="x">Value to round up</param>
        /// <returns>The next power of 2 from x inclusive</returns>
        public static int CeilingNextPowerOfTwo(int x)
        {
            var result = 2;
            while (result < x) {
                result <<= 1;
            }

            return result;
        }
    }
}
