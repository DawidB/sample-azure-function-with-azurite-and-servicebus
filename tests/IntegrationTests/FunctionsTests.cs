using System.Threading.Tasks;
using Xunit;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Testcontainers.Azurite;
using Testcontainers.ServiceBus;

namespace IntegrationTests;

public class IntegrationTests : IAsyncLifetime
{
    // Using the dedicated container classes provided by Testcontainers.Azurite and Testcontainers.ServiceBus
    private readonly AzuriteContainer _azuriteContainer;
    private readonly ServiceBusContainer _serviceBusContainer;

    public IntegrationTests()
    {
        // Configure Azurite container for Azure Storage
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite")
            .WithName("azurite")
            .WithPortBinding(10000, 10000)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
            .Build();

        // For Service Bus, you might use a container if available or a mocked service
        _serviceBusContainer = new ServiceBusBuilder()
            .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator")
            .WithName("servicebus")
            .WithPortBinding(5672, 5672)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _azuriteContainer.StartAsync();
        await _serviceBusContainer.StartAsync();
        // Initialize any additional setup like creating the blob container or Service Bus queue.
    }

    public async Task DisposeAsync()
    {
        await _azuriteContainer.StopAsync();
        await _serviceBusContainer.StopAsync();
    }

    [Fact]
    public async Task FullFlowIntegrationTest()
    {
        // Arrange: configure your functions to point to the test container endpoints.
        // You might use environment variables or configuration overrides for connection strings.
        // For example, "StorageConnection" might point to "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=...;BlobEndpoint=http://localhost:10000/devstoreaccount1;"
        // Similarly, set up the Service Bus connection string to point to your container.

        // Act: 
        // - Trigger Function 1’s HTTP endpoint (using HttpClient or the in-process function host)
        // - Wait for the message to be processed and for Function 2 to complete its work.
        // You might poll the blob storage or Service Bus for the updated message.

        // Assert:
        // - Verify that the blob was created and updated correctly.
        // - Verify that the messages in the Service Bus follow the expected flow.
    }
}
