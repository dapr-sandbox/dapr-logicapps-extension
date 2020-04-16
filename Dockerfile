FROM mcr.microsoft.com/dotnet/core/runtime:3.1

COPY bin/Release/netcoreapp3.1/publish/ app/
COPY Resources.resx /app
COPY ResponseLogicApp.json /

ENTRYPOINT ["dotnet", "app/Dapr.LogicApps.dll"]