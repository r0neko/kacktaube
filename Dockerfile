FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env

WORKDIR /pisstaube

COPY . /pisstaube

RUN dotnet restore
RUN dotnet publish Pisstaube -c Release -o /pisstaube/out

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /pisstaube

COPY --from=build-env /pisstaube/out .

RUN touch .env

VOLUME [ "/pisstaube/data" ]

ENTRYPOINT ["dotnet", "Pisstaube.dll"]
