# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore (context is the CatalogService folder)
COPY ["CatalogService.csproj", "./"]
RUN dotnet restore "CatalogService.csproj"

# copy everything and publish
COPY . .
WORKDIR "/src"
RUN dotnet publish "CatalogService.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CatalogService.dll"]
