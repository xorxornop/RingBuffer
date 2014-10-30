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

namespace RingByteBuffer
{
    /// <summary>
    ///     Interface for a simple ring/cyclic-byte-data buffer ("ringbuffer")
    ///     with asynchronous I/O capability.
    /// </summary>
    public interface IRingBuffer
    {
        /// <summary>
        ///     Maximum data possible to be stored.
        /// </summary>
        int MaximumCapacity { get; }

        /// <summary>
        ///     Length of data stored.
        /// </summary>
        int CurrentLength { get; }

        /// <summary>
        ///     Capacity not filled with data.
        /// </summary>
        int SpareLength { get; }

        /// <summary>
        ///     Whether data is overwritable.
        /// </summary>
        /// <value><c>true</c> if overwritable; otherwise, <c>false</c>.</value>
        bool Overwritable { get; }

        /// <summary>
        ///     Puts the single byte <paramref name="input" /> in the ringbuffer.
        /// </summary>
        /// <param name="input">Byte to write to the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        void Put(byte input);

        /// <summary>
        ///     Put the data in <paramref name="buffer" /> in its entirety into the ringbuffer.
        /// </summary>
        /// <param name="buffer">Buffer to take input bytes from.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        void Put(byte[] buffer);

        /// <summary>
        ///     Put <paramref name="count" /> bytes from <paramref name="buffer" />, into the ringbuffer.
        /// </summary>
        /// <param name="buffer">Buffer to take input bytes from.</param>
        /// <param name="offset">Offset in <paramref name="buffer" /> to take bytes from.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        void Put(byte[] buffer, int offset, int count);

        /// <summary>
        ///     Reads <paramref name="count" /> bytes from <paramref name="source" />
        ///     and puts them directly into the ringbuffer.
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <returns>
        ///     Number of bytes actually put into the ringbuffer.
        ///     If less than <paramref name="count" />, end of <paramref name="source" /> was found.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        int PutFrom(Stream source, int count);

        /// <summary>
        ///     Reads <paramref name="count" /> bytes from <paramref name="source" />
        ///     and puts them directly into the ringbuffer.
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        /// <exception cref="EndOfStreamException">The <paramref name="source" /> does not have enough in it (insufficient length).</exception>
        void PutExactlyFrom(Stream source, int count);

        /// <summary>
        ///     Reads up to <paramref name="count" /> bytes from <paramref name="source" /> asynchronously
        ///     and puts them directly into the ringbuffer.
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        ///     Number of bytes actually put into the ringbuffer.
        ///     If less than <paramref name="count" />, end of <paramref name="source" /> was found.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        Task<int> PutFromAsync(Stream source, int count, CancellationToken cancellationToken);

        /// <summary>
        ///     Reads <paramref name="count" /> bytes from <paramref name="source" /> asynchronously
        ///     and puts them directly into the ringbuffer.
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much in it (insufficient spare length).</exception>
        /// <exception cref="EndOfStreamException">The <paramref name="source" /> does not have enough in it (insufficient data).</exception>
        Task PutExactlyFromAsync(Stream source, int count, CancellationToken cancellationToken);

        /// <summary>
        ///     Take a single byte from the ringbuffer.
        /// </summary>
        /// <exception cref="InvalidOperationException">Ringbuffer does not have enough in it (insufficient length).</exception>
        byte Take();

        /// <summary>
        ///     Takes <paramref name="count" /> bytes from the ringbuffer
        ///     and returns a new buffer of them.
        /// </summary>
        /// <param name="count">Quantity of bytes to take.</param>
        /// <returns>Data from the ringbuffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it (insufficient length).</exception>
        byte[] Take(int count);

        /// <summary>
        ///     Takes bytes from the ringbuffer as necessary to fill <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">Buffer to fill with bytes taken from the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="ArgumentException">
        ///     Ringbuffer does not have enough in it (insufficient length),
        ///     or the destination <paramref name="buffer" /> is too small.
        /// </exception>
        void Take(byte[] buffer);

        /// <summary>
        ///     Takes <paramref name="count" /> bytes from the ringbuffer and writes
        ///     them to <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">Buffer to write bytes from the ringbuffer in.</param>
        /// <param name="offset">Offset in <paramref name="buffer" /> to write the bytes from the ringbuffer to.</param>
        /// <param name="count">Quantity of bytes to read into <paramref name="buffer" />.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="ArgumentException">
        ///     Ringbuffer does not have enough in it (insufficient length), or the destination <paramref name="buffer" /> is too
        ///     small.
        /// </exception>
        void Take(byte[] buffer, int offset, int count);

        /// <summary>
        ///     Takes <paramref name="count" /> bytes directly from the
        ///     ringbuffer and writes them into <paramref name="destination" />.
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="destination">Stream to write bytes to that are taken from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read into <paramref name="destination" />.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it (insufficient length).</exception>
        void TakeTo(Stream destination, int count);

        /// <summary>
        ///     Takes <paramref name="count" /> bytes directly from the
        ///     ringbuffer and writes them into <paramref name="destination" /> asynchronously.
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="destination">Stream to write bytes to that are taken from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read into <paramref name="destination" />.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it (insufficient length).</exception>
        Task TakeToAsync(Stream destination, int count, CancellationToken cancellationToken);

        /// <summary>
        ///     Skips/discards <paramref name="count" /> number of bytes from the ringbuffer.
        /// </summary>
        /// <remarks>
        ///     Skipped bytes are not actually discarded until overwritten,
        ///     so if security is needed, use <see cref="RingBuffer.Reset" />.
        /// </remarks>
        /// <param name="count">Number of bytes to skip ahead.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough in it.</exception>
        void Skip(int count);

        /// <summary>
        ///     Resets the ringbuffer to an empty state, and sets every byte to zero.
        /// </summary>
        void Reset();

        /// <summary>
        ///     Emits the entire length of the buffer in use. Ringbuffer will be empty after use.
        /// </summary>
        /// <returns>Ringbuffer data.</returns>
        byte[] ToArray();
    }
}
