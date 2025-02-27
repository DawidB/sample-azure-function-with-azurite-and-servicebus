using Azure.Messaging.ServiceBus;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Hosting;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace IntegrationTests;

public class IntegrationTests : IAsyncLifetime
{
    private readonly INetwork _network;
    
    // Using the dedicated container classes provided by Testcontainers.Azurite and Testcontainers.ServiceBus
    private readonly AzuriteContainer _azuriteContainer;
    private readonly MsSqlContainer _msSqlContainer;
    private readonly ServiceBusContainer _serviceBusContainer;

    private readonly IHost _functionsHost;

    public static string DatabaseNetworkAlias => ServiceBusBuilder.DatabaseNetworkAlias;
    
    public IntegrationTests()
    {
        // Create a custom network
        // _network = new NetworkBuilder()
        //     .WithName("az-function-with-sb-network")
        //     .Build();
        //
        // // Configure Azurite container for Azure Storage
        // //https://mcr.microsoft.com/en-us/artifact/mar/azure-storage/azurite/tags
        // _azuriteContainer = new AzuriteBuilder()
        //     .WithImage("mcr.microsoft.com/azure-storage/azurite:3.33.0") //3.33.0 is from 10/25/2024
        //     .WithName("qwe_azurite")
        //     .WithPortBinding(10000, 10000)
        //     .WithNetwork(_network)
        //     .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
        //     .Build();
        //
        // _msSqlContainer = new MsSqlBuilder()
        //     .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        //     .WithName("qwe_mssql")
        //     .WithPortBinding(1433, 1433)
        //     .WithNetwork(_network)
        //     .WithNetworkAliases(DatabaseNetworkAlias)
        //     .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
        //     .Build();
        //
        // // For Service Bus, you might use a container if available or a mocked service
        // //https://mcr.microsoft.com/en-us/artifact/mar/azure-messaging/servicebus-emulator/tags
        // //https://java.testcontainers.org/modules/azure/#azure-service-bus-emulator
        // _serviceBusContainer = new ServiceBusBuilder()
        //     .DependsOn(_msSqlContainer)
        //     .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:1.0.1") //1.0.1 is from 11/18/2024
        //     .WithName("qwe_servicebus")
        //     .WithAcceptLicenseAgreement(true)
        //     //.withConfig(MountableFile.forClasspathResource("/service-bus-config.json"))
        //     .WithMsSqlContainer(_network, _msSqlContainer, DatabaseNetworkAlias)
        //     .WithPortBinding(5672, 5672)
        //     //.WithNetwork(_network)
        //     //.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
        //     .Build();
        
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
        // await _network.CreateAsync();
        // await _azuriteContainer.StartAsync();
        // await _msSqlContainer.StartAsync();
        // await _serviceBusContainer.StartAsync();
        
        // Initialize any additional setup like creating the blob container or Service Bus queue.
        // await _functionsHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        // await _functionsHost.StopAsync();
        // _functionsHost.Dispose();
        //
        // await _azuriteContainer.StopAsync();
        // await _serviceBusContainer.StopAsync();
        // await _network.DeleteAsync();
    }

    [Fact]
    public async Task FullFlowIntegrationTest()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var function1Url = "http://127.0.0.1:7208/api/HealthCheck";
        var serviceBusConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your_key_here";
        var queueName = "myqueue";

        // Act
        var response = await httpClient.PostAsync(function1Url, null);
        response.EnsureSuccessStatusCode();

        // Wait for the message to be processed
        await Task.Delay(5000);

        // Assert
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        var receiver = client.CreateReceiver(queueName);
        var message = await receiver.PeekMessageAsync();
        //var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10)); //PeekMessageAsync?

        Assert.NotNull(message);
        Assert.Equal("ExpectedMessageContent", message.Body.ToString());

        // Clean up
        // await receiver.CompleteMessageAsync(message);
        // await receiver.CloseAsync();
        //await client.DisposeAsync();
    }
}
