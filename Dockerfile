FROM mcr.microsoft.com/dotnet/core-nightly/sdk:3.0.100-preview6-alpine3.9 AS build-env

WORKDIR /pisstaube

COPY . /pisstaube

RUN dotnet restore
RUN dotnet publish Pisstaube -c Release -o out

FROM microsoft/dotnet:aspnetcore-runtime
WORKDIR /pisstaube

COPY --from=build-env /pisstaube/out .

RUN touch .env

VOLUME [ "/app/data" ]

ENTRYPOINT ["dotnet", "Pisstaube.dll"]
