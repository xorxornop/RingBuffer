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

namespace RingByteBuffer
{
    /// <summary>
    ///     Simple ring/cyclic data buffer ("ringbuffer").
    /// </summary>
    /// <remarks>
    ///     Makes efficient use of memory.
    ///     Ensure initialised capacity can hold typical use case requirement with some overflow tolerance.
    /// </remarks>
    public class RingBuffer
    {
        private readonly byte[] _buffer;
        private readonly int _capacity;
        private int _head, _tail;
        private int _length;
        private readonly bool _overwriteable;

        /// <summary>
        ///     Maximum data possible to be stored.
        /// </summary>
        public int Capacity
        {
            get { return _capacity; }
        }

        /// <summary>
        ///     Length of data stored.
        /// </summary>
        public int Length
        {
            get { return _length; }
        }

        /// <summary>
        ///     Capacity not filled with data.
        /// </summary>
        public int Spare
        {
            get { return _capacity - _length; }
        }

        /// <summary>
        ///     Whether data is overwritable.
        /// </summary>
        /// <value><c>true</c> if overwritable; otherwise, <c>false</c>.</value>
        public bool Overwritable
        {
            get { return _overwriteable; }
        }

        /// <summary>
        ///     Initialises a new instance of a <see cref="RingBuffer" />.
        /// </summary>
        /// <param name="capacity">Maximum required storage capability of the ringbuffer.</param>
        /// <param name="allowOverwrite">If set to <c>true</c> overwrite will be allowed, otherwise <c>false</c>.</param>
        public RingBuffer(int capacity, bool allowOverwrite = false)
        {
            if (capacity < 2) {
                throw new ArgumentException("Capacity must be at least 2 bytes.");
            }
            _capacity = capacity;
            _buffer = new byte[capacity];
            _overwriteable = allowOverwrite;
        }

        /// <summary>
        ///     Initialises a new instance of a <see cref="RingBuffer" />.
        /// </summary>
        /// <param name="capacity">Maximum required storage capability of the ringbuffer.</param>
        /// <param name="buffer">Data to initialise the ringbuffer with.</param>
        /// <param name="allowOverwrite">If set to <c>true</c> allow overwrite.</param>
        /// <exception cref="ArgumentNullException">Supplied data array is null.</exception>
        /// <exception cref="ArgumentException">
        ///     Capacity is less than 2 bytes, or <paramref name="buffer"/> length exceeds <paramref name="capacity"/>.
        /// </exception>
        public RingBuffer(int capacity, byte[] buffer, bool allowOverwrite = false)
            : this(capacity, allowOverwrite)
        {
            if (capacity < 2) {
                throw new ArgumentException("Capacity must be at least 2 bytes.");
            }
            if (buffer == null) {
                throw new ArgumentNullException("buffer");
            }
            if (buffer.Length > capacity) {
                throw new ArgumentException("Initialisation data length exceeds allocated capacity.", "buffer");
            }

            buffer.CopyBytes(0, _buffer, 0, buffer.Length);
            _tail += buffer.Length;
            _length += _tail;
        }


        /// <summary>
        ///     Puts the single byte <paramref name="input"/> in the ringbuffer.
        /// </summary>
        /// <param name="input">Byte to write to the ringbuffer.</param>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much data in it.</exception>
        public void Put(byte input)
        {
            if (_length + 1 > _capacity) {
                if (_overwriteable) {
                    Skip(1);
                } else {
                    throw new InvalidOperationException("Buffer capacity insufficient for write operation.");
                }
            }

            _buffer[_tail++] = input;
            if (_tail == _capacity) {
                _tail = 0;
            }
            _length++;
        }

        /// <summary>
        ///     Put the data in <paramref name="buffer"/> in its entirety into the ringbuffer.
        /// </summary>
        /// <param name="buffer">Buffer to take input bytes from.</param>
        /// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much data in it.</exception>
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
        /// <exception cref="InvalidOperationException">Ringbuffer has too much data in it.</exception>
        public void Put(byte[] buffer, int offset, int count)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offset must be positive.");
            }
            PutFromInitial(count);

            while (count > 0) {
                int chunk = Math.Min(_capacity - _tail, count);
                buffer.CopyBytes(offset, _buffer, _tail, chunk);
                _tail = (_tail + chunk == _capacity) ? 0 : _tail + chunk;
                _length += chunk;
                offset += chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Reads <paramref name="count"/> bytes from <paramref name="source"/>  
        ///     and puts them directly into the ringbuffer. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much data in it.</exception>
        public void PutFrom(Stream source, int count)
        {
            PutFromInitial(count);

            while (count > 0) {
                int chunk = Math.Min(_capacity - _tail, count);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    var iterIn = source.Read(_buffer, _tail, chunkIn - chunk);
                    if (iterIn == 0) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                _tail = (_tail + chunk == _capacity) ? 0 : _tail + chunk;
                _length += chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Reads <paramref name="count"/> bytes from <paramref name="source"/> asynchronously 
        ///     and puts them directly into the ringbuffer. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to read bytes from to put into the ringbuffer.</param>
        /// <param name="count">Number of bytes to put into the ringbuffer.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="InvalidOperationException">Ringbuffer has too much data in it.</exception>
        public async Task PutFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            PutFromInitial(count);

            while (count > 0) {
                int chunk = Math.Min(_capacity - _tail, count);
                int chunkIn = 0;
                while (chunkIn < chunk) {
                    int iterIn = await source.ReadAsync(_buffer, _tail, chunkIn - chunk, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) {
                        return;
                    }
                    if (iterIn == 0) {
                        throw new EndOfStreamException();
                    }
                    chunkIn += iterIn;
                }
                _tail = (_tail + chunk == _capacity) ? 0 : _tail + chunk;
                _length += chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Verifies validity of <paramref name="count"/> parameter value.
        /// </summary>
        /// <param name="count">Number of bytes to put/write.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer has too much in it.</exception>
        protected void PutFromInitial(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (_length + count > _capacity) {
                if (_overwriteable) {
                    int skip = _capacity - (_length + count);
                    Skip(skip);
                } else {
                    throw new InvalidOperationException("Buffer capacity insufficient for write operation. " +
                                                        "Write a smaller quantity relative to the capacity to avoid this.");
                }
            }
        }

        /// <summary>
        ///     Take a single byte from the ringbuffer.
        /// </summary>
        /// <exception cref="InvalidOperationException">Ringbuffer does not have enough data in it.</exception>
        public byte Take()
        {
            if (_length == 0) {
                throw new InvalidOperationException("Buffer contents insufficient for read operation.");
            }

            byte output = _buffer[_head++];
            if (_head == _capacity) {
                _head = 0;
            }
            _length--;

            return output;
        }

        /// <summary>
        ///     Takes <paramref name="count"/> bytes from the ringbuffer 
        ///     and returns a new buffer of them.
        /// </summary>
        /// <param name="count">Quantity of bytes to take.</param>
        /// <returns>Data from the ringbuffer.</returns>
        public byte[] Take(int count)
        {
            if (count < 0) 
                throw new ArgumentOutOfRangeException();
            if (count == 0) 
                return new byte[0]; 

            var output = new byte[count];
            Take(output);
            return output;
        }

        /// <summary>
        ///     Takes bytes from the ringbuffer as necessary to fill <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">Buffer to fill with bytes taken from the ringbuffer.</param>
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
        ///     Ringbuffer does not have enough data in it, or the destination <paramref name="buffer"/> is too small.
        /// </exception>
        public void Take(byte[] buffer, int offset, int count)
        {
            if (offset < 0) {
                throw new ArgumentOutOfRangeException("offset", "Negative offset specified. Offsets must be positive.");
            }
            TakeCheck(count);
            if (buffer.Length < offset + count) {
                throw new ArgumentException("Destination array too small for requested output.");
            }

            while (count > 0) {
                int chunk = Math.Min(_capacity - _head, count);
                _buffer.CopyBytes(_head, buffer, offset, chunk);
                _head = (_head + chunk == _capacity) ? 0 : _head + count;
                _length -= chunk;
                offset += chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Takes <paramref name="count"/> bytes directly from the 
        ///     ringbuffer and writes them into <paramref name="destination"/>. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="destination">Stream to write bytes to that are taken from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read into <paramref name="destination"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
        public void TakeTo(Stream destination, int count)
        {
            TakeCheck(count);

            while (count > 0) {
                int chunk = Math.Min(_capacity - _head, count);

                destination.Write(_buffer, _head, chunk);
                _head = (_head + chunk == _capacity) ? 0 : _head + count;
                _length -= chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Takes <paramref name="count"/> bytes directly from the 
        ///     ringbuffer and writes them into <paramref name="destination"/> asynchronously. 
        ///     Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="destination">Stream to write bytes to that are taken from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read into <paramref name="destination"/>.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
        public async Task TakeToAsync(Stream destination, int count, CancellationToken cancellationToken)
        {
            TakeCheck(count);

            while (count > 0) {
                int chunk = Math.Min(_capacity - _head, count);
                await destination.WriteAsync(_buffer, _head, chunk, cancellationToken);
                if (cancellationToken.IsCancellationRequested) {
                    return;
                }
                _head = (_head + chunk == _capacity) ? 0 : _head + count;
                _length -= chunk;
                count -= chunk;
            }
        }

        /// <summary>
        ///     Verifies validity of <paramref name="count"/> parameter value.
        /// </summary>
        /// <param name="count">Number of bytes to take/read.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
        protected void TakeCheck(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (count > _length) {
                throw new ArgumentException("Ringbuffer contents insufficient for take/read operation. " +
                                            "Request a smaller quantity relative to the capacity to avoid this.", "count");
            }
        }

        /// <summary>
        ///     Skips/discards <paramref name="count"/> number of bytes from the ringbuffer.
        /// </summary>
        /// <remarks>
        ///     Skipped bytes are not actually discarded until overwritten, 
        ///     so if security is needed, use <see cref="Reset"/>.
        /// </remarks>
        /// <param name="count">Number of bytes to skip ahead.</param>
        /// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
        /// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
        public void Skip(int count)
        {
            if (count < 0) {
                throw new ArgumentOutOfRangeException("count", "Negative count specified. Count must be positive.");
            }
            if (count > _length) {
                throw new ArgumentException("Ringbuffer contents insufficient for operation.", "count");
            }

            // Modular division gives new index positions
            _head = (_head + count) % _capacity;
            _tail = (_tail + count) % _capacity;
            _length -= count;
        }

        /// <summary>
        ///     Resets the ringbuffer to an empty state - 
        ///     sets every byte in the internal array (buffer) to zero.
        /// </summary>
        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _tail = 0;
            _length = 0;
        }

        /// <summary>
        ///     Emits the entire length of the buffer in use. Ringbuffer will be empty after use.
        /// </summary>
        /// <returns>Ringbuffer data.</returns>
        public byte[] ToArray()
        {
            return Take(Length);
        }
    }
}
