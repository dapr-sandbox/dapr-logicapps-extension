FROM mcr.microsoft.com/dotnet/core/runtime:3.1.3-alpine3.11

RUN apk update && apk add --no-cache libc6-compat && \
ln -s /lib/libc.musl-x86_64.so.1 /lib/ld-linux-x86-64.so.2

COPY src/Dapr.LogicApps/bin/Release/netcoreapp3.1/linux-x64/publish/ app/
COPY Resources.resx /app
RUN chmod +x app/Dapr.LogicApps.dll

ENTRYPOINT []
