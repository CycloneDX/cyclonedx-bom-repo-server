#!/usr/bin/env bash
mkdir --parents repo
docker build --tag cyclonedx-bom-repo-server .
docker run --volume "$(pwd)/repo":/repo --env REPO__DIRECTORY=/repo --env ALLOWEDMETHODS__GET="true" --env ALLOWEDMETHODS__POST="true" --env ALLOWEDMETHODS__POST="delete" --tty --interactive -p 8000:8000 cyclonedx-bom-repo-server