using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Core;
using Core.Models;
using FluentAssertions;

namespace IntegrationTests.Tests;

[Collection(TestConstants.CollectionDefinitionName)]
public class ExternalFunctionTests(TestFixture fixture) : IAsyncLifetime
{
    //were testing the external function in isolation from internal function
    //(so we can check messages in the queue before internal function processes it)
    public async Task InitializeAsync() => await fixture.WarehouseFunctionContainer.PauseAsync();

    public async Task DisposeAsync() => await fixture.WarehouseFunctionContainer.UnpauseAsync();
    
    [Fact]
    public async Task HealthCheckRequest_WhenRequested_FunctionReturnsOk()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var functionUrl = $"{fixture.ExternalFunctionUrl}/HealthCheck";

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

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        (await fixture.BlobExistsAsync(Constants.InputBlobContainerName, orderDto.ToInputBlobName())).Should().BeTrue();
        
        IReadOnlyList<ServiceBusReceivedMessage> messages = await fixture.PeekMessagesAsync();
        messages.Any(m => m.Body.ToString().Contains(orderDto.Id.ToString())).Should().BeTrue();
    }
}
