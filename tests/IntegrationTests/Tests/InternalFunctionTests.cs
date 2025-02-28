using System.Text;
using System.Text.Json;
using Core;
using Core.Models;
using FluentAssertions;

namespace IntegrationTests.Tests;

[Collection(TestConstants.CollectionDefinitionName)]
public class InternalFunctionTests(TestFixture fixture)
{
    [Fact]
    public async Task ProcessBlobMessage_GivenValidOrder_ProcessesAndStoresResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var functionUrl = $"{fixture.ExternalFunctionUrl}/SendOrder";
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
        await Task.Delay(TimeSpan.FromSeconds(1)); // Allow time for processing
        var processedOrder = await fixture.DownloadBlobAsync<DocumentDto>(Constants.OutputBlobContainerName, orderDto.ToOutputBlobName());

        // Assert
        processedOrder.Should().NotBeNull();
        processedOrder.Id.Should().Be(orderDto.Id);
        processedOrder.Status.Should().Be("ready");
        processedOrder.AvailableItemCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ProcessBlobMessage_GivenNonExistentBlob_NoOutputBlobCreated()
    {
        // Arrange
        var nonExistentBlobName = $"nonexistent-{Guid.NewGuid()}.json";
        
        // Act
        await fixture.SendMessageAsync(nonExistentBlobName);
        await Task.Delay(TimeSpan.FromSeconds(1)); // Allow time for processing
        bool outputBlobExists = await fixture.BlobExistsAsync(Constants.OutputBlobContainerName, nonExistentBlobName.Replace("nonexistent", "output"));

        // Assert
        outputBlobExists.Should().BeFalse();
    }
}