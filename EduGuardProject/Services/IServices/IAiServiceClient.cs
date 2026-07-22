namespace EduGuardProject.Services.IServices;

public interface IAiServiceClient
{
    Task<FaceVectorResponse> ExtractVectorFrom3FacesAsync(
        Stream frontStream, string frontName,
        Stream leftStream, string leftName,
        Stream rightStream, string rightName);

    Task<VideoVectorsResponse> ExtractVectorsFromVideoAsync(Stream videoStream, string fileName);
}