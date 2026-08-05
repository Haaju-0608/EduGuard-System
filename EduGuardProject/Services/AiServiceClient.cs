using EduGuardProject.Services.IServices;
using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace EduGuardProject.Services
{
    public class AiServiceClient : IAiServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiUrl;

        public AiServiceClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _pythonApiUrl = configuration["AiServer:BaseUrl"] ?? "https://eduguard-ai-33re.onrender.com";
        }

        public async Task<FaceVectorResponse> ExtractVectorFrom3FacesAsync(
            Stream frontStream, string frontName,
            Stream leftStream, string leftName,
            Stream rightStream, string rightName)
        {
            using var content = new MultipartFormDataContent();

            var frontContent = new StreamContent(frontStream);
            var leftContent = new StreamContent(leftStream);
            var rightContent = new StreamContent(rightStream);

            content.Add(frontContent, "front_file", frontName);
            content.Add(leftContent, "left_file", leftName);
            content.Add(rightContent, "right_file", rightName);

            var response = await _httpClient.PostAsync($"{_pythonApiUrl}/attendance/register-3-faces", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FaceVectorResponse>();
            return result ?? throw new Exception("Không thể trích xuất vector từ bộ 3 ảnh eKYC.");
        }

        // 🌟 SỬA ĐỔI: Trả về Task<VideoVectorsResponse> nguyên khối thay vì chỉ trả về List<float[]>
        public async Task<VideoVectorsResponse> ExtractVectorsFromVideoAsync(Stream videoStream, string fileName)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(videoStream);
            content.Add(streamContent, "video_file", fileName);

            var response = await _httpClient.PostAsync($"{_pythonApiUrl}/attendance/verify-attendance-video", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VideoVectorsResponse>();
            return result ?? new VideoVectorsResponse();
        }

        public async Task<float[]> ExtractAverageVectorFromUrlsAsync(List<string> imageUrls)
        {
            var payload = new { urls = imageUrls };
            var response = await _httpClient.PostAsJsonAsync($"{_pythonApiUrl}/attendance/extract-average-vector-from-urls", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi trích xuất vector trung bình ({(int)response.StatusCode}): {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<AverageVectorResponse>();
            return result?.Vector ?? throw new Exception("Không nhận được vector từ AI service.");
        }

        //CHeck mat khi lam bai thi 
        public async Task<float[]> ExtractSingleFaceVectorAsync(Stream liveCaptureStream, string fileName)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(liveCaptureStream);
            content.Add(streamContent, "live_capture", fileName);

            var response = await _httpClient.PostAsync($"{_pythonApiUrl}/attendance/extract-single-face-vector", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi trích xuất vector khuôn mặt ({(int)response.StatusCode}): {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<SingleFaceVectorResponse>();
            return result?.Vector ?? throw new Exception("Không nhận được vector từ AI service.");
        }
    }

    // DTOs hứng dữ liệu từ FastAPI
    public class FaceVectorResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;
        [JsonPropertyName("front_url")]
        public string FrontUrl { get; set; } = string.Empty;
        [JsonPropertyName("left_url")]
        public string LeftUrl { get; set; } = string.Empty;
        [JsonPropertyName("right_url")]
        public string RightUrl { get; set; } = string.Empty;
        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();
    }

    public class VideoVectorsResponse
    {
        [JsonPropertyName("faces_count")]
        public int FacesCount { get; set; }

        [JsonPropertyName("video_url")]
        public string VideoUrl { get; set; } = string.Empty;

        [JsonPropertyName("vectors")]
        public List<float[]> Vectors { get; set; } = new();

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class AverageVectorResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();
    }

    // thêm cùng chỗ với FaceVectorResponse, VideoVectorsResponse...
    public class SingleFaceVectorResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();
    }
}