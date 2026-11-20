FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so the restore layer is cached until a package changes.
COPY Directory.Build.props Directory.Packages.props PartnerLink.sln ./
COPY src/PartnerLink.Core/PartnerLink.Core.csproj src/PartnerLink.Core/
COPY src/PartnerLink.Infrastructure/PartnerLink.Infrastructure.csproj src/PartnerLink.Infrastructure/
COPY src/PartnerLink.Web/PartnerLink.Web.csproj src/PartnerLink.Web/
COPY tests/PartnerLink.Tests/PartnerLink.Tests.csproj tests/PartnerLink.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/PartnerLink.Web -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PartnerLink.Web.dll"]
