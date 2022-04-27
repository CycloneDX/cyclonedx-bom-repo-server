#!/usr/bin/env bash
mkdir --parents repo
docker build --tag localhost/cyclonedx-bom-repo-server .
docker run --volume "$(pwd)/repo":/repo --env REPO__DIRECTORY=/repo --env ALLOWEDMETHODS__GET="true" --env ALLOWEDMETHODS__POST="true" --env ALLOWEDMETHODS__DELETE="true" --tty --interactive -p 8000:8080 localhost/cyclonedx-bom-repo-server
