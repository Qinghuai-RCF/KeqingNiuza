﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    internal partial class HttpConnection : IDisposable
    {
        private sealed class ChunkedEncodingWriteStream : HttpContentWriteStream
        {
            private static readonly byte[] s_finalChunkBytes = { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

            public ChunkedEncodingWriteStream(HttpConnection connection) : base(connection)
            {
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ignored)
            {
                return WriteAsyncInternal(new ReadOnlyMemory<byte>(buffer, offset, count), ignored);
            }

            private Task WriteAsyncInternal(ReadOnlyMemory<byte> buffer, CancellationToken ignored)
            {
                Debug.Assert(_connection._currentRequest != null);

                // The token is ignored because it's coming from SendAsync and the only operations
                // here are those that are already covered by the token having been registered with
                // to close the connection.

                Task task = buffer.Length == 0 ?
                    // Don't write if nothing was given, especially since we don't want to accidentally send a 0 chunk,
                    // which would indicate end of body.  Instead, just ensure no content is stuck in the buffer.
                    _connection.FlushAsync() :
                    WriteChunkAsync(buffer);

                return task;
            }

            private async Task WriteChunkAsync(ReadOnlyMemory<byte> buffer)
            {
                // Write chunk length in hex followed by \r\n
                await _connection.WriteHexInt32Async(buffer.Length).ConfigureAwait(false);
                await _connection.WriteTwoBytesAsync((byte)'\r', (byte)'\n').ConfigureAwait(false);

                // Write chunk contents followed by \r\n
                await _connection.WriteAsync(buffer).ConfigureAwait(false);
                await _connection.WriteTwoBytesAsync((byte)'\r', (byte)'\n').ConfigureAwait(false);
            }
            
            public override async Task FinishAsync()
            {
                // Send 0 byte chunk to indicate end, then final CrLf
                await _connection.WriteBytesAsync(s_finalChunkBytes).ConfigureAwait(false);
                _connection = null;
            }
        }
    }
}
