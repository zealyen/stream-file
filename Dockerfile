# Use the official .NET Core SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory in the container
WORKDIR /app

# Copy the project file(s) and restore dependencies
COPY *.csproj ./
RUN dotnet restore 

# Copy the rest of the project files
COPY . ./

# Build the application
RUN dotnet publish -c Release -o out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS app
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "stream-file.dll"]
