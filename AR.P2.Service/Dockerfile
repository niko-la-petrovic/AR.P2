#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["AR.P2.Service/AR.P2.Service.csproj", "AR.P2.Service/"]
RUN dotnet restore "AR.P2.Service/AR.P2.Service.csproj"
COPY . .
WORKDIR "/src/AR.P2.Service"
RUN dotnet build "AR.P2.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AR.P2.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AR.P2.Service.dll"]