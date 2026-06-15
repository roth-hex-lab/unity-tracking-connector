namespace HEXLab.Hextrackingconnector
{
    public enum SkeletonRecordingFormat
    {
        JsonLines,
        Binary,
    }

    public enum SkeletonRecorderSourceMode
    {
        ProviderOutput,
        CaptureSource,
        Auto,
    }

    public enum SkeletonRecordingOverflowMode
    {
        StopRecordingAndWarn,
        DropNewestFrame,
    }

    public enum SkeletonPlaybackTimeSource
    {
        RecordedTime,
        SourceTimestamp,
        ReceivedTime,
        FixedFrameRate,
    }

    public enum SkeletonPlaybackCatchUpMode
    {
        LatestDueFrame,
        AllDueFrames,
    }

    public enum SkeletonPlaybackEndBehavior
    {
        HoldLastFrame,
        ClearPose,
        Stop,
    }

    public enum SkeletonPlaybackState
    {
        Stopped,
        Playing,
        Paused,
    }

    public enum SkeletonProviderSwitchSelection
    {
        Primary,
        Secondary,
        Tertiary,
    }
}
