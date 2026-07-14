namespace EduGuardProject.DTOs.Response
{
    public class AiRegisterResponse
    {
        public string Message { get; set; } = null!;

        //  KHỚP VỚI PYTHON: Python trả về mảng số thực float[], 
        // .NET sẽ nhận vào mảng float[] rồi convert sang kiểu Pgvector.Vector sau
        public float[] Vector { get; set; } = null!;
    }
}
