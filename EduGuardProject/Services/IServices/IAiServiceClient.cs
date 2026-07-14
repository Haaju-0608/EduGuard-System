namespace EduGuardProject.Services.IServices
{
    public interface IAiServiceClient
    {
        Task<float[]> ExtractVectorFrom3FacesAsync(
            Stream frontStream, string frontName,
            Stream leftStream, string leftName,
            Stream rightStream, string rightName);

        Task<List<float[]>> ExtractVectorsFromVideoAsync(Stream videoStream, string fileName);
    }
}
