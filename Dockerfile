FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN find . -name "appsettings*.json" -type f -delete

RUN dotnet restore "IqraAIWebSessionMiddlewareApp/IqraAIWebSessionMiddlewareApp.csproj" -r linux-x64
RUN dotnet publish "IqraAIWebSessionMiddlewareApp/IqraAIWebSessionMiddlewareApp.csproj" -c Release -r linux-x64 --self-contained true --no-restore -o /app/publish

# Frontend Runtime Target
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS publish_target
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["./IqraAIWebSessionMiddlewareApp"]