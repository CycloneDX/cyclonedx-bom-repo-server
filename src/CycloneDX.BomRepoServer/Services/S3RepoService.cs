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
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.Models.v1_3;
using CycloneDX.Protobuf;

/*
 * TODO
 *
 * - Add storage metadata file
 * 
 */
namespace CycloneDX.BomRepoServer.Services
{
    class S3RepoService : IRepoService
    {
        private const int InternalStorageVersion = 1;
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;

        public S3RepoService(IAmazonS3 s3Client, string bucketName = "bomserver")
        {
            _s3Client = s3Client;
            _bucketName = bucketName;
        }

        public void Delete(string serialNumber, int version)
        {
            Observable.FromAsync(async () =>
                {
                    await _s3Client.Paginators.ListObjectsV2(new ListObjectsV2Request
                        {
                            BucketName = _bucketName,
                            Prefix = BomDirectory(serialNumber, version)
                        })
                        .S3Objects
                        .ToObservable()
                        .Select(s3Object => new KeyVersion {Key = s3Object.Key})
                        .Buffer(1000)
                        .SelectMany(objectKeys =>
                        {
                            // Limit concurrency?
                            return _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                            {
                                BucketName = _bucketName,
                                Objects = objectKeys.ToList()
                            });
                        });
                })
                .ToTask()
                .Wait();
        }

        public void DeleteAll(string serialNumber)
        {
            Observable.FromAsync(async () =>
                {
                    await _s3Client.Paginators.ListObjectsV2(new ListObjectsV2Request
                        {
                            BucketName = _bucketName,
                            Prefix = BomInstanceBaseDirectory(serialNumber)
                        })
                        .S3Objects
                        .ToObservable()
                        .Select(s3Object => new KeyVersion {Key = s3Object.Key})
                        .Buffer(1000)
                        .SelectMany(objectKeys =>
                        {
                            // Limit concurrency?
                            return _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                            {
                                BucketName = _bucketName,
                                Objects = objectKeys.ToList()
                            });
                        });
                })
                .ToTask()
                .Wait();
        }

        public IEnumerable<string> GetAllBomSerialNumbers()
        {
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
                })
                .ToEnumerable();
        }

        public IEnumerable<int> GetAllVersions(string serialNumber)
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
                .OrderBy(v => v)
                .ToEnumerable();
        }

        public DateTime GetBomAge(string serialNumber, int version) // TODO Not covered by tests
        {
            return _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = BomFilename(serialNumber, version),
                })
                .ToObservable()
                .Select(s3Object => s3Object.LastModified)
                .ToTask()
                .Result;
        }

        public Bom Retrieve(string serialNumber, int? version = null)
        {
            if (!version.HasValue) version = GetLatestVersion(serialNumber);
            if (!version.HasValue) return null;

            var filename = BomFilename(serialNumber, version.Value);
            return Observable.FromAsync(async () =>
                {
                    try
                    {
                        var response = await _s3Client.GetObjectAsync(new GetObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = filename
                        });
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
                })
                .SingleOrDefaultAsync()
                .ToTask()
                .Result;
        }

        public List<Bom> RetrieveAll(string serialNumber)
        {
            var boms = new List<Bom>();
            var versions = GetAllVersions(serialNumber);
            foreach (var version in versions)
            {
                boms.Add(Retrieve(serialNumber, version));
            }

            return boms;
        }

        public OriginalBom RetrieveOriginal(string serialNumber, int version)
        {
            var directoryName = BomDirectory(serialNumber, version);
            return _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = directoryName
                })
                .S3Objects
                .Where(s3Object => s3Object.Key.StartsWith("bom.") && !s3Object.Key.EndsWith(".cdx"))
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
                .FirstOrDefaultAsync()
                .Result;
        }

        public Bom Store(Bom bom)
        {
            if (string.IsNullOrEmpty(bom.SerialNumber)) bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();

            if (!bom.Version.HasValue)
            {
                var latestVersion = GetLatestVersion(bom.SerialNumber);
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

            return Observable.FromAsync(async () =>
                {
                    try
                    {
                        await _s3Client
                            .GetObjectMetadataAsync(new GetObjectMetadataRequest
                            {
                                BucketName = _bucketName,
                                Key = fileName,
                            });
                        throw new BomAlreadyExistsException();
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
                            ContentType = "application/protobuf" // TODO What content type?
                        });
                        return bom;
                    }
                })
                .ToAsyncEnumerable()
                .SingleOrDefaultAsync()
                .Result;
        }

        public Task StoreOriginal(string serialNumber, int version, Stream bomStream, Format format,
            SpecificationVersion specificationVersion)
        {
            var fileName = OriginalBomFilename(serialNumber, version, format, specificationVersion);

            return Observable.FromAsync(async () =>
                {
                    try
                    {
                        await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                        {
                            BucketName = _bucketName,
                            Key = fileName
                        });
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
                        ContentType = MediaTypes.GetMediaType(format)
                    });
                })
                .ToTask();
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

        private int? GetLatestVersion(string serialNumber)
        {
            var versions = GetAllVersions(serialNumber);
            return versions.LastOrDefault();
        }
    }
}