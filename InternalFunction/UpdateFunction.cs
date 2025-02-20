using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace InternalFunction;

public static class UpdateFunction
{
    [FunctionName("ProcessBlobMessage")]
    public static async Task Run(
        [ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")] string blobName,
        [ServiceBus("myqueue", Connection = "ServiceBusConnection")] IAsyncCollector<string> outputMessage,
        ILogger log)
    {
        log.LogInformation($"Triggered for blob: {blobName}");

        // Retrieve the blob
        var blobServiceClient = new BlobServiceClient(System.Environment.GetEnvironmentVariable("StorageConnection"));
        var containerClient = blobServiceClient.GetBlobContainerClient("jsoncontainer");
        var blobClient = containerClient.GetBlobClient(blobName);

        var downloadInfo = await blobClient.DownloadAsync();
        string json;
        using (var reader = new StreamReader(downloadInfo.Value.Content, Encoding.UTF8))
        {
            json = await reader.ReadToEndAsync();
        }
        log.LogInformation("JSON retrieved from storage.");

        // Perform an update – for example, append a timestamp
        string updatedJson = json.Insert(json.Length - 1, ", \"processedAt\": \"" + System.DateTime.UtcNow + "\"");

        // Save the updated JSON back to storage (overwriting or creating a new blob)
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(updatedJson)))
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }
        log.LogInformation("Blob updated.");

        // Post a message back to Service Bus (could be a new correlation message)
        await outputMessage.AddAsync($"Updated:{blobName}");
        log.LogInformation("Updated message sent to Service Bus.");
    }
}
