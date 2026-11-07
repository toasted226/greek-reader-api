# syntax=docker/dockerfile:1

# Build from the API repository root. Compose supplies the F# engine as the
# named `engine` build context so the existing ProjectReference path is kept.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/csharp/GreekReaderAPI

COPY GreekReaderAPI/GreekReaderAPI.csproj GreekReaderAPI/
COPY GreekReaderAPI/appsettings.json GreekReaderAPI/
COPY GreekReaderAPI/appsettings.Production.json GreekReaderAPI/
COPY GreekReaderAPI/Data/ GreekReaderAPI/Data/
COPY GreekReaderAPI/Email/ GreekReaderAPI/Email/
COPY GreekReaderAPI/Endpoints/ GreekReaderAPI/Endpoints/
COPY GreekReaderAPI/ErrorHandling/ GreekReaderAPI/ErrorHandling/
COPY GreekReaderAPI/Models/ GreekReaderAPI/Models/
COPY GreekReaderAPI/Program.cs GreekReaderAPI/
COPY GreekReaderAPI/Security/ GreekReaderAPI/Security/
COPY GreekReaderAPI/Services/ GreekReaderAPI/Services/
COPY GreekReaderAPI/Utilities/ GreekReaderAPI/Utilities/

COPY --from=engine GreekReader.fsproj Api.fs Service.fs scratch.fsx /src/fsharp/GreekReader/
COPY --from=engine Interop/ /src/fsharp/GreekReader/Interop/
COPY --from=engine Model/ /src/fsharp/GreekReader/Model/
COPY --from=engine Parsing/ /src/fsharp/GreekReader/Parsing/
COPY --from=engine Resources/ /src/fsharp/GreekReader/Resources/
COPY --from=engine Wiktionary/ /src/fsharp/GreekReader/Wiktionary/

RUN dotnet restore GreekReaderAPI/GreekReaderAPI.csproj
RUN dotnet publish GreekReaderAPI/GreekReaderAPI.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    
USER app

WORKDIR /app

COPY --from=build /app/publish/ .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GreekReaderAPI.dll"]
