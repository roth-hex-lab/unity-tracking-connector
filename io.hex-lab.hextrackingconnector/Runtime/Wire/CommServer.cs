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

    [SkeletonPipelineNode("CommServer")]
    public class CommServer : MonoBehaviour, ISkeletonProvider
    {
        private const int MaxPayloadBytes = 1024 * 1024;
        private const string DefaultPipeName = "UnityMediaPipeBody";

        [SerializeField] private TransportMode transportMode = TransportMode.Pipe;
        [SerializeField] private string pipeName = DefaultPipeName;
        [SerializeField, Min(1)] private int udpPort = 5000;
        [SerializeField] private InputSkeletonSelection inputSkeleton = InputSkeletonSelection.Auto;
        [SerializeField] private PoseCoordinateSource coordinateSource = PoseCoordinateSource.Free;
        [SerializeField] private PoseMirrorMode mirrorMode = PoseMirrorMode.None;
        [SerializeField] private bool logConnectionEvents = true;

        private readonly ConcurrentQueue<SkeletonFrame> pendingFrames = new ConcurrentQueue<SkeletonFrame>();
        private readonly object transportLock = new object();

        private Thread transportThread;
        private NamedPipeServerStream pipeServer;
        private UdpClient udpServer;
        private volatile bool isRunning;
        private int sequenceNumber;

        private SkeletonFrame latestPose;
        private bool hasLatestPose;

        public event Action<SkeletonFrame> PoseReceived;

        public TransportMode CurrentTransportMode => transportMode;

        public InputSkeletonSelection InputSkeleton
        {
            get => inputSkeleton;
            set => inputSkeleton = value;
        }

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

        public bool IsRunning => isRunning;
        public int PendingFrameCount => pendingFrames.Count;

        public bool TryGetLatestPose(out SkeletonFrame pose)
        {
            pose = latestPose;
            return hasLatestPose;
        }

        private void Start()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            StartTransport();
        }

        private void Update()
        {
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
                poseToPublish = rawPose;
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
            SkeletonProviderUtility.RaisePoseReceived(PoseReceived, pose, this);
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

            if (!InputSkeletonRegistry.TryGetMapper(inputSkeleton, frame.skeleton_id, out var mapper))
            {
                Debug.LogWarning($"Unsupported input skeleton id '{frame.skeleton_id}'. Skipping frame.");
                return false;
            }

            var definition = mapper.Definition;
            var jointPoses = new SkeletonJointPose[definition.JointCount];
            var foundAny = false;

            foreach (var landmarkData in landmarks)
            {
                if (landmarkData == null ||
                    !mapper.TryMapIndex(landmarkData.index, out var joint))
                {
                    continue;
                }

                var mappedIndex = definition.IndexOf(joint);
                if (!definition.IsValidIndex(mappedIndex))
                {
                    continue;
                }

                jointPoses[mappedIndex] = landmarkData.ToJointPose();
                foundAny = true;
            }

            if (!foundAny)
            {
                return false;
            }

            var coordinateSpace = coordinateSource == PoseCoordinateSource.Anchored
                ? SkeletonCoordinateSpace.RootRelative
                : SkeletonCoordinateSpace.World;
            var pose = new SkeletonPose(definition, jointPoses, coordinateSpace);
            if (mirrorMode == PoseMirrorMode.SwapLeftRight)
            {
                pose = SkeletonPoseTransforms.MirrorLeftRight(pose);
            }

            var metadata = new SkeletonFrameMetadata(
                Interlocked.Increment(ref sequenceNumber),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                sourceId: frame.skeleton_id);

            skeletonFrame = new SkeletonFrame(
                pose,
                metadata);
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
