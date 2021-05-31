FROM mcr.microsoft.com/dotnet/aspnet:5.0

COPY bin/ /cyclonedx

WORKDIR cyclonedx

ENTRYPOINT [ "/cyclonedx/CycloneDX.BomRepoServer" ]