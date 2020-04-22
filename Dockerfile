FROM mcr.microsoft.com/dotnet/core/runtime:3.1

COPY bin/Release/netcoreapp3.1/linux-x64/publish/ app/
COPY Resources.resx /app

ENTRYPOINT []