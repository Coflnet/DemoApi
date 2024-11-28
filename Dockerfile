FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /build
COPY DemoApi.csproj DemoApi.csproj
RUN dotnet restore
COPY . .
RUN dotnet test
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app

# install ffmpeg
RUN apt-get update && apt-get install -y ffmpeg

COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8000

RUN useradd --uid $(shuf -i 2000-65000 -n 1) app-user
USER app-user

ENTRYPOINT ["dotnet", "DemoApi.dll", "--hostBuilder:reloadConfigOnChange=false"]
