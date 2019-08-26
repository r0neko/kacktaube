FROM mcr.microsoft.com/dotnet/core-nightly/sdk:3.0.100-preview7-alpine3.9 AS build-env

WORKDIR /pisstaube

COPY . /pisstaube

RUN dotnet restore
RUN dotnet publish Pisstaube -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-alpine3.9
WORKDIR /pisstaube

COPY --from=build-env /pisstaube/out .

RUN touch .env

VOLUME [ "/pisstaube/data" ]

ENTRYPOINT ["dotnet", "Pisstaube.dll"]
