using System;

// Purposely in a separate namespace so as not to conflict with other possible implementations
namespace NeoSmart.Web.DateTimeExtensions
{
    public static class DateTimeExtensionMethods
    {
        private static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime AsUnixTimeMilliseconds(this double unixTime)
        {
            return _epoch.AddMilliseconds(unixTime);
        }

        public static DateTime AsUnixTimeMilliseconds(this long unixTime)
        {
            return _epoch.AddMilliseconds(unixTime);
        }

        public static DateTime AsUnixTimeMilliseconds(this ulong unixTime)
        {
            return _epoch.AddMilliseconds(unixTime);
        }

        public static DateTime AsUnixTimeSeconds(this double unixTime)
        {
            return _epoch.AddSeconds(unixTime);
        }

        public static DateTime AsUnixTimeSeconds(this long unixTime)
        {
            return _epoch.AddSeconds(unixTime);
        }

        public static DateTime AsUnixTimeSeconds(this ulong unixTime)
        {
            return _epoch.AddSeconds(unixTime);
        }

        public static double ToUnixTimeMilliseconds(this DateTime unixTime)
        {
            return (unixTime - _epoch).TotalMilliseconds;
        }

        public static double ToUnixTimeSeconds(this DateTime unixTime)
        {
            return (unixTime - _epoch).TotalSeconds;
        }
    }

#if false
    public static class DateTimeOffsetExtensionMethods
    {
        private static readonly DateTimeOffset _epoch = new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        public static DateTimeOffset AsUnixTimeMilliseconds(this double unixTime)
        {
            return _epoch.AddMilliseconds(unixTime);
        }

        public static DateTimeOffset AsUnixTimeMilliseconds(this long unixTime)
        {
            return _epoch.AddMilliseconds(unixTime);
        }

        public static DateTimeOffset AsUnixTimeMilliseconds(this ulong unixTime)
        {
            return _epoch.AddMilliseconds(unixTime);
        }

        public static DateTimeOffset AsUnixTimeSeconds(this double unixTime)
        {
            return _epoch.AddSeconds(unixTime);
        }

        public static DateTimeOffset AsUnixTimeSeconds(this long unixTime)
        {
            return _epoch.AddSeconds(unixTime);
        }

        public static DateTimeOffset AsUnixTimeSeconds(this ulong unixTime)
        {
            return _epoch.AddSeconds(unixTime);
        }

        public static double ToUnixTimeMilliseconds(this DateTimeOffset unixTime)
        {
            return (unixTime - _epoch).TotalMilliseconds;
        }

        public static double ToUnixTimeSeconds(this DateTimeOffset unixTime)
        {
            return (unixTime - _epoch).TotalSeconds;
        }
    }
#endif
}
