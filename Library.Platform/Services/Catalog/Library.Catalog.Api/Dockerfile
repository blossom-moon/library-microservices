# Лаб. 10. Контекст збірки — КОРІНЬ рішення (Library.Platform), бо Api посилається на сусідні проєкти:
#   docker build -t catalog-api:v1 -f Services/Catalog/Library.Catalog.Api/Dockerfile .

# ---- Етап 1: збірка. Образ SDK (~800 МБ) лише компілює; у фінальний образ він не потрапляє ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Спершу копіюються лише .csproj і виконується restore: цей шар кешується,
# тому при зміні коду NuGet-залежності не завантажуються заново
COPY ["Services/Catalog/Library.Catalog.Api/Library.Catalog.Api.csproj",       "Services/Catalog/Library.Catalog.Api/"]
COPY ["Services/Catalog/Library.Catalog.Bll/Library.Catalog.Bll.csproj",       "Services/Catalog/Library.Catalog.Bll/"]
COPY ["Services/Catalog/Library.Catalog.Dal/Library.Catalog.Dal.csproj",       "Services/Catalog/Library.Catalog.Dal/"]
COPY ["Services/Catalog/Library.Catalog.Domain/Library.Catalog.Domain.csproj", "Services/Catalog/Library.Catalog.Domain/"]
COPY ["Platform.ServiceDefaults/Platform.ServiceDefaults.csproj",              "Platform.ServiceDefaults/"]
COPY ["Platform.Security/Platform.Security.csproj",                            "Platform.Security/"]
COPY ["Platform.Messaging/Platform.Messaging.csproj",                          "Platform.Messaging/"]
RUN dotnet restore "Services/Catalog/Library.Catalog.Api/Library.Catalog.Api.csproj"

# Далі — увесь код (зміна коду інвалідує кеш лише з цього місця)
COPY . .
WORKDIR "/src/Services/Catalog/Library.Catalog.Api"
RUN dotnet build "Library.Catalog.Api.csproj" -c Release --no-restore -o /app/build

# ---- Етап 2: публікація ----
FROM build AS publish
RUN dotnet publish "Library.Catalog.Api.csproj" -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- Етап 3: фінальний образ (aspnet, ~200 МБ): лише результат публікації ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Образи ASP.NET Core 8+ слухають порт 8080 і працюють під непривілейованим користувачем
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Library.Catalog.Api.dll"]
