using System.Text.Json.Serialization;

namespace TrafficDataCollection.Api.Models
{
    // DTO for Kafka message - VML_Camera_Collection
    public class CameraCollectionMessage
    {
        [JsonPropertyName("SeqId")]
        public string SeqId { get; set; }

        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Type")]
        public string Type { get; set; }

        [JsonPropertyName("CameraSource")]
        public string CameraSource { get; set; }

        [JsonPropertyName("CameraId")]
        public string CameraId { get; set; }

        [JsonPropertyName("CameraLiveUrl")]
        public string CameraLiveUrl { get; set; }

        [JsonPropertyName("CameraName")]
        public string CameraName { get; set; }



        [JsonPropertyName("FramesInSecond")]
        public int FramesInSecond { get; set; }

        [JsonPropertyName("DurationExtractFrame")]
        public int DurationExtractFrame { get; set; }

        public List<List<List<int>>> Bboxes { get; set; }

        public List<List<double>> TimestampBBox { get; set; }
    }
}
