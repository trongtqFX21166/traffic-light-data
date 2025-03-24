using System.Text.Json.Serialization;

namespace TrafficDataCollection.Api.Models
{
    public class TriggerCollectLightsRequest
    {
        [JsonPropertyName("airflow_dag_id")]
        public string AirflowDagId { get; set; }

        [JsonPropertyName("airflow_dag_run_id")]
        public string AirflowDagRunId { get; set; }
    }

    // DTO for Check-Collect-Lights response
    public class CheckCollectLightsResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; }
    }
}
