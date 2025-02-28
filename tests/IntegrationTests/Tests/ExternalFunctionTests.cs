using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Core;
using Core.Models;
using FluentAssertions;

namespace IntegrationTests;

[Collection(Constants.CollectionDefinitionName)]
public class ExternalFunctionTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public ExternalFunctionTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthCheckRequest_WhenRequested_FunctionReturnsOk()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var functionUrl = $"{_fixture.ExternalFunctionUrl}/HealthCheck";

        // Act
        HttpResponseMessage response = await httpClient.GetAsync(functionUrl);
        
        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }
    
    [Fact]
    public async Task SendOrderRequest_GivenNewOrder_FunctionIngestsOrder()
    {
        // Arrange
        await _fixture.WarehouseFunctionContainer.StopAsync(); // Stops the function to prevent it from processing the order
        using var httpClient = new HttpClient();
        var functionUrl = $"{_fixture.ExternalFunctionUrl}/SendOrder";
        var orderDto = new DocumentDto
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.Now,
            OrderedItemCount = 123
        };
        string body = JsonSerializer.Serialize(orderDto);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await httpClient.PostAsync(functionUrl, content);
        response.EnsureSuccessStatusCode();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        (await BlobExistsAsync(orderDto.ToInputBlobName())).Should().BeTrue();
        
        IReadOnlyList<ServiceBusReceivedMessage> messages = await PeekMessagesAsync();
        messages.Any(m => m.Body.ToString().Contains(orderDto.Id.ToString())).Should().BeTrue();        
    }

    private async Task<bool> BlobExistsAsync(string blobName)
    {
        var cs = _fixture.AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        var containerClient = blobServiceClient.GetBlobContainerClient(Constants.InputBlobContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        var blobExists = await blobClient.ExistsAsync();
        return blobExists.Value;
    }

    private async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessagesAsync()
    {
        var serviceBusConnectionString = _fixture.ServiceBusContainer.GetConnectionString();
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        var receiver = client.CreateReceiver(Constants.DefaultQueueName);
        var messages = await receiver.PeekMessagesAsync(100);
        return messages;
    }
}
