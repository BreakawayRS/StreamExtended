﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace StreamExtended.Network
{
    public class CustomBufferedPeekStream : IBufferedStream
    {
        private readonly CustomBufferedStream baseStream;

        internal int Position { get; private set; }

        internal CustomBufferedPeekStream(CustomBufferedStream baseStream, int startPosition = 0)
        {
            this.baseStream = baseStream;
            Position = startPosition;
        }

        /// <summary>
        /// Gets a value indicating whether data is available.
        /// </summary>
        bool IBufferedStream.DataAvailable => Available > 0;

        /// <summary>
        /// Gets the available data size.
        /// </summary>
        internal int Available => baseStream.Available - Position;

        internal async Task<bool> EnsureBufferLength(int length, CancellationToken cancellationToken)
        {
            var val = await baseStream.PeekByteAsync(Position + length - 1, cancellationToken);
            return val != -1;
        }

        internal byte ReadByte()
        {
            return baseStream.PeekByteFromBuffer(Position++);
        }

        internal int ReadInt16()
        {
            int i1 = ReadByte();
            int i2 = ReadByte();
            return (i1 << 8) + i2;
        }

        internal int ReadInt24()
        {
            int i1 = ReadByte();
            int i2 = ReadByte();
            int i3 = ReadByte();
            return (i1 << 16) + (i2 << 8) + i3;
        }

        internal byte[] ReadBytes(int length)
        {
            var buffer = new byte[length];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = ReadByte();
            }

            return buffer;
        }

        /// <summary>
        /// Fills the buffer asynchronous.
        /// </summary>
        /// <returns></returns>
        Task<bool> IBufferedStream.FillBufferAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return baseStream.FillBufferAsync(cancellationToken);
        }

        /// <summary>
        /// Reads a byte from buffer.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception">Buffer is empty</exception>
        byte IBufferedStream.ReadByteFromBuffer()
        {
            return ReadByte();
        }

        Task<int> IBufferedStream.ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }
}
