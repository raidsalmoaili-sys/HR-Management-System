# syntax=docker/dockerfile:1.6

# ─── Build stage: .NET SDK + Node.js (for Tailwind) ─────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node.js for the Tailwind build step.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
 && apt-get install -y --no-install-recommends nodejs \
 && rm -rf /var/lib/apt/lists/*

# 1. Restore .NET deps (cache layer).
COPY *.csproj ./
RUN dotnet restore

# 2. Restore npm deps (cache layer).
COPY package.json package-lock.json ./
RUN npm ci

# 3. Copy source.
COPY . ./

# 4. Build Tailwind into wwwroot/css/app.min.css.
RUN npm run css:build

# 5. Publish the .NET app. wwwroot is copied into out/ automatically.
RUN dotnet publish -c Release -o /app/out --no-restore

# ─── Runtime stage: ASP.NET only ────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Project_HR_1.dll"]
