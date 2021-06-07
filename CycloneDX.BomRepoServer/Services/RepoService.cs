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
    
    public class RepoService
    {
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

        public CycloneDX.Models.v1_3.Bom RetrieveLatest(string serialNumber)
        {
            var version = GetLatestVersion(serialNumber);
            if (!version.HasValue) return null;

            return Retrieve(serialNumber, version.Value);
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

        public CycloneDX.Models.v1_3.Bom Retrieve(string serialNumber, int version)
        {
            var filename = BomFilename(serialNumber, version);
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
        
            var directoryName = BomDirectory(bom.SerialNumber);
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
        
        public void DeleteAll(string serialNumber)
        {
            var directoryName = BomDirectory(serialNumber);
            _fileSystem.Directory.Delete(directoryName, recursive: true);
        }
        
        public void Delete(string serialNumber, int version)
        {
            var fileName = BomFilename(serialNumber, version);
            _fileSystem.File.Delete(BomFilename(serialNumber, version));
        }

        private int? GetLatestVersion(string serialNumber)
        {
            var versions = GetAllVersions(serialNumber);
            return versions.LastOrDefault();
        }

        private IEnumerable<int> GetAllVersions(string serialNumber)
        {
            var directoryName = BomDirectory(serialNumber);
            var versions = new List<int>();
            
            if (_fileSystem.Directory.Exists(directoryName))
            {
                var filenames = _fileSystem.Directory.GetFiles(directoryName);
                foreach (var filename in filenames)
                {
                    int version;
                    if (int.TryParse(_fileSystem.Path.GetFileName(filename), out version))
                    {
                        versions.Add(version);
                    }
                }
            }

            versions.Sort();
            return versions;
        }

        public IEnumerable<string> GetAllBomSerialNumbers()
        {
            if (_fileSystem.Directory.Exists(BomBaseDirectory()))
            {
                var dirnames = _fileSystem.Directory.GetDirectories(BomBaseDirectory());
                foreach (var dirname in dirnames)
                {
                    var dirinfo = new System.IO.DirectoryInfo(dirname);
                    if (dirinfo.Name.StartsWith("urn_uuid_"))
                        yield return dirinfo.Name.Replace("_", ":");
                }
            }
        }

        private string BomBaseDirectory()
        {
            return _fileSystem.Path.Combine(_repoOptions.Directory, $"v{InternalStorageVersion}");
        }

        private string BomDirectory(string serialNumber)
        {
            // replace : with _ for Windows file systems
            return _fileSystem.Path.Combine(BomBaseDirectory(), serialNumber.Replace(':', '_'));
        }

        private string BomFilename(string serialNumber, int version)
        {
            return _fileSystem.Path.Combine(BomDirectory(serialNumber), version.ToString());
        }
    }
}