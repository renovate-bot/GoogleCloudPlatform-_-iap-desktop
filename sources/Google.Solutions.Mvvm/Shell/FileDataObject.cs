﻿//
// Copyright 2024 Google LLC
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

using Google.Solutions.Common.Interop;
using Google.Solutions.Common.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using UCOMIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Google.Solutions.Mvvm.Shell
{
    /// <summary>
    /// IDataObject that allows handling mutiple virtual files in a 
    /// single operation.
    /// </summary>
    /// <remarks>
    /// DataObject only supports handling a single virtual file 
    /// (using CFSTR_FILEDESCRIPTORW, CFSTR_FILECONTENTS). 
    /// 
    /// This class extends DataObject to support multiple files, inspired by
    /// https://www.codeproject.com/Articles/23139/Transferring-Virtual-Files-to-Windows-Explorer-in
    /// </remarks>
    public sealed class FileDataObject
        : DataObject, UCOMIDataObject, IDisposable
    {
        private readonly IList<Descriptor> files;
        private int currentFile = 0;

        public FileDataObject(IList<Descriptor> files)
        {
            this.files = files;

            //
            // Enable delayed rendering
            //
            SetData(CFSTR_FILEDESCRIPTORW, null);
            SetData(CFSTR_FILECONTENTS, null);
            SetData(CFSTR_PERFORMEDDROPEFFECT, null);
        }

        //----------------------------------------------------------------------
        // Overrides.
        //----------------------------------------------------------------------

        public override object GetData(string format, bool autoConvert)
        {
            if (CFSTR_FILEDESCRIPTORW.Equals(format, StringComparison.OrdinalIgnoreCase))
            {
                //
                // Supply group descriptor for all files.
                //
                base.SetData(
                    CFSTR_FILEDESCRIPTORW, 
                    Descriptor.ToNativeGroupDescriptorStream(this.files));
            }
            else if (CFSTR_FILECONTENTS.Equals(format, StringComparison.OrdinalIgnoreCase))
            {
                //
                // Supply data for the current (!) file.
                //
                base.SetData(
                    CFSTR_FILECONTENTS,
                    this.currentFile < this.files.Count
                        ? this.files[this.currentFile].ContentStream
                        : null);
            }

            return base.GetData(format, autoConvert);
        }

        void UCOMIDataObject.GetData(ref FORMATETC formatetc, out STGMEDIUM medium)
        {
            //
            // NB. The only purpose of overriding this method is to get the
            //     index of the file that is currently being processed. This
            //     information is encoded in the FORMATETC parameter.
            //

            if (formatetc.cfFormat == (short)DataFormats.GetFormat(CFSTR_FILECONTENTS).Id)
            {
                //
                // Cache the index so that we can use it in GetData(format, autoConvert).
                //
                this.currentFile = formatetc.lindex;
            }
            
            //
            // Now it would be good to call base.GetData(...), but that's not possible
            // because the method is an EIMI. Therefore, we replicate its logic here.
            //

            medium = default(STGMEDIUM);
            if (GetTymedUseable(formatetc.tymed))
            {
                if ((formatetc.tymed & TYMED.TYMED_HGLOBAL) != 0)
                {
                    medium.tymed = TYMED.TYMED_HGLOBAL;
                    medium.unionmember = NativeMethods.GlobalAlloc(GHND | GMEM_DDESHARE, 1);
                    if (medium.unionmember == IntPtr.Zero)
                    {
                        throw new OutOfMemoryException();
                    }

                    try
                    {
                        //
                        // Copy data. This will invoke GetData(format, autoConvert), which
                        // in turn uses the cached index to provide the right data.
                        //
                        ((UCOMIDataObject)this).GetDataHere(ref formatetc, ref medium);
                        return;
                    }
                    catch
                    {
                        NativeMethods.GlobalFree(new HandleRef(medium, medium.unionmember));
                        medium.unionmember = IntPtr.Zero;

                        throw;
                    }
                }

                medium.tymed = formatetc.tymed;
                ((UCOMIDataObject)this).GetDataHere(ref formatetc, ref medium);
            }
            else
            {
                Marshal.ThrowExceptionForHR(DV_E_TYMED);
            }

            bool GetTymedUseable(TYMED tymed)
            {
                var allowed = new TYMED[5]
                {
                    TYMED.TYMED_HGLOBAL,
                    TYMED.TYMED_ISTREAM,
                    TYMED.TYMED_ENHMF,
                    TYMED.TYMED_MFPICT,
                    TYMED.TYMED_GDI
                };

                for (int i = 0; i < allowed.Length; i++)
                {
                    if ((tymed & allowed[i]) != 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Dispose()
        {
            foreach (var file in this.files)
            {
                file.ContentStream.Dispose();
            }
        }

        //----------------------------------------------------------------------
        // Inner types.
        //----------------------------------------------------------------------

        /// <summary>
        /// Represents a virtual file and its metadata.
        /// </summary>
        public class Descriptor
        {
            public Descriptor(
                string name, 
                ulong size,
                FileAttributes attributes,
                Stream content)
            {
                Precondition.Expect(
                    !name.Contains("\\"),
                    "Name must not contain a path separator");

                this.Name = name;
                this.Size = size;
                this.Attributes = attributes;
                this.ContentStream = content;
            }

            /// <summary>
            /// File size.
            /// </summary>
            public ulong Size { get; }

            /// <summary>
            /// File name, without path.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// File attributes.
            /// </summary>
            public FileAttributes Attributes { get;  }

            /// <summary>
            /// Stream containing the file contents.
            /// </summary>
            internal Stream ContentStream { get; }

            /// <summary>
            /// File creation time.
            /// </summary>
            public DateTime? CreationTime { get; set; }

            /// <summary>
            /// Last access time.
            /// </summary>
            public DateTime? LastAccessTime { get; set; }

            /// <summary>
            /// Last change time.
            /// </summary>
            public DateTime? LastWriteTime { get; set; }

            /// <summary>
            /// Convert to FILEDESCRIPTORW struct. 
            /// </summary>
            internal FILEDESCRIPTORW ToNativeFileDescriptor()
            {
                var native = new FILEDESCRIPTORW()
                {
                    dwFlags =
                        FD_FILESIZE |
                        FD_PROGRESSUI |
                        FD_UNICODE,
                    cFileName = this.Name,
                    dwFileAttributes = (uint)this.Attributes,
                    nFileSizeHigh = (uint)(this.Size >> 32),
                    nFileSizeLow = (uint)(this.Size & 0xFFFFFFFF),
                };

                if (this.CreationTime != null)
                {
                    var creationTime = this.CreationTime.Value.ToFileTimeUtc();

                    native.dwFlags |= FD_CREATETIME;
                    native.ftCreationTime = new System.Runtime.InteropServices.ComTypes.FILETIME()
                    {
                        dwHighDateTime = (int)(creationTime >> 32),
                        dwLowDateTime = (int)(creationTime & 0xFFFFFFFF),
                    };
                }

                if (this.LastAccessTime != null)
                {
                    var lastAccessTime = this.LastAccessTime.Value.ToFileTimeUtc();

                    native.dwFlags |= FD_ACCESSTIME;
                    native.ftLastAccessTime = new System.Runtime.InteropServices.ComTypes.FILETIME()
                    {
                        dwHighDateTime = (int)(lastAccessTime >> 32),
                        dwLowDateTime = (int)(lastAccessTime & 0xFFFFFFFF),
                    };
                }

                if (this.LastWriteTime != null)
                {
                    var lastWriteTime = this.LastWriteTime.Value.ToFileTimeUtc();

                    native.dwFlags |= FD_WRITESTIME;
                    native.ftLastWriteTime = new System.Runtime.InteropServices.ComTypes.FILETIME()
                    {
                        dwHighDateTime = (int)(lastWriteTime >> 32),
                        dwLowDateTime = (int)(lastWriteTime & 0xFFFFFFFF),
                    };
                }

                return native;
            }

            /// <summary>
            /// Convert to a FILEGROUPDESCRIPTORW, wrapped in a stream.
            /// </summary>
            internal static MemoryStream ToNativeGroupDescriptorStream(
                IList<Descriptor> fileDescriptors)
            {
                //
                // FILEGROUPDESCRIPTORW is a variabe-length struct,
                // so we write it member by member.
                //
                // 1. Write cItems.
                //
                var stream = new MemoryStream();
                stream.Write(BitConverter.GetBytes(fileDescriptors.Count), 0, sizeof(uint));

                //
                // 2. Write fgd[..].
                //
                var structSize = Marshal.SizeOf<FILEDESCRIPTORW>();
                using (var ptr = GlobalAllocSafeHandle.GlobalAlloc((uint)structSize))
                {
                    var buffer = new byte[structSize];

                    for (int i = 0; i < fileDescriptors.Count; i++)
                    {
                        Marshal.StructureToPtr(
                            fileDescriptors[i].ToNativeFileDescriptor(),
                            ptr.DangerousGetHandle(),
                            false);

                        Marshal.Copy(ptr.DangerousGetHandle(), buffer, 0, structSize);
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    return stream;
                }
            }
        }

        //----------------------------------------------------------------------
        // Interop declarations.
        //----------------------------------------------------------------------

        private const uint FD_CLSID = 0x00000001;
        private const uint FD_SIZEPOINT = 0x00000002;
        private const uint FD_ATTRIBUTES = 0x00000004;
        private const uint FD_CREATETIME = 0x00000008;
        private const uint FD_ACCESSTIME = 0x00000010;
        private const uint FD_WRITESTIME = 0x00000020;
        private const uint FD_FILESIZE = 0x00000040;
        private const uint FD_PROGRESSUI = 0x00004000;
        private const uint FD_LINKUI = 0x00008000;
        private const uint FD_UNICODE = 0x80000000;

        internal const string CFSTR_FILEDESCRIPTORW = "FileGroupDescriptorW";
        internal const string CFSTR_FILECONTENTS = "FileContents";
        internal const string CFSTR_PREFERREDDROPEFFECT = "Preferred DropEffect";
        internal const string CFSTR_PERFORMEDDROPEFFECT = "Performed DropEffect";

        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint GMEM_ZEROINIT = 0x0040;
        private const uint GHND = (GMEM_MOVEABLE | GMEM_ZEROINIT);
        private const uint GMEM_DDESHARE = 0x2000;

        public const int DV_E_TYMED = unchecked((int)0x80040069);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct FILEDESCRIPTORW
        {
            public uint dwFlags;

            /// <summary>
            /// The file type identifier.
            /// </summary>
            public Guid clsid;

            /// <summary>
            /// The width and height of the file icon.
            /// </summary>
            public System.Drawing.Size sizel;

            /// <summary>
            /// The screen coordinates of the file object.
            /// </summary>
            public System.Drawing.Point pointl;

            /// <summary>
            /// File attribute flags, in FILE_ATTRIBUTE_ format.
            /// </summary>
            public uint dwFileAttributes;

            /// <summary>
            /// The FILETIME structure that contains the time that the file was last accessed.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;

            /// <summary>
            /// The FILETIME structure that contains the time that the file was last accessed.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;

            /// <summary>
            /// The FILETIME structure that contains the time of the last write operation.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;

            /// <summary>
            /// The high-order DWORD of the file size, in bytes.
            /// </summary>
            public uint nFileSizeHigh;

            /// <summary>
            /// The low-order DWORD of the file size, in bytes.
            /// </summary>
            public uint nFileSizeLow;

            /// <summary>
            /// The null-terminated string that contains the name of the file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", ExactSpelling = true)]
            public static extern IntPtr GlobalAlloc(uint uFlags, int dwBytes);

            [DllImport("kernel32.dll", ExactSpelling = true)]
            public static extern IntPtr GlobalFree(HandleRef handle);
        }
    }
}
