using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Platform.KafkaClient;
using Platform.TrafficDataCollection.ExtractFrames.Service.Data;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Platform.TrafficDataCollection.ExtractFrames.Service
{
    public class HostedService : BackgroundService
    {
        private IConsumer _consumer;
        private IProducer _producer;

        private readonly ILogger<HostedService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _outputDirectory;
        private readonly string _baseUrl;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _region;
        private readonly string _service;
        private readonly string _bucketName;
        private readonly string _keyPrefix;


        public HostedService(ILogger<HostedService> logger,
            IConsumer consumer,
            IProducer producer,
            IConfiguration configuration)
        {
            _logger = logger;
            _consumer = consumer;
            _producer = producer;
            _consumer.Consume += eventConsumer_Consume;


            _configuration = configuration;
            _outputDirectory = _configuration.GetValue<string>("OutputDirectory", "/app/ExtractedFrames");

            _baseUrl = _configuration.GetValue<string>("Minio:BaseUrl");
            _accessKey = _configuration.GetValue<string>("Minio:AccessKey");
            _secretKey = _configuration.GetValue<string>("Minio:SecretKey");
            _region = _configuration.GetValue<string>("Minio:Region");
            _service = _configuration.GetValue<string>("Minio:Service");
            _bucketName = _configuration.GetValue<string>("Minio:BucketName");
            _keyPrefix = _configuration.GetValue<string>("Minio:KeyPrefix");
        }

        private async void eventConsumer_Consume(Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, string> consumeResult)
        {
            if (string.IsNullOrWhiteSpace(consumeResult.Value))
            {
                return;
            }

            try
            {
                var eventModel = JsonConvert.DeserializeObject<VMLCameraCollectionDto>(consumeResult.Value);

                if (string.IsNullOrEmpty(eventModel.VideoUrl))
                {
                    _logger.LogInformation($"VideoUrl rỗng");
                    return;
                }

                // Tạo thư mục tạm để trích xuất frame
                string tempDir = Path.Combine(Path.GetTempPath(), "VideoFrameExtractor_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                _logger.LogInformation($"Đã tạo thư mục tạm: {tempDir}");

                // Đo thời gian tổng thể
                var totalStopwatch = new Stopwatch();
                totalStopwatch.Start();

                // Trích xuất frames
                await ExtractCustomFramesAsync(tempDir, eventModel);
                _logger.LogInformation("ExtractCustomFramesAsync done");
                // Nếu có frames đã được trích xuất và đã cấu hình để upload

                totalStopwatch.Stop();
                _logger.LogInformation($"Tổng thời gian thực hiện: {totalStopwatch.Elapsed.TotalSeconds:F2} giây");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<(int successCount, int failCount)> UploadImagesToS3Direct(string imageDirectory, string format)
        {
            var files = Directory.GetFiles(imageDirectory, $"*.{format}");
            int totalFiles = files.Length;
            int successCount = 0;
            int failCount = 0;

            _logger.LogInformation($"Tổng số {totalFiles} hình ảnh cần tải lên Minio");

            // Tính toán thống kê thời gian
            double totalUploadTime = 0;
            double minUploadTime = double.MaxValue;
            double maxUploadTime = 0;
            var uploadTimes = new List<double>();

            using (var handler = new HttpClientHandler())
            {
                // Bỏ qua lỗi SSL nếu cần
                handler.ServerCertificateCustomValidationCallback =
                    (sender, cert, chain, sslPolicyErrors) => true;

                using (var client = new HttpClient(handler))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string filePath = files[i];
                        string fileName = Path.GetFileName(filePath);
                        var stopwatch = new Stopwatch();

                        try
                        {
                            _logger.LogInformation($"Đang tải lên {i + 1}/{totalFiles}: {fileName}...");

                            // Tạo ngày giờ hiện tại theo định dạng AWS
                            DateTime now = DateTime.UtcNow;
                            string amzDate = now.ToString("yyyyMMddTHHmmssZ");
                            string dateStamp = now.ToString("yyyyMMdd");

                            // Đọc dữ liệu file
                            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

                            // Tạo timestamp mới cho tên file
                            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                            string newFileName = $"{timestamp}_{i:D7}.{format}";

                            // Tạo URL đầy đủ
                            string resourcePath = $"/{_bucketName}/{_keyPrefix}/{newFileName}";
                            string fullUrl = $"{_baseUrl}{resourcePath}";

                            // Tạo request
                            var request = new HttpRequestMessage(HttpMethod.Put, fullUrl);

                            // Tính toán SHA256 hash đúng cho file dữ liệu
                            string payloadHash = ToHexString(SHA256.Create().ComputeHash(fileBytes));

                            // Thiết lập X-Amz-Content-Sha256 với giá trị đúng
                            request.Headers.Add("X-Amz-Content-Sha256", payloadHash);

                            // Thiết lập X-Amz-Date
                            request.Headers.Add("X-Amz-Date", amzDate);

                            // Thiết lập Content-Type
                            string contentType = format == "jpg" ? "image/jpeg" : "image/png";
                            request.Content = new ByteArrayContent(fileBytes);
                            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                            // Thiết lập Host header - quan trọng cho việc ký
                            string host = new Uri(_baseUrl).Host;
                            if (new Uri(_baseUrl).Port != 80 && new Uri(_baseUrl).Port != 443)
                            {
                                host += ":" + new Uri(_baseUrl).Port;
                            }
                            request.Headers.Host = host;

                            // Thiết lập content-length
                            request.Content.Headers.ContentLength = fileBytes.Length;

                            // Tạo danh sách các headers được ký
                            string signedHeaders = "content-length;content-type;host;x-amz-content-sha256;x-amz-date";

                            // Tạo canonical request
                            var canonicalRequest = new StringBuilder();
                            canonicalRequest.Append("PUT\n");                          // HTTPMethod
                            canonicalRequest.Append(resourcePath + "\n");              // CanonicalURI
                            canonicalRequest.Append("\n");                             // CanonicalQueryString (trống)

                            // CanonicalHeaders
                            canonicalRequest.Append("content-length:" + fileBytes.Length + "\n");
                            canonicalRequest.Append("content-type:" + contentType + "\n");
                            canonicalRequest.Append("host:" + host + "\n");
                            canonicalRequest.Append("x-amz-content-sha256:" + payloadHash + "\n");  // Sử dụng hash đúng
                            canonicalRequest.Append("x-amz-date:" + amzDate + "\n");
                            canonicalRequest.Append("\n");                             // Dòng trống sau headers

                            // SignedHeaders
                            canonicalRequest.Append(signedHeaders + "\n");

                            // PayloadHash - sử dụng hash đúng cho payload
                            canonicalRequest.Append(payloadHash);

                            // Tính hash của canonical request
                            string canonicalRequestHash = ToHexString(
                                SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest.ToString())));

                            // Tạo string to sign
                            string credentialScope = $"{dateStamp}/{_region}/{_service}/aws4_request";
                            var stringToSign = new StringBuilder();
                            stringToSign.Append("AWS4-HMAC-SHA256\n");
                            stringToSign.Append(amzDate + "\n");
                            stringToSign.Append(credentialScope + "\n");
                            stringToSign.Append(canonicalRequestHash);

                            // Tính chữ ký
                            byte[] signingKey = GetSignatureKey(_secretKey, dateStamp, _region, _service);
                            byte[] signature = HmacSHA256(signingKey, stringToSign.ToString());
                            string signatureHex = ToHexString(signature);

                            // Tạo Authorization header
                            string authHeader = $"AWS4-HMAC-SHA256 " +
                                $"Credential={_accessKey}/{credentialScope}, " +
                                $"SignedHeaders={signedHeaders}, " +
                                $"Signature={signatureHex}";

                            // Thêm Authorization header
                            request.Headers.TryAddWithoutValidation("Authorization", authHeader);

                            // Bắt đầu đo thời gian
                            stopwatch.Start();

                            // Gửi request
                            var response = await client.SendAsync(request);

                            // Dừng đo thời gian
                            stopwatch.Stop();
                            double uploadTime = stopwatch.Elapsed.TotalSeconds;

                            // Cập nhật thống kê
                            totalUploadTime += uploadTime;
                            if (uploadTime < minUploadTime) minUploadTime = uploadTime;
                            if (uploadTime > maxUploadTime) maxUploadTime = uploadTime;
                            uploadTimes.Add(uploadTime);

                            // Kiểm tra kết quả
                            if (response.IsSuccessStatusCode)
                            {
                                _logger.LogInformation($"Tải lên thành công! Thời gian: {uploadTime:F2}s - URL: {fullUrl}");
                                successCount++;
                            }
                            else
                            {
                                string errorContent = await response.Content.ReadAsStringAsync();
                                _logger.LogError($"Lỗi! Thời gian: {uploadTime:F2}s - HTTP {(int)response.StatusCode}");
                                _logger.LogError($"Chi tiết lỗi: {errorContent}");
                                failCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (stopwatch.IsRunning) stopwatch.Stop();
                            _logger.LogError($"Lỗi khi tải lên {fileName}: {ex.Message}");
                            failCount++;
                        }
                    }
                }
            }

            // Hiển thị thống kê
            if (uploadTimes.Count > 0)
            {
                double avgUploadTime = totalUploadTime / uploadTimes.Count;

                // Tính trung vị
                uploadTimes.Sort();
                double medianUploadTime;
                if (uploadTimes.Count % 2 == 0)
                {
                    medianUploadTime = (uploadTimes[uploadTimes.Count / 2 - 1] + uploadTimes[uploadTimes.Count / 2]) / 2;
                }
                else
                {
                    medianUploadTime = uploadTimes[uploadTimes.Count / 2];
                }

                _logger.LogInformation("===== Thống kê thời gian tải lên Minio =====");
                _logger.LogInformation($"Tổng thời gian tải lên: {totalUploadTime:F2} giây");
                _logger.LogInformation($"Thời gian trung bình: {avgUploadTime:F2} giây/hình");
                _logger.LogInformation($"Thời gian trung vị: {medianUploadTime:F2} giây/hình");
                _logger.LogInformation($"Thời gian nhanh nhất: {minUploadTime:F2} giây");
                _logger.LogInformation($"Thời gian chậm nhất: {maxUploadTime:F2} giây");
                _logger.LogInformation($"Tốc độ trung bình: {(successCount / totalUploadTime):F2} hình/giây");
            }

            _logger.LogInformation($"Tổng kết: {successCount} tải lên thành công, {failCount} thất bại từ tổng số {totalFiles} hình ảnh.");

            return (successCount, failCount);
        }

        static string ConvertSecondsToTime(int seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.ToString(@"hh\:mm\:ss");
        }

        private async Task ExtractCustomFramesAsync(string outputDir, VMLCameraCollectionDto eventModel)
        {
            int totalFramesExtracted = 0;
            var frameExtractionWatch = new Stopwatch();
            var lastFrameTime = DateTime.Now;
            var frameExtractionTimes = new List<double>();
            FileSystemWatcher _directoryWatcher;

            // Thiết lập các tùy chọn
            string imageFormat = _configuration.GetValue<string>("ImageFormat", "jpg");
            string quality = _configuration.GetValue<string>("Quality", "95");

            // Độ phân giải
            string resolution = _configuration.GetValue<string>("Resolution", "original");
            string scaleFilter = "";
            bool fullHD = _configuration.GetValue<bool>("FullHD", false);

            if (fullHD || resolution == "fullhd")
            {
                scaleFilter = "scale=1920:1080:force_original_aspect_ratio=decrease,";
            }
            else if (resolution == "hd" || resolution == "1280x720")
            {
                scaleFilter = "scale=1280:720:force_original_aspect_ratio=decrease,";
            }

            // Lấy thời gian bắt đầu và kết thúc từ cấu hình
            string startTime = _configuration.GetValue<string>("StartTime", "00:00:00");
            string endTime = ConvertSecondsToTime(eventModel.DurationExtractFrame);
            bool useTimeRange = !string.IsNullOrEmpty(startTime) || !string.IsNullOrEmpty(endTime);

            // Lấy khoảng thời gian giữa các frame (tính bằng giây)
            double fps = 1 / eventModel.FramesInSecond;

            // Thiết lập để theo dõi các file mới được tạo
            _directoryWatcher = new FileSystemWatcher(outputDir);
            _directoryWatcher.Created += (sender, e) =>
            {
                totalFramesExtracted++;
                // Tính thời gian giữa các khung hình
                DateTime now = DateTime.Now;
                TimeSpan elapsed = now - lastFrameTime;
                lastFrameTime = now;
                // Bỏ qua khung hình đầu tiên vì không có khung hình trước để so sánh
                if (totalFramesExtracted > 1)
                {
                    frameExtractionTimes.Add(elapsed.TotalSeconds);
                }
                // Hiển thị thông tin về khung hình mới
                _logger.LogInformation($"Khung hình {totalFramesExtracted}: {Path.GetFileName(e.FullPath)} - Thời gian: {elapsed.TotalSeconds:F3}s");
            }; ;
            _directoryWatcher.Filter = $"*.{imageFormat}";
            _directoryWatcher.EnableRaisingEvents = true;

            // Tìm đường dẫn ffmpeg trong hệ thống
            string ffmpegPath = FindFFmpegPath();
            _logger.LogInformation($"Sử dụng FFmpeg tại: {ffmpegPath}");

            // Tạo output pattern cho FFmpeg
            string outputPattern = Path.Combine(outputDir, $"frame_%04d.{imageFormat}");

            // Xây dựng chuỗi tham số FFmpeg
            StringBuilder arguments = new StringBuilder();

            if (useTimeRange)
            {
                // Thêm tham số thời gian bắt đầu nếu có
                if (!string.IsNullOrEmpty(startTime))
                {
                    arguments.Append($"-ss {startTime} ");
                }

                // Thêm tham số thời gian kết thúc nếu có
                if (!string.IsNullOrEmpty(endTime))
                {
                    arguments.Append($"-to {endTime} ");
                }
            }

            // Thêm protocol whitelist và input URL
            arguments.Append($"-protocol_whitelist file,http,https,tcp,tls,crypto -i \"{eventModel.VideoUrl}\" ");

            // Thêm filter và tùy chọn định dạng
            if (imageFormat == "png")
            {
                // Đối với PNG, sử dụng màu sắc tốt nhất
                arguments.Append($"-vf \"{scaleFilter}fps={fps}\" -pix_fmt rgb24 \"{outputPattern}\"");
            }
            else
            {
                // Đối với JPG, sử dụng tham số chất lượng
                arguments.Append($"-vf \"{scaleFilter}fps={fps}\" -q:v {quality} \"{outputPattern}\"");
            }

            string ffmpegArgs = arguments.ToString();
            _logger.LogInformation($"Lệnh FFmpeg: {ffmpegPath} {ffmpegArgs}");
            _logger.LogInformation("Bắt đầu trích xuất khung hình...");

            // Khởi động bộ đếm thời gian
            frameExtractionWatch.Start();
            long beginExtractFrameTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.EnableRaisingEvents = true;

                // Theo dõi output của FFmpeg để phát hiện tiến trình
                var outputBuilder = new StringBuilder();
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogDebug($"FFmpeg Output: {e.Data}");
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                // Theo dõi error stream của FFmpeg (thực tế ffmpeg ghi log ra error stream)
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Kiểm tra xem có thông tin về thời gian không
                        if (e.Data.Contains("time="))
                        {
                            _logger.LogInformation($"Tiến trình FFmpeg: {e.Data.Trim()}");
                        }
                        else if (e.Data.Contains("frame=") && e.Data.Contains("fps="))
                        {
                            _logger.LogInformation($"Tiến trình FFmpeg: {e.Data.Trim()}");
                        }
                        else
                        {
                            _logger.LogDebug($"FFmpeg Error: {e.Data}");
                        }

                        outputBuilder.AppendLine(e.Data);
                    }
                };

                // Bắt đầu process
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Đợi process hoàn thành hoặc bị hủy
                try
                {
                    await process.WaitForExitAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Quá trình trích xuất đã bị hủy");

                    // Nếu process vẫn chạy, hãy kết thúc nó
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill();
                            _logger.LogInformation("Đã kết thúc FFmpeg process");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi khi kết thúc FFmpeg process: {ex.Message}");
                        }
                    }

                    throw;
                }

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"FFmpeg đã kết thúc với mã lỗi: {process.ExitCode}");
                    _logger.LogError($"Output: {outputBuilder}");
                }
                else
                {
                    _logger.LogInformation($"FFmpeg đã hoàn thành trích xuất thành công");
                }
            }

            // Dừng theo dõi file mới
            _directoryWatcher.EnableRaisingEvents = false;
            _directoryWatcher.Dispose();

            // Dừng bộ đếm thời gian
            frameExtractionWatch.Stop();
            long endExtractFrameTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();


            // Hiển thị thống kê về thời gian trích xuất khung hình
            if (frameExtractionTimes.Count > 0)
            {
                double avgFrameTime = frameExtractionTimes.Average();
                double minFrameTime = frameExtractionTimes.Min();
                double maxFrameTime = frameExtractionTimes.Max();

                // Tính thời gian trung vị
                var sortedTimes = frameExtractionTimes.OrderBy(t => t).ToList();
                double medianFrameTime;
                if (sortedTimes.Count % 2 == 0)
                {
                    medianFrameTime = (sortedTimes[sortedTimes.Count / 2 - 1] + sortedTimes[sortedTimes.Count / 2]) / 2;
                }
                else
                {
                    medianFrameTime = sortedTimes[sortedTimes.Count / 2];
                }

                _logger.LogInformation("===== Thống kê thời gian trích xuất khung hình =====");
                _logger.LogInformation($"Tổng số khung hình đã trích xuất: {totalFramesExtracted}");
                _logger.LogInformation($"Thời gian trung bình trích xuất mỗi khung: {avgFrameTime:F3} giây");
                _logger.LogInformation($"Thời gian trung vị: {medianFrameTime:F3} giây");
                _logger.LogInformation($"Thời gian nhanh nhất: {minFrameTime:F3} giây");
                _logger.LogInformation($"Thời gian chậm nhất: {maxFrameTime:F3} giây");
                _logger.LogInformation($"Tốc độ trích xuất: {(1 / avgFrameTime):F2} khung/giây");
                _logger.LogInformation($"Tổng thời gian trích xuất: {frameExtractionWatch.Elapsed.TotalSeconds:F2} giây");
            }

            _logger.LogInformation($"Trích xuất hoàn tất. Đã tạo {totalFramesExtracted} khung hình.");

            if (totalFramesExtracted > 0)
            {
                // Upload lên S3
                _logger.LogInformation("Bắt đầu tải các frame lên S3...");
                await UploadImagesToS3Direct(outputDir, imageFormat);

                await _producer.ProduceMessageAsync(JsonConvert.SerializeObject(new VMLAnalyzationExtractFramesDto()
                {
                    Id = eventModel.Id,
                    Type = eventModel.Type,
                    CameraSource = eventModel.CameraSource,
                    SeqId = eventModel.SeqId,
                    FrameUrl = "http://localhost:9000/iothub-images?list-type=2&prefix={light-id}",
                    FramesInSecond = eventModel.FramesInSecond,
                    DurationExtractFrame = eventModel.DurationExtractFrame,
                    BeginExtractFrameTime = beginExtractFrameTime,
                    EndExtractFrameTime = endExtractFrameTime,
                    Bboxes = new List<List<List<int>>>
            {
                new List<List<int>>
                {
                    new List<int> {17, 125},
                    new List<int> {38, 125},
                    new List<int> {38, 165},
                    new List<int> {17, 165}
                }
            },
                    TimestampBBox = new List<List<double>>
            {
                new List<double> {363.6, 3.6},
                new List<double> {516.0, 3.6},
                new List<double> {516.0, 18.0},
                new List<double> {363.6, 18.0}
            }
                }));

                // Xóa thư mục tạm nếu được cấu hình
                Directory.Delete(outputDir, true);
                _logger.LogInformation($"Đã xóa thư mục tạm: {outputDir}");
            }
        }

        private static string ToHexString(byte[] array)
        {
            var hex = new StringBuilder(array.Length * 2);
            foreach (byte b in array)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        private static byte[] HmacSHA256(byte[] key, string data)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            }
        }

        private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            byte[] kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
            byte[] kDate = HmacSHA256(kSecret, dateStamp);
            byte[] kRegion = HmacSHA256(kDate, regionName);
            byte[] kService = HmacSHA256(kRegion, serviceName);
            byte[] kSigning = HmacSHA256(kService, "aws4_request");
            return kSigning;
        }

        private string FindFFmpegPath()
        {
            // Mặc định sử dụng ffmpeg trong PATH
            string ffmpegPath = "ffmpeg";

            // Kiểm tra các vị trí phổ biến của ffmpeg
            string[] possiblePaths = new[]
            {
                "/app/tools/ffmpeg",
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "ffmpeg"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    // Nếu là đường dẫn tuyệt đối, kiểm tra sự tồn tại
                    if (path.StartsWith("/") && File.Exists(path))
                    {
                        ffmpegPath = path;
                        break;
                    }
                    // Nếu là lệnh trong PATH, thử kiểm tra bằng which
                    else if (!path.StartsWith("/"))
                    {
                        var whichProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "which",
                                Arguments = path,
                                RedirectStandardOutput = true,
                                UseShellExecute = false
                            }
                        };
                        whichProcess.Start();
                        string result = whichProcess.StandardOutput.ReadToEnd().Trim();
                        whichProcess.WaitForExit();

                        if (!string.IsNullOrEmpty(result) && File.Exists(result))
                        {
                            ffmpegPath = result;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Không thể kiểm tra đường dẫn {path}: {ex.Message}");
                }
            }

            return ffmpegPath;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(Task.Factory.StartNew(() => _consumer.RegisterConsume(stoppingToken)));
        }
    }
}
