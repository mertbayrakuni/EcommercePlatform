FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore (context is the PaymentService folder)
COPY ["PaymentService.csproj", "./"]
RUN dotnet restore "PaymentService.csproj"

# copy everything and publish
COPY . .
WORKDIR "/src"
RUN dotnet publish "PaymentService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.dll"]
