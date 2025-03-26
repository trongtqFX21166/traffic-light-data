namespace Platform.TrafficDataCollection.ExtractFrames.Service.Data
{
    internal class VMLCameraCollectionDto
    {
        public string SeqId { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        
        public int FramesInSecond { get; set; }
        public int DurationExtractFrame { get; set; }
        public string CameraLiveUrl { get; set; }
        public string CameraSource { get; set; }
        public string CameraName { get; set; }

        public List<List<List<int>>> Bboxes { get; set; }

        public List<List<double>> TimestampBBox { get; set; }

    }
}
