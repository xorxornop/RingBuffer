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
    ///     Exposes a <see cref="RingBuffer" /> as a Stream.
    ///     Provides buffer capability with standard stream interface.
    /// </summary>
    public sealed class RingBufferStream : Stream
    {
        private RingBuffer _ringBuffer;

        /// <summary>
        ///     Initializes a new <see cref="RingBufferStream" />.
        /// </summary>
        /// <param name="capacity">Maximum required storage capability of the ringbuffer.</param>
        /// <param name="allowOverwrite">If set to <c>true</c> allow overwrite.</param>
        public RingBufferStream(int capacity, bool allowOverwrite)
        {
            _ringBuffer = new SequentialRingBuffer(capacity, null, allowOverwrite);
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get { return _ringBuffer.CurrentLength > 0; }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get { return _ringBuffer.CurrentLength > 0; }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get { return _ringBuffer.CurrentLength < _ringBuffer.MaximumCapacity; }
        }

        /// <summary>
        ///     Does nothing in this implementation.
        /// </summary>
        public override void Flush()
        {
            // Do nothing
        }

        /// <inheritdoc />
        public override long Length
        {
            get { return _ringBuffer.CurrentLength; }
        }

        /// <summary>
        ///     Maximum storage capacity of the ringbuffer.
        /// </summary>
        /// <value>The maximum length of the ringbuffer.</value>
        public int Capacity
        {
            get { return _ringBuffer.MaximumCapacity; }
        }

        /// <summary>
        ///     Currently remaining capacity of ringbuffer.
        /// </summary>
        /// <value>The maximum length of data that can be written at the current capacity of the ringbuffer.</value>
        public int Spare
        {
            get { return _ringBuffer.SpareLength; }
        }

        /// <summary>
        ///     Gets the position. Setting position not allowed.
        /// </summary>
        public override long Position
        {
            get { return 0; }
            set { throw new InvalidOperationException("Setting position not supported."); }
        }

        /// <summary>
        ///     Read a single byte from the ringbuffer.
        /// </summary>
        public override int ReadByte()
        {
            return _ringBuffer.Take();
        }

        /// <summary>
        ///     Takes <paramref name="count" /> bytes from the ringbuffer and puts
        ///     them in <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">Buffer to write bytes from the ringbuffer in.</param>
        /// <param name="offset">Offset in <paramref name="buffer" /> to write the bytes from the ringbuffer to.</param>
        /// <param name="count">Quantity of bytes to read into <paramref name="buffer" />.</param>
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min(count, _ringBuffer.CurrentLength);
            _ringBuffer.Take(buffer, offset, count);
            return count;
        }

        /// <summary>
        ///     Takes <paramref name="count" /> bytes from the ringbuffer and puts
        ///     them in <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">Buffer to write bytes from the ringbuffer in.</param>
        /// <param name="offset">Offset in <paramref name="buffer" /> to write the bytes from the ringbuffer to.</param>
        /// <param name="count">Quantity of bytes to read into <paramref name="buffer" />.</param>
        /// <param name="exact">
        ///     If set to <c>true</c>, returning less than <paramref name="count" /> bytes is unacceptable
        ///     (exception is thrown).
        /// </param>
        public int Read(byte[] buffer, int offset, int count, bool exact)
        {
            if (_ringBuffer.CurrentLength == 0 && exact && count > 0) {
                throw new EndOfStreamException();
            }
            if (exact && _ringBuffer.CurrentLength < count) {
                count = _ringBuffer.CurrentLength;
            }
            _ringBuffer.Take(buffer, offset, count);
            return count;
        }

        /// <summary>
        ///     Reads from the ringbuffer, and writes to <paramref name="destination" />.
        /// </summary>
        /// <param name="destination">Destination to write bytes to, after being read from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <returns>Number of bytes written (read from the buffer).</returns>
        public int ReadTo(Stream destination, int count)
        {
            if (_ringBuffer.CurrentLength == 0 && count > 0) {
                throw new EndOfStreamException();
            }
            if (_ringBuffer.CurrentLength < count) {
                count = _ringBuffer.CurrentLength;
            }
            _ringBuffer.TakeTo(destination, count);
            return count;
        }

        /// <summary>
        ///     Reads from the ringbuffer, and writes to <paramref name="destination" /> asynchronously.
        /// </summary>
        /// <param name="destination">Destination to write bytes to, after being read from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <returns>Number of bytes written (read from the ringbuffer).</returns>
        public Task ReadToAsync(Stream destination, int count)
        {
            if (_ringBuffer.CurrentLength == 0 && count > 0) {
                throw new EndOfStreamException();
            }
            return _ringBuffer.TakeToAsync(destination, count, CancellationToken.None);
        }

        /// <summary>
        ///     Reads from the ringbuffer, and writes to <paramref name="destination" /> asynchronously.
        /// </summary>
        /// <param name="destination">Destination to write bytes to, after being read from the ringbuffer.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>Number of bytes written (read from the ringbuffer).</returns>
        public Task ReadToAsync(Stream destination, int count, CancellationToken cancellationToken)
        {
            if (_ringBuffer.CurrentLength == 0 && count > 0) {
                throw new EndOfStreamException();
            }
            return _ringBuffer.TakeToAsync(destination, count, cancellationToken);
        }

        /// <summary>
        ///     Writes a single byte to the ringbuffer.
        /// </summary>
        /// <param name="value">Byte to write to the ringbuffer.</param>
        public override void WriteByte(byte value)
        {
            _ringBuffer.Put(value);
        }

        /// <summary>
        ///     Write <paramref name="count" /> bytes from <paramref name="buffer" /> into the ringbuffer.
        /// </summary>
        /// <param name="buffer">Buffer to take input bytes from.</param>
        /// <param name="offset">Offset in <paramref name="buffer" /> to take bytes from.</param>
        /// <param name="count">Number of bytes to write into the ringbuffer.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _ringBuffer.Put(buffer, offset, count);
        }

        /// <summary>
        ///     Reads <paramref name="count" /> bytes from <paramref name="source" />,
        ///     and writes to the ringbuffer.
        /// </summary>
        /// <param name="source">Source to take bytes from for writing.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <returns>Number of bytes written (read from the source).</returns>
        public int WriteFrom(Stream source, int count)
        {
            _ringBuffer.PutFrom(source, count);
            return count;
        }

        /// <summary>
        ///     Reads <paramref name="count" /> bytes from <paramref name="source" />
        ///     asynchronously, and writes to the ringbuffer.
        /// </summary>
        /// <param name="source">Source to take bytes from for writing.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <returns>Number of bytes written (read from the source).</returns>
        public Task WriteFromAsync(Stream source, int count)
        {
            return _ringBuffer.PutFromAsync(source, count, CancellationToken.None);
        }

        /// <summary>
        ///     Reads <paramref name="count" /> bytes from <paramref name="source" />
        ///     asynchronously, and writes to the ringbuffer.
        /// </summary>
        /// <param name="source">Source to take bytes from for writing.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>Number of bytes written (read from the source).</returns>
        public Task WriteFromAsync(Stream source, int count, CancellationToken cancellationToken)
        {
            return _ringBuffer.PutFromAsync(source, count, cancellationToken);
        }

        /// <summary>
        ///     Advances the stream a specified number of bytes.
        ///     It is not possible to revert this action.
        /// </summary>
        /// <param name="offset">Number of bytes to skip ahead.</param>
        /// <param name="origin">
        ///     Use only values of <see cref="SeekOrigin.Begin" /> or <see cref="SeekOrigin.Current" /> (identical meaning).
        /// </param>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.End) {
                throw new NotSupportedException("Seek only possible from current stream position (Begin/Current).");
            }
            _ringBuffer.Skip((int) offset);
            return offset;
        }

        /// <summary>
        ///     Shortens the contents of the ringbuffer (extension is not supported).
        /// </summary>
        public override void SetLength(long value)
        {
            if (value < 0) {
                throw new ArgumentException("Value cannot be negative.");
            }
            if (value > _ringBuffer.CurrentLength) {
                throw new NotSupportedException("Cannot extend contents of ringbuffer.");
            }

            _ringBuffer.Skip(_ringBuffer.CurrentLength - (int) value);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _ringBuffer.Reset();
            base.Dispose(disposing);
        }
    }
}
