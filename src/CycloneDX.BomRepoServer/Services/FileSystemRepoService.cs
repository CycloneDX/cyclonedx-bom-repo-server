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
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.Models;
using CycloneDX.Protobuf;

namespace CycloneDX.BomRepoServer.Services
{
    public class FileSystemRepoService : IRepoService
    {
        // The InternalStorageVersion is to support future changes to the underlying storage mechanism
        private const int InternalStorageVersion = 1;
        private StorageMetadata _metadata;
        private readonly IFileSystem _fileSystem;
        private readonly FileSystemRepoOptions _repoOptions;

        public FileSystemRepoService(IFileSystem fileSystem, FileSystemRepoOptions repoOptions)
        {
            _fileSystem = fileSystem;
            _repoOptions = repoOptions;
        }

        public IAsyncEnumerable<Bom> RetrieveAllAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetAllVersionsAsync(serialNumber, cancellationToken)
                .SelectAwait(async version => await RetrieveAsync(serialNumber, version, cancellationToken));
        }

        public Task<Bom> RetrieveAsync(string serialNumber, int? version = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(async () =>
            {
                if (!version.HasValue) version = await GetLatestVersion(serialNumber, cancellationToken);
                if (!version.HasValue) return null;

                var filename = BomFilename(serialNumber, version.Value);
                if (!_fileSystem.File.Exists(filename)) return null;
                await using var fs = _fileSystem.FileStream.Create(filename, FileMode.Open,
                    FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                var bom = Serializer.Deserialize(fs);
                return bom;
            }, cancellationToken);
        }

        public Task<Bom> StoreAsync(Bom bom, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(async () =>
            {
                if (string.IsNullOrEmpty(bom.SerialNumber)) bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();

                if (!bom.Version.HasValue)
                {
                    var latestVersion = await GetLatestVersion(bom.SerialNumber, cancellationToken);
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
                    await using var fs = _fileSystem.File.Open(fileName, FileMode.CreateNew, FileAccess.Write);
                    Serializer.Serialize(bom, fs);
                }
                catch (IOException)
                {
                    if (_fileSystem.File.Exists(fileName))
                        throw new BomAlreadyExistsException();
                    throw;
                }

                return bom;
            }, cancellationToken);
        }

        public Task StoreOriginalAsync(string serialNumber, int version, Stream bomStream, SerializationFormat format, SpecificationVersion specificationVersion, CancellationToken cancellationToken = default(CancellationToken))
        {
            var directoryName = BomDirectory(serialNumber, version);
            if (!_fileSystem.Directory.Exists(directoryName)) _fileSystem.Directory.CreateDirectory(directoryName);

            var fileName = OriginalBomFilename(serialNumber, version, format, specificationVersion);

            return Task.Run(async () =>
            {
                try
                {
                    await using var fs = _fileSystem.File.Open(fileName, FileMode.CreateNew,
                        FileAccess.Write);
                    await bomStream.CopyToAsync(fs, cancellationToken);
                }
                catch (IOException)
                {
                    if (_fileSystem.File.Exists(fileName))
                        throw new BomAlreadyExistsException();
                    throw;
                }
            }, cancellationToken);
        }

        public async Task PostConstructAsync()
        {
            if (!_fileSystem.Directory.Exists(_repoOptions.Directory))
                _fileSystem.Directory.CreateDirectory(_repoOptions.Directory);

            var metadataFilename = _fileSystem.Path.Join(_repoOptions.Directory, "storage-metadata");
            if (_fileSystem.File.Exists(metadataFilename))
            {
                var metadataJson = await _fileSystem.File.ReadAllTextAsync(metadataFilename);
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

                await _fileSystem.File.WriteAllTextAsync(metadataFilename, metadataJson);
            }
        }

        public Task<OriginalBom> RetrieveOriginalAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() =>
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
                        var specificationVersion = baseFilename.Substring(firstBreak + 1, lastBreak - firstBreak - 1);

                        if (Enum.TryParse(formatString, true, out SerializationFormat parsedFormat)
                            && Enum.TryParse(specificationVersion, true, out SpecificationVersion parsedSpecificationVersion))
                        {
                            return Task.FromResult(new OriginalBom
                            {
                                Format = parsedFormat,
                                SpecificationVersion = parsedSpecificationVersion,
                                BomStream = _fileSystem.File.Open(file, FileMode.Open, FileAccess.Read)
                            });
                        }
                    }
                }

                return null;
            }, cancellationToken);
        }

        public Task DeleteAllAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() =>
            {
                var directoryName = BomInstanceBaseDirectory(serialNumber);
                _fileSystem.Directory.Delete(directoryName, recursive: true);
            }, cancellationToken);
        }

        public Task DeleteAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() =>
            {
                _fileSystem.Directory.Delete(BomDirectory(serialNumber, version), recursive: true);
                return Task.CompletedTask;
            }, cancellationToken);
        }

        private async Task<int?> GetLatestVersion(string serialNumber, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await GetAllVersionsAsync(serialNumber, cancellationToken)
                .LastOrDefaultAsync(cancellationToken);
        }

        public async IAsyncEnumerable<int> GetAllVersionsAsync(string serialNumber, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            var items = await Task.Run(() =>
            {
                var instanceDirname = BomInstanceBaseDirectory(serialNumber);
                var versions = new List<int>();
                if (_fileSystem.Directory.Exists(instanceDirname))
                {
                    var dirNames = _fileSystem.Directory.GetDirectories(instanceDirname);
                    foreach (var dirname in dirNames)
                    {
                        var dirInfo = new DirectoryInfo(dirname);

                        if (int.TryParse(dirInfo.Name, out int version))
                        {
                            versions.Add(version);
                        }
                    }

                    versions.Sort();
                }

                return versions;
            }, cancellationToken);
            foreach (var item in items)
            {
                yield return item;
            }
        }

        public async IAsyncEnumerable<string> GetAllBomSerialNumbersAsync([EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            var items = await Task.Run(() =>
            {
                if (!_fileSystem.Directory.Exists(BomBaseDirectory()))
                {
                    return null;
                }

                return _fileSystem.Directory.GetDirectories(BomBaseDirectory())
                    .Aggregate(new List<string>(), (accumulator, dirname) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        var dirInfo = new DirectoryInfo(dirname);
                        if (dirInfo.Name.StartsWith("urn_uuid_"))
                            accumulator.Add(dirInfo.Name.Replace("_", ":"));
                        return accumulator;
                    });
            }, cancellationToken);
            if (items is null)
                yield break;
            foreach (var item in items)
            {
                yield return item;
            }
        }

        public Task<DateTime> GetBomAgeAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(_fileSystem.File.GetCreationTimeUtc(BomFilename(serialNumber, version)));
        }

        private string ReplaceInvalidFilepathSegmentCharacters(string filePathSegment)
        {
            // The only invalid character possible is ":" in serial number
            return filePathSegment.Replace(':', '_');
        }

        private string BomBaseDirectory()
        {
            return _fileSystem.Path.Combine(_repoOptions.Directory, $"v{_metadata.InternalStorageVersion}");
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

        private string OriginalBomFilename(string serialNumber, int version, SerializationFormat format, SpecificationVersion specificationVersion)
        {
            return _fileSystem.Path.Combine(BomDirectory(serialNumber, version), $"bom.{specificationVersion}.{format.ToString().ToLowerInvariant()}");
        }
    }
}