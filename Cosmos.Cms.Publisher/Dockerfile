#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base

# Install NodeJS 10
RUN apt-get update -y && apt-get install -y gnupg2 && apt-get install -y curl && curl -sL https://deb.nodesource.com/setup_18.x | bash - && apt-get update -y && apt-get install -y nodejs

WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY ["Cosmos.Cms.Publisher/Cosmos.Cms.Publisher.csproj", "Cosmos.Cms.Publisher/"]
RUN dotnet restore "Cosmos.Cms.Publisher/Cosmos.Cms.Publisher.csproj"
COPY . .
WORKDIR "/src/Cosmos.Cms.Publisher"
RUN dotnet build "Cosmos.Cms.Publisher.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cosmos.Cms.Publisher.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cosmos.Cms.Publisher.dll"]
