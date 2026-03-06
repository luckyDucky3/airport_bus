# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LeetCode.sln ./
COPY BusApi/BusApi.csproj BusApi/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Models/Models.csproj Models/
COPY Services/Services.csproj Services/
RUN dotnet restore BusApi/BusApi.csproj

COPY . .
RUN dotnet publish BusApi/BusApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8000
ENV ASPNETCORE_URLS=http://+:8000

ENTRYPOINT ["dotnet", "BusApi.dll"]
