using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HEXLab.Hextrackingconnector
{
    // Frame admission and signal disposal share a gate, including callbacks already in flight at stop.
    internal sealed class RecordingSession
    {
        private readonly object gate = new object();
        private readonly Queue<QueuedFrame> frames = new Queue<QueuedFrame>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Func<SkeletonDefinition, ISkeletonRecordingWriter> createWriter;
        private readonly int maxQueuedFrames;
        private readonly SkeletonRecordingOverflowMode overflowMode;
        private readonly bool stopOnDefinitionChange;
        private readonly Action<Exception> reportFailure;
        private bool accepting = true;
        private bool disposed;
        private Exception failure;
        private string stopReason;
        private int writtenFrameCount;
        private int droppedFrameCount;

        internal RecordingSession(Func<SkeletonDefinition, ISkeletonRecordingWriter> createWriter,
            int maxQueuedFrames, SkeletonRecordingOverflowMode overflowMode, bool stopOnDefinitionChange,
            Action<Exception> reportFailure = null)
        {
            this.createWriter = createWriter;
            this.maxQueuedFrames = maxQueuedFrames;
            this.overflowMode = overflowMode;
            this.stopOnDefinitionChange = stopOnDefinitionChange;
            this.reportFailure = reportFailure;
        }

        internal Task Completion { get; private set; } = Task.CompletedTask;
        internal bool IsRecording { get { lock (gate) return accepting; } }
        internal int QueuedFrameCount { get { lock (gate) return frames.Count; } }
        internal int WrittenFrameCount => Volatile.Read(ref writtenFrameCount);
        internal int DroppedFrameCount => Volatile.Read(ref droppedFrameCount);
        internal double Duration { get { lock (gate) return clock.Elapsed.TotalSeconds; } }
        internal Exception Failure { get { lock (gate) return failure; } }
        internal string StopReason { get { lock (gate) return stopReason; } }

        internal void Start()
        {
            Completion = Task.Factory.StartNew(RunWriter, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        internal bool TryEnqueue(SkeletonFrame frame)
        {
            lock (gate)
            {
                if (!accepting) return false;
                if (frames.Count >= maxQueuedFrames)
                {
                    Interlocked.Increment(ref droppedFrameCount);
                    if (overflowMode == SkeletonRecordingOverflowMode.StopRecordingAndWarn)
                        StopUnderLock("SkeletonPoseRecorder stopped because its recording queue filled up.");
                    return false;
                }

                frames.Enqueue(new QueuedFrame(frame, clock.Elapsed.TotalSeconds));
                signal.Set();
                return true;
            }
        }

        internal void RequestStop()
        {
            lock (gate) StopUnderLock(null);
        }

        private void StopUnderLock(string reason)
        {
            accepting = false;
            clock.Stop();
            if (stopReason == null) stopReason = reason;
            if (!disposed) signal.Set();
        }

        private void RunWriter()
        {
            ISkeletonRecordingWriter writer = null;
            SkeletonDefinition definition = null;
            try
            {
                while (true)
                {
                    QueuedFrame queued = default;
                    bool hasFrame;
                    lock (gate)
                    {
                        hasFrame = frames.Count > 0;
                        if (hasFrame) queued = frames.Dequeue();
                        else if (!accepting) break;
                    }

                    if (!hasFrame)
                    {
                        signal.WaitOne();
                        continue;
                    }

                    if (writer == null)
                    {
                        definition = queued.Frame.Definition;
                        writer = createWriter(definition);
                    }
                    else if (stopOnDefinitionChange && !string.Equals(definition.Id,
                                 queued.Frame.Definition.Id, StringComparison.Ordinal))
                    {
                        lock (gate) StopUnderLock($"SkeletonPoseRecorder stopped because source definition changed from '{definition.Id}' to '{queued.Frame.Definition.Id}'.");
                        continue;
                    }

                    writer.WriteFrame(new SkeletonRecordedFrame(writtenFrameCount, queued.RecordedTime, queued.Frame));
                    Interlocked.Increment(ref writtenFrameCount);
                }
            }
            catch (Exception exception) { RecordFailure(exception); }
            finally
            {
                try { writer?.Dispose(); }
                catch (Exception exception) { RecordFailure(exception); }
                lock (gate)
                {
                    StopUnderLock(null);
                    frames.Clear();
                    disposed = true;
                    signal.Dispose();
                }
                // Reporting also belongs to the session: its component may already be destroyed.
                // The sink must be thread-safe and must not depend on a Unity scene object.
                if (failure != null && reportFailure != null)
                {
                    try { reportFailure(failure); }
                    catch (Exception exception) { RecordFailure(exception); }
                }
            }
        }

        private void RecordFailure(Exception exception)
        {
            lock (gate)
            {
                failure = failure == null ? exception : new AggregateException(failure, exception);
                StopUnderLock(null);
            }
        }

        private readonly struct QueuedFrame
        {
            internal QueuedFrame(SkeletonFrame frame, double recordedTime)
            {
                Frame = frame;
                RecordedTime = recordedTime;
            }
            internal SkeletonFrame Frame { get; }
            internal double RecordedTime { get; }
        }
    }
}
