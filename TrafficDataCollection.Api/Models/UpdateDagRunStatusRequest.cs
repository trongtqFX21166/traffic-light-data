using System.Text.Json.Serialization;

namespace TrafficDataCollection.Api.Models
{
    public class UpdateDagRunStatusRequest
    {
        [JsonPropertyName("airflow_dag_id")]
        public string AirflowDagId { get; set; }

        [JsonPropertyName("airflow_dag_run_id")]
        public string AirflowDagRunId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
}
