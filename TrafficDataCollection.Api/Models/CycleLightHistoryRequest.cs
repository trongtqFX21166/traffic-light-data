using System.ComponentModel.DataAnnotations;

namespace TrafficDataCollection.Api.Models
{
    public class CycleLightHistoryRequest
    {
        /// <summary>
        /// Start date/time for filtering records (ISO format, e.g. 2025-04-01T00:00:00Z)
        /// </summary>
        public DateTime? FromTime { get; set; }

        /// <summary>
        /// End date/time for filtering records (ISO format, e.g. 2025-04-03T23:59:59Z)
        /// </summary>
        public DateTime? ToTime { get; set; }

        /// <summary>
        /// Maximum number of records to return
        /// </summary>
        [Range(1, 1000, ErrorMessage = "Limit must be between 1 and 1000")]
        public int? Limit { get; set; } = 100;
    }
}
