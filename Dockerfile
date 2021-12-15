FROM mcr.microsoft.com/dotnet/aspnet:6.0.1

COPY bin/ /cyclonedx

WORKDIR cyclonedx

ENTRYPOINT [ "/cyclonedx/CycloneDX.BomRepoServer" ]