namespace TrafficDataCollection.Api.Helper
{
    public static class DateTimeHelper
    {
        /// <summary>
        /// Converts a DateTime to Unix timestamp (milliseconds since epoch)
        /// </summary>
        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            var dateTimeOffset = new DateTimeOffset(dateTime.ToUniversalTime());
            return dateTimeOffset.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Converts Unix timestamp (milliseconds since epoch) to DateTime
        /// </summary>
        public static DateTime FromUnixTimeMilliseconds(long unixTimeMilliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds).DateTime;
        }

        /// <summary>
        /// Formats a DateTime as a standard string (yyyy-MM-dd HH:mm:ss)
        /// </summary>
        public static string ToStandardString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
