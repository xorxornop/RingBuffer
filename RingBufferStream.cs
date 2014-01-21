﻿//
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
	/// Exposes a <see cref="RingBuffer"/> as a Stream. 
	/// Provides buffer capability with standard stream interface. 
	/// </summary>
    public sealed class RingBufferStream : Stream
    {
        private readonly RingBuffer _ringBuffer;

		/// <summary>
		/// Initializes a new <see cref="RingBufferStream"/>.
		/// </summary>
		/// <param name="capacity">Maximum storage capacity of ringbuffer.</param>
		/// <param name="allowOverwrite">If set to <c>true</c> allow overwrite.</param>
		public RingBufferStream(int capacity, bool allowOverwrite) {
			_ringBuffer = new RingBuffer(capacity, allowOverwrite);
        }

		/// <inheritdoc/>
        public override bool CanRead {
            get { return _ringBuffer.Length > 0; }
        }

		/// <inheritdoc/>
        public override bool CanSeek {
            get { return _ringBuffer.Length > 0; }
        }

		/// <inheritdoc/>
        public override bool CanWrite {
            get { return _ringBuffer.Length < _ringBuffer.Capacity; }
        }

        /// <summary>
		/// Does nothing in this implementation.
        /// </summary>
        public override void Flush () {
            // Do nothing
        }

		/// <inheritdoc/>
        public override long Length {
            get { return _ringBuffer.Length; }
        }

		/// <summary>
		/// Maximum storage capacity of the ringbuffer.
		/// </summary>
		/// <value>The capacity.</value>
		public int Capacity
		{
			get { return _ringBuffer.Capacity; }
		}

		/// <summary>
		/// Capacity of ringbuffer not storing data.
		/// </summary>
		/// <value>The capacity.</value>
		public int Spare
		{
			get { return _ringBuffer.Spare; }
		}

		/// <summary>
		/// Gets the position. Setting position not allowed.
		/// </summary>
        public override long Position {
            get { return 0; }
			set { throw new InvalidOperationException ("Setting position not supported."); }
        }

		/// <inheritdoc/>
        public override int Read (byte[] buffer, int offset, int count) {
            count = Math.Min(count, _ringBuffer.Length);
            _ringBuffer.Take(buffer, offset, count);
            return count;
        }

		/// <summary>
		/// Read the specified buffer, offset, count and exact.
		/// </summary>
		/// <param name="buffer">Buffer.</param>
		/// <param name="offset">Offset.</param>
		/// <param name="count">Count.</param>
		/// <param name="exact">If set to <c>true</c> exact.</param>
        public int Read (byte[] buffer, int offset, int count, bool exact) {
            if(_ringBuffer.Length == 0 && exact && count > 0) throw new EndOfStreamException();
            if (exact && _ringBuffer.Length < count) count = _ringBuffer.Length;
            _ringBuffer.Take(buffer, offset, count);
            return count;
        }

        /// <summary>
        /// Read from the ringbuffer, writing to a stream destination.
        /// </summary>
        /// <param name="destination">Destination to write bytes that are read.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <param name="exact">To read less bytes than specified is unacceptable.</param>
        /// <returns>Number of bytes written (read from the buffer).</returns>
        public int ReadTo (Stream destination, int count, bool exact) {
            if(_ringBuffer.Length == 0 && exact && count > 0) throw new EndOfStreamException();
            if(exact && _ringBuffer.Length < count) count = _ringBuffer.Length;
            _ringBuffer.TakeTo(destination, count);
            return count;
        }

        /// <summary>
        /// Write to the ringbuffer, reading from a stream source. 
        /// Non-standard stream method for high performance.
        /// </summary>
        /// <param name="source">Source to take bytes from for writing.</param>
        /// <param name="count">Number of bytes to read/write.</param>
        /// <returns>Number of bytes written (read from the source).</returns>
        public int WriteFrom(Stream source, int count) {
            _ringBuffer.PutFrom(source, count);
            return count;
        }

        /// <summary>
        /// Advances the stream a specified number of bytes. 
        /// Skipped data is non-recoverable; state is not remembered, as position cannot be reverted.
        /// </summary>
        /// <param name="offset">Number of bytes to skip ahead.</param>
        /// <param name="origin">Use only values of Begin or Current (same effect).</param>
        public override long Seek(long offset, SeekOrigin origin) {
            if(origin == SeekOrigin.End) throw new ArgumentException("Seek only applicable from current stream position (Begin/Current).");
            return _ringBuffer.Skip((int)offset);
        }

		/// <summary>
		/// Not supported.
		/// </summary>
		/// <param name="value">Value.</param>
        public override void SetLength (long value) {
            throw new NotSupportedException();
        }

		/// <inheritdoc/>
        public override void Write (byte[] buffer, int offset, int count) {
			_ringBuffer.Put(buffer, offset, count);
        }

		/// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
			_ringBuffer.Reset ();
            base.Dispose(disposing);
        }
    }
}