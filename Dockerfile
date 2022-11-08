FROM mcr.microsoft.com/dotnet/sdk:6.0 AS builder

ARG APP_VERSION=0.0.0

COPY . /app

RUN cd /app && mkdir /app/bin \
    && dotnet publish src/CycloneDX.BomRepoServer/CycloneDX.BomRepoServer.csproj \
      --nologo \
      --configuration Release \
      --output bin \
      --no-self-contained \
      -p:Version=${APP_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:7.0.0

ENV TZ=Etc/UTC \
    LANG=C.UTF-8 \
    REPO__DIRECTORY=/repo \
    # tells ASP.NET to listen on 8080 per default (aka. an unprivileged port)
    ASPNETCORE_URLS=http://+:8080

ARG APP_VERSION=0.0.0
ARG COMMIT_SHA=unknowen
ARG UID=1001
ARG GID=1001

COPY --from=builder /app/bin /cyclonedx

# this should guaranty that container runtime enviironments with arbitrary UID assignment can still run this contaienr
RUN mkdir -p -m 770 ${REPO__DIRECTORY} \
    && addgroup --system --gid ${GID} cyclonedx || true \
    && adduser --system --disabled-login --ingroup cyclonedx --no-create-home --home /nonexistent --gecos "cyclonedx user" --shell /bin/false --uid ${UID} cyclonedx || true \
    && chown -R cyclonedx:0 ${REPO__DIRECTORY} /cyclonedx \
    && chmod -R g=u ${REPO__DIRECTORY} /cyclonedx

USER ${UID}

WORKDIR /cyclonedx

ENTRYPOINT [ "/cyclonedx/CycloneDX.BomRepoServer" ]

EXPOSE 8080

# metadata labels
LABEL \
    org.opencontainers.image.vendor="CycloneDX" \
    org.opencontainers.image.title="Official CycloneDX BOM Repository Server Container image" \
    org.opencontainers.image.description="CycloneDX BOM Repository Server is a BOM repository server for distributing CycloneDX BOMs" \
    org.opencontainers.image.version="${APP_VERSION}" \
    org.opencontainers.image.url="https://cyclonedx.org/" \
    org.opencontainers.image.source="https://github.com/CycloneDX/cyclonedx-bom-repo-server" \
    org.opencontainers.image.revision="${COMMIT_SHA}" \
    org.opencontainers.image.licenses="Apache-2.0"
