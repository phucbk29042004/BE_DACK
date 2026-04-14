# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj và restore các dependency
COPY ["BE_DACK/BE_DACK.csproj", "BE_DACK/"]
RUN dotnet restore "BE_DACK/BE_DACK.csproj"

# Copy toàn bộ code và build
COPY ["BE_DACK/Controllers/", "BE_DACK/Controllers/"]
COPY ["BE_DACK/Helpers/", "BE_DACK/Helpers/"]
COPY ["BE_DACK/Models/", "BE_DACK/Models/"]
COPY ["BE_DACK/Properties/", "BE_DACK/Properties/"]
COPY ["BE_DACK/Service/", "BE_DACK/Service/"]
COPY ["BE_DACK/Program.cs", "BE_DACK/"]
COPY ["BE_DACK/appsettings.json", "BE_DACK/"]
COPY ["BE_DACK/appsettings.Development.json", "BE_DACK/"]

WORKDIR /src/BE_DACK
RUN dotnet publish "BE_DACK.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

# Render sẽ cung cấp cổng qua biến môi trường PORT
ENTRYPOINT ["sh", "-c", "dotnet BE_DACK.dll --urls http://+:${PORT:-8080}"]
