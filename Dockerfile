FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/DoanTrungHieu_2380600643.Api/DoanTrungHieu_2380600643.Api.csproj -c Release -o /publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publish ./
RUN mkdir -p /app/App_Data && chown -R app:app /app/App_Data
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "DoanTrungHieu_2380600643.Api.dll"]
