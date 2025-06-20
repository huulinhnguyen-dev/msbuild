﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class ZipDirectory_Tests
    {
        private readonly MockEngine _mockEngine = new MockEngine();

        public enum CompressionSupportKind
        {
            NotSupported,
            NotSupportedOnNetFramework,
            Supported,
        }

        [Theory]
        [InlineData(null, CompressionSupportKind.Supported)]
        [InlineData("Optimal", CompressionSupportKind.Supported)]
        [InlineData("Fastest", CompressionSupportKind.Supported)]
        [InlineData("NoCompression", CompressionSupportKind.Supported)]
#if NET
        [InlineData("SmallestSize", CompressionSupportKind.Supported)]
#elif NETFRAMEWORK
        [InlineData("SmallestSize", CompressionSupportKind.NotSupportedOnNetFramework)]
#endif
        [InlineData("RandomUnsupportedValue", CompressionSupportKind.NotSupported)]
        public void CanZipDirectory(string? compressionLevel, CompressionSupportKind compressionSupportKind)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "6DE6060259C44DB6B145159376751C22.txt", "6DE6060259C44DB6B145159376751C22");
                testEnvironment.CreateFile(sourceFolder, "CDA3DD8C25A54A7CAC638A444CB1EAD0.txt", "CDA3DD8C25A54A7CAC638A444CB1EAD0");

                string zipFilePath = Path.Combine(testEnvironment.CreateFolder(createFolder: true).Path, "test.zip");

                ZipDirectory zipDirectory = new ZipDirectory
                {
                    BuildEngine = _mockEngine,
                    CompressionLevel = compressionLevel,
                    DestinationFile = new TaskItem(zipFilePath),
                    SourceDirectory = new TaskItem(sourceFolder.Path),
                };

                zipDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(zipFilePath, customMessage: _mockEngine.Log);

                if (compressionSupportKind == CompressionSupportKind.NotSupported)
                {
                    _mockEngine.Log.ShouldContain("MSB3944", customMessage: _mockEngine.Log);
                }
                else if (compressionSupportKind == CompressionSupportKind.NotSupportedOnNetFramework)
                {
                    _mockEngine.Log.ShouldContain("MSB3945", customMessage: _mockEngine.Log);
                }
                else
                {
                    Assert.Equal(CompressionSupportKind.Supported, compressionSupportKind);

                    // Should not contain any warnings between MSB3941 - MSB3950
                    _mockEngine.Log.ShouldNotContain("MSB394", customMessage: _mockEngine.Log); // Prefix
                    _mockEngine.Log.ShouldNotContain("MSB3950", customMessage: _mockEngine.Log);
                }

                using (FileStream stream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    archive.Entries
                        .Select(i => i.FullName)
                        .ToList()
                        .ShouldBe(
                            [
                                "6DE6060259C44DB6B145159376751C22.txt",
                                "CDA3DD8C25A54A7CAC638A444CB1EAD0.txt"
                            ],
                            ignoreOrder: true);
                }
            }
        }

        [Fact]
        public void CanOverwriteExistingFile()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder sourceFolder = testEnvironment.CreateFolder(createFolder: true);

                testEnvironment.CreateFile(sourceFolder, "F1C22D660B0D4DAAA296C1B980320B03.txt", "F1C22D660B0D4DAAA296C1B980320B03");
                testEnvironment.CreateFile(sourceFolder, "AA825D1CB154492BAA58E1002CE1DFEB.txt", "AA825D1CB154492BAA58E1002CE1DFEB");

                TransientTestFile file = testEnvironment.CreateFile(testEnvironment.DefaultTestDirectory, "test.zip", contents: "test");

                ZipDirectory zipDirectory = new ZipDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new TaskItem(file.Path),
                    Overwrite = true,
                    SourceDirectory = new TaskItem(sourceFolder.Path)
                };

                zipDirectory.Execute().ShouldBeTrue(_mockEngine.Log);

                _mockEngine.Log.ShouldContain(sourceFolder.Path, customMessage: _mockEngine.Log);
                _mockEngine.Log.ShouldContain(file.Path, customMessage: _mockEngine.Log);

                using (FileStream stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    archive.Entries
                        .Select(i => i.FullName)
                        .ToList()
                        .ShouldBe(
                            [
                                "F1C22D660B0D4DAAA296C1B980320B03.txt",
                                "AA825D1CB154492BAA58E1002CE1DFEB.txt"
                            ],
                            ignoreOrder: true);
                }
            }
        }

        [Fact]
        public void LogsErrorIfDestinationExists()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFolder folder = testEnvironment.CreateFolder(createFolder: true);

                TransientTestFile file = testEnvironment.CreateFile("foo.zip", "foo");

                ZipDirectory zipDirectory = new ZipDirectory
                {
                    BuildEngine = _mockEngine,
                    DestinationFile = new TaskItem(file.Path),
                    SourceDirectory = new TaskItem(folder.Path)
                };

                zipDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

                _mockEngine.Log.ShouldContain("MSB3942", customMessage: _mockEngine.Log);
            }
        }

        [Fact]
        public void LogsErrorIfDirectoryDoesNotExist()
        {
            ZipDirectory zipDirectory = new ZipDirectory
            {
                BuildEngine = _mockEngine,
                SourceDirectory = new TaskItem(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            };

            zipDirectory.Execute().ShouldBeFalse(_mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB3941", customMessage: _mockEngine.Log);
        }
    }
}
