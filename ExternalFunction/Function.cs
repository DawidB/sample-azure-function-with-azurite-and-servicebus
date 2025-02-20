using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Text;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace ExternalFunction;

public class Function
{
    [FunctionName("PostEndpoint")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "data")] HttpRequest req,
        [ServiceBus("myqueue", Connection = "ServiceBusConnection")] IAsyncCollector<string> outputMessage,
        ILogger log)
    {
        // Read JSON from request body
        string json = await new StreamReader(req.Body).ReadToEndAsync();

        // Save JSON to blob storage
        string blobName = $"input-{System.Guid.NewGuid()}.json";
        var blobServiceClient = new BlobServiceClient(System.Environment.GetEnvironmentVariable("StorageConnection"));
        var containerClient = blobServiceClient.GetBlobContainerClient("jsoncontainer");
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(blobName);
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            await blobClient.UploadAsync(stream);
        }
        log.LogInformation($"Stored JSON blob: {blobName}");

        // Send a message to Service Bus with the blob name (or any correlation data)
        await outputMessage.AddAsync(blobName);
        log.LogInformation("Message sent to Service Bus.");

        return new OkObjectResult(new { blobName });
    }
}