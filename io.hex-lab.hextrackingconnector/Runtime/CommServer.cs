using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace HEXLab.Hextrackingconnector
{
    public enum TransportMode
    {
        Pipe,
        Udp,
    }

    public class CommServer : MonoBehaviour
    {
        public const int JOINT_COUNT = SkeletonFrame.JointCount;

        private const int MaxPayloadBytes = 1024 * 1024;
        private const string DefaultPipeName = "UnityMediaPipeBody";

        [SerializeField] private TransportMode transportMode = TransportMode.Pipe;
        [SerializeField] private string pipeName = DefaultPipeName;
        [SerializeField, Min(1)] private int udpPort = 5000;
        [SerializeField] private PoseCoordinateSource coordinateSource = PoseCoordinateSource.Free;
        [SerializeField] private PoseMirrorMode mirrorMode = PoseMirrorMode.None;
        [SerializeField] private PoseSmoothingMode smoothingMode = PoseSmoothingMode.None;
        [SerializeField, Min(1)] private int movingAverageWindowSize = 5;
        [SerializeField] private bool logConnectionEvents = true;

        private readonly ConcurrentQueue<SkeletonFrame> pendingFrames = new ConcurrentQueue<SkeletonFrame>();
        private readonly object transportLock = new object();

        private Thread transportThread;
        private NamedPipeServerStream pipeServer;
        private UdpClient udpServer;
        private volatile bool isRunning;
        private int sequenceNumber;

        private IPoseSmoother poseSmoother;
        private PoseSmoothingMode activeSmoothingMode;
        private int activeMovingAverageWindowSize;
        private SkeletonFrame latestPose;
        private bool hasLatestPose;

        public event Action<SkeletonFrame> PoseReceived;

        public TransportMode CurrentTransportMode => transportMode;

        public PoseCoordinateSource CoordinateSource
        {
            get => coordinateSource;
            set => coordinateSource = value;
        }

        public PoseMirrorMode MirrorMode
        {
            get => mirrorMode;
            set => mirrorMode = value;
        }

        public PoseSmoothingMode SmoothingMode
        {
            get => smoothingMode;
            set
            {
                if (smoothingMode == value)
                {
                    return;
                }

                smoothingMode = value;
                ResetSmoother();
            }
        }

        public int MovingAverageWindowSize
        {
            get => movingAverageWindowSize;
            set
            {
                movingAverageWindowSize = Mathf.Max(1, value);
                ResetSmoother();
            }
        }

        public bool IsRunning => isRunning;
        public int PendingFrameCount => pendingFrames.Count;

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        public void ResetSmoother()
        {
            EnsureSmoother(forceReset: true);
        }

        private void Start()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            EnsureSmoother(forceReset: true);
            StartTransport();
        }

        private void Update()
        {
            EnsureSmoother(forceReset: false);
            PublishPendingFrames();
        }

        private void OnDisable()
        {
            StopTransport();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(pipeName))
            {
                pipeName = DefaultPipeName;
            }

            udpPort = Mathf.Max(1, udpPort);
            movingAverageWindowSize = Mathf.Max(1, movingAverageWindowSize);
        }

        private void StartTransport()
        {
            if (isRunning)
            {
                return;
            }

            DrainPendingFrames();
            isRunning = true;
            transportThread = new Thread(RunTransportLoop)
            {
                IsBackground = true,
                Name = "HEX Tracking Connector Transport",
            };
            transportThread.Start();
        }

        private void StopTransport()
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            CloseActiveTransport();

            if (transportThread != null && transportThread.IsAlive)
            {
                transportThread.Join(250);
            }

            transportThread = null;
        }

        private void DrainPendingFrames()
        {
            while (pendingFrames.TryDequeue(out _))
            {
            }
        }

        private void PublishPendingFrames()
        {
            var hasPoseToPublish = false;
            var poseToPublish = default(SkeletonFrame);

            while (pendingFrames.TryDequeue(out var rawPose))
            {
                poseToPublish = poseSmoother.Smooth(rawPose);
                hasPoseToPublish = true;
            }

            if (!hasPoseToPublish)
            {
                return;
            }

            latestPose = poseToPublish;
            hasLatestPose = true;
            RaisePoseReceived(poseToPublish);
        }

        private void RaisePoseReceived(SkeletonFrame pose)
        {
            var handlers = PoseReceived;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<SkeletonFrame> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(pose);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void EnsureSmoother(bool forceReset)
        {
            if (!forceReset &&
                poseSmoother != null &&
                activeSmoothingMode == smoothingMode &&
                activeMovingAverageWindowSize == movingAverageWindowSize)
            {
                return;
            }

            poseSmoother = PoseSmootherFactory.Create(smoothingMode, movingAverageWindowSize);
            activeSmoothingMode = smoothingMode;
            activeMovingAverageWindowSize = movingAverageWindowSize;
        }

        private void RunTransportLoop()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            while (isRunning)
            {
                try
                {
                    if (transportMode == TransportMode.Pipe)
                    {
                        RunPipeLoop();
                    }
                    else
                    {
                        RunUdpLoop();
                    }
                }
                catch (ObjectDisposedException) when (!isRunning)
                {
                }
                catch (SocketException) when (!isRunning)
                {
                }
                catch (IOException) when (!isRunning)
                {
                }
                catch (Exception exception)
                {
                    if (isRunning)
                    {
                        Debug.LogWarning($"HEX tracking transport error: {exception.Message}");
                    }
                }

                if (isRunning)
                {
                    LogConnection("Client disconnected. Waiting for reconnection...");
                    Thread.Sleep(100);
                }
            }
        }

        private void RunPipeLoop()
        {
            using (var server = new NamedPipeServerStream(
                       pipeName,
                       PipeDirection.InOut,
                       1,
                       PipeTransmissionMode.Message))
            {
                lock (transportLock)
                {
                    pipeServer = server;
                }

                LogConnection($"Waiting for pipe connection on '{pipeName}'...");
                server.WaitForConnection();
                LogConnection("Connected via named pipe.");

                using (var reader = new BinaryReader(server, Encoding.UTF8))
                {
                    while (isRunning && server.IsConnected)
                    {
                        var payloadLength = checked((int)reader.ReadUInt32());
                        if (payloadLength <= 0 || payloadLength > MaxPayloadBytes)
                        {
                            throw new InvalidDataException($"Invalid pose payload length: {payloadLength}.");
                        }

                        var payload = reader.ReadBytes(payloadLength);
                        if (payload.Length != payloadLength)
                        {
                            break;
                        }

                        ParseAndEnqueue(Encoding.UTF8.GetString(payload));
                    }
                }
            }

            lock (transportLock)
            {
                pipeServer = null;
            }
        }

        private void RunUdpLoop()
        {
            using (var server = new UdpClient(udpPort))
            {
                lock (transportLock)
                {
                    udpServer = server;
                }

                var remote = new IPEndPoint(IPAddress.Any, 0);
                LogConnection($"Waiting for Python UDP packets on port {udpPort}...");

                while (isRunning)
                {
                    var datagram = server.Receive(ref remote);
                    if (datagram.Length < sizeof(int))
                    {
                        continue;
                    }

                    var payloadLength = BitConverter.ToInt32(datagram, 0);
                    if (payloadLength <= 0 ||
                        payloadLength > MaxPayloadBytes ||
                        datagram.Length < sizeof(int) + payloadLength)
                    {
                        Debug.LogWarning($"Skipping invalid UDP pose packet from {remote}.");
                        continue;
                    }

                    ParseAndEnqueue(Encoding.UTF8.GetString(datagram, sizeof(int), payloadLength));
                }
            }

            lock (transportLock)
            {
                udpServer = null;
            }
        }

        private void ParseAndEnqueue(string json)
        {
            if (TryParseSkeletonFrame(json, out var frame))
            {
                pendingFrames.Enqueue(frame);
            }
        }

        private bool TryParseSkeletonFrame(string json, out SkeletonFrame skeletonFrame)
        {
            skeletonFrame = default;

            PoseFrame frame;
            try
            {
                frame = JsonUtility.FromJson<PoseFrame>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to parse PoseFrame JSON. Skipping frame. {exception.Message}");
                return false;
            }

            if (frame == null)
            {
                return false;
            }

            var landmarks = frame.GetLandmarks(coordinateSource);
            if (landmarks == null || landmarks.Length == 0)
            {
                return false;
            }

            var positions = new Vector3[SkeletonFrame.JointCount];
            var tracked = new bool[SkeletonFrame.JointCount];
            var foundAny = false;

            foreach (var landmarkData in landmarks)
            {
                if (landmarkData == null ||
                    !MediaPipePoseLandmarkDefinition.TryMapIndex(landmarkData.index, mirrorMode, out var joint))
                {
                    continue;
                }

                var mappedIndex = (int)joint;
                positions[mappedIndex] = landmarkData.ToVector3();
                tracked[mappedIndex] = true;
                foundAny = true;
            }

            if (!foundAny)
            {
                return false;
            }

            skeletonFrame = new SkeletonFrame(
                HumanPoseSkeleton33.Definition,
                positions,
                tracked,
                Interlocked.Increment(ref sequenceNumber),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
            return true;
        }

        private void CloseActiveTransport()
        {
            lock (transportLock)
            {
                try
                {
                    pipeServer?.Dispose();
                }
                catch (Exception)
                {
                }

                try
                {
                    udpServer?.Close();
                }
                catch (Exception)
                {
                }

                pipeServer = null;
                udpServer = null;
            }
        }

        private void LogConnection(string message)
        {
            if (logConnectionEvents)
            {
                Debug.Log(message);
            }
        }
    }
}
