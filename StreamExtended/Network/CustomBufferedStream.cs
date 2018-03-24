﻿using StreamExtended.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StreamExtended.Network
{
    /// <summary>
    /// A custom network stream inherited from stream
    /// with an underlying buffer 
    /// </summary>
    /// <seealso cref="System.IO.Stream" />
    public class CustomBufferedStream : Stream, IBufferedStream
    {
        private readonly Stream baseStream;
        private readonly bool leaveOpen;
        private byte[] streamBuffer;

        private readonly byte[] oneByteBuffer = new byte[1];

        private int bufferLength;

        private int bufferPos;

        private bool disposed;

        private bool closed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomBufferedStream"/> class.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        /// <param name="bufferSize">Size of the buffer.</param>
        /// <param name="leaveOpen"><see langword="true" /> to leave the stream open after disposing the <see cref="T:CustomBufferedStream" /> object; otherwise, <see langword="false" />.</param>
        public CustomBufferedStream(Stream baseStream, int bufferSize, bool leaveOpen = false)
        {
            this.baseStream = baseStream;
            this.leaveOpen = leaveOpen;
            streamBuffer = BufferPool.GetBuffer(bufferSize);
        }

        /// <summary>
        /// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
            baseStream.Flush();
        }

        /// <summary>
        /// When overridden in a derived class, sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            bufferLength = 0;
            bufferPos = 0;
            return baseStream.Seek(offset, origin);
        }

        /// <summary>
        /// When overridden in a derived class, sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        /// <summary>
        /// When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (bufferLength == 0)
            {
                FillBuffer();
            }

            int available = Math.Min(bufferLength, count);
            if (available > 0)
            {
                Buffer.BlockCopy(streamBuffer, bufferPos, buffer, offset, available);
                bufferPos += available;
                bufferLength -= available;
            }

            return available;
        }

        /// <summary>
        /// When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        [DebuggerStepThrough]
        public override void Write(byte[] buffer, int offset, int count)
        {
            OnDataSent(buffer, offset, count);
            baseStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Asynchronously reads the bytes from the current stream and writes them to another stream, using a specified buffer size and cancellation token.
        /// </summary>
        /// <param name="destination">The stream to which the contents of the current stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer. This value must be greater than zero. The default size is 81920.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="P:System.Threading.CancellationToken.None" />.</param>
        /// <returns>
        /// A task that represents the asynchronous copy operation.
        /// </returns>
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (bufferLength > 0)
            {
                await destination.WriteAsync(streamBuffer, bufferPos, bufferLength, cancellationToken);
                bufferLength = 0;
            }

            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        /// <summary>
        /// Asynchronously clears all buffers for this stream, causes any buffered data to be written to the underlying device, and monitors cancellation requests.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="P:System.Threading.CancellationToken.None" />.</param>
        /// <returns>
        /// A task that represents the asynchronous flush operation.
        /// </returns>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return baseStream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream,
        /// advances the position within the stream by the number of bytes read,
        /// and monitors cancellation requests.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer" /> at which 
        /// to begin writing data from the stream.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. 
        /// The default value is <see cref="P:System.Threading.CancellationToken.None" />.</param>
        /// <returns>
        /// A task that represents the asynchronous read operation.
        /// The value of the parameter contains the total 
        /// number of bytes read into the buffer.
        /// The result value can be less than the number of bytes
        /// requested if the number of bytes currently available is
        /// less than the requested number, or it can be 0 (zero)
        /// if the end of the stream has been reached.
        /// </returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (bufferLength == 0)
            {
                await FillBufferAsync(cancellationToken);
            }

            int available = Math.Min(bufferLength, count);
            if (available > 0)
            {
                Buffer.BlockCopy(streamBuffer, bufferPos, buffer, offset, available);
                bufferPos += available;
                bufferLength -= available;
            }

            return available;
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
        /// </summary>
        /// <returns>
        /// The unsigned byte cast to an Int32, or -1 if at the end of the stream.
        /// </returns>
        public override int ReadByte()
        {
            if (bufferLength == 0)
            {
                FillBuffer();
            }

            if (bufferLength == 0)
            {
                return -1;
            }

            bufferLength--;
            return streamBuffer[bufferPos++];
        }

        /// <summary>
        /// Peeks a byte asynchronous.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public async Task<int> PeekByteAsync(int index)
        {
            if (Available <= index)
            {
                await FillBufferAsync();
            }

            if (Available <= index)
            {
                return -1;
            }

            return streamBuffer[bufferPos + index];
        }

        /// <summary>
        /// Peeks a byte from buffer.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Index is out of buffer size</exception>
        public byte PeekByteFromBuffer(int index)
        {
            if (bufferLength <= index)
            {
                throw new Exception("Index is out of buffer size");
            }

            return streamBuffer[bufferPos + index];
        }

        /// <summary>
        /// Reads a byte from buffer.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception">Buffer is empty</exception>
        public byte ReadByteFromBuffer()
        {
            if (bufferLength == 0)
            {
                throw new Exception("Buffer is empty");
            }

            bufferLength--;
            return streamBuffer[bufferPos++];
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream, advances the current position within this stream by the number of bytes written, and monitors cancellation requests.
        /// </summary>
        /// <param name="buffer">The buffer to write data from.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> from which to begin copying bytes to the stream.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="P:System.Threading.CancellationToken.None" />.</param>
        /// <returns>
        /// A task that represents the asynchronous write operation.
        /// </returns>
        [DebuggerStepThrough]
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            OnDataSent(buffer, offset, count);
            return baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
        /// </summary>
        /// <param name="value">The byte to write to the stream.</param>
        public override void WriteByte(byte value)
        {
            oneByteBuffer[0] = value;
            OnDataSent(oneByteBuffer, 0, 1);
            baseStream.Write(oneByteBuffer, 0, 1);
        }

        protected virtual void OnDataSent(byte[] buffer, int offset, int count)
        {
        }

        protected virtual void OnDataReceived(byte[] buffer, int offset, int count)
        {
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" /> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                closed = true;
                if (!leaveOpen)
                {
                    baseStream.Dispose();
                }

                var buffer = streamBuffer;
                streamBuffer = null;
                BufferPool.ReturnBuffer(buffer);
            }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead => baseStream.CanRead;

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek => baseStream.CanSeek;

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite => baseStream.CanWrite;

        /// <summary>
        /// Gets a value that determines whether the current stream can time out.
        /// </summary>
        public override bool CanTimeout => baseStream.CanTimeout;

        /// <summary>
        /// When overridden in a derived class, gets the length in bytes of the stream.
        /// </summary>
        public override long Length => baseStream.Length;

        /// <summary>
        /// Gets a value indicating whether data is available.
        /// </summary>
        public bool DataAvailable => bufferLength > 0;

        /// <summary>
        /// Gets the available data size.
        /// </summary>
        public int Available => bufferLength;

        /// <summary>
        /// When overridden in a derived class, gets or sets the position within the current stream.
        /// </summary>
        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
        /// </summary>
        public override int ReadTimeout
        {
            get => baseStream.ReadTimeout;
            set => baseStream.ReadTimeout = value;
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
        /// </summary>
        public override int WriteTimeout
        {
            get => baseStream.WriteTimeout;
            set => baseStream.WriteTimeout = value;
        }

        /// <summary>
        /// Fills the buffer.
        /// </summary>
        public bool FillBuffer()
        {
            if (closed)
            {
                return false;
            }

            if (bufferLength > 0)
            {
                //normally we fill the buffer only when it is empty, but sometimes we need more data
                //move the remanining data to the beginning of the buffer 
                Buffer.BlockCopy(streamBuffer, bufferPos, streamBuffer, 0, bufferLength);
            }

            bufferPos = 0;
            try
            {
                int readBytes = baseStream.Read(streamBuffer, bufferLength, streamBuffer.Length - bufferLength);
                bool result = readBytes > 0;
                if (result)
                {
                    OnDataReceived(streamBuffer, bufferLength, readBytes);
                    bufferLength += readBytes;
                }
                else
                {
                    closed = true;
                }

                return result;
            }
            catch
            {
                closed = true;
                return false;
            }
        }

        /// <summary>
        /// Fills the buffer asynchronous.
        /// </summary>
        /// <returns></returns>
        public Task<bool> FillBufferAsync()
        {
            return FillBufferAsync(CancellationToken.None);
        }

        /// <summary>
        /// Fills the buffer asynchronous.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<bool> FillBufferAsync(CancellationToken cancellationToken)
        {
            if (closed)
            {
                return false;
            }

            if (bufferLength > 0)
            {
                //normally we fill the buffer only when it is empty, but sometimes we need more data
                //move the remanining data to the beginning of the buffer 
                Buffer.BlockCopy(streamBuffer, bufferPos, streamBuffer, 0, bufferLength);
            }

            int bytesToRead = streamBuffer.Length - bufferLength;
            if (bytesToRead == 0)
            {
                return false;
            }

            bufferPos = 0;
            try
            {
                int readBytes = await baseStream.ReadAsync(streamBuffer, bufferLength, bytesToRead, cancellationToken);
                bool result = readBytes > 0;
                if (result)
                {
                    OnDataReceived(streamBuffer, bufferLength, readBytes);
                    bufferLength += readBytes;
                }
                else
                {
                    closed = true;
                }

                return result;
            }
            catch
            {
                closed = true;
                return false;
            }
        }
    }
}
