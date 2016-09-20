using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

public static class HightResTime
{
    private static bool _available;

    [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
    private static extern void GetSystemTimePreciseAsFileTime(out long filetime); 

    static HightResTime()
    {
        try
        {
            long filetime;
            GetSystemTimePreciseAsFileTime(out filetime);
            _available = true;
        }
        catch (EntryPointNotFoundException)
        {
            // Not running Windows 8 or higher.
            _available = false;
        }
    }

    /// <summary>
    /// Get current UTC time
    /// </summary>
    public static DateTime NowUTC()
    {
        if (!_available)
        {
            return DateTime.Now.ToUniversalTime();
        }
        else
        {
            long filetime;
            GetSystemTimePreciseAsFileTime(out filetime);

            return DateTime.FromFileTimeUtc(filetime);
        }
    }

}
