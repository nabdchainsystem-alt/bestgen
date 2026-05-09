# syntax=docker/dockerfile:1.6
# ----- Build stage -----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Bestgen.csproj ./
RUN dotnet restore Bestgen.csproj

COPY . ./
RUN dotnet publish Bestgen.csproj -c Release -o /app /p:UseAppHost=false

# ----- Runtime stage -----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# ICU for ar-SA culture support (already in the base image, listed for clarity).
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app ./

# Render injects PORT at runtime; Program.cs reads it and binds Kestrel.
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Bestgen.dll"]
