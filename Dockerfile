FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY SqlValidatorMcp.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Copy published app
COPY --from=build /app .

# Set entry point
ENTRYPOINT ["dotnet", "SqlValidatorMcp.dll"]
