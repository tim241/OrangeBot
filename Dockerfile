FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

# Copy everything and build
COPY . ./
RUN dotnet publish OrangeBot/OrangeBot.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=build-env /app/Deployment/run.sh ./run.sh
ENTRYPOINT ["./run.sh"]