# CycloneDX BOM Repository Server

A BOM repository server for distributing CycloneDX BOMs.

You can test it out locally by running (requires Docker):

```
docker run cyclonedx/cyclonedx-bom-repo-server
```

## API Endpoints

The server supports Swagger/Open API Specification.

The JSON endpoint is `/swagger/v1/swagger.json`. The UI can be accessed at
`/swagger/index.html`.

A summary of the available endpoints and methods are below:

| Path | HTTP Method | Description |
| --- | --- | --- |
| /bom | GET | Retrieves a specific BOM from the repository by specifying the `serialNumber` parameter. Supports HTTP content negotiation for all CycloneDX BOM formats and versions. |
| /bom | POST | Adds a new BOM to the repository. Supports all CycloneDX BOM formats and versions. If the submitted BOM does not have an existing serial number one will be generated and returned in the response `Location` header. |
| /bom | DELETE | Deletes a specific BOM from the repository by specifying the `serialNumber` parameter. |

NOTE: BOM serial numbers _should_ be unique for every generated BOM. Even
when updating an existing BOM for the same software version. For this reason,
updating an existing BOM is not supported. There is, of course, nothing to
stop deleting an existing BOM and re-publishing it with the same serial number.
But this is not recommended.

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

It's recommended to deploy two instances of the BOM repository server.
One, requiring authentication with GET, POST and DELETE methods permitted.
And a second, public facing one, with only the GET method enabled. And
authentication configured if required.

## System Requirements

The CycloneDX BOM Repository Server has been designed as a lightweight BOM
repository server. Any production web server should be capable of running it.

All BOMs are converted to Protocol Buffer format before storage to minimize
required disk space.

.NET Core runtime dependencies are required.