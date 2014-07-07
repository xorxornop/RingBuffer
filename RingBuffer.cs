//
//  Copyright 2013  Matthew Ducker
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System;
using System.IO;

namespace RingByteBuffer
{
	/// <summary>
	/// Simple cyclic/ring data buffer.
	/// </summary>
	/// <remarks>
	/// Makes efficient use of memory.
	/// Ensure initialised capacity can hold typical use case requirement with some overflow tolerance.	
	/// </remarks>	
	public sealed class RingBuffer
	{
		private readonly byte[] _buffer;
		private readonly int _capacity;
		private int _head, _tail;
		private int _length;
		private readonly bool _overwiteable;

		/// <summary>
		/// Maximum storage possible in this instance.
		/// </summary>
		public int Capacity { 
			get { return _capacity; } 
		}

		/// <summary>
		/// Length of data stored.
		/// </summary>
		public int Length { 
			get { return _length; } 
		}

		/// <summary>
		/// Capacity of buffer not filled with data.
		/// </summary>
		public int Spare { 
			get { return _capacity - _length; } 
		}

		/// <summary>
		/// Whether data is overwritable.
		/// </summary>
		/// <value><c>true</c> if overwritable; otherwise, <c>false</c>.</value>
		public bool Overwritable
		{
			get { return _overwiteable; }
		}

		/// <summary>
		/// Initialises a new instance of a <see cref="RingBuffer"/>.
		/// </summary>
		/// <param name="capacity">Maximum storage capability.</param>
		/// <param name="allowOverwrite">If set to <c>true</c> allow overwrite.</param>
		public RingBuffer (int capacity, bool allowOverwrite = false) {
			_capacity = capacity;
			_buffer = new byte[capacity];
			_overwiteable = allowOverwrite;
		}

		/// <summary>
		/// Initialises a new instance of a <see cref="RingBuffer"/>.
		/// </summary>
		/// <param name="capacity">Maximum storage capability.</param>
		/// <param name="buffer">Data to place in the ringbuffer.</param>
		/// <param name="allowOverwrite">If set to <c>true</c> allow overwrite.</param>
		/// <exception cref="ArgumentNullException">Supplied data array is null.</exception>
		/// <exception cref="ArgumentException">Supplied data length exceeds capacity of created ringbuffer.</exception>
		public RingBuffer (int capacity, byte[] buffer, bool allowOverwrite = false) 
			: this(capacity, allowOverwrite) 
		{
			if (buffer == null) {
				throw new ArgumentNullException ("buffer");
			}
			if (buffer.Length > capacity) {
				throw new ArgumentException ("Initialisation data length exceeds allocated capacity.", "buffer");
			}

            buffer.CopyBytes(0, _buffer, 0, buffer.Length);
			_tail += buffer.Length;
			_length += _tail;
		}

		/// <summary>
		/// Put the specified data in its entirety into the ringbuffer.
		/// </summary>
		/// <param name="buffer">Buffer to put.</param>
		public void Put (byte[] buffer) {
			Put(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Put the specified number of bytes of data, from a specified offset, into the ringbuffer.
		/// </summary>
		/// <param name="buffer">Buffer to take input bytes from.</param>
		/// <param name="offset">Offset to take bytes from.</param>
		/// <param name="count">Number of bytes to put in.</param>
		/// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
		/// <exception cref="InvalidOperationException">Too much being written.</exception>
		/// <exception cref="ArgumentException">Buffer too small.</exception>
		public void Put (byte[] buffer, int offset, int count) {
			if (offset < 0) {
				throw new ArgumentOutOfRangeException ("offset", "Negative offset specified. Offset must be positive.");
			} else if (count < 0) {
				throw new ArgumentOutOfRangeException ("count", "Negative count specified. Count must be positive.");
			} else if (_length + count > _capacity) {
				if (_overwiteable) {
					var skip = (_length + count) - _capacity;
					Skip (skip);
				} else {
					throw new InvalidOperationException ("Buffer capacity insufficient for write operation. " +
						"Write a smaller quantity relative to the capacity to avoid this.");
				}
			} else if (offset + count > buffer.Length) {
				throw new ArgumentException ("Insufficient data in input buffer.");
			}

			if (count == 0)
				return;

			if (_tail + count >= _capacity) {
				var chunkSize = _capacity - _tail;
                buffer.CopyBytes(offset, _buffer, _tail, chunkSize);
				_tail = 0;
				offset += chunkSize;
				count -= chunkSize;
				_length += chunkSize;
			}
            buffer.CopyBytes(offset, _buffer, _tail, count);
			_tail += count;
			_length += count;
		}

		/// <summary>
		/// Put the specified byte in the ringbuffer.
		/// </summary>
		/// <param name="input">Byte to put in.</param>
		/// <exception cref="InvalidOperationException">Ringbuffer is full.</exception>
		public void Put (byte input) {
			if (_length + 1 > _capacity) {
				if (_overwiteable) {
					Skip (1);
				} else {
					throw new InvalidOperationException ("Buffer capacity insufficient for write operation.");
				}
			}

			_buffer[_tail++] = input;
			if (_tail == _capacity) _tail = 0;
			_length++;
		}

        /// <summary>
        /// Reads a stream directly into the ringbuffer. 
		/// Avoids overhead of unnecessary copying.
        /// </summary>
        /// <param name="source">Stream to take bytes from to write to the ringbuffer.</param>
        /// <param name="count">Number of bytes to take/read.</param>
		/// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
		/// <exception cref="InvalidOperationException">Too much being written.</exception>
		public void PutFrom (Stream source, int count) {
			if (count < 0) {
				throw new ArgumentOutOfRangeException ("count", "Negative count specified. Count must be positive.");
			} else if (_length + count > _capacity) {
				if (_overwiteable) {
					int skip = (_length + count) - _capacity;
					Skip (skip);
				} else {
					throw new InvalidOperationException ("Buffer capacity insufficient for write operation. " +
						"Write a smaller quantity relative to the capacity to avoid this.");
				}
			}

			if (count == 0)
				return;

			if (_tail + count >= _capacity) {
				var chunkSize = _capacity - _tail;
				var totalIn = 0;
				var iterIn = 0;
				while (totalIn < chunkSize) {
					iterIn = source.Read(_buffer, _tail, totalIn - chunkSize);
					if (iterIn == 0) {
						throw new EndOfStreamException ();
					}
					totalIn += iterIn;
				}
				_tail = 0;
				count -= chunkSize;
				_length += chunkSize;
			}
            source.Read(_buffer, _tail, count);
            _tail += count;
			_length += count;
        }

		/// <summary>
		/// Take bytes from the ringbuffer to fill the specified buffer.
		/// </summary>
		/// <param name="buffer">Buffer to fill with bytes from the ringbuffer.</param>
		public void Take (byte[] buffer) {
			Take(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Take the specified quantity of bytes and return a buffer of them.
		/// </summary>
		/// <param name="count">Quantity of bytes to take.</param>
		/// <returns>Data from the ringbuffer.</returns>
		public byte[] Take (int count) {
			var output = new byte[count];
			Take(output);
			return output;
		}

		/// <summary>
		/// Take bytes from the ringbuffer and put them in a buffer at a specified offset.
		/// </summary>
		/// <param name="buffer">Buffer to write bytes from the ringbuffer in.</param>
		/// <param name="offset">Offset in the buffer to write the bytes from the ringbuffer at.</param>
		/// <param name="count">Quantity of bytes to read.</param>
		/// <exception cref="ArgumentOutOfRangeException">Offset or count is negative.</exception>
		/// <exception cref="ArgumentException">
		/// Ringbuffer does not have enough data in it, or the receiving buffer is too small.
		/// </exception>
		public void Take (byte[] buffer, int offset, int count) {
			if (offset < 0) {
				throw new ArgumentOutOfRangeException ("offset", "Negative offset specified. Offsets must be positive.");
			} else if (count < 0) {
					throw new ArgumentOutOfRangeException ("count", "Negative count specified. Count must be positive.");
			} else if (count > _length) {
				throw new ArgumentException ("Ringbuffer contents insufficient for read operation. " +
					"Request a smaller quantity relative to the length to avoid this.", "count");
			} else if (buffer.Length < offset + count) {
				throw new ArgumentException ("Destination array too small for requested output.");
			}

			if(count == 0) 
				return;

			if (_head + count >= _capacity) {
				var chunkSize = _capacity - _head;
                _buffer.CopyBytes(_head, buffer, offset, chunkSize);
				_head = 0;
				offset += chunkSize;
				count -= chunkSize;
				_length -= chunkSize;
			}
            _buffer.CopyBytes(_head, buffer, offset, count);
			_head += count;
			_length -= count;
		}

		/// <summary>
		/// Take a single byte from the ringbuffer.
		/// </summary>
		/// <exception cref="InvalidOperationException">Ringbuffer does not have enough data in it.</exception>
		public byte Take () {
			if (_length == 0) 
				throw new InvalidOperationException("Buffer contents insufficient for read operation.");

			var output = _buffer[_head++];
			if (_head == _capacity) _head = 0;
			_length--;

			return output;
		}

		/// <summary>
		/// Read bytes directly from the ringbuffer into stream. 
		/// Avoids overhead of unnecessary copying.
		/// </summary>
		/// <param name="destination">Destination.</param>
		/// <param name="count">Count.</param>
		/// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
		public void TakeTo (Stream destination, int count) {
			if (count < 0) {
				throw new ArgumentOutOfRangeException ("count", "Negative count specified. Count must be positive.");
			} else if (count > _length) {
				throw new ArgumentException ("Buffer contents insufficient for read operation. " +
					"Request a smaller quantity relative to the capacity to avoid this.", "count");
			}

			if (count == 0) 
				return;

			if (_head + count >= _capacity) {
				var chunkSize = _capacity - _head;
                destination.Write(_buffer, _head, chunkSize);
				_head = 0;
				count -= chunkSize;
				_length -= chunkSize;
			}
			destination.Write(_buffer, _head, count);
			_head += count;
			_length -= count;
        }

		/// <summary>
		/// Advances the stream a specified number of bytes. 
		/// Skipped data is non-recoverable; state is not persisted.
		/// </summary>
		/// <param name="count">Number of bytes to skip ahead.</param>
		/// <exception cref="ArgumentOutOfRangeException">Count is negative.</exception>
		/// <exception cref="ArgumentException">Ringbuffer does not have enough data in it.</exception>
		public int Skip (int count) {
			if (count < 0) {
				throw new ArgumentOutOfRangeException ("count", "Negative count specified. Count must be positive.");
			} else if (count > _length) {
				throw new ArgumentException("Count specified exceeds data available.", "count");
			}

			var skipped = count;

			if (_head + count > _capacity) {
				var remove = _capacity - _head;
				_head = 0;
				count -= remove;
				_length -= remove;
			}
			_head += count;
			_length -= count;

			return skipped;
		}

		/// <summary>
		/// Reset the ringbuffer to an empty state. 
		/// Sets every byte in the internal array (buffer) to zero.
		/// </summary>
		public void Reset () {
			Array.Clear (_buffer, 0, _buffer.Length);
			_head = 0;
			_tail = 0;
			_length = 0;
		}

		/// <summary>
		/// Emit the entire length of the buffer in use.
		/// </summary>
		/// <returns>Ringbuffer data.</returns>
		public byte[] ToArray () {
			return Take(Length);
		}
	}
}
