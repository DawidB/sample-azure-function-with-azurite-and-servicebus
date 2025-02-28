using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace IntegrationTests;

public class TestFixture : IAsyncLifetime
{
    public const int ExternalFunctionPort = 8081;
    public string ExternalFunctionUrl => $"http://localhost:{ExternalFunctionPort}/api";
    
    public INetwork Network { get; }
    public AzuriteContainer AzuriteContainer { get; }
    public MsSqlContainer MsSqlContainer { get; }
    public ServiceBusContainer ServiceBusContainer { get; }
    public IContainer OrdersFunctionContainer { get; }

    public TestFixture()
    {
        Network = new NetworkBuilder()
            .WithName("az-function-with-sb-network")
            .WithReuse(true)
            .Build();
        
        AzuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:3.33.0")
            .WithName("azforders_azurite")
            .WithPortBinding(11000, AzuriteBuilder.BlobPort)
            .WithPortBinding(11001, AzuriteBuilder.QueuePort)
            .WithPortBinding(11002, AzuriteBuilder.TablePort)
            .WithNetwork(Network)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(AzuriteBuilder.BlobPort))
            .Build();
        
        MsSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithName("azforders_mssql")
            .WithPortBinding(1433, 1433)
            .WithNetwork(Network)
            .WithNetworkAliases(ServiceBusBuilder.DatabaseNetworkAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
        
        ServiceBusContainer = new ServiceBusBuilder()
            .DependsOn(MsSqlContainer)
            .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:1.0.1")
            .WithName("azforders_servicebus")
            .WithAcceptLicenseAgreement(true)
            .WithMsSqlContainer(Network, MsSqlContainer, ServiceBusBuilder.DatabaseNetworkAlias)
            .WithPortBinding(5672, 5672)
            .Build();
        
        OrdersFunctionContainer = new ContainerBuilder()
            .WithImage("az-func-with-sb-external")
            .WithName("azforders_externalfunction")
            .WithNetwork(Network)
            .WithNetworkAliases("azforders_externalfunction_network")
            .WithPortBinding(ExternalFunctionPort, 80)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await Network.CreateAsync();
        await AzuriteContainer.StartAsync();
        await MsSqlContainer.StartAsync();
        await ServiceBusContainer.StartAsync();
        await OrdersFunctionContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await OrdersFunctionContainer.DisposeAsync();
        await ServiceBusContainer.StopAsync();
        await MsSqlContainer.StopAsync();
        await AzuriteContainer.StopAsync();
        await Network.DeleteAsync();
    }
}
