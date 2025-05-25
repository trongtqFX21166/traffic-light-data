using System.Text.Json.Serialization;

namespace TrafficDataCollection.Api.Models
{
    public class SummaryJobAndPushNotifyRequest
    {
        [JsonPropertyName("airflow_dag_id")]
        public string AirflowDagId { get; set; }

        [JsonPropertyName("airflow_dag_run_id")]
        public string AirflowDagRunId { get; set; }

        [JsonPropertyName("notify_to")]
        public string[] NotifyTo { get; set; }
    }

    public class JobSummary
    {
        [JsonPropertyName("dag_id")]
        public string DagId { get; set; }

        [JsonPropertyName("dag_run_id")]
        public string DagRunId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("execution_date")]
        public DateTime ExecutionDate { get; set; }

        [JsonPropertyName("start_time")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; }

        [JsonPropertyName("total_lights")]
        public int TotalTrafficLights { get; set; }

        [JsonPropertyName("processed_lights")]
        public int ProcessedTrafficLights { get; set; }

        [JsonPropertyName("total_commands")]
        public int TotalCommands { get; set; }

        [JsonPropertyName("completed_commands")]
        public int CompletedCommands { get; set; }

        [JsonPropertyName("error_commands")]
        public int ErrorCommands { get; set; }

        [JsonPropertyName("timeout_commands")]
        public int TimeoutCommands { get; set; }

        [JsonPropertyName("success_rate")]
        public double SuccessRate { get; set; }

        [JsonPropertyName("error_details")]
        public List<CommandErrorDetail> ErrorDetails { get; set; }
    }

    public class CommandErrorDetail
    {
        [JsonPropertyName("traffic_light_id")]
        public int TrafficLightId { get; set; }

        [JsonPropertyName("camera_id")]
        public string CameraId { get; set; }

        [JsonPropertyName("camera_source_id")]
        public string CameraSourceId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }


        [JsonPropertyName("reason_code")]
        public string ReasonCode { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

    }
}