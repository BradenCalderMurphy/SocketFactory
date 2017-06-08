using System;
using System.Runtime.InteropServices;

namespace SocketFactory.Environment
{
    public struct DateTimeEnvironment
    {
        public DateTimeEnvironment(long ticks)
            : this()
        {
            this.Ticks = ticks;
        }

        public TimeSpanEnvironment Subtract(DateTimeEnvironment dt)
        {
            return new TimeSpanEnvironment(this.Ticks - dt.Ticks);
        }

        public DateTimeEnvironment AddMilliseconds(int milliseconds)
        {
            return new DateTimeEnvironment(this.Ticks + milliseconds);
        }

        public DateTimeEnvironment AddSeconds(int seconds)
        {
            return new DateTimeEnvironment(this.Ticks + (seconds * 1000));
        }

        public DateTimeEnvironment AddMinutes(int minutes)
        {
            return new DateTimeEnvironment(this.Ticks + (minutes * 60000));
        }


        [DllImport("kernel32")]
        extern static UInt64 GetTickCount64();

        public static DateTimeEnvironment Now
        {
            get
            {
                ulong now = GetTickCount64();
                long now2 = unchecked((long)now);
                return new DateTimeEnvironment(now2);
            }
        }

        public long Ticks { set; get; }
    }
}
