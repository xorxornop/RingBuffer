#region License

//  	Copyright 2013-2015 Matthew Ducker
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

using System.Threading;
using System.Threading.Tasks;

namespace RingByteBuffer.FullyConcurrent
{
    /// <summary>
    ///     Exposes a <see cref="FullyConcurrentRingBuffer" /> as a <see cref="T:System.IO.Stream" />.
    ///     Provides additional capabilities over that of the <see cref="RingBufferStream" /> base class.
    /// </summary>
    public class FullyConcurrentRingBufferStream : RingBufferStream
    {
        /// <summary>
        ///     Initialises a new instance of the <see cref="FullyConcurrentRingBufferStream" /> class.
        /// </summary>
        /// <param name="capacity">The maximum capacity of the ringbuffer.</param>
        /// <param name="parallelism">The maximum I/O parallelism (number of concurrent operations).</param>
        protected FullyConcurrentRingBufferStream(int capacity, int? parallelism = null)
            : base(new FullyConcurrentRingBuffer(capacity, null, parallelism)) {}


        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await ((FullyConcurrentRingBuffer) base.RingBuffer).Take(buffer, offset, count, cancellationToken);
            return count;
        }


        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ((FullyConcurrentRingBuffer) base.RingBuffer).Put(buffer, offset, count, cancellationToken);
        }
    }
}
