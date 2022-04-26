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
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.Models.v1_3;
using CycloneDX.Protobuf;

namespace CycloneDX.BomRepoServer.Services
{
    class S3RepoService : IRepoService
    {
        private const int InternalStorageVersion = 1;
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;
        private StorageMetadata _metadata;

        public S3RepoService(IAmazonS3 s3Client, string bucketName = "bomserver")
        {
            _s3Client = s3Client;
            _bucketName = bucketName;
        }

        public async Task PostConstructAsync()
        {
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName))
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _bucketName,
                });
            }

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

        public async Task DeleteAsync(string serialNumber, int version, CancellationToken cancellationToken = default)
        {
            await ListObjects(BomDirectory(serialNumber, version))
                .Select(s3Object => new KeyVersion {Key = s3Object.Key})
                .ToObservable()
                .Buffer(999)
                .Select(list =>
                {
                    return Observable.FromAsync(async () =>
                        await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                        {
                            BucketName = _bucketName,
                            Objects = list.ToList()
                        }, cancellationToken));
                })
                .Concat()
                .ToTask(cancellationToken);
        }

        public async Task DeleteAllAsync(string serialNumber, CancellationToken cancellationToken = default)
        {
            await ListObjects(BomInstanceBaseDirectory(serialNumber))
                .Select(s3Object => new KeyVersion {Key = s3Object.Key})
                .ToObservable()
                .Buffer(999)
                .Select(list =>
                {
                    return Observable.FromAsync(async () =>
                        await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                        {
                            BucketName = _bucketName,
                            Objects = list.ToList()
                        }, cancellationToken));
                })
                .Concat()
                .ToTask(cancellationToken);
        }

        public IAsyncEnumerable<string> GetAllBomSerialNumbersAsync(CancellationToken cancellationToken = default)
        {
            return ListObjects()
                .Where(s3Object => s3Object.Key.StartsWith("v1/urn_uuid_"))
                .Select(s3Object =>
                {
                    var segments = s3Object.Key.Split("/", StringSplitOptions.RemoveEmptyEntries);
                    return segments[1].Replace("_", ":");
                });
        }

        public IAsyncEnumerable<int> GetAllVersionsAsync(string serialNumber, CancellationToken cancellationToken = default)
        {
            var prefix = BomInstanceBaseDirectory(serialNumber);
            return ListObjects(prefix)
                .Where(s3Object => s3Object.Key.EndsWith("bom.cdx"))
                .Select(s3Object =>
                {
                    var segments = s3Object.Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    int.TryParse(segments[^2], out var result);
                    return result;
                })
                .OrderBy(v => v);
        }

        public async Task<DateTime> GetBomAgeAsync(string serialNumber, int version, CancellationToken cancellationToken = default)
        {
            var response = await _s3Client.GetObjectMetadataAsync(_bucketName, BomFilename(serialNumber, version), cancellationToken);
            return response.LastModified;
        }

        public async Task<Bom> RetrieveAsync(string serialNumber, int? version = null, CancellationToken cancellationToken = default)
        {
            if (!version.HasValue) version = await GetLatestVersionAsync(serialNumber, cancellationToken);
            if (!version.HasValue) return null;

            var filename = BomFilename(serialNumber, version.Value);
            try
            {
                var response = await _s3Client.GetObjectAsync(_bucketName, filename, cancellationToken);
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

        public async IAsyncEnumerable<Bom> RetrieveAllAsync(string serialNumber, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var version in GetAllVersionsAsync(serialNumber, cancellationToken))
            {
                yield return await RetrieveAsync(serialNumber, version, cancellationToken);
            }
        }

        public async Task<OriginalBom> RetrieveOriginalAsync(string serialNumber, int version, CancellationToken cancellationToken = default)
        {
            var prefix = BomDirectory(serialNumber, version);
            return await ListObjects(prefix)
                .Where(s3Object => s3Object.Key.Contains("/bom.") && !s3Object.Key.EndsWith(".cdx"))
                .SelectAwait(async s3Object =>
                {
                    var baseFilename = Path.GetFileName(s3Object.Key);
                    if (baseFilename == null)
                    {
                        return null;
                    }

                    var firstBreak = baseFilename.IndexOf(".", StringComparison.InvariantCulture);
                    var lastBreak = baseFilename.LastIndexOf(".", StringComparison.InvariantCulture);

                    var formatString = baseFilename.Substring(lastBreak + 1);
                    var specificationVersion = baseFilename.Substring(firstBreak + 1, lastBreak - firstBreak - 1);
                    Enum.TryParse(formatString, true, out Format parsedFormat);
                    Enum.TryParse(specificationVersion, true,
                        out SpecificationVersion parsedSpecificationVersion);

                    var getObjectResponse = await _s3Client.GetObjectAsync(_bucketName, s3Object.Key, cancellationToken);
                    return new OriginalBom
                    {
                        Format = parsedFormat,
                        SpecificationVersion = parsedSpecificationVersion,
                        BomStream = getObjectResponse.ResponseStream
                    };
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Bom> StoreAsync(Bom bom, CancellationToken cancellationToken = default)
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

            var fileName = BomFilename(bom.SerialNumber, bom.Version.Value);
            if (await KeyExists(fileName))
            {
                throw new BomAlreadyExistsException();
            }

            await using var memoryStream = new MemoryStream();
            Serializer.Serialize(bom, memoryStream);
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = memoryStream,
                ContentType = $"application/x.vnd.cyclonedx+protobuf; version={bom.SpecVersion}",
                CalculateContentMD5Header = true
            }, cancellationToken);
            return bom;
        }

        public async Task StoreOriginalAsync(string serialNumber, int version, Stream bomStream, Format format, SpecificationVersion specificationVersion, CancellationToken cancellationToken = default)
        {
            var fileName = OriginalBomFilename(serialNumber, version, format, specificationVersion);
            if (await KeyExists(fileName))
            {
                throw new BomAlreadyExistsException();
            }

            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                InputStream = bomStream,
                Key = fileName,
                ContentType = $"{MediaTypes.GetMediaType(format)}; version={specificationVersion}",
                CalculateContentMD5Header = true
            }, cancellationToken);
        }

        private IPaginatedEnumerable<S3Object> ListObjects(string prefix = "")
        {
            return _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                })
                .S3Objects;
        }

        private async Task<bool> KeyExists(string key)
        {
            var results = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request()
            {
                BucketName = _bucketName,
                Prefix = key,
                MaxKeys = 1
            });
            return results.KeyCount > 0;
        }

        private string BomBaseDirectory()
        {
            return $"v{_metadata.InternalStorageVersion}";
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

        private string OriginalBomFilename(string serialNumber, int version, Format format, SpecificationVersion specificationVersion)
        {
            return
                $"{BomDirectory(serialNumber, version)}/bom.{specificationVersion}.{format.ToString().ToLowerInvariant()}";
        }

        private async Task<int?> GetLatestVersionAsync(string serialNumber, CancellationToken cancellationToken)
        {
            return await GetAllVersionsAsync(serialNumber, cancellationToken)
                .LastOrDefaultAsync(cancellationToken);
        }
    }
}