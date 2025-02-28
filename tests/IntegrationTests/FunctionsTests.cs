using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Core;
using Core.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace IntegrationTests;

public class IntegrationTests : IAsyncLifetime
{
    private const int ExternalFunctionPort = 8081;
    private string ExternalFunctionUrl => $"http://localhost:{ExternalFunctionPort}/api";
    
    private readonly INetwork _network;
    
    private readonly AzuriteContainer _azuriteContainer;
    private readonly MsSqlContainer _msSqlContainer;
    private readonly ServiceBusContainer _serviceBusContainer;

    private readonly IContainer _ordersFunctionContainer;
    
    public IntegrationTests()
    {
        // Create a custom network
        _network = new NetworkBuilder()
            .WithName("az-function-with-sb-network")
            .WithReuse(true)
            .Build();
        
        // Configure Azurite container for Azure Storage
        //https://mcr.microsoft.com/en-us/artifact/mar/azure-storage/azurite/tags
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:3.33.0") //3.33.0 is from 10/25/2024
            .WithName("azforders_azurite")
            .WithPortBinding(11000, AzuriteBuilder.BlobPort)
            .WithPortBinding(11001, AzuriteBuilder.QueuePort)
            .WithPortBinding(11002, AzuriteBuilder.TablePort)
            .WithNetwork(_network)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(AzuriteBuilder.BlobPort))
            .Build();
        
        _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithName("azforders_mssql")
            .WithPortBinding(1433, 1433)
            .WithNetwork(_network)
            .WithNetworkAliases(ServiceBusBuilder.DatabaseNetworkAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
        
        // For Service Bus, you might use a container if available or a mocked service
        //https://mcr.microsoft.com/en-us/artifact/mar/azure-messaging/servicebus-emulator/tags
        //https://java.testcontainers.org/modules/azure/#azure-service-bus-emulator
        _serviceBusContainer = new ServiceBusBuilder()
            .DependsOn(_msSqlContainer)
            .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:1.0.1") //1.0.1 is from 11/18/2024
            .WithName("azforders_servicebus")
            .WithAcceptLicenseAgreement(true)
            //.withConfig(MountableFile.forClasspathResource("/service-bus-config.json"))
            .WithMsSqlContainer(_network, _msSqlContainer, ServiceBusBuilder.DatabaseNetworkAlias)
            .WithPortBinding(5672, 5672)
            // .WithNetwork(_network) // throws error as it already finds another instance of that network (probably from mssql container)
            // .WithNetworkAliases(ServiceBusBuilder.ServiceBusNetworkAlias)
            // .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
            .Build();
        
        _ordersFunctionContainer = new ContainerBuilder()
            .WithImage("az-func-with-sb-external")
            .WithName("azforders_externalfunction")
            .WithNetwork(_network)
            .WithNetworkAliases("azforders_externalfunction_network")
            .WithPortBinding(ExternalFunctionPort, 80)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80))
            .Build();

        // var loggerMock = NSubstitute.Substitute.For<ILogger<QueueOrderFunction>>();
        // Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_ID", Guid.NewGuid().ToString());
        // Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_PORT", "7208");   // some free port
        // Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_HOST", "127.0.0.1");
        // Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        // Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true");
        // Environment.SetEnvironmentVariable("Functions:Worker:HostEndpoint", "http://127.0.0.1:7208");
        //
        // _functionsHost = new HostBuilder()
        //     .ConfigureFunctionsWorkerDefaults()
        //     // .ConfigureAppConfiguration(configBuilder =>
        //     // {
        //     //     var config = new Dictionary<string, string>
        //     //     {
        //     //         {"Functions:Worker:HostEndpoint", "http://127.0.0.1:7208"},
        //     //         {"AzureWebJobsStorage", "UseDevelopmentStorage=true"},
        //     //         {"FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated"}
        //     //     };
        //     //     configBuilder.AddInMemoryCollection(config);
        //     // })
        //     .ConfigureServices(services =>
        //     {
        //         //services.AddSingleton(serviceMock.Object);
        //         services.AddSingleton(loggerMock);
        //         services.AddSingleton<QueueOrderFunction>();
        //         
        //         // Remove the gRPC listener service to avoid errors in test
        //         var grpcWorkerDescriptor = services.FirstOrDefault(
        //             d => d.ImplementationType?.Name == "WorkerHostedService");
        //         if (grpcWorkerDescriptor != null)
        //             services.Remove(grpcWorkerDescriptor);
        //         // ... register any test-specific services or overrides ...
        //     })
        //     .Build();
        
        // _functionsHost = new HostBuilder()
        //     .ConfigureFunctionsWorkerDefaults()
        //     //.ConfigureFunctionsWorkerDefaults() // registers Functions infrastructure
        //     .ConfigureServices(services =>
        //     {
        //         // Optionally, register any additional dependencies for your functions here.
        //     })
        //     .Build();
        
        // var startup = new Startup();
        // _functionsHost = new HostBuilder().ConfigureWebJobs(startup.Configure).Build();
        //_functionsHost.Start();
        
        // var builder = FunctionsApplication.CreateBuilder([]);
        // builder.ConfigureFunctionsWorkerDefaults();
        // //builder.ConfigureFunctionsWebApplication();
        //
        // builder.Services.AddLogging();
        //
        // _functionsHost = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();
        await _azuriteContainer.StartAsync();
        await _msSqlContainer.StartAsync();
        await _serviceBusContainer.StartAsync();
        
        await _ordersFunctionContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _ordersFunctionContainer.DisposeAsync();
        
        await _azuriteContainer.StopAsync();
        await _serviceBusContainer.StopAsync();
        await _msSqlContainer.StopAsync();
        await _network.DeleteAsync();
    }

    [Fact]
    public async Task HealthCheckRequest_WhenRequested_FunctionReturnsOk()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var functionUrl = $"{ExternalFunctionUrl}/HealthCheck";

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
        var functionUrl = $"{ExternalFunctionUrl}/SendOrder";
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
        //var message = await receiver.PeekMessageAsync();
        var messages = await receiver.PeekMessagesAsync(100);

        var cs = _azuriteContainer.GetConnectionString();
        var blobServiceClient = new BlobServiceClient(cs);
        var containerClient = blobServiceClient.GetBlobContainerClient(Constants.InputBlobContainerName);
        var blobClient = containerClient.GetBlobClient(orderDto.ToInputBlobName());
        var blobExists = await blobClient.ExistsAsync();
        // await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
        // {
        //     Console.WriteLine(blobItem.Name);
        // }
        
        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        messages.Any(m => m.Body.ToString().Contains(orderDto.Id.ToString())).Should().BeTrue();
        blobExists.Value.Should().BeTrue();
    }
}
