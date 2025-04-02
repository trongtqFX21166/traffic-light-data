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

                if (string.IsNullOrEmpty(eventModel.CameraLiveUrl))
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

                //todo: add push kafka neu khong the extarct image
                //await _producer.ProduceMessageAsync(JsonConvert.SerializeObject(new VMLAnalyzationExtractFramesDto()
                //{
                //    Id = eventModel.Id,
                //    Type = eventModel.Type,
                //    CameraSource = eventModel.CameraSource,
                //    CameraName = eventModel.CameraName,
                //    SeqId = eventModel.SeqId,
                //    FrameUrl = $"",
                //    FramesInSecond = eventModel.FramesInSecond,
                //    DurationExtractFrame = eventModel.DurationExtractFrame,
                //    BeginExtractFrameTime = beginExtractFrameTime,
                //    EndExtractFrameTime = endExtractFrameTime,
                //    Bboxes = eventModel.Bboxes,
                //    TimestampBBox = eventModel.TimestampBBox
                //}));

                totalStopwatch.Stop();
                _logger.LogInformation($"Tổng thời gian thực hiện: {totalStopwatch.Elapsed.TotalSeconds:F2} giây");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<(int successCount, int failCount)> UploadImagesToS3Direct(string imageDirectory, string lightId, long unixTime, string format)
        {
            var files = Directory.GetFiles(imageDirectory, $"*.{format}")
                    .OrderBy(f => Path.GetFileName(f))
                    .ToArray();
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
                            string resourcePath = $"/{_bucketName}/{lightId}/{unixTime}/{newFileName}";
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
            long dateUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
            FileSystemWatcher _directoryWatcher;

            // Thiết lập các tùy chọn
            string imageFormat = _configuration.GetValue<string>("ImageFormat", "jpg");

            // Higher quality settings for better image output
            // For JPEG: Lower value = higher quality (1-31 scale, 2-5 is very high quality)
            string quality = _configuration.GetValue<string>("Quality", "2");

            // Lấy thời gian bắt đầu và kết thúc từ cấu hình
            string startTime = _configuration.GetValue<string>("StartTime", "00:00:00");
            string endTime = ConvertSecondsToTime(eventModel.DurationExtractFrame);
            bool useTimeRange = !string.IsNullOrEmpty(startTime) || !string.IsNullOrEmpty(endTime);

            // Lấy khoảng thời gian giữa các frame (tính bằng giây)
            double fps = 1 / eventModel.FramesInSecond;

            // Get denoising strength from configuration (0 = off, 1-3 = light to strong)
            int denoiseStrength = _configuration.GetValue<int>("DenoiseStrength", 0);

            // Get sharpening amount from configuration (0 = off, 0.5-1.5 = light to strong)
            double sharpenAmount = _configuration.GetValue<double>("SharpenAmount", 0);

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
            };
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

            // Add hwaccel for GPU acceleration if available
            arguments.Append("-hwaccel auto ");

            // Thêm protocol whitelist và input URL
            arguments.Append($"-protocol_whitelist file,http,https,tcp,tls,crypto -i \"{eventModel.CameraLiveUrl}\" ");

            // Build video filter chain based on configuration
            StringBuilder filterChain = new StringBuilder("fps=" + fps);

            // Add denoising if configured
            if (denoiseStrength > 0)
            {
                // Use hqdn3d filter for high-quality denoising, adjust strength based on configuration
                filterChain.Append($",hqdn3d={Math.Min(denoiseStrength, 3)}:1:2:3");
            }

            // Add sharpening if configured
            if (sharpenAmount > 0)
            {
                // Use unsharp filter for sharpening, adjust amount based on configuration
                double amount = Math.Min(sharpenAmount, 2.0);
                filterChain.Append($",unsharp=3:3:{amount}:3:3:0");
            }

            // Create final filter graph
            string filterGraph = filterChain.ToString();

            // Thêm filter và tùy chọn định dạng
            if (imageFormat == "png")
            {
                // Đối với PNG, sử dụng màu sắc tốt nhất
                arguments.Append($"-vf \"{filterGraph}\" -pix_fmt rgb24 ");

                // Use zlib compression level 9 for better compression but smaller file size
                arguments.Append("-compression_level 9 ");

                arguments.Append($"\"{outputPattern}\"");
            }
            else
            {
                // Đối với JPG, sử dụng tham số chất lượng
                arguments.Append($"-vf \"{filterGraph}\" -q:v {quality} ");

                // Additional options for better JPEG quality
                arguments.Append("-huffman optimal -sample_fmt yuvj420p ");

                arguments.Append($"\"{outputPattern}\"");
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

            // Rest of the method remains unchanged
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
                await UploadImagesToS3Direct(outputDir, eventModel.Id, dateUnix, imageFormat);

                await _producer.ProduceMessageAsync(JsonConvert.SerializeObject(new VMLAnalyzationExtractFramesDto()
                {
                    Id = eventModel.Id,
                    Type = eventModel.Type,
                    CameraSource = eventModel.CameraSource,
                    CameraName = eventModel.CameraName,
                    SeqId = eventModel.SeqId,
                    FrameUrl = $"{_baseUrl}/{_bucketName}?list-type=2&prefix={eventModel.Id}/{dateUnix}",
                    FramesInSecond = eventModel.FramesInSecond,
                    DurationExtractFrame = eventModel.DurationExtractFrame,
                    BeginExtractFrameTime = beginExtractFrameTime,
                    EndExtractFrameTime = endExtractFrameTime,
                    Bboxes = eventModel.Bboxes,
                    TimestampBBox = eventModel.TimestampBBox
                }));

                // Xóa thư mục tạm nếu được cấu hình
                Directory.Delete(outputDir, true);
                _logger.LogInformation($"Đã xóa thư mục tạm: {outputDir}");
            }
            else { 
            
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
            //string tempDir = Path.Combine(Path.GetTempPath(), "VideoFrameExtractor_" + Guid.NewGuid().ToString());
            //Directory.CreateDirectory(tempDir);
            //await ExtractCustomFramesAsync(tempDir, new VMLCameraCollectionDto
            //{
            //    Id = "55920",
            //    FramesInSecond = 1,
            //    DurationExtractFrame = 180,
            //    CameraLiveUrl = "http://192.168.8.64:8089/stream_th145.m3u8"
            //});
        }
    }
}
