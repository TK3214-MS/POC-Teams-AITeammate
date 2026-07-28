FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["src/TeamsAITeammate.Agent/TeamsAITeammate.Agent.csproj", "src/TeamsAITeammate.Agent/"]
COPY ["src/TeamsAITeammate.Core/TeamsAITeammate.Core.csproj", "src/TeamsAITeammate.Core/"]
COPY ["src/TeamsAITeammate.Infrastructure/TeamsAITeammate.Infrastructure.csproj", "src/TeamsAITeammate.Infrastructure/"]
COPY ["src/TeamsAITeammate.AI/TeamsAITeammate.AI.csproj", "src/TeamsAITeammate.AI/"]
RUN dotnet restore "src/TeamsAITeammate.Agent/TeamsAITeammate.Agent.csproj"

COPY src/ src/
WORKDIR /src/src/TeamsAITeammate.Agent
RUN dotnet build -c Release -o /app/build

FROM node:22-alpine AS sidepanel-build
WORKDIR /src
COPY ["src/TeamsAITeammate.SidePanel/package.json", "src/TeamsAITeammate.SidePanel/package-lock.json", "./"]
RUN npm ci
COPY ["src/TeamsAITeammate.SidePanel/", "./"]
RUN npm run build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=sidepanel-build /src/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TeamsAITeammate.Agent.dll"]
