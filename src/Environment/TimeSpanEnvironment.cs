using System;
using System.Collections.Generic;
using System.Text;

namespace SocketFactory.Environment
{
    public struct TimeSpanEnvironment
    {
        public TimeSpanEnvironment(long totalTicks)
            : this()
        {
            _totalTicks = totalTicks;
        }

        public long TotalMinutes
        {
            get
            {
                long total = TotalSeconds -
                    ((TotalSeconds) % 60);
                total /= 60;
                return total;
            }
        }

        public long TotalSeconds
        {
            get
            {
                long total = TotalMilliseconds -
                    ((TotalMilliseconds) % 1000);
                total /= 1000;
                return total;
            }
        }

        public long TotalMilliseconds
        {
            get
            {
                return this._totalTicks;
            }
        }


        private long _totalTicks;
        public long TotalTicks
        {
            get
            {
                return _totalTicks;
            }
        }

        public static long SecondsBetween(DateTimeEnvironment now, DateTimeEnvironment then)
        {
            return Math.Abs(now.Subtract(then).TotalSeconds);
        }

        public static long MinutesBetween(DateTimeEnvironment now, DateTimeEnvironment then)
        {
            return Math.Abs(now.Subtract(then).TotalMinutes);
        }

        public static long MillisecondsBetween(DateTimeEnvironment now, DateTimeEnvironment then)
        {
            return Math.Abs(now.Subtract(then).TotalMilliseconds);
        }
    }
}
