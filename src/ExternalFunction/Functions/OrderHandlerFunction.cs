using System.Text.Json;
using Core;
using Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Services;

namespace ExternalFunction.Functions;

public class OrderHandlerFunction(ILogger<OrderHandlerFunction> logger, IBlobService blobService)
{
    [Function("SendOrder")]
    [ServiceBusOutput(Constants.DefaultQueueName, Connection = Constants.ServiceBusConnectionName)]
    public async Task<string> SendOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        logger.LogInformation("HTTP POST trigger function received a SendOrder request");

        string json = await new StreamReader(req.Body).ReadToEndAsync();
        var order = JsonSerializer.Deserialize<DocumentDto>(json, new JsonSerializerOptions(){PropertyNameCaseInsensitive = false});
        string blobName = order!.ToInputBlobName();
        await blobService.UploadBlobAsync(json, Constants.InputBlobContainerName, blobName);

        logger.LogInformation("Sending message to Service Bus");
        return blobName;
    }
    
    [Function("CheckOrderStatus")]
    public async Task<IActionResult> CheckOrderStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation("HTTP GET trigger function received a CheckOrderStatus request");

        string id = req.Query["id"]!;
        if (string.IsNullOrEmpty(id))
        {
            return new BadRequestObjectResult("Please provide a valid id.");
        }

        string blobName = id.ToOutputBlobName();
        string? blobContent = await blobService.DownloadBlobAsync(Constants.OutputBlobContainerName, blobName);
        if (blobContent == null)
        {
            return new NotFoundObjectResult("Blob not found.");
        }

        logger.LogInformation("Loaded blob content from storage");
        var orderDto = JsonSerializer.Deserialize<DocumentDto>(blobContent); 
        return new OkObjectResult(orderDto);
    }
}