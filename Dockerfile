FROM mcr.microsoft.com/dotnet/core/sdk:2.2-alpine3.9 AS build-env

WORKDIR /pisstaube

COPY . /pisstaube

RUN dotnet restore
RUN dotnet publish Pisstaube -c Release -o out

FROM microsoft/dotnet:aspnetcore-runtime
WORKDIR /pisstaube

COPY --from=build-env /pisstaube/Pisstaube/out .

RUN touch .env

# ASP.NET
ENV ASPNETCORE_ENVIRONMENT=Production

# PostgreSQL Database
ENV MARIADB_HOST=127.0.0.1
ENV MARIADB_PORT=3306
ENV MARIADB_DATABASE=pisstaube
ENV MARIADB_USERNAME=root
ENV MARIADB_PASSWORD=

# Crawler Settings
ENV CRAWLER_DISABLED=false
ENV CRAWLER_THREADS=16

# Osu Settings
ENV OSU_API_KEY=
ENV OSU_EMAIL=
ENV OSU_PASSWORD=

# ElasticSearch Settings
ENV ELASTIC_HOSTNAME=127.0.0.1
ENV ELASTIC_PORT=9200

# Cleaner Settings
# B = Bytes, K = Kilobytes, M = Megabytes, G = Gigabytes, T = Terabytes
ENV CLEANER_MAX_SIZE=500G

VOLUME [ "/app/data" ]

ENTRYPOINT ["dotnet", "Pisstaube.dll"]
