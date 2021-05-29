# CycloneDX BOM Repository Server

A BOM repository server for distributing CycloneDX BOMs.

You can test it out locally with Docker by running:

```
docker run cyclonedx/cyclonedx-bom-repo-server
```

## API Endpoints

The server supports Swagger/Open API Specification.

The JSON endpoint is `/swagger/v1/swagger.json`. The UI can be accessed at
`/swagger/index.html`.

A summary of the available endpoints and methods are below:

| Path | HTTP Method | Required Parameters | Optional Parameters | Description |
| --- | --- | --- | --- | --- |
| /bom | GET | `serialNumber` | `version` | If only the `serialNumber` parameter is supplied, retrieve the latest version of the BOM from the repository. If providing `serialNumber` and `version`, a specific version of the BOM will be retrieved. Supports HTTP content negotiation for all CycloneDX BOM formats and versions. |
| /bom | POST | BOM content in request body and appropriate `Content-Type` header | | Adds a new BOM to the repository. Supports all CycloneDX BOM formats and versions. If the submitted BOM does not have a serial number, one will be generated. If the BOM does not have a version the next version number will be added. The response will contain an appropriate `Location` header to reference the BOM in the repository. |
| /bom | DELETE | `serialNumber` | `version` | If only the `serialNumber` parameter is supplied, all versions of the BOM will be deleted from the repository. If `serialNumber` and `version` are supplied, only the specific version will be deleted from the repository. |

NOTE:
BOM serial numbers should be unique for a particular device/software version.
When updating an existing BOM for the same software version the BOM serial number
should remain the same and the version number should be incremented.
For this reason, updating an existing BOM version is not supported.
There is, of course, nothing to prevent deleting an existing BOM version and re-publishing
it with the same serial number and version. But this is not recommended.

### Example cURL Usage

Retrieving a BOM from the repository

```
curl -X GET "https://www.example.com/bom?serialNumber=urn%3Auuid%3A3e671687-395b-41f5-a30f-a58921a69b79" -H  "accept: application/vnd.cyclonedx+json; version=1.3"
```

Adding a new BOM to the repository

```
curl -X POST "https://www.example.com/bom" -H  "accept: */*" -H  "Content-Type: application/vnd.cyclonedx+json; version=1.3" -d "{\"bomFormat\":\"CycloneDX\",\"specVersion\":\"1.3\",\"serialNumber\":\"urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79\",\"version\":1,\"components\":[{\"type\":\"library\",\"name\":\"acme-library\",\"version\":\"1.0.0\"}]}"```
```

Deleting a BOM from the repository

```
curl -X DELETE "https://www.example.com/bom?serialNumber=urn%3Auuid%3A3e671687-395b-41f5-a30f-a58921a69b79" -H  "accept: */*"
```

## Configuration

The server can be configured by changing the `appsettings.json` file or setting
the following environment variables

| Environment Variable Name | Supported Values | Description | Default Value |
| --- | --- | --- | --- |
| REPO__DIRECTORY | Any valid, existing, directory path | The directory BOMs are stored | `Repo` | 
| ALLOWEDMETHODS__GET | `true` or `false` | Allows or forbids BOM retrieval | `false` |
| ALLOWEDMETHODS__POST | `true` or `false` | Allows or forbids BOM creation | `false` |
| ALLOWEDMETHODS__DELETE | `true` or `false` | Allows or forbids BOM deletion | `false` |

## Authentication and Authorization

Authentication and authorization are expected to be configured at the web
server or API gateway level.

For simplicity of deployment the allowed methods can be configured, and
default to safe options (everything is forbidden by default).

It is recommended to deploy two instances of the BOM repository server.

One, requiring authentication and additional security controls, with
GET and POST methods permitted to support publishing BOMs.

And a second instance, public facing, with only the GET method enabled. And
authentication configured if required.

More advanced authentication and authorization use cases should be handled
with a solution like an API gateway. And are considered out of scope of
this project.

NOTE: It is recommended,
subject to your operational environment and risk profile,
to not require authentication for public facing instances.
This enables downstream consumers,
who might not have a direct commercial arrangement with your organization,
to retrieve BOMs.

## System Requirements

The CycloneDX BOM Repository Server has been designed as a lightweight BOM
repository server. Any production web server should be capable of running it.

However, there is an in memory cache of BOM metadata.
Memory requirements will differ based on the amount of BOM metadata that requires caching.

All BOMs are converted to Protocol Buffer format before storage for efficiency.

.NET Core runtime dependencies are required.