using System.Text.Json;
using Core;
using Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Services;

namespace InternalFunction.Functions;

public class BlobProcessingFunction
{
    private readonly ILogger<BlobProcessingFunction> _logger;
    private readonly IBlobService _blobService;

    public BlobProcessingFunction(ILogger<BlobProcessingFunction> logger, IBlobService blobService)
    {
        _logger = logger;
        _blobService = blobService;
    }

    [Function("ProcessBlobMessage")]
    public async Task ProcessBlobMessage(
        [ServiceBusTrigger(Constants.DefaultQueueName, Connection = Constants.ServiceBusConnectionName)] string blobName)
    {
        _logger.LogInformation("Service Bus trigger function received a ProcessBlobMessage request");

        string? blobData = await _blobService.DownloadBlobAsync(Constants.InputBlobContainerName, blobName);
        if (blobData == null)
        {
            _logger.LogError("Blob not found: {BlobName}", blobName);
            return;
        }

        var orderDto = JsonSerializer.Deserialize<DocumentDto>(blobData)!;
        _logger.LogInformation("Order retrieved from storage");

        // Perform an update – for example, append a timestamp
        orderDto.AvailableItemCount = Random.Shared.Next(1, 100);
        orderDto.Status = "ready";

        var updatedBlobData = JsonSerializer.Serialize(orderDto);
        var updatedBlobName = orderDto.ToOutputBlobName();
        await _blobService.UploadBlobAsync(updatedBlobData, Constants.OutputBlobContainerName, updatedBlobName);
    }
}
