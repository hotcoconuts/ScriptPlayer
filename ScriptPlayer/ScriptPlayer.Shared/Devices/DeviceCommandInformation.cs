using System;

namespace ScriptPlayer.Shared
{
    public class DeviceCommandInformation
    {
        public int PositionFromTransformed;
        public int PositionToTransformed;
        public int SpeedTransformed;

        public int PositionFromOriginal;
        public int PositionToOriginal;
        public int SpeedOriginal;

        public TimeSpan Duration;
        public double SpeedMultiplier { get; set; } = 1;
        public double SpeedMin { get; set; } = 0;
        public double SpeedMax { get; set; } = 1;
        public double PlaybackRate { get; set; } = 1;
        public TimeSpan DurationStretched { get; set; }

        public double TransformSpeed(double speed)
        {
            return Math.Min(SpeedMax, Math.Max(SpeedMin, speed * SpeedMultiplier));
        }
    }

    public class IntermediateCommandInformation
    {
        public DeviceCommandInformation DeviceInformation;
        public double Progress;
    }
}
