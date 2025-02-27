namespace Services;

public interface IBlobService
{
    Task UploadBlobAsync(string json, string containerName, string blobName);
    Task<string?> DownloadBlobAsync(string containerName, string blobName);
}