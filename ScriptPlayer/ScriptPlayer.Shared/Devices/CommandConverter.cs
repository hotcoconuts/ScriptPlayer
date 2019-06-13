﻿using System;

namespace ScriptPlayer.Shared
{
    public static class CommandConverter
    {
        public static uint LaunchToVorzeSpeed(DeviceCommandInformation info)
        {
            //Information from https://github.com/metafetish/syncydink/blob/4c8c31d6f8ffba2c9d1f3fcb69209630b209cd89/src/utils/HapticsToButtplug.ts#L186

            double delta = Math.Abs(info.PositionToOriginal - info.PositionFromOriginal) / 99.0;
            double speed = 25000 * Math.Pow(delta / info.Duration.TotalMilliseconds, 1.05) / 100.0;
            // 100ms = ~0.95
            speed = info.TransformSpeed(speed) * 100.0;

            return (uint)Math.Max(0, Math.Min(99, speed));
        }

        // Reverted to 0.0 by request of github user "sextoydb":
        // https://github.com/FredTungsten/ScriptPlayer/issues/64
        public static double LaunchToVibrator(int position)
        {
            const double max = 1.0;
            const double min = 0.0;

            double speedRelative = 1.0 - ((position + 1) / 100.0);
            double result = min + (max - min) * speedRelative;
            return Math.Min(max, Math.Max(min, result));
        }

        public static uint LaunchToKiiroo(int position, uint min, uint max)
        {
            double pos = position / 0.99;

            uint result = Math.Min(max, Math.Max(min, (uint)Math.Round(pos * (max - min) + min)));

            return result;
        }
    }
}
