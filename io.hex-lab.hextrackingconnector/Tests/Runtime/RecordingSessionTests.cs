using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HEXLab.Hextrackingconnector.Tests
{
    public class RecordingSessionTests
    {
        [Test]
        public void StopBeforeAnyFramesCompletesWithoutOpeningAWriter()
        {
            var session = new RecordingSession(_ => throw new Exception("No writer should be opened."),
                10, SkeletonRecordingOverflowMode.StopRecordingAndWarn, true);
            session.Start();
            session.RequestStop();
            Assert.IsTrue(session.Completion.Wait(2000));
            Assert.IsNull(session.Failure);
            Assert.IsFalse(session.IsRecording);
        }

        [Test]
        public void WriterCreationFailureCompletesAndClosesFrameAdmission()
        {
            var failure = new IOException("opening writer failed");
            var session = new RecordingSession(_ => throw failure,
                10, SkeletonRecordingOverflowMode.StopRecordingAndWarn, true);
            session.Start();
            Assert.IsTrue(session.TryEnqueue(SessionLifecycleTests.CreateFrame(1)));
            Assert.IsTrue(session.Completion.Wait(2000));
            Assert.AreSame(failure, session.Failure);
            Assert.IsFalse(session.IsRecording);
            Assert.IsFalse(session.TryEnqueue(SessionLifecycleTests.CreateFrame(2)));
        }

        [Test]
        public void StopDrainsAcceptedFramesAndRejectsLateCallbacks()
        {
            var writer = new TestWriter();
            var session = CreateSession(writer);
            session.Start();
            for (var i = 0; i < 50; i++) Assert.IsTrue(session.TryEnqueue(SessionLifecycleTests.CreateFrame(i)));
            session.RequestStop();
            Assert.IsTrue(session.Completion.Wait(2000));
            Assert.IsFalse(session.TryEnqueue(SessionLifecycleTests.CreateFrame(99)));
            session.RequestStop();
            Assert.IsFalse(session.IsRecording);
            Assert.IsNull(session.Failure);
            Assert.AreEqual(50, session.WrittenFrameCount);
            Assert.AreEqual(0, session.QueuedFrameCount);
            Assert.AreEqual(1, writer.DisposeCount);
            CollectionAssert.AreEqual(System.Linq.Enumerable.Range(0, 50), writer.Sequences);
        }

        [Test]
        public void ConcurrentCaptureAndStopNeverLoseAnAcceptedFrameOrUseDisposedSignal()
        {
            var writer = new TestWriter();
            var session = CreateSession(writer);
            var frame = SessionLifecycleTests.CreateFrame(1);
            var accepted = 0;
            using (var capturing = new ManualResetEvent(false))
            {
                session.Start();
                var producer = Task.Run(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        if (session.TryEnqueue(frame)) Interlocked.Increment(ref accepted);
                        if (i == 10) capturing.Set();
                    }
                });
                Assert.IsTrue(capturing.WaitOne(2000));
                session.RequestStop();
                Assert.IsTrue(producer.Wait(2000));
                Assert.IsTrue(session.Completion.Wait(2000));
                Assert.AreEqual(accepted, session.WrittenFrameCount);
                Assert.IsNull(session.Failure);
                Assert.IsFalse(session.TryEnqueue(frame));
            }
        }

        [Test]
        public void WriteAndFinalizationFailuresAreBothObservedWithoutFaultingCompletion()
        {
            var writer = new TestWriter { FailWrite = true, FailDispose = true };
            var session = CreateSession(writer);
            session.Start();
            session.TryEnqueue(SessionLifecycleTests.CreateFrame(1));
            Assert.IsTrue(session.Completion.Wait(2000));
            Assert.IsFalse(session.IsRecording);
            Assert.IsInstanceOf<AggregateException>(session.Failure);
            var errors = ((AggregateException)session.Failure).Flatten().InnerExceptions;
            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("write failed", errors[0].Message);
            StringAssert.Contains("close failed", errors[1].Message);
            Assert.AreEqual(1, writer.DisposeCount);
        }

        [TestCase(SkeletonRecordingFormat.Binary)]
        [TestCase(SkeletonRecordingFormat.JsonLines)]
        public void WritersCloseTheirStreamEvenWhenFinalizationFails(SkeletonRecordingFormat format)
        {
            var stream = new FailingStream();
            ISkeletonRecordingWriter writer = format == SkeletonRecordingFormat.Binary
                ? (ISkeletonRecordingWriter)new BinarySkeletonRecordingWriter(stream, HumanPoseSkeleton33.Definition)
                : new JsonLinesSkeletonRecordingWriter(stream, HumanPoseSkeleton33.Definition);
            stream.FailWrites = true;
            var failure = Assert.Catch(() => writer.Dispose());
            var errors = failure is AggregateException aggregate
                ? aggregate.Flatten().InnerExceptions
                : new System.Collections.ObjectModel.ReadOnlyCollection<Exception>(new[] { failure });
            foreach (var error in errors) Assert.IsInstanceOf<IOException>(error);
            Assert.IsTrue(stream.WasDisposed);
            Assert.DoesNotThrow(() => writer.Dispose(), "Finalization must be idempotent even after an error.");
        }

        [Test]
        public void RecorderReportsLateFinalizationFailureAfterItsComponentIsDestroyed()
        {
            var gameObject = new GameObject("LateRecordingFailure");
            gameObject.SetActive(false);
            var source = gameObject.AddComponent<LifecycleTestProvider>();
            var recorder = gameObject.AddComponent<SkeletonPoseRecorder>();
            typeof(SkeletonPoseRecorder).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recorder, source);
            var folder = Path.Combine(Path.GetTempPath(), "hex-session-" + Guid.NewGuid().ToString("N"));
            recorder.SetRecordingFolder(folder);
            RecordingSession session = null;
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            {
                recorder.WriterFactory = (_, __, ___) => new TestWriter
                {
                    Entered = entered, Release = release, FailDispose = true,
                };
                try
                {
                    gameObject.SetActive(true);
                    Assert.IsTrue(recorder.StartRecording());
                    session = recorder.Session;
                    source.Publish(SessionLifecycleTests.CreateFrame(1));
                    Assert.IsTrue(entered.WaitOne(2000));
                    LogAssert.Expect(LogType.Warning, new Regex("writer is still finishing"));
                    UnityEngine.Object.DestroyImmediate(gameObject);
                    LogAssert.Expect(LogType.Exception, new Regex("IOException: close failed"));
                    release.Set();
                    Assert.IsTrue(session.Completion.Wait(2000));
                }
                finally
                {
                    release.Set();
                    session?.Completion.Wait(2000);
                    if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                }
            }
        }

        [Test]
        public void DisabledRecorderFinishesAndRestartsWithoutUpdateOrOldCallbacks()
        {
            var gameObject = new GameObject("RecorderLifecycle");
            gameObject.SetActive(false);
            var source = gameObject.AddComponent<LifecycleTestProvider>();
            var recorder = gameObject.AddComponent<SkeletonPoseRecorder>();
            typeof(SkeletonPoseRecorder).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recorder, source);
            var folder = Path.Combine(Path.GetTempPath(), "hex-session-" + Guid.NewGuid().ToString("N"));
            recorder.SetRecordingFolder(folder);
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            {
                var firstWriter = new TestWriter { Entered = entered, Release = release };
                var secondWriter = new TestWriter();
                var calls = 0;
                recorder.WriterFactory = (_, __, ___) => calls++ == 0 ? firstWriter : secondWriter;
                try
                {
                    gameObject.SetActive(true);
                    Assert.IsTrue(recorder.StartRecording());
                    var oldCallback = source.SnapshotHandlers();
                    source.Publish(SessionLifecycleTests.CreateFrame(1));
                    Assert.IsTrue(entered.WaitOne(2000));
                    LogAssert.Expect(LogType.Warning, new Regex("writer is still finishing"));
                    recorder.enabled = false;
                    Assert.IsTrue(recorder.IsStopping);
                    Assert.IsFalse(recorder.StartRecording(), "An unfinished session must not overlap its replacement.");
                    var stoppedSession = recorder.Session;
                    release.Set();
                    Assert.IsTrue(stoppedSession.Completion.Wait(2000));
                    Assert.AreEqual(1, firstWriter.DisposeCount);
                    Assert.IsFalse(recorder.IsStopping);

                    recorder.enabled = true;
                    Assert.IsTrue(recorder.StartRecording(), "No Update ran to clear the completed session.");
                    oldCallback(SessionLifecycleTests.CreateFrame(99));
                    source.Publish(SessionLifecycleTests.CreateFrame(2));
                    recorder.StopRecording();
                    CollectionAssert.AreEqual(new[] { 2 }, secondWriter.Sequences);
                }
                finally
                {
                    release.Set();
                    recorder.StopRecording();
                    UnityEngine.Object.DestroyImmediate(gameObject);
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                }
            }
        }

        private static RecordingSession CreateSession(TestWriter writer)
            => new RecordingSession(_ => writer, 10000, SkeletonRecordingOverflowMode.StopRecordingAndWarn, true);

        private sealed class TestWriter : ISkeletonRecordingWriter
        {
            internal readonly List<int> Sequences = new List<int>();
            internal ManualResetEvent Entered;
            internal ManualResetEvent Release;
            internal bool FailWrite;
            internal bool FailDispose;
            internal int DisposeCount;
            public int FrameCount => Sequences.Count;
            public double Duration => 0;
            public void WriteFrame(SkeletonRecordedFrame frame)
            {
                Entered?.Set();
                if (Release != null && !Release.WaitOne(5000)) throw new TimeoutException("Test writer was not released.");
                if (FailWrite) throw new IOException("write failed");
                Sequences.Add(frame.Frame.SequenceNumber);
            }
            public void Dispose()
            {
                DisposeCount++;
                if (FailDispose) throw new IOException("close failed");
            }
        }

        private sealed class FailingStream : MemoryStream
        {
            internal bool FailWrites;
            internal bool WasDisposed;
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (FailWrites) throw new IOException("write failed");
                base.Write(buffer, offset, count);
            }
            public override void WriteByte(byte value)
            {
                if (FailWrites) throw new IOException("write failed");
                base.WriteByte(value);
            }
            protected override void Dispose(bool disposing)
            {
                WasDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
