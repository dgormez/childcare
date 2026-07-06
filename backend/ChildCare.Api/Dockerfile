FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ChildCare.Domain/ChildCare.Domain.csproj ChildCare.Domain/
COPY ChildCare.Contracts/ChildCare.Contracts.csproj ChildCare.Contracts/
COPY ChildCare.Application/ChildCare.Application.csproj ChildCare.Application/
COPY ChildCare.Infrastructure/ChildCare.Infrastructure.csproj ChildCare.Infrastructure/
COPY ChildCare.Api/ChildCare.Api.csproj ChildCare.Api/
RUN dotnet restore ChildCare.Api/ChildCare.Api.csproj
COPY . .
RUN dotnet publish ChildCare.Api/ChildCare.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChildCare.Api.dll"]
