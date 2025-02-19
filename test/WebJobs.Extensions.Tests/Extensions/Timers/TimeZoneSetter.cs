using System;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers;

internal class TimeZoneSetter : IDisposable
{
    // There are so many internal benefits to using DateTimeKind.Local for us, that we're relying 
    // on it to provide the proper roundtripping support between DateTime and DateTimeOffset. This appears
    // to be the only way to "mock" this value as it's hard-coded inside a lot of .NET libraries when
    // calculating offsets, time zones, etc.
    public TimeZoneSetter(string timeZoneId)
    {
        var info = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
        var cachedData = info.GetValue(null);
        var field = cachedData.GetType().GetField("_localTimeZone", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(cachedData, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
    }

    public static TimeZoneSetter PacificStandard => new("Pacific Standard Time");

    public static TimeZoneSetter TokyoStandard => new("Tokyo Standard Time");

    public void Dispose()
    {
        TimeZoneInfo.ClearCachedData();
    }
}
