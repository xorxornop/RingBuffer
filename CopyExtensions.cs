//
//  Copyright 2014  Matthew Ducker
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

namespace RingByteBuffer
{
    /// <summary>
    ///     Extension methods for copying byte arrays.
    /// </summary>
    public static class CopyExtensions
    {
        /// <summary>
        ///     Produce a deep copy (by value) of <paramref name="data"/> array.
        /// </summary>
        /// <param name="data">Array to produce a copy of.</param>
        /// <returns>Copy of <paramref name="data"/> array.</returns>
        public static byte[] DeepCopy(this byte[] data)
        {
            if (data == null) {
                return null;
            }
            var dst = new byte[data.Length];
            data.CopyBytes(0, dst, 0, data.Length);
            return dst;
        }

        /// <summary>
        ///     Copy bytes from <paramref name="src"/> into <paramref name="dst"/>.
        /// </summary>
        /// <param name="src">The source byte array.</param>
        /// <param name="srcOffset">
        ///     The offset in <paramref name="src"/> at which the source data begins.
        /// </param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="dst">The destination byte array.</param>
        /// <param name="dstOffset">
        ///     The offset in <paramref name="dst"/> at which to copy into.
        /// </param>
        public static void CopyBytes(this byte[] src, int srcOffset, byte[] dst, int dstOffset, int length)
        {
#if INCLUDE_UNSAFE
            const int unsafeLimit = 128;
            if (length > unsafeLimit) {
                if (srcOffset + length > src.Length || dstOffset + length > dst.Length) {
                    throw new ArgumentException(
                        "Either/both src or dst offset is incompatible with array length. Security risk in unsafe execution!");
                }
                unsafe {
                    fixed (byte* srcPtr = src) {
                        fixed (byte* dstPtr = dst) {
                            CopyMemory(dstPtr + dstOffset, srcPtr + srcOffset, length);
                        }
                    }
                }
            } else {
                Array.Copy(src, srcOffset, dst, dstOffset, length);
            }
#else
            const int bufferBlockCopyLimit = 1024;
            if (length > bufferBlockCopyLimit) {
                Buffer.BlockCopy(src, srcOffset, dst, dstOffset, length);
            } else {
                Array.Copy(src, srcOffset, dst, dstOffset, length);
            }
#endif
        }

#if INCLUDE_UNSAFE
        /// <summary>
        ///     Copy data from <paramref name="src"/> into <paramref name="dst"/>.
        /// </summary>
        /// <param name="src">Pointer to source of data.</param>
        /// <param name="dst">Pointer to destination for data.</param>
        /// <param name="length">Length of data to copy in bytes.</param>
        internal static unsafe void CopyMemory(byte* dst, byte* src, int length)
        {
            while (length >= 16) {
                *(ulong*) dst = *(ulong*) src;
                dst += 8;
                src += 8;
                *(ulong*) dst = *(ulong*) src;
                dst += 8;
                src += 8;
                length -= 16;
            }

            if (length >= 8) {
                *(ulong*) dst = *(ulong*) src;
                dst += 8;
                src += 8;
                length -= 8;
            }

            if (length >= 4) {
                *(uint*) dst = *(uint*) src;
                dst += 4;
                src += 4;
                length -= 4;
            }

            if (length >= 2) {
                *(ushort*) dst = *(ushort*) src;
                dst += 2;
                src += 2;
                length -= 2;
            }

            if (length != 0) {
                *dst = *src;
            }
        }
#endif
    }
}
