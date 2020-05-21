FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -r linux-x64 --self-contained false -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1.3-alpine3.11
WORKDIR /app
COPY --from=build-env /app/out app/

RUN apk update && apk add --no-cache libc6-compat && \
ln -s /lib/libc.musl-x86_64.so.1 /lib/ld-linux-x86-64.so.2

RUN chmod +x app/Dapr.Workflows.dll

ENTRYPOINT []
