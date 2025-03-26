using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platform.TrafficDataCollection.ExtractFrames.Service.Data
{
    public class VMLAnalyzationExtractFramesDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string CameraId { get; set; }
        public string CameraSource { get; set; }
        public string CameraName { get; set; }
        public string SeqId { get; set; }
        public string FrameUrl { get; set; }
        public int FramesInSecond { get; set; }
        public int DurationExtractFrame { get; set; }
        public long BeginExtractFrameTime { get; set; }
        public long EndExtractFrameTime { get; set; }
        public List<List<List<int>>> Bboxes { get; set; }
        public List<List<double>> TimestampBBox { get; set; }
    }
}
