# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UserService.csproj", "./"]
RUN dotnet restore "UserService.csproj"

COPY . .
WORKDIR "/src"
RUN dotnet publish "UserService.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]
