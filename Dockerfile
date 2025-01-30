FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000

USER root
RUN mkdir -p /app/data
RUN mkdir -p /app/data/cdn
RUN mkdir -p /app/data/cdn/images
RUN chmod -R 777 /app/data/cdn/images
RUN adduser -u 5678 --disabled-password --gecos "" appuser
RUN chown -R appuser /app
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG configuration=Release
WORKDIR /src
COPY ["TED.API/", "TED.API/"]
COPY ["TED/", "TED/"]
RUN dotnet restore "TED.API/TED.API.csproj"
COPY . .
WORKDIR "/src/TED.API"
RUN dotnet build "TED.API.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "TED.API.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TED.API.dll"]
