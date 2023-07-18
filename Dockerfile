# Use the official .NET Core SDK image as the base image
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS build

# Set the working directory in the container
WORKDIR /home/app

# Copy the rest of the project files
# https://github.com/dotnet/dotnet-docker/blob/037f5107b1e3f535cd8ba3f6f3e38ee9223acf8c/src/sdk/6.0/alpine3.18/amd64/Dockerfile
COPY . /home/app
ENV DOTNET_SDK_VERSION=6.0.412
RUN set -ex; \
    wget -O dotnet.tar.gz https://dotnetcli.azureedge.net/dotnet/Sdk/$DOTNET_SDK_VERSION/dotnet-sdk-$DOTNET_SDK_VERSION-linux-musl-x64.tar.gz \
    && dotnet_sha512='0cc7a93fbe53b4b17e877e402386e9b552ea4c9fcaced8f138e149403db1f3e358cc1be86d2a20408e3c3f82045bf897e2f6ccf0225cca49c834a52697f8d412' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -oxzf dotnet.tar.gz -C /usr/share/dotnet ./packs ./sdk ./sdk-manifests ./templates ./LICENSE.txt ./ThirdPartyNotices.txt \
    && rm dotnet.tar.gz \
    # Trigger first run experience by running arbitrary cmd
    && dotnet help;\
    apk add --no-cache; \
    # restore dependencies
    dotnet restore; \
    # Build the application
    dotnet publish -c Release -o bin; \ 
    rm -rf Controllers Properties obj model lib Program.cs \
    rm -rf Dockerfile docker-build.yml; \
    ls -al; 

# Build the runtime image
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS app
WORKDIR /home/app
CMD ["dotnet", "./bin/stream-file.dll"]
RUN set -ex; \
    apk add --no-cache curl; 
COPY --from=build /home/app .
