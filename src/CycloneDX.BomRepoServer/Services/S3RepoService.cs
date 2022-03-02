// This file is part of CycloneDX BOM Repository Server
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.Models.v1_3;
using CycloneDX.Protobuf;
using Microsoft.Extensions.Hosting;

/*
 * TODO
 *
 * - Add a hosted service that calls EnsureMetadataAsync on startup
 * - Decide how to handle the situation where the bucket doesn't exist (healthcheck?)
 * 
 */
namespace CycloneDX.BomRepoServer.Services
{
    class S3RepoService : IRepoService
    {
        private const int InternalStorageVersion = 1;
        private StorageMetadata _metadata;
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;

        public S3RepoService(IAmazonS3 s3Client, string bucketName = "bomserver")
        {
            _s3Client = s3Client;
            _bucketName = bucketName;
        }

        public async Task EnsureMetadataAsync()
        {
            try
            {
                var metadataJsonObject = await _s3Client.GetObjectAsync(_bucketName, "storage-metadata");
                _metadata = await JsonSerializer.DeserializeAsync<StorageMetadata>(metadataJsonObject.ResponseStream);
                return;
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != "NoSuchKey")
                {
                    throw;
                }
            }

            _metadata = new StorageMetadata
            {
                InternalStorageVersion = InternalStorageVersion
            };

            await using MemoryStream memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(
                memoryStream,
                _metadata,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );
            memoryStream.Seek(0, SeekOrigin.Begin);
            await _s3Client.PutObjectAsync(new PutObjectRequest()
            {
                BucketName = _bucketName,
                Key = "storage-metadata",
                InputStream = memoryStream,
                ContentType = "application/json",
                CalculateContentMD5Header = true
            });
        }

        public async Task DeleteAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken))
        {
            var objects = _s3Client.Paginators.ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = BomDirectory(serialNumber, version),
                })
                .S3Objects;
            var buffer = new List<KeyVersion>();
            await foreach (var s3Object in objects)
            {
                buffer.Add(new KeyVersion {Key = s3Object.Key});
                if (buffer.Count > 999)
                {
                    await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = buffer.ToList()
                    }, cancellationToken);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = buffer.ToList()
                }, cancellationToken);
            }
        }

        public async Task DeleteAllAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            var objects = _s3Client.Paginators.ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = BomInstanceBaseDirectory(serialNumber)
                })
                .S3Objects;
            var buffer = new List<KeyVersion>();
            await foreach (var s3Object in objects)
            {
                buffer.Add(new KeyVersion {Key = s3Object.Key});
                if (buffer.Count > 999)
                {
                    await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = buffer.ToList()
                    }, cancellationToken);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = buffer.ToList()
                }, cancellationToken);
            }
        }

        public IAsyncEnumerable<string> GetAllBomSerialNumbersAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO Use cancellationtoken
            return _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName
                })
                .S3Objects
                .Where(s3Object => s3Object.Key.StartsWith("v1/urn_uuid_"))
                .Select(s3Object =>
                {
                    var segments = s3Object.Key.Split("/", StringSplitOptions.RemoveEmptyEntries);
                    return segments[1].Replace("_", ":");
                });
        }

        public IAsyncEnumerable<int> GetAllVersionsAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            var instanceDirname = BomInstanceBaseDirectory(serialNumber);
            return _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = instanceDirname,
                })
                .S3Objects
                .Where(s3Object => s3Object.Key.EndsWith("bom.cdx"))
                .Select(s3Object =>
                {
                    // TODO There are probably better ways to do this
                    var segments = s3Object.Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    int.TryParse(segments[^2], out var result);
                    return result;
                })
                .OrderBy(v => v);
        }

        public async Task<DateTime> GetBomAgeAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken)) // TODO Not covered by tests
        {
            var response = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = BomFilename(serialNumber, version),
            });
            return response.LastModified;
        }

        public async Task<Bom> RetrieveAsync(string serialNumber, int? version = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!version.HasValue) version = await GetLatestVersionAsync(serialNumber, cancellationToken);
            if (!version.HasValue) return null;

            var filename = BomFilename(serialNumber, version.Value);
            try
            {
                var response = await _s3Client.GetObjectAsync(_bucketName, filename);
                await using (response.ResponseStream)
                {
                    var bom = Deserializer.Deserialize(response.ResponseStream);
                    return bom;
                }
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode == "NoSuchKey") return null;
                throw;
            }
        }

        public async IAsyncEnumerable<CycloneDX.Models.v1_3.Bom> RetrieveAllAsync(string serialNumber, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            await foreach (var version in GetAllVersionsAsync(serialNumber, cancellationToken))
            {
                yield return await RetrieveAsync(serialNumber, version);
            }
        }

        public async Task<OriginalBom> RetrieveOriginalAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken))
        {
            var directoryName = BomDirectory(serialNumber, version);
            return await _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = directoryName
                })
                .S3Objects
                .Where(s3Object => s3Object.Key.Contains("/bom.") && !s3Object.Key.EndsWith(".cdx"))
                .Where(s3Object =>
                {
                    var baseFilename = Path.GetFileName(s3Object.Key);
                    var firstBreak = baseFilename.IndexOf(".", StringComparison.InvariantCulture);
                    var lastBreak = baseFilename.LastIndexOf(".", StringComparison.InvariantCulture);

                    var formatString = baseFilename.Substring(lastBreak + 1);
                    var specificationVersion = baseFilename.Substring(firstBreak + 1, lastBreak - firstBreak - 1);

                    return Format.TryParse(formatString, true, out Format parsedFormat)
                           && SpecificationVersion.TryParse(specificationVersion, true,
                               out SpecificationVersion parsedSpecificationVersion);
                })
                .Select(s3Object =>
                {
                    var result = _s3Client.GetObjectAsync(new GetObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = s3Object.Key,
                        })
                        .Result;

                    var baseFilename = Path.GetFileName(s3Object.Key);
                    var firstBreak = baseFilename.IndexOf(".", StringComparison.InvariantCulture);
                    var lastBreak = baseFilename.LastIndexOf(".", StringComparison.InvariantCulture);

                    var formatString = baseFilename.Substring(lastBreak + 1);
                    var specificationVersion = baseFilename.Substring(firstBreak + 1, lastBreak - firstBreak - 1);

                    Format.TryParse(formatString, true, out Format parsedFormat);
                    SpecificationVersion.TryParse(specificationVersion, true,
                        out SpecificationVersion parsedSpecificationVersion);

                    return new OriginalBom
                    {
                        Format = parsedFormat,
                        SpecificationVersion = parsedSpecificationVersion,
                        BomStream = result.ResponseStream
                    };
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Bom> StoreAsync(Bom bom, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(bom.SerialNumber)) bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();

            if (!bom.Version.HasValue)
            {
                var latestVersion = await GetLatestVersionAsync(bom.SerialNumber, cancellationToken);
                if (latestVersion.HasValue)
                {
                    bom.Version = latestVersion.Value + 1;
                }
                else
                {
                    bom.Version = 1;
                }
            }

            var directoryName = BomDirectory(bom.SerialNumber, bom.Version.Value);
            var fileName = BomFilename(bom.SerialNumber, bom.Version.Value);

            try
            {
                await _s3Client
                    .GetObjectMetadataAsync(_bucketName, fileName);
                        throw new BomAlreadyExistsException(); // TODO Implement with object locking in governance mode instead?
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != "NotFound")
                    throw;
            }

            await using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(bom, memoryStream);
                await _s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = memoryStream,
                    ContentType = $"application/x.vnd.cyclonedx+protobuf; version={bom.SpecVersion}",
                    CalculateContentMD5Header = true
                            
                });
            }
            return bom;
        }

        public async Task StoreOriginalAsync(string serialNumber, int version, Stream bomStream, Format format,
            SpecificationVersion specificationVersion, CancellationToken cancellationToken = default(CancellationToken))
        {
            var fileName = OriginalBomFilename(serialNumber, version, format, specificationVersion);

            try
            {
                await _s3Client.GetObjectMetadataAsync( _bucketName,fileName);
                throw new BomAlreadyExistsException();
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != "NotFound")
                    throw;
            }

            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                InputStream = bomStream,
                Key = fileName,
                ContentType = $"{MediaTypes.GetMediaType(format)}; version={specificationVersion}",
                        CalculateContentMD5Header = true
            });
        }

        private string BomBaseDirectory()
        {
            return $"v{InternalStorageVersion}";
        }

        private string ReplaceInvalidFilepathSegmentCharacters(string filePathSegment)
        {
            // The only invalid character possible is ":" in serial number
            return filePathSegment.Replace(':', '_');
        }

        private string BomInstanceBaseDirectory(string serialNumber)
        {
            return $"{BomBaseDirectory()}/{ReplaceInvalidFilepathSegmentCharacters(serialNumber)}";
        }

        private string BomDirectory(string serialNumber, int version)
        {
            return $"{BomInstanceBaseDirectory(serialNumber)}/{version.ToString()}";
        }

        private string BomFilename(string serialNumber, int version)
        {
            return $"{BomDirectory(serialNumber, version)}/bom.cdx";
        }

        private string OriginalBomFilename(string serialNumber, int version, Format format,
            SpecificationVersion specificationVersion)
        {
            return $"{BomDirectory(serialNumber, version)}/bom.{specificationVersion}.{format.ToString().ToLowerInvariant()}";
        }

        private async Task<int?> GetLatestVersionAsync(string serialNumber, CancellationToken cancellationToken)
        {
            return await GetAllVersionsAsync(serialNumber, cancellationToken)
                .LastOrDefaultAsync(cancellationToken);
        }
    }
}