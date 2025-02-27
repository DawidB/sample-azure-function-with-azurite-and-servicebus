FROM mcr.microsoft.com/dotnet/sdk:9.0 AS installer-env

#use ExternalFunction/InternalFunction
ARG FunctionDir 

COPY . /build
RUN cd /build/src/${FunctionDir} && \
    mkdir -p /home/site/wwwroot && \
dotnet publish *.csproj --output /home/site/wwwroot

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated9.0-appservice
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated9.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    ServiceBusConnection="Endpoint=sb://host.docker.internal;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;" \
    AzureWebJobsStorage="AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://host.docker.internal:11000/devstoreaccount1;QueueEndpoint=http://host.docker.internal:11001/devstoreaccount1;TableEndpoint=http://host.docker.internal:11002/devstoreaccount1;"

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]