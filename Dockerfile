# base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

# build image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

# final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY Data/IntentData.csv Data/IntentData.csv
COPY MLModels/IntentModel.zip MLModels/IntentModel.zip
ENTRYPOINT ["dotnet", "ChatFinancial.dll"]
