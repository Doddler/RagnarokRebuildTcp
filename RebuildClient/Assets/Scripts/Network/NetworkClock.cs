using System.Diagnostics;

namespace Assets.Scripts.Network
{
    //Monotonic millisecond clock. Unlike UnityEngine.Time this is safe to read from the socket thread, which is where
    //inbound packets are stamped. Wraps after 24 days of client uptime, which unchecked subtraction rides out.
    public static class NetworkClock
    {
        private static readonly long Start = Stopwatch.GetTimestamp();

        //Scaled up before the divide, otherwise the integer division truncates to whole seconds.
        public static int Ms => (int)((Stopwatch.GetTimestamp() - Start) * 1000L / Stopwatch.Frequency);
    }
}
