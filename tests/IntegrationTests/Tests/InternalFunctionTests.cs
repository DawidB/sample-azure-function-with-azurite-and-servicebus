using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Core;
using Core.Models;
using FluentAssertions;

namespace IntegrationTests;

[Collection(Constants.CollectionDefinitionName)]
public class InternalFunctionTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public InternalFunctionTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessBlobMessage_GivenValidOrder_ProcessesAndStoresResult()
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
        
        // Upload test blob
        // await UploadBlobAsync(Constants.InputBlobContainerName, blobName, orderDto);
        
        // Send message to Service Bus
        // await SendMessageAsync(blobName);

        // Act - wait for processing
        await Task.Delay(TimeSpan.FromSeconds(5)); // Allow time for processing

        // Assert
        var outputBlobName = orderDto.ToOutputBlobName();
        var processedOrder = await DownloadBlobAsync<DocumentDto>(Constants.OutputBlobContainerName, outputBlobName);
        
        processedOrder.Should().NotBeNull();
        processedOrder!.Id.Should().Be(orderDto.Id);
        processedOrder.Status.Should().Be("ready");
        processedOrder.AvailableItemCount.Should().BeInRange(1, 100);
    }

    [Fact]
    public async Task ProcessBlobMessage_GivenNonExistentBlob_NoOutputBlobCreated()
    {
        // Arrange
        string nonExistentBlobName = $"nonexistent-{Guid.NewGuid()}.json";
        
        // Act
        await SendMessageAsync(nonExistentBlobName);
        await Task.Delay(TimeSpan.FromSeconds(5)); // Allow time for processing

        // Assert
        var outputBlobExists = await BlobExistsAsync(Constants.OutputBlobContainerName, 
            nonExistentBlobName.Replace("nonexistent", "output"));
        outputBlobExists.Should().BeFalse();
    }

    private async Task SendMessageAsync(string message)
    {
        var serviceBusConnectionString = _fixture.ServiceBusContainer.GetConnectionString();
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        var sender = client.CreateSender(Constants.DefaultQueueName);
        await sender.SendMessageAsync(new ServiceBusMessage(message));
    }

    private async Task UploadBlobAsync<T>(string containerName, string blobName, T content)
    {
        var cs = _fixture.AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        
        var json = JsonSerializer.Serialize(content);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, true);
    }

    private async Task<T?> DownloadBlobAsync<T>(string containerName, string blobName)
    {
        var cs = _fixture.AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            return default;
        }

        var response = await blobClient.DownloadContentAsync();
        return JsonSerializer.Deserialize<T>(response.Value.Content);
    }

    private async Task<bool> BlobExistsAsync(string containerName, string blobName)
    {
        var cs = _fixture.AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        var blobExists = await blobClient.ExistsAsync();
        return blobExists.Value;
    }
}