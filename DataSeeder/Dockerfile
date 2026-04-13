# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DataSeeder.csproj", "./"]
RUN dotnet restore "DataSeeder.csproj"

COPY . .
RUN dotnet publish "DataSeeder.csproj" -c Release -o /app/publish

# Runtime stage — console app uses runtime, not aspnet
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DataSeeder.dll"]
