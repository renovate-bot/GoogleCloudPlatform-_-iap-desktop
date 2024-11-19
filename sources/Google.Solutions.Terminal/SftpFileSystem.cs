﻿//
// Copyright 2022 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Solutions.Common.Util;
using Google.Solutions.Mvvm.Controls;
using Google.Solutions.Mvvm.Shell;
using Google.Solutions.Platform.Interop;
using Google.Solutions.Ssh;
using Google.Solutions.Ssh.Native;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#pragma warning disable CS0067 // The event ... is never used

namespace Google.Solutions.Terminal
{
    /// <summary>
    /// Implements IFileSystem on a SFTP channel.
    /// </summary>
    internal sealed class SftpFileSystem : IFileSystem, IDisposable
    {
        private readonly FileTypeCache fileTypeCache;
        private readonly ISftpChannel channel;

        private static readonly Regex configFileNamePattern = new Regex("co?ni?f(ig)?$");

        /// <summary>
        /// Default permissions to apply to new files.
        /// </summary>
        public FilePermissions DefaultFilePermissions { get; set; } =
            FilePermissions.OwnerRead | FilePermissions.OwnerWrite;

        internal FileType TranslateFileType(Libssh2SftpFileInfo sftpFile)
        {
            if (sftpFile.IsDirectory)
            {
                return this.fileTypeCache.Lookup(
                    sftpFile.Name,
                    FileAttributes.Directory,
                    FileType.IconFlags.None);
            }
            else if (sftpFile.Permissions.HasFlag(FilePermissions.SymbolicLink))
            {
                //
                // Treat like an LNK file.
                //
                // NB. We can't tell whether the symlink points to a directory
                // or not, that would require resolving the link. So we treat
                // all symlinks like files.
                //
                return this.fileTypeCache.Lookup(
                    ".lnk",
                    FileAttributes.Normal,
                    FileType.IconFlags.None);
            }
            else if (sftpFile.Permissions.HasFlag(FilePermissions.OwnerExecute) ||
                     sftpFile.Permissions.HasFlag(FilePermissions.GroupExecute) ||
                     sftpFile.Permissions.HasFlag(FilePermissions.OtherExecute))
            {
                //
                // Treat like an EXE file.
                //
                return this.fileTypeCache.Lookup(
                    ".exe",
                    FileAttributes.Normal,
                    FileType.IconFlags.None);
            }
            else if (configFileNamePattern.IsMatch(sftpFile.Name))
            {
                //
                // Treat like an INI file.
                //
                return this.fileTypeCache.Lookup(
                    ".ini",
                    FileAttributes.Normal,
                    FileType.IconFlags.None);
            }
            else
            {
                //
                // Lookup file type using Shell.
                //
                return this.fileTypeCache.Lookup(
                    Win32Filename.IsValidFilename(sftpFile.Name) ? sftpFile.Name : "file",
                    FileAttributes.Normal,
                    FileType.IconFlags.None);
            }
        }

        public SftpFileSystem(ISftpChannel channel)
        {
            this.channel = channel.ExpectNotNull(nameof(channel));
            this.fileTypeCache = new FileTypeCache();
            this.Root = new SftpRootItem();
        }

        //---------------------------------------------------------------------
        // IFileSystem.
        //---------------------------------------------------------------------

        public IFileItem Root { get; }

        public async Task<ObservableCollection<IFileItem>> ListFilesAsync(
            IFileItem directory)
        {
            directory.ExpectNotNull(nameof(directory));
            Debug.Assert(!directory.Type.IsFile);

            var remotePath = directory == this.Root
                ? "/"
                : directory.Path;
            Debug.Assert(!remotePath.StartsWith("//"));

            var sftpFiles = await this.channel
                .ListFilesAsync(remotePath)
                .ConfigureAwait(false);

            //
            // NB. SFTP returns files/directories in arbitrary order.
            //

            var filteredSftpFiles = sftpFiles
                .Where(f => f.Name != "." && f.Name != "..")
                .OrderBy(f => !f.IsDirectory).ThenBy(f => f.Name)
                .Select(f => new SftpFileItem(
                    directory,
                    f,
                    TranslateFileType(f)))
                .ToList();

            return new ObservableCollection<IFileItem>(filteredSftpFiles);
        }

        public Task<Stream> OpenFileAsync(
            IFileItem file,
            FileAccess access)
        {
            Precondition.Expect(file.Type.IsFile, $"{file.Name} is not a file");

            return this.channel.CreateFileAsync(
                file.Path,
                FileMode.Open,
                access,
                FilePermissions.None);
        }

        public Task<Stream> OpenFileAsync(
            IFileItem directory,
            string name,
            FileMode mode,
            FileAccess access)
        {
            Precondition.Expect(!directory.Type.IsFile, $"{directory.Name} is not a directory");
            Precondition.Expect(!name.Contains("/"), "Name must not be a path");

            return this.channel.CreateFileAsync(
                $"{directory.Path}/{name}",
                mode,
                access,
                this.DefaultFilePermissions);
        }

        //---------------------------------------------------------------------
        // IDisposable.
        //---------------------------------------------------------------------

        public void Dispose()
        {
            this.fileTypeCache.Dispose();
            this.channel?.Dispose();
        }

        //---------------------------------------------------------------------
        // Inner classes.
        //---------------------------------------------------------------------

        private class SftpRootItem : IFileItem
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            public string Name
            {
                get => "/";
            }

            public FileAttributes Attributes
            {
                get => FileAttributes.Directory;
            }

            public DateTime LastModified
            {
                get => DateTimeOffset.FromUnixTimeSeconds(0).DateTime;
            }

            public ulong Size
            {
                get => 0;
            }

            public FileType Type
            {
                get => new FileType(
                    "Server",
                    false,
                    StockIcons.GetIcon(StockIcons.IconId.Server, StockIcons.IconSize.Small));
            }

            public bool IsExpanded { get; set; } = true;

            public string Path
            {
                get => string.Empty;
            }
        }

        private class SftpFileItem : IFileItem
        {
            private readonly IFileItem parent;
            private readonly Libssh2SftpFileInfo fileInfo;

            internal SftpFileItem(
                IFileItem parent,
                Libssh2SftpFileInfo fileInfo,
                FileType type)
            {
                this.parent = parent;
                this.fileInfo = fileInfo;
                this.Type = type;
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Path
            {
                get => (this.parent?.Path ?? string.Empty) + "/" + this.Name;
            }

            public string Name
            {
                get => this.fileInfo.Name;
            }

            public DateTime LastModified
            {
                get => this.fileInfo.LastModifiedDate;
            }

            public ulong Size
            {
                get => this.fileInfo.Size;
            }

            public bool IsExpanded { get; set; }

            public FileType Type { get; }

            public FileAttributes Attributes
            {
                get
                {
                    var attributes = FileAttributes.Normal;

                    if (this.fileInfo.IsDirectory)
                    {
                        attributes |= FileAttributes.Directory;
                    }

                    if (this.fileInfo.Name.StartsWith("."))
                    {
                        attributes |= FileAttributes.Hidden;
                    }

                    if (this.fileInfo.Permissions.IsLink())
                    {
                        attributes |= FileAttributes.ReparsePoint;
                    }

                    if (this.fileInfo.Permissions.IsSocket() ||
                        this.fileInfo.Permissions.IsFifo() ||
                        this.fileInfo.Permissions.IsCharacterDevice() ||
                        this.fileInfo.Permissions.IsBlockDevice())
                    {
                        attributes |= FileAttributes.Device;
                    }

                    return attributes;
                }
            }
        }
    }
}