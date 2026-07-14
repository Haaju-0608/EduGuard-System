using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services
{
    public class AiServiceClient : IAiServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiUrl = "http://127.0.0.1:8000"; 

        public AiServiceClient(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<float[]> ExtractVectorFrom3FacesAsync(
            Stream frontStream, string frontName,
            Stream leftStream, string leftName,
            Stream rightStream, string rightName)
        {
            using var content = new MultipartFormDataContent();

            var frontContent = new StreamContent(frontStream);
            var leftContent = new StreamContent(leftStream);
            var rightContent = new StreamContent(rightStream);

            // 🌟 KHỚP CHUẨN: Tên key phải trùng với tham số bên FastAPI Python đã viết ở Bước 2
            content.Add(frontContent, "front_file", frontName);
            content.Add(leftContent, "left_file", leftName);
            content.Add(rightContent, "right_file", rightName);

            var response = await _httpClient.PostAsync($"{_pythonApiUrl}/attendance/register-3-faces", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FaceVectorResponse>();
            return result?.Vector ?? throw new Exception("Không thể trích xuất vector từ bộ 3 ảnh eKYC.");
        }

        public async Task<List<float[]>> ExtractVectorsFromVideoAsync(Stream videoStream, string fileName)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(videoStream);
            content.Add(streamContent, "video_file", fileName); // Khớp key video_file với Python

            var response = await _httpClient.PostAsync($"{_pythonApiUrl}/attendance/verify-attendance-video", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VideoVectorsResponse>();
            return result?.Vectors ?? new List<float[]>();
        }
    }

    public class FaceVectorResponse { public float[] Vector { get; set; } = null!; }
    public class VideoVectorsResponse { public List<float[]> Vectors { get; set; } = null!; }

}