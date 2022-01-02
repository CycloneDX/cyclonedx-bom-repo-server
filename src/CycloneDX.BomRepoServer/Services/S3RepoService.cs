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

//TODO need to make use of async methods once suitable methods have been added to the core library
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.Models.v1_3;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;

namespace CycloneDX.BomRepoServer.Services
{
    class S3RepoService : IRepoService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private const int InternalStorageVersion = 1;

        public S3RepoService(IAmazonS3 s3Client)
        {
            this._s3Client = s3Client;
            this._bucketName = "bomserver"; // TODO Configurable
        }

        public void Delete(string serialNumber, int version)
        {
            throw new NotImplementedException();
        }

        public void DeleteAll(string serialNumber)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetAllBomSerialNumbers()
        {
            return _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request{
                    BucketName = "bomserver"
                })
                .S3Objects
                .Where(s3Object => s3Object.Key.StartsWith("urn_uuid_"))
                .Select(s3Object => s3Object.Key.Replace("_", ":"))
                .ToEnumerable();
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

        public IEnumerable<int> GetAllVersions(string serialNumber)
        {
            var instanceDirname = BomInstanceBaseDirectory(serialNumber);
            return _s3Client
                .Paginators
                .ListObjectsV2(new ListObjectsV2Request{
                    BucketName = this._bucketName,
                    Prefix = instanceDirname,

                })
                .S3Objects
                .Where((s3Object) => s3Object.Key.EndsWith("bom.cdx"))
                .Select((s3Object) => {
                    // TODO There are probably better ways to do this
                    var segments = s3Object.Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    int.TryParse(segments[^2], out int result);
                    return result;
                })
                .ToEnumerable();
            
        }

        public DateTime GetBomAge(string serialNumber, int version)
        {
            throw new NotImplementedException();
        }

        public Bom Retrieve(string serialNumber, int? version = null)
        {
            if (!version.HasValue) version = GetLatestVersion(serialNumber);
            if (!version.HasValue) return null;

            var filename = BomFilename(serialNumber, version.Value);
            var response = this._s3Client.GetObjectAsync(new GetObjectRequest() {
                BucketName = this._bucketName,
                Key = filename,

            });
            var result = response.GetAwaiter().GetResult();
            using var responseStream = result.ResponseStream;
            var bom = Protobuf.Deserializer.Deserialize(responseStream);
            return bom;
        }

        public List<Bom> RetrieveAll(string serialNumber)
        {
            var boms = new List<CycloneDX.Models.v1_3.Bom>();
            var versions = GetAllVersions(serialNumber);
            foreach (var version in versions)
            {
                boms.Add(Retrieve(serialNumber, version));
            }
            return boms;
        }

        public OriginalBom RetrieveOriginal(string serialNumber, int version)
        {
            throw new NotImplementedException();
        }

        public Stream RetrieveStream(string serialNumber, int? version = null)
        {
            throw new NotImplementedException();
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
            return $"{BomDirectory(serialNumber, version)}/bom.{specificationVersion}.{format.ToString().ToLowerInvariant()}";
            //return _fileSystem.Path.Combine(BomDirectory(serialNumber, version), $"bom.{specificationVersion}.{format.ToString().ToLowerInvariant()}");
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
            
            using (var stream = new MemoryStream()) {
                Protobuf.Serializer.Serialize(bom, stream);
                _s3Client.PutObjectAsync(new PutObjectRequest{
                    BucketName = this._bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = "application/protobuf" // TODO What content type?
                })
                .GetAwaiter()
                .GetResult();
            }

            return bom;
        }

        private int? GetLatestVersion(string serialNumber)
        {
            var versions = GetAllVersions(serialNumber);
            return versions.LastOrDefault();
        }

        public Task StoreOriginal(string serialNumber, int version, Stream bomStream, Format format, SpecificationVersion specificationVersion)
        {
            var directoryName = BomDirectory(serialNumber, version);
            var fileName = OriginalBomFilename(serialNumber, version, format, specificationVersion);

            // TODO Check if object already exists
            return _s3Client.PutObjectAsync(new PutObjectRequest() {
                BucketName = this._bucketName,
                InputStream = bomStream,
                Key = fileName,
                ContentType = "application/protobuf" // TODO Correct format
            });
            /*try
            {
                using var fs = _fileSystem.File.Open(fileName, System.IO.FileMode.CreateNew,
                    System.IO.FileAccess.Write);
                await bomStream.CopyToAsync(fs);
            }
            catch (System.IO.IOException)
            {
                if (_fileSystem.File.Exists(fileName))
                    throw new BomAlreadyExistsException();
                throw;
            }*/
        }
    }
}