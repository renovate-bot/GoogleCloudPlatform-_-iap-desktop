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

using Google.Solutions.Mvvm.Shell;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace Google.Solutions.Mvvm.Controls
{
    public interface IFileSystem : IDisposable
    {
        /// <summary>
        /// Root of the file system.
        /// </summary>
        IFileItem Root { get; }

        /// <summary>
        /// List files and directory. 
        /// Returns an empty collection if the directory is empty.
        /// </summary>
        Task<ObservableCollection<IFileItem>> ListFilesAsync(IFileItem directory);

        /// <summary>
        /// Open a file for reading or writing.
        /// </summary>
        /// <param name="directory">Parent directory</param>
        /// <param name="name">Name of the file</param>
        /// <param name="mode">Defines whether to create or open a file</param>
        /// <param name="access">Type of access</param>
        Task<Stream> OpenFileAsync(
            IFileItem directory,
            string name,
            FileMode mode,
            FileAccess access);

        /// <summary>
        /// Open a file for reading or writing.
        /// </summary>
        /// <param name="file">File to open</param>
        /// <param name="access">Type of access</param>
        Task<Stream> OpenFileAsync(
            IFileItem file,
            FileAccess access);
    }

    /// <summary>
    /// A file or directory.
    /// </summary>
    public interface IFileItem : INotifyPropertyChanged
    {
        /// <summary>
        /// Absolute path of file. Empty for the root.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Unqualified name of file.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// File attributes.
        /// </summary>
        FileAttributes Attributes { get; }

        /// <summary>
        /// Time of last access, in UTC.
        /// </summary>
        DateTime LastModified { get; }

        /// <summary>
        /// Size of file.
        /// </summary>
        ulong Size { get; }

        /// <summary>
        /// Type information.
        /// </summary>
        FileType Type { get; }

        /// <summary>
        /// Access information, this can be in Unix-style "rwx"
        /// format or in some other summary format.
        /// </summary>
        string Access { get; }

        /// <summary>
        /// Gets or sets the expansion state.
        /// </summary>
        bool IsExpanded { get; set; }
    }
}
