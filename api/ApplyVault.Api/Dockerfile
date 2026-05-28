FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ApplyVault.Api.sln ./
COPY ApplyVault.Api.csproj ./
RUN dotnet restore ApplyVault.Api.csproj

COPY . .
RUN dotnet publish ApplyVault.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        chromium \
        curl \
        fonts-liberation \
        libasound2 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libcups2 \
        libdbus-1-3 \
        libdrm2 \
        libgbm1 \
        libgtk-3-0 \
        libnspr4 \
        libnss3 \
        libx11-xcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxfixes3 \
        libxrandr2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV CvHtmlExport__ChromiumExecutablePath=/usr/bin/chromium
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ApplyVault.Api.dll"]
