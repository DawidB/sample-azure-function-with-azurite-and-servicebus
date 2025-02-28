using System.Text.Json;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Core;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace IntegrationTests;

public class TestFixture : IAsyncLifetime
{
    private const string ContainerPrefix = "azf_it_";
    private const int DefaultFunctionPort = 80,
        ExternalFunctionPort = 8081,
        InternalFunctionPort = 8082;

    public string ExternalFunctionUrl => $"http://localhost:{ExternalFunctionPort}/api";
    public string InternalFunctionUrl => $"http://localhost:{InternalFunctionPort}/api";
    
    public INetwork Network { get; }
    public AzuriteContainer AzuriteContainer { get; }
    public MsSqlContainer MsSqlContainer { get; }
    public ServiceBusContainer ServiceBusContainer { get; }
    public IContainer OrdersFunctionContainer { get; }
    public IContainer WarehouseFunctionContainer { get; }

    public TestFixture()
    {
        var fixtureUid = Guid.NewGuid().ToString().Substring(0, 6);
        
        Network = new NetworkBuilder()
            .WithName("az-functions-network-" + fixtureUid)
            .WithReuse(true)
            .Build();
        
        AzuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:3.33.0")
            .WithName($"{ContainerPrefix}azurite_{fixtureUid}")
            .WithPortBinding(11000, AzuriteBuilder.BlobPort)
            .WithPortBinding(11001, AzuriteBuilder.QueuePort)
            .WithPortBinding(11002, AzuriteBuilder.TablePort)
            .WithNetwork(Network)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(AzuriteBuilder.BlobPort))
            .Build();
        
        MsSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithName($"{ContainerPrefix}mssql_{fixtureUid}")
            //.WithPortBinding(MsSqlBuilder.MsSqlPort, MsSqlBuilder.MsSqlPort)
            .WithNetwork(Network)
            .WithNetworkAliases(ServiceBusBuilder.DatabaseNetworkAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
        
        ServiceBusContainer = new ServiceBusBuilder()
            .DependsOn(MsSqlContainer)
            .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:1.0.1")
            .WithName($"{ContainerPrefix}servicebus_{fixtureUid}")
            .WithAcceptLicenseAgreement(true)
            .WithMsSqlContainer(Network, MsSqlContainer, ServiceBusBuilder.DatabaseNetworkAlias)
            .WithPortBinding(ServiceBusBuilder.ServiceBusPort, ServiceBusBuilder.ServiceBusPort)
            .Build();
        
        OrdersFunctionContainer = new ContainerBuilder()
            .WithImage("az-func-with-sb-external")
            .WithName($"{ContainerPrefix}external_function_{fixtureUid}")
            .WithNetwork(Network)
            .WithNetworkAliases("azforders_externalfunction_network")
            .WithPortBinding(ExternalFunctionPort, DefaultFunctionPort)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(DefaultFunctionPort))
            //.WithEnvironment("", ServiceBusContainer.GetConnectionString())
            .Build();
            
        WarehouseFunctionContainer = new ContainerBuilder()
            .WithImage("az-func-with-sb-internal")
            .WithName($"{ContainerPrefix}internal_function_{fixtureUid}")
            .WithNetwork(Network)
            .WithNetworkAliases("azforders_externalfunction_network")
            .WithPortBinding(InternalFunctionPort, DefaultFunctionPort)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(DefaultFunctionPort))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await Network.CreateAsync();
        await AzuriteContainer.StartAsync();
        await MsSqlContainer.StartAsync();
        await ServiceBusContainer.StartAsync();
        await OrdersFunctionContainer.StartAsync();
        await WarehouseFunctionContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await OrdersFunctionContainer.DisposeAsync();
        await WarehouseFunctionContainer.DisposeAsync();
        await ServiceBusContainer.StopAsync();
        await MsSqlContainer.StopAsync();
        await AzuriteContainer.StopAsync();
        await Network.DeleteAsync();
    }

    public async Task<T?> DownloadBlobAsync<T>(string containerName, string blobName)
    {
        string? cs = AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        BlobContainerClient? containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient? blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            return default;
        }

        Response<BlobDownloadResult>? response = await blobClient.DownloadContentAsync();
        return JsonSerializer.Deserialize<T>(response.Value.Content);
    }
    
    public async Task<bool> BlobExistsAsync(string containerName, string blobName)
    {
        string? cs = AzuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        BlobContainerClient? containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient? blobClient = containerClient.GetBlobClient(blobName);
        Response<bool>? blobExists = await blobClient.ExistsAsync();
        return blobExists.Value;
    }
    
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessagesAsync()
    {
        string? serviceBusConnectionString = ServiceBusContainer.GetConnectionString();
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(Constants.DefaultQueueName);
        IReadOnlyList<ServiceBusReceivedMessage>? messages = await receiver.PeekMessagesAsync(100);
        return messages;
    }

    public async Task SendMessageAsync(string message)
    {
        string? serviceBusConnectionString = ServiceBusContainer.GetConnectionString();
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        ServiceBusSender? sender = client.CreateSender(Constants.DefaultQueueName);
        await sender.SendMessageAsync(new ServiceBusMessage(message));
    }
}
