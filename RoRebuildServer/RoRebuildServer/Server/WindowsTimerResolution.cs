using System.Runtime.InteropServices;

namespace RoRebuildServer.Server;

//Windows wakes sleeping threads on a 15.6ms timer by default, so Task.Delay rounds up to it.
internal static class WindowsTimerResolution
{
    private const uint PeriodMs = 1;

    private const int ProcessPowerThrottling = 4;
    private const uint PowerThrottlingCurrentVersion = 1;
    private const uint PowerThrottlingIgnoreTimerResolution = 0x4;

    private static bool isRaised;

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uPeriod);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, uint size);

    public static void Raise()
    {
        if (!OperatingSystem.IsWindows() || isRaised)
            return;

        //windows 11 drops the request while the console is minimized or covered, unless the process opts out
        var state = new PowerThrottlingState
        {
            Version = PowerThrottlingCurrentVersion,
            ControlMask = PowerThrottlingIgnoreTimerResolution,
            StateMask = 0
        };
        SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>());

        isRaised = timeBeginPeriod(PeriodMs) == 0;
    }

    public static void Restore()
    {
        if (!isRaised)
            return;

        timeEndPeriod(PeriodMs);
        isRaised = false;
    }
}
