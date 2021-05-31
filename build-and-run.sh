#!/usr/bin/env bash
mkdir --parents repo
mkdir --parents bin
dotnet publish CycloneDX.BomRepoServer/CycloneDX.BomRepoServer.csproj --configuration Release --output bin
docker build --tag cyclonedx-bom-repo-server .
docker run --volume "$(pwd)/repo":/repo --env REPO__DIRECTORY=/repo --env ALLOWEDMETHODS__GET="true" --env ALLOWEDMETHODS__POST="true" --env ALLOWEDMETHODS__DELETE="true" --tty --interactive -p 8000:80 cyclonedx-bom-repo-server