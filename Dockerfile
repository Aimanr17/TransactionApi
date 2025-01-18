# Use the official ASP.NET Core runtime as a base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TransactionApi/TransactionApi.csproj", "TransactionApi/"]
RUN dotnet restore "TransactionApi/TransactionApi.csproj"
COPY . .
WORKDIR "/src/TransactionApi"
RUN dotnet build "TransactionApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransactionApi.csproj" -c Release -o /app/publish

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransactionApi.dll"]
