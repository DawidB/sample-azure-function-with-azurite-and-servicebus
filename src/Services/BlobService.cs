using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Services;

public class BlobService(ILogger<BlobService> logger, string storageConnectionString) : IBlobService
{
    public async Task UploadBlobAsync(string json, string containerName, string blobName)
    {
        BlobServiceClient blobServiceClient = CreateBlobServiceClient();
        BlobContainerClient? containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();
        BlobClient? blobClient = containerClient.GetBlobClient(blobName);
        
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            await blobClient.UploadAsync(stream);
        }

        logger.LogInformation("Stored JSON blob: {BlobName}", blobName);
    }
    
    public async Task<string?> DownloadBlobAsync(string containerName, string blobName)
    {
        BlobServiceClient blobServiceClient = CreateBlobServiceClient();
        BlobContainerClient? containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient? blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            logger.LogInformation("Blob not found: {BlobName}", blobName);
            return null;
        }
        
        Response<BlobDownloadInfo>? downloadInfo = await blobClient.DownloadAsync();
        using var reader = new StreamReader(downloadInfo.Value.Content, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private BlobServiceClient CreateBlobServiceClient() => new(storageConnectionString);
}