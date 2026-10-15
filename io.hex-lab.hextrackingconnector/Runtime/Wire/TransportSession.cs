using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HEXLab.Hextrackingconnector
{
    // One run owns its I/O and cancellation. It never waits for Unity's synchronization context.
    internal sealed class TransportSession
    {
        internal const int MaxPayloadBytes = 1024 * 1024;
        private readonly object gate = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CancellationToken token;
        private readonly TransportMode mode;
        private readonly string pipeName;
        private readonly int udpPort;
        private readonly Action<TransportSession, string> receive;
        private readonly Action<string> log;
        private readonly Action<Exception> reportError;
        private IDisposable activeTransport;
        private bool stopping;

        internal TransportSession(TransportMode mode, string pipeName, int udpPort,
            Action<TransportSession, string> receive, Action<string> log, Action<Exception> reportError)
        {
            this.mode = mode;
            this.pipeName = pipeName;
            this.udpPort = udpPort;
            this.receive = receive;
            this.log = log;
            this.reportError = reportError;
            token = cancellation.Token;
        }

        internal Task Completion { get; private set; } = Task.CompletedTask;
        internal bool IsStopping => token.IsCancellationRequested;

        internal void Start()
        {
            Completion = Task.Run(RunAsync);
        }

        internal void RequestStop()
        {
            IDisposable transport;
            lock (gate)
            {
                if (stopping) return;
                stopping = true;
                cancellation.Cancel();
                transport = activeTransport;
                activeTransport = null;
            }

            // In particular, closing UDP interrupts ReceiveAsync on Unity's .NET profile.
            Close(transport);
        }

        private async Task RunAsync()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (mode == TransportMode.Pipe)
                            await RunPipeAsync().ConfigureAwait(false);
                        else
                            await RunUdpAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception) when (token.IsCancellationRequested &&
                        (exception is OperationCanceledException || exception is ObjectDisposedException ||
                         exception is IOException || exception is SocketException))
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        reportError(exception);
                    }

                    if (!token.IsCancellationRequested)
                    {
                        log("Client disconnected. Waiting for reconnection...");
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally
            {
                RequestStop();
                lock (gate) cancellation.Dispose();
            }
        }

        private async Task RunPipeAsync()
        {
            using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                       PipeTransmissionMode.Message, PipeOptions.Asynchronous))
            {
                if (!Activate(server)) return;
                try
                {
                    log($"Waiting for pipe connection on '{pipeName}'...");
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    log("Connected via named pipe.");
                    await ReadPipeFramesAsync(server, json => receive(this, json), token).ConfigureAwait(false);
                }
                finally { Retire(server); }
            }
        }

        private async Task RunUdpAsync()
        {
            using (var server = new UdpClient(udpPort))
            {
                if (!Activate(server)) return;
                try
                {
                    log($"Waiting for Python UDP packets on port {udpPort}...");
                    while (!token.IsCancellationRequested)
                    {
                        var packet = await server.ReceiveAsync().ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        var data = packet.Buffer;
                        if (data.Length < sizeof(int)) continue;
                        var length = ReadLength(data);
                        if (length <= 0 || length > MaxPayloadBytes || data.Length - sizeof(int) < length)
                        {
                            reportError(new InvalidDataException($"Skipping invalid UDP pose packet from {packet.RemoteEndPoint}."));
                            continue;
                        }

                        receive(this, Encoding.UTF8.GetString(data, sizeof(int), length));
                    }
                }
                finally { Retire(server); }
            }
        }

        internal static async Task ReadPipeFramesAsync(Stream stream, Action<string> receive, CancellationToken token)
        {
            var header = new byte[sizeof(int)];
            while (await ReadExactlyAsync(stream, header, allowEof: true, token).ConfigureAwait(false))
            {
                var length = ReadLength(header);
                if (length <= 0 || length > MaxPayloadBytes)
                    throw new InvalidDataException($"Invalid pose payload length: {length}.");

                var payload = new byte[length];
                await ReadExactlyAsync(stream, payload, allowEof: false, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                receive(Encoding.UTF8.GetString(payload));
            }
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, bool allowEof, CancellationToken token)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token).ConfigureAwait(false);
                if (count == 0)
                {
                    if (allowEof && offset == 0) return false;
                    throw new EndOfStreamException("Python disconnected during a pose packet.");
                }
                offset += count;
            }
            return true;
        }

        private static int ReadLength(byte[] data)
        {
            return data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24;
        }

        private bool Activate(IDisposable transport)
        {
            lock (gate)
            {
                if (stopping) return false;
                activeTransport = transport;
                return true;
            }
        }

        private void Retire(IDisposable transport)
        {
            lock (gate)
            {
                if (ReferenceEquals(activeTransport, transport)) activeTransport = null;
            }
        }

        private void Close(IDisposable transport)
        {
            try { transport?.Dispose(); }
            catch (Exception exception) { reportError(exception); }
        }
    }
}
