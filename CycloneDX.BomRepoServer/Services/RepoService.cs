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
// Copyright (c) Patrick Dwyer. All Rights Reserved.
    
//TODO need to make use of async methods once suitable methods have been added to the core library
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Services
{
    class StorageMetadata
    {
        public int InternalStorageVersion { get; set; } 
    }

    public class OriginalBom : IDisposable
    {
        public Format Format { get; set; }
        public SchemaVersion SchemaVersion { get; set; }
        public System.IO.Stream BomStream { get; set; }

        public void Dispose() => BomStream.Dispose();
    }
    
    public class RepoService
    {
        private const string InvalidFilePathSegmentCharacters = "<>:\"/\\|?*";

        // The InternalStorageVersion is to support future changes to the underlying storage mechanism
        private const int InternalStorageVersion = 1;
        private readonly StorageMetadata _metadata;
        private readonly IFileSystem _fileSystem;
        private readonly RepoOptions _repoOptions;
        private readonly ILogger _logger;

        public RepoService(IFileSystem fileSystem, RepoOptions repoOptions, ILogger logger = null)
        {
            _fileSystem = fileSystem;
            _repoOptions = repoOptions;
            _logger = logger;

            if (!_fileSystem.Directory.Exists(_repoOptions.Directory))
                _fileSystem.Directory.CreateDirectory(_repoOptions.Directory);

            var metadataFilename = _fileSystem.Path.Join(_repoOptions.Directory, "storage-metadata");
            if (_fileSystem.File.Exists(metadataFilename))
            {
                var metadataJson = _fileSystem.File.ReadAllText(metadataFilename);
                _metadata = JsonSerializer.Deserialize<StorageMetadata>(metadataJson);
            }
            else
            {
                _metadata = new StorageMetadata
                {
                    InternalStorageVersion = InternalStorageVersion
                };
                
                var metadataJson = JsonSerializer.Serialize(
                    _metadata,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    });
                
                _fileSystem.File.WriteAllText(metadataFilename, metadataJson);
            }
        }

        public List<CycloneDX.Models.v1_3.Bom> RetrieveAll(string serialNumber)
        {
            var boms = new List<CycloneDX.Models.v1_3.Bom>();
            var versions = GetAllVersions(serialNumber);
            foreach (var version in versions)
            {
                boms.Add(Retrieve(serialNumber, version));
            }
            return boms;
        }

        public System.IO.Stream RetrieveStream(string serialNumber, int? version = null)
        {
            if (!version.HasValue) version = GetLatestVersion(serialNumber);
            if (!version.HasValue) return null;
            
            var filename = BomFilename(serialNumber, version.Value);
            if (!_fileSystem.File.Exists(filename)) return null;
            
            var fs = _fileSystem.FileStream.Create(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            return fs;
        }

        public CycloneDX.Models.v1_3.Bom Retrieve(string serialNumber, int? version = null)
        {
            if (!version.HasValue) version = GetLatestVersion(serialNumber);
            if (!version.HasValue) return null;
            
            var filename = BomFilename(serialNumber, version.Value);
            if (!_fileSystem.File.Exists(filename)) return null;
            
            using var fs = _fileSystem.FileStream.Create(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var bom = Protobuf.Deserializer.Deserialize(fs);
            return bom;
        }
        
        public CycloneDX.Models.v1_3.Bom Store(CycloneDX.Models.v1_3.Bom bom)
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
            if (!_fileSystem.Directory.Exists(directoryName)) _fileSystem.Directory.CreateDirectory(directoryName);
        
            var fileName = BomFilename(bom.SerialNumber, bom.Version.Value);
            
            try
            {
                using var fs = _fileSystem.File.Open(fileName, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write);
                Protobuf.Serializer.Serialize(fs, bom);
            }
            catch (System.IO.IOException)
            {
                if (_fileSystem.File.Exists(fileName))
                    throw new BomAlreadyExistsException();
                throw;
            }

            return bom;
        }
        
        public async Task StoreOriginal(string serialNumber, int version, System.IO.Stream bomStream, Format format, SchemaVersion schemaVersion)
        {
            var directoryName = BomDirectory(serialNumber, version);
            if (!_fileSystem.Directory.Exists(directoryName)) _fileSystem.Directory.CreateDirectory(directoryName);
        
            var fileName = OriginalBomFilename(serialNumber, version, format, schemaVersion);
            
            try
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
            }
        }
        
        public OriginalBom RetrieveOriginal(string serialNumber, int version)
        {
            var directoryName = BomDirectory(serialNumber, version);
            if (!_fileSystem.Directory.Exists(directoryName)) _fileSystem.Directory.CreateDirectory(directoryName);

            foreach (var file in _fileSystem.Directory.GetFiles(BomDirectory(serialNumber, version), "bom.*"))
            {
                if (!file.EndsWith(".cdx"))
                {
                    var baseFilename = _fileSystem.Path.GetFileName(file);
                    var firstBreak = baseFilename.IndexOf(".", StringComparison.InvariantCulture);
                    var lastBreak = baseFilename.LastIndexOf(".", StringComparison.InvariantCulture);
                    
                    var formatString = baseFilename.Substring(lastBreak + 1);
                    var schemaVersion = baseFilename.Substring(firstBreak + 1, lastBreak - firstBreak - 1);

                    if (Format.TryParse(formatString, true, out Format parsedFormat)
                        && SchemaVersion.TryParse(schemaVersion, true, out SchemaVersion parsedSchemaVersion))
                    {
                        return new OriginalBom
                        {
                            Format = parsedFormat,
                            SchemaVersion = parsedSchemaVersion,
                            BomStream = _fileSystem.File.Open(file, System.IO.FileMode.Open, System.IO.FileAccess.Read)
                        };
                    }
                }
            }

            return null;
        }
        
        public void DeleteAll(string serialNumber)
        {
            var directoryName = BomInstanceBaseDirectory(serialNumber);
            _fileSystem.Directory.Delete(directoryName, recursive: true);
        }
        
        public void Delete(string serialNumber, int version)
        {
            _fileSystem.Directory.Delete(BomDirectory(serialNumber, version), recursive: true);
        }

        private int? GetLatestVersion(string serialNumber)
        {
            var versions = GetAllVersions(serialNumber);
            return versions.LastOrDefault();
        }

        public IEnumerable<int> GetAllVersions(string serialNumber)
        {
            var instanceDirname = BomInstanceBaseDirectory(serialNumber);
            var versions = new List<int>();
            if (_fileSystem.Directory.Exists(instanceDirname))
            {
                var dirNames = _fileSystem.Directory.GetDirectories(instanceDirname);
                foreach (var dirname in dirNames)
                {
                    var dirInfo = new System.IO.DirectoryInfo(dirname);

                    if (int.TryParse(dirInfo.Name, out int version))
                    {
                        versions.Add(version);
                    }
                }
                versions.Sort();
            }
            return versions;
        }

        public IEnumerable<string> GetAllBomSerialNumbers()
        {
            if (_fileSystem.Directory.Exists(BomBaseDirectory()))
            {
                var dirNames = _fileSystem.Directory.GetDirectories(BomBaseDirectory());
                foreach (var dirname in dirNames)
                {
                    var dirInfo = new System.IO.DirectoryInfo(dirname);
                    if (dirInfo.Name.StartsWith("urn_uuid_"))
                        yield return dirInfo.Name.Replace("_", ":");
                }
            }
        }

        public DateTime GetBomAge(string serialNumber, int version)
        {
            return _fileSystem.File.GetCreationTimeUtc(BomFilename(serialNumber, version));
        }

        private string ReplaceInvalidFilepathSegmentCharacters(string filePathSegment)
        {
            // The only invalid character possible is ":" in serial number
            return filePathSegment.Replace(':', '_');
        }

        private string BomBaseDirectory()
        {
            return _fileSystem.Path.Combine(_repoOptions.Directory, $"v{InternalStorageVersion}");
        }

        private string BomInstanceBaseDirectory(string serialNumber)
        {
            return _fileSystem.Path.Combine(
                BomBaseDirectory(),
                ReplaceInvalidFilepathSegmentCharacters(serialNumber));
        }

        private string BomDirectory(string serialNumber, int version)
        {
            return _fileSystem.Path.Combine(
                BomInstanceBaseDirectory(serialNumber),
                version.ToString());
        }

        private string BomFilename(string serialNumber, int version)
        {
            return _fileSystem.Path.Combine(BomDirectory(serialNumber, version), "bom.cdx");
        }
        
        private string OriginalBomFilename(string serialNumber, int version, Format format, SchemaVersion schemaVersion)
        {
            return _fileSystem.Path.Combine(BomDirectory(serialNumber, version), $"bom.{schemaVersion}.{format.ToString().ToLowerInvariant()}");
        }
    }
}