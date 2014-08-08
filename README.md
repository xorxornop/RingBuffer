RingByteBuffer
==============

Classic ringbuffer (with optional .NET BCL System.IO.Stream interface), in C#. Packaged as a portable class library (PCL).

*****

How to use it
-------------

Buffer is available through use of RingBuffer or RingBufferStream.
It supports overwriting instead of throwing exceptions when capacity is filled, for use-cases such as multimedia streaming.

Asynchronous I/O from streams is supported to boost performance.
When compiled with the #INCLUDE_UNSAFE compiler directive (easily accessible through ReleaseWithUnsafe build configuration), high-performance copying is available. As an fallback from this, Buffer.BlockCopy is used to accelerate copying of data.
The extension methods that enable this are public-scoped, so they can be used outside of RingByteBuffer.

RingBuffer methods:


+  	ctor: (int capacity, bool allowOverwrite = false) , (int capacity, byte[] buffer, bool allowOverwrite = false)
+ 	Put : (byte input) , (byte[] buffer) , (byte[] buffer, int offset, int count)
+ 	PutFrom (Stream source, int count)
+ 	PutFromAsync (Stream source, int count, CancellationToken cancellationToken)
+ 	Take : () => byte , () => byte[] , (int count) => byte[] , (byte[] buffer, int offset, int count)
+ 	TakeTo (Stream destination, int count)
+ 	TakeToAsync (Stream destination, int count, CancellationToken cancellationToken)
+ 	Skip (int count)
+ 	Reset()
+ 	ToArray() => byte[]

It has these properties:

+ 	Overwriteable
+ 	Capacity
+ 	Length
+ 	Spare

RingBufferStream exposes these methods through common System.IO.Stream methods, e.g. Write is mapped to Put.
It also exposes the PutFrom and TakeTo performance methods as WriteFrom and ReadTo, respectively. Use of these methods allows no-copy transfers between streams.
Asynchronous versions of the stream-oriented methods are available for maximum performance potential.

*****

Licensed under the Apache License Version 2.0 - so basically you can use it in whatever you want so long as you leave the license text in the files, and if you modify the code, a note of this is made in the file. It is also GPL-compatible.
