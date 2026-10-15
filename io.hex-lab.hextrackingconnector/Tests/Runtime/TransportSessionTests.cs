using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HEXLab.Hextrackingconnector.Tests
{
    public class TransportSessionTests
    {
        [TestCase(0)] // waiting for a header
        [TestCase(2)] // incomplete header
        [TestCase(6)] // incomplete body
        public void PipeReadCanBeCancelledAtEveryPacketBoundary(int availableBytes)
        {
            var packet = Packet("payload");
            using (var stream = new IncompleteStream(packet, availableBytes))
            using (var cancellation = new CancellationTokenSource())
            {
                var delivered = 0;
                var read = TransportSession.ReadPipeFramesAsync(stream, _ => delivered++, cancellation.Token);
                Assert.IsTrue(stream.Waiting.WaitOne(2000));
                cancellation.Cancel();
                Assert.IsTrue(SpinWait.SpinUntil(() => read.IsCompleted, 2000));
                Assert.Catch<OperationCanceledException>(() => read.GetAwaiter().GetResult());
                Assert.AreEqual(0, delivered);
            }
        }

        [Test]
        public void FragmentedPipeReadsDeliverCompleteFramesAndRejectTruncatedPayload()
        {
            var first = Packet("first");
            var second = Packet("second");
            var bytes = new byte[first.Length + second.Length];
            Array.Copy(first, bytes, first.Length);
            Array.Copy(second, 0, bytes, first.Length, second.Length);
            var delivered = new System.Collections.Generic.List<string>();
            using (var stream = new FragmentedStream(bytes))
                TransportSession.ReadPipeFramesAsync(stream, delivered.Add, CancellationToken.None).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "first", "second" }, delivered);

            using (var truncated = new FragmentedStream(new byte[] { 3, 0, 0, 0, 65 }))
                Assert.Throws<EndOfStreamException>(() =>
                    TransportSession.ReadPipeFramesAsync(truncated, delivered.Add, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(2, delivered.Count);
        }

        [Test]
        public void IdleUdpSessionStopsAndReleasesItsPortForAnotherSession()
        {
            var port = AvailablePort();
            for (var i = 0; i < 3; i++)
            {
                using (var ready = new ManualResetEvent(false))
                {
                    var errors = new ConcurrentQueue<Exception>();
                    var session = new TransportSession(TransportMode.Udp, null, port, (_, __) => { },
                        _ => ready.Set(), errors.Enqueue);
                    session.Start();
                    try { Assert.IsTrue(ready.WaitOne(2000), "UDP did not bind."); }
                    finally { session.RequestStop(); }
                    Assert.IsTrue(session.Completion.Wait(2000), "Idle receive did not stop.");
                    session.RequestStop();
                    Assert.IsEmpty(errors);
                    using (new UdpClient(port)) { }
                }
            }
        }

        [TestCase(-1)] // no client
        [TestCase(0)]  // connected but silent
        [TestCase(2)]  // incomplete header
        [TestCase(6)]  // incomplete body
        public void WindowsNamedPipeSessionStopsWithoutFurtherClientData(int sentBytes)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                Assert.Ignore("Windows named-pipe integration test.");
            var name = "hex-test-" + Guid.NewGuid().ToString("N");
            var errors = new ConcurrentQueue<Exception>();
            using (var ready = new ManualResetEvent(false))
            using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut))
            {
                var session = new TransportSession(TransportMode.Pipe, name, 0, (_, __) => { },
                    _ => ready.Set(), errors.Enqueue);
                session.Start();
                try
                {
                    Assert.IsTrue(ready.WaitOne(2000));
                    if (sentBytes >= 0)
                    {
                        client.Connect(2000);
                        if (sentBytes > 0) client.Write(Packet("payload"), 0, sentBytes);
                    }
                }
                finally { session.RequestStop(); }
                Assert.IsTrue(session.Completion.Wait(2000), "Pipe I/O prevented shutdown.");
                Assert.IsEmpty(errors);
            }
        }

        [UnityTest]
        public IEnumerator ReenableWaitsForAnInFlightCaptureAndRejectsItsRetiredPose()
        {
            var gameObject = new GameObject("DeferredTransportRestart");
            gameObject.SetActive(false);
            var server = gameObject.AddComponent<CommServer>();
            var port = AvailablePort();
            SetField(server, "transportMode", TransportMode.Udp);
            SetField(server, "udpPort", port);
            SetField(server, "logConnectionEvents", false);
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var sender = new UdpClient())
            {
                server.FrameCaptured += _ =>
                {
                    entered.Set();
                    if (!release.WaitOne(5000)) throw new TimeoutException("Test capture was not released.");
                };
                try
                {
                    gameObject.SetActive(true);
                    var first = server.Session;
                    var packet = Packet("{\"free\":[{\"index\":0,\"x\":1,\"y\":1,\"z\":2}]}");
                    var deadline = Time.realtimeSinceStartup + 3f;
                    while (!entered.WaitOne(0) && Time.realtimeSinceStartup < deadline)
                    {
                        sender.Send(packet, packet.Length, IPAddress.Loopback.ToString(), port);
                        yield return null;
                    }
                    Assert.IsTrue(entered.WaitOne(0));
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("transport is still stopping"));
                    server.enabled = false;
                    server.enabled = true;
                    Assert.AreSame(first, server.Session, "Restart overlapped an unfinished session.");
                    Assert.IsFalse(server.IsRunning);
                    release.Set();
                    deadline = Time.realtimeSinceStartup + 3f;
                    while (ReferenceEquals(first, server.Session) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.IsTrue(first.Completion.IsCompleted);
                    Assert.AreNotSame(first, server.Session);
                    Assert.IsFalse(server.TryGetLatestPose(out _));
                    Assert.IsFalse(server.TryGetLatestCapturedFrame(out _));
                    Assert.AreEqual(0, server.PendingFrameCount);
                    yield return SendUntilPose(sender, server, port, 2);
                }
                finally
                {
                    release.Set();
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator ComponentCanDisableReenableAndDestroyWithoutRetainingTransportOrPose()
        {
            var gameObject = new GameObject("TransportLifecycle");
            gameObject.SetActive(false);
            var server = gameObject.AddComponent<CommServer>();
            var port = AvailablePort();
            SetField(server, "transportMode", TransportMode.Udp);
            SetField(server, "udpPort", port);
            SetField(server, "logConnectionEvents", false);
            using (var sender = new UdpClient())
            {
                try
                {
                    gameObject.SetActive(true);
                    var first = server.Session;
                    yield return SendUntilPose(sender, server, port, 1);
                    server.enabled = false;
                    Assert.IsTrue(first.Completion.IsCompleted);
                    Assert.IsFalse(server.IsRunning);
                    Assert.IsFalse(server.TryGetLatestPose(out _));
                    Assert.IsFalse(server.TryGetLatestCapturedFrame(out _));
                    Assert.AreEqual(0, server.PendingFrameCount);

                    server.enabled = true;
                    var second = server.Session;
                    Assert.AreNotSame(first, second);
                    yield return null;
                    Assert.IsFalse(server.TryGetLatestPose(out _), "A retired run published a stale frame.");
                    yield return SendUntilPose(sender, server, port, 2);
                    UnityEngine.Object.Destroy(gameObject);
                    yield return null;
                    Assert.IsTrue(second.Completion.IsCompleted);
                    using (new UdpClient(port)) { }
                }
                finally
                {
                    if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        private static IEnumerator SendUntilPose(UdpClient sender, CommServer server, int port, int x)
        {
            var packet = Packet("{\"free\":[{\"index\":0,\"x\":" + x + ",\"y\":1,\"z\":2}]}");
            var deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                sender.Send(packet, packet.Length, IPAddress.Loopback.ToString(), port);
                yield return null;
                if (server.TryGetLatestPose(out var pose) && pose.TryGetJoint(BodyJoints.Nose, out var nose) && nose.x == x)
                    yield break;
            }
            Assert.Fail("The active transport did not publish its pose.");
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
        }

        private static int AvailablePort()
        {
            using (var socket = new UdpClient(0)) return ((IPEndPoint)socket.Client.LocalEndPoint).Port;
        }

        private static byte[] Packet(string json)
        {
            var data = Encoding.UTF8.GetBytes(json);
            var packet = new byte[data.Length + 4];
            Array.Copy(BitConverter.GetBytes(data.Length), packet, 4);
            Array.Copy(data, 0, packet, 4, data.Length);
            return packet;
        }

        private class FragmentedStream : MemoryStream
        {
            internal FragmentedStream(byte[] data) : base(data) { }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
                => base.ReadAsync(buffer, offset, Math.Min(2, count), token);
        }

        private sealed class IncompleteStream : FragmentedStream
        {
            internal readonly ManualResetEvent Waiting = new ManualResetEvent(false);
            internal IncompleteStream(byte[] packet, int length) : base(CopyPrefix(packet, length)) { }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                if (Position < Length) return await base.ReadAsync(buffer, offset, count, token).ConfigureAwait(false);
                Waiting.Set();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return 0;
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) Waiting.Dispose();
                base.Dispose(disposing);
            }
            private static byte[] CopyPrefix(byte[] data, int length)
            {
                var prefix = new byte[length]; Array.Copy(data, prefix, length); return prefix;
            }
        }
    }
}
