# Dev image: .NET 10 SDK + Node 22. PostgreSQL runs as a sibling Kubernetes service.
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg git \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs docker.io \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet dev-certs https --trust || true

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
    NG_CLI_ANALYTICS=false

WORKDIR /workspace
CMD ["sleep", "infinity"]
