# Etapa de build: restaura y publica NODUS.API (con sus proyectos referenciados)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar primero solo los .csproj para aprovechar la caché de capas en el restore
COPY NODUS.API/NODUS.API.csproj NODUS.API/
COPY NODUS.Data/NODUS.Data.csproj NODUS.Data/
COPY NODUS.Models/NODUS.Models.csproj NODUS.Models/
COPY NODUS.Services/NODUS.Services.csproj NODUS.Services/
RUN dotnet restore NODUS.API/NODUS.API.csproj

COPY NODUS.API/ NODUS.API/
COPY NODUS.Data/ NODUS.Data/
COPY NODUS.Models/ NODUS.Models/
COPY NODUS.Services/ NODUS.Services/
RUN dotnet publish NODUS.API/NODUS.API.csproj -c Release -o /app/publish --no-restore

# Etapa final: solo el runtime de ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Docker Compose lo vuelve a definir; en Railway la variable PORT tiene prioridad (ver Program.cs)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NODUS.API.dll"]
