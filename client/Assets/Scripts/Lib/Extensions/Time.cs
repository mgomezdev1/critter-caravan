using System;

namespace Extensions
{
    public static class TimeUtils
    {
        public static TimeSpan TimeUntilNow(this DateTime time)
        {
            return DateTime.Now - time;
        }
        public static TimeSpan TimeSinceNow(this DateTime time)
        {
            return time - DateTime.Now;
        }
    }
}