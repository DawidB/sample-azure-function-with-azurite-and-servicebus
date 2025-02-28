using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Core;
using Core.Models;
using FluentAssertions;

namespace IntegrationTests;

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
        
        var serviceBusConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        var receiver = client.CreateReceiver("queue.1");
        var messages = await receiver.PeekMessagesAsync(100);

        var cs = _fixture.AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        var containerClient = blobServiceClient.GetBlobContainerClient(Constants.InputBlobContainerName);
        var blobClient = containerClient.GetBlobClient(orderDto.ToInputBlobName());
        var blobExists = await blobClient.ExistsAsync();
        
        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        messages.Any(m => m.Body.ToString().Contains(orderDto.Id.ToString())).Should().BeTrue();
        blobExists.Value.Should().BeTrue();
    }
}
