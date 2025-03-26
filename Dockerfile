# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Platform.TrafficDataCollection.ExtractFrames.Service/Platform.TrafficDataCollection.ExtractFrames.Service.csproj", "Platform.TrafficDataCollection.ExtractFrames.Service/"]
COPY ["Common/Platform.KafkaClient/Platform.KafkaClient.csproj", "Common/Platform.KafkaClient/"]
COPY ["Common/Platform.Serilog/Platform.Serilog.csproj", "Common/Platform.Serilog/"]
RUN dotnet restore "./Platform.TrafficDataCollection.ExtractFrames.Service/Platform.TrafficDataCollection.ExtractFrames.Service.csproj"
COPY . .
WORKDIR "/src/Platform.TrafficDataCollection.ExtractFrames.Service"
RUN dotnet build "./Platform.TrafficDataCollection.ExtractFrames.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Platform.TrafficDataCollection.ExtractFrames.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Platform.TrafficDataCollection.ExtractFrames.Service.dll"]