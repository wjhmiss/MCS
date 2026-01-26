FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/MCS.Silo/MCS.Silo.csproj", "src/MCS.Silo/"]
COPY ["src/MCS.Grains/MCS.Grains.csproj", "src/MCS.Grains/"]
COPY ["src/MCS.Core/MCS.Core.csproj", "src/MCS.Core/"]
RUN dotnet restore "src/MCS.Silo/MCS.Silo.csproj"
COPY . .
WORKDIR "/src/src/MCS.Silo"
RUN dotnet build "MCS.Silo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MCS.Silo.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MCS.Silo.dll"]
