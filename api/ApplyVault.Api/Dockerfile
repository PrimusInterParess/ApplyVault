FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY api/ApplyVault.Api/ApplyVault.Api.csproj api/ApplyVault.Api/
COPY shared/cv-section-catalog/cv-section-catalog.json shared/cv-section-catalog/
RUN dotnet restore api/ApplyVault.Api/ApplyVault.Api.csproj

COPY api/ApplyVault.Api/ api/ApplyVault.Api/
COPY shared/cv-section-catalog/ shared/cv-section-catalog/
RUN dotnet publish api/ApplyVault.Api/ApplyVault.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Ubuntu 24.04's chromium apt package is a snap stub and does not provide
# /usr/bin/chromium. Chrome for Testing ships a real binary for amd64 and arm64.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        fonts-liberation \
        jq \
        unzip \
        libasound2t64 \
        libatk-bridge2.0-0t64 \
        libatk1.0-0t64 \
        libcups2t64 \
        libdbus-1-3 \
        libdrm2 \
        libgbm1 \
        libgtk-3-0t64 \
        libnspr4 \
        libnss3 \
        libx11-xcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxfixes3 \
        libxkbcommon0 \
        libxrandr2 \
        libxshmfence1 \
    && arch="$(dpkg --print-architecture)" \
    && case "$arch" in \
         amd64) platform=linux64 ;; \
         arm64) platform=linux-arm64 ;; \
         *) echo "Unsupported architecture: $arch" >&2; exit 1 ;; \
       esac \
    && chrome_url="$(curl -fsSL https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json \
         | jq -r --arg platform "$platform" '.channels.Stable.downloads.chrome[] | select(.platform == $platform) | .url')" \
    && test -n "$chrome_url" && test "$chrome_url" != "null" \
    && curl -fsSL "$chrome_url" -o /tmp/chrome.zip \
    && unzip -q /tmp/chrome.zip -d /opt \
    && rm /tmp/chrome.zip \
    && ln -sf /opt/chrome-"$platform"/chrome /usr/bin/chromium \
    && chromium --version \
    && apt-get purge -y jq unzip \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV CvHtmlExport__ChromiumExecutablePath=/usr/bin/chromium
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ApplyVault.Api.dll"]
