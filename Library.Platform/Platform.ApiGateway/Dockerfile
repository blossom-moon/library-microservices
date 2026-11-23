# Лаб. 10. Контекст збірки — корінь рішення (Library.Platform):
#   docker build -t gateway:v1 -f Platform.ApiGateway/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Шар restore: лише .csproj (кешується, поки не змінилися залежності)
COPY ["Platform.ApiGateway/Platform.ApiGateway.csproj",           "Platform.ApiGateway/"]
COPY ["Platform.ServiceDefaults/Platform.ServiceDefaults.csproj", "Platform.ServiceDefaults/"]
COPY ["Platform.Security/Platform.Security.csproj",               "Platform.Security/"]
RUN dotnet restore "Platform.ApiGateway/Platform.ApiGateway.csproj"

COPY . .
WORKDIR "/src/Platform.ApiGateway"
RUN dotnet build "Platform.ApiGateway.csproj" -c Release --no-restore -o /app/build

FROM build AS publish
RUN dotnet publish "Platform.ApiGateway.csproj" -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.ApiGateway.dll"]
