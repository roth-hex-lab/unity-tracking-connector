namespace HEXLab.Hextrackingconnector
{
    public readonly struct SkeletonRecordedFrame
    {
        public SkeletonRecordedFrame(int recordIndex, double recordedTime, SkeletonFrame frame)
        {
            RecordIndex = recordIndex;
            RecordedTime = recordedTime;
            Frame = frame;
        }

        public int RecordIndex { get; }
        public double RecordedTime { get; }
        public SkeletonFrame Frame { get; }
    }
}
