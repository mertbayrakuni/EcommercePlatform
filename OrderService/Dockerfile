# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore (context is the OrderService folder)
COPY ["OrderService.csproj", "./"]
RUN dotnet restore "OrderService.csproj"

# copy everything and publish
COPY . .
WORKDIR "/src"
RUN dotnet publish "OrderService.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.dll"]
