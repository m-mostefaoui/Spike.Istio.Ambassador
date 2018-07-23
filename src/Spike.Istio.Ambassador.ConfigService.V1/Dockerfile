FROM microsoft/dotnet:2.0-sdk AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# copy and build everything else
COPY . ./

RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.0-runtime AS runtime
WORKDIR /app
COPY --from=build /app/out ./
EXPOSE 8030
ENTRYPOINT ["dotnet", "Spike.Istio.Ambassador.ConfigService.V1.dll"]