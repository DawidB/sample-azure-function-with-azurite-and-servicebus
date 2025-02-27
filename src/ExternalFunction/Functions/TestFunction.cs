using Azure.Messaging.ServiceBus;
using Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExternalFunction.Functions;

public class TestFunction(ILogger<TestFunction> logger, IConfiguration configuration)
{
    private readonly string _serviceBusConnection = configuration[Constants.ServiceBusConnectionName] 
                                                    ?? throw new ArgumentNullException(Constants.ServiceBusConnectionName);

    [Function("HealthCheck")]
    public IActionResult HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
        string name)
    {
        logger.LogInformation("HTTP GET trigger function received a HealthCheck request");

        return new OkObjectResult("Hello, this is a response from the HTTP GET trigger.");
    }

    [Function("PeekMessageCount")]
    public async Task<IActionResult> PeekMessageCount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation("HTTP GET trigger function processing a PeekMessageCount request");

        await using ServiceBusClient client = new(_serviceBusConnection);
        await using ServiceBusReceiver receiver = client.CreateReceiver(Constants.DefaultQueueName);
        IReadOnlyList<ServiceBusReceivedMessage> messages = await receiver.PeekMessagesAsync(maxMessages: 100);
        
        logger.LogInformation("HTTP GET trigger function processed a PeekMessageCount request");
        
        int messageCountInQueue = messages.Count;
        return new OkObjectResult(new
        {
            messageCount = messageCountInQueue,
            messages = messages.Select(m => m.Body.ToString()).ToArray()
        });
    }

    [Function("SendMessage")]
    public async Task<IActionResult> SendMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequest req,
        string name)
    {
        logger.LogInformation("HTTP POST trigger function processing a SendMessage request");

        await using ServiceBusClient client = new(_serviceBusConnection);
        await using ServiceBusSender sender = client.CreateSender(Constants.DefaultQueueName);
        
        ServiceBusMessage message = new("Hello world! Message sent at " + DateTime.UtcNow);
        await sender.SendMessageAsync(message);
        
        logger.LogInformation("HTTP POST trigger function processed a SendMessage request");

        return new OkObjectResult("OK");
    }

    [Function("ReceiveMessage")]
    public async Task<IActionResult> ReceiveMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequest req,
        string name)
    {
        logger.LogInformation("HTTP POST trigger function processing a ReceiveMessage request");

        await using ServiceBusClient client = new(_serviceBusConnection);
        await using ServiceBusReceiver receiver = client.CreateReceiver(Constants.DefaultQueueName);
        
        ServiceBusReceivedMessage receivedMessage = await receiver.ReceiveMessageAsync();
        var body = receivedMessage.Body.ToString();
        Console.WriteLine(body);

        await receiver.CompleteMessageAsync(receivedMessage);

        logger.LogInformation("HTTP POST trigger function processed a ReceiveMessage request");
        
        return new OkObjectResult("Received message: \n" + body);
    }
}