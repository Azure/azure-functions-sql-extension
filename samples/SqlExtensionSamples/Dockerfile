#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/azure-functions/dotnet:3.0 AS base

FROM mcr.microsoft.com/mssql/server:2019-latest
RUN /opt/mssql/bin/sqlservr

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["samples/SqlExtensionSamples/SqlExtensionSamples.csproj", "samples/SqlExtensionSamples/"]
COPY ["src/SqlBinding/SqlBinding.csproj", "src/SqlBinding/"]
RUN dotnet restore "samples/SqlExtensionSamples/SqlExtensionSamples.csproj"
COPY . .
WORKDIR "/src/samples/SqlExtensionSamples"
RUN dotnet build "SqlExtensionSamples.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SqlExtensionSamples.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=publish /app/publish .
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true 