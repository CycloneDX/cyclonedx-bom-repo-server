#!/bin/sh

rm -rf ./BuildReports
dotnet test --results-directory ./BuildReports --collect "XPlat Code Coverage"
reportgenerator -reports:`find ./BuildReports -name coverage.cobertura.xml` -targetdir:BuildReports/Coverage -reporttypes:"HTML;HTMLSummary"