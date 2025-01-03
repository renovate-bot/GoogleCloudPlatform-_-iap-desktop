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
using Google.Solutions.IapDesktop.Application.Theme;
using Google.Solutions.IapDesktop.Core.ObjectModel;
using Google.Solutions.Platform.Interop;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Google.Solutions.IapDesktop.Application.Windows.Dialog
{
    public interface ICredentialDialog
    {
        /// <summary>
        /// Prompt for Windows credential using the CredUI API.
        /// </summary>
        DialogResult PromptForWindowsCredentials(
            IWin32Window? owner,
            CredentialDialogParameters parameters,
            out bool save,
            out NetworkCredential? credential);

        /// <summary>
        /// Prompt for a username. There's no CredUI counterpart to this.
        /// </summary>
        DialogResult PromptForUsername(
            IWin32Window? owner,
            string caption,
            string message,
            out string? username);
    }

    public struct CredentialDialogParameters
    {
        /// <summary>
        /// Caption to show in dialog.
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// Message to show in dialog.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Authentication package.
        /// </summary>
        public AuthenticationPackage Package { get; set; }

        /// <summary>
        /// Display checkbox to save credentials.
        /// </summary>
        public bool ShowSaveCheckbox { get; set; }

        /// <summary>
        /// Credential to pre-populate the dialog with.
        /// </summary>
        public NetworkCredential? InputCredential { get; set; }
    }

    public enum AuthenticationPackage
    {
        Ntlm,
        Kerberos,
        Negoriate,
        Any
    }

    public class CredentialDialog : ICredentialDialog
    {
        private readonly Service<ISystemDialogTheme> theme;

        public CredentialDialog(Service<ISystemDialogTheme> theme)
        {
            this.theme = theme.ExpectNotNull(nameof(theme));
        }

        public DialogResult PromptForWindowsCredentials(
            IWin32Window? owner,
            CredentialDialogParameters parameters,
            out bool save,
            out NetworkCredential? credential)
        {
            var uiInfo = new NativeMethods.CREDUI_INFO()
            {
                cbSize = Marshal.SizeOf<NativeMethods.CREDUI_INFO>(),
                hwndParent = owner?.Handle ?? IntPtr.Zero,
                pszCaptionText = parameters.Caption,
                pszMessageText = parameters.Message
            };

            using (var packedInCredential = new PackedCredential(
                parameters.InputCredential ?? new NetworkCredential()))
            {
                var packageId = LookupAuthenticationPackageId(parameters.Package);
                var saveCheckboxState = false;
                var flags = NativeMethods.CREDUIWIN_FLAGS.AUTHPACKAGE_ONLY;

                if (parameters.ShowSaveCheckbox)
                {
                    flags |= NativeMethods.CREDUIWIN_FLAGS.CHECKBOX;
                }

                var error = NativeMethods.CredUIPromptForWindowsCredentials(
                    ref uiInfo,
                    0,
                    ref packageId,
                    packedInCredential.Handle,
                    packedInCredential.Size,
                    out var outAuthBuffer,
                    out var outAuthBufferSize,
                    ref saveCheckboxState,
                    flags);

                if (error == NativeMethods.ERROR_CANCELLED)
                {
                    credential = null;
                    save = false;
                    return DialogResult.Cancel;
                }
                else if (error != NativeMethods.ERROR_NOERROR)
                {
                    throw new Win32Exception(error);
                }

                using (var packedOutCredential = new PackedCredential(
                    outAuthBuffer,
                    outAuthBufferSize))
                {
                    credential = packedOutCredential.Unpack();
                    save = saveCheckboxState;
                    return DialogResult.OK;
                }
            }
        }

        internal static uint LookupAuthenticationPackageId(AuthenticationPackage package)
        {
            if (package == AuthenticationPackage.Any)
            {
                return 0;
            }

            using (var lsa = Lsa.ConnectUntrusted())
            {
                var packageName = package switch
                {
                    AuthenticationPackage.Ntlm => Lsa.MSV1_0_PACKAGE_NAME,
                    AuthenticationPackage.Kerberos => Lsa.MICROSOFT_KERBEROS_NAME_A,
                    AuthenticationPackage.Negoriate => Lsa.NEGOSSP_NAME_A,
                    _ => throw new ArgumentException(nameof(package)),
                };

                return lsa.LookupAuthenticationPackage(packageName);
            }
        }

        public DialogResult PromptForUsername(
            IWin32Window? owner,
            string caption,
            string message,
            out string? username)
        {
            using (var dialog = new SystemInputDialog(
                new InputDialogParameters()
                {
                    Title = "Security",
                    Caption = caption,
                    Message = message,
                    Cue = "User name"
                }))
            {
                try
                {
                    this.theme.Activate().ApplyTo(dialog);
                }
                catch (UnknownServiceException)
                { }

                var result = dialog.ShowDialog(owner);
                username = dialog.Value;
                return result;
            }
        }


        //---------------------------------------------------------------------
        // P/Invoke.
        //---------------------------------------------------------------------

        internal class PackedCredential : IDisposable
        {
            private readonly CoTaskMemAllocSafeHandle buffer;

            public IntPtr Handle => this.buffer.DangerousGetHandle();
            public uint Size { get; }

            public PackedCredential(
                CoTaskMemAllocSafeHandle buffer,
                uint bufferSize)
            {
                this.buffer = buffer;
                this.Size = bufferSize;
            }

            public PackedCredential(NetworkCredential inputCredential)
            {
                var username = inputCredential.UserName;
                var password = inputCredential.Password;

                if (!string.IsNullOrEmpty(inputCredential.Domain) &&
                    !string.IsNullOrEmpty(username) &&
                    !username.Contains("\\") &&
                    !username.Contains("@"))
                {
                    //
                    // The input credential specifies a domain. 
                    // CredPackAuthenticationBuffer doesn't let us
                    // pass that domain, so we need to
                    // prepend it to the username in NetBIOS format.
                    //
                    // NB. If the username also contains a domain
                    //     (domain\user or user@domain), then we let
                    //     that take precedence.
                    //
                    username = $"{inputCredential.Domain}\\{username}";
                }

                uint bufferSize = 0;
                if (!NativeMethods.CredPackAuthenticationBuffer(
                    NativeMethods.CRED_PACK_PROTECTED_CREDENTIALS,
                    username,
                    password,
                    IntPtr.Zero,
                    ref bufferSize) && bufferSize == 0)
                {
                    throw new Win32Exception();
                }

                this.Size = bufferSize;
                this.buffer = CoTaskMemAllocSafeHandle.Alloc((int)this.Size);

                if (!NativeMethods.CredPackAuthenticationBuffer(
                    NativeMethods.CRED_PACK_PROTECTED_CREDENTIALS,
                    username,
                    password,
                    this.buffer.DangerousGetHandle(),
                    ref bufferSize))
                {
                    this.buffer.Dispose();
                    throw new Win32Exception();
                }
            }

            public NetworkCredential Unpack()
            {
                var usernameBuffer = new StringBuilder(256);
                var passwordBuffer = new StringBuilder(256);
                var domainBuffer = new StringBuilder(256);

                var usernameLength = usernameBuffer.Capacity;
                var passwordLength = passwordBuffer.Capacity;
                var domainLength = domainBuffer.Capacity;

                if (!NativeMethods.CredUnPackAuthenticationBuffer(
                    NativeMethods.CRED_PACK_PROTECTED_CREDENTIALS,
                    this.buffer,
                    this.Size,
                    usernameBuffer,
                    ref usernameLength,
                    domainBuffer,
                    ref domainLength,
                    passwordBuffer,
                    ref passwordLength))
                {
                    throw new Win32Exception();
                }

                return new NetworkCredential(
                    usernameBuffer.ToString(),
                    passwordBuffer.ToString(),
                    domainBuffer.ToString());
            }

            public void Dispose()
            {
                this.buffer.Dispose();
            }
        }

        private static class NativeMethods
        {
            public const int ERROR_NOERROR = 0;
            public const int ERROR_CANCELLED = 1223;

            public const uint CRED_PACK_PROTECTED_CREDENTIALS = 0x1;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CREDUI_INFO
            {
                public int cbSize;
                public IntPtr hwndParent;
                public string pszMessageText;
                public string pszCaptionText;
                public IntPtr hbmBanner;
            }

            [Flags]
            public enum CREDUIWIN_FLAGS
            {
                GENERIC = 0x1,
                CHECKBOX = 0x2,
                AUTHPACKAGE_ONLY = 0x10,
                IN_CRED_ONLY = 0x20,
                ENUMERATE_ADMINS = 0x100,
                ENUMERATE_CURRENT_USER = 0x200,
                SECURE_PROMPT = 0x1000,
                PACK_32_WOW = 0x10000000,
            }

            [DllImport("credui.dll", CharSet = CharSet.Unicode)]
            public static extern int CredUIPromptForWindowsCredentials(
                ref CREDUI_INFO uiInfo,
                int authError,
                ref uint authPackage,
                IntPtr inAuthBuffer,
                uint inAuthBufferSize,
                out CoTaskMemAllocSafeHandle outAuthBuffer,
                out uint outAuthBufferSize,
                ref bool save,
                CREDUIWIN_FLAGS flags);

            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredPackAuthenticationBuffer(
                uint dwFlags,
                [MarshalAs(UnmanagedType.LPWStr)] string pszUserName,
                [MarshalAs(UnmanagedType.LPWStr)] string pszPassword,
                IntPtr pPackedCredentials,
                ref uint pcbPackedCredentials);

            [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredUnPackAuthenticationBuffer(
                uint dwFlags,
                CoTaskMemAllocSafeHandle pAuthBuffer,
                uint cbAuthBuffer,
                StringBuilder pszUserName,
                ref int pcchMaxUserName,
                StringBuilder pszDomainName,
                ref int pcchMaxDomainame,
                StringBuilder pszPassword,
                ref int pcchMaxPassword);
        }

        //---------------------------------------------------------------------
        // Helper class for LSA API.
        //---------------------------------------------------------------------

        internal sealed class Lsa : IDisposable
        {
            public const string MSV1_0_PACKAGE_NAME = "MICROSOFT_AUTHENTICATION_PACKAGE_V1_0";
            public const string MICROSOFT_KERBEROS_NAME_A = "Kerberos";
            public const string NEGOSSP_NAME_A = "Negotiate";

            private readonly LsaSafeHandle handle;

            private Lsa(LsaSafeHandle handle)
            {
                this.handle = handle;
            }

            public static Lsa ConnectUntrusted()
            {
                var status = NativeMethods.LsaConnectUntrusted(out var handle);
                if (status == 0 && handle != null)
                {
                    return new Lsa(handle);
                }
                else
                {
                    throw new Win32Exception(
                        NativeMethods.LsaNtStatusToWinError(status));
                }
            }

            public void Dispose()
            {
                this.handle.Dispose();
            }

            public uint LookupAuthenticationPackage(string packageName)
            {
                using (var packageNameHandle = CoTaskMemAllocSafeHandle.Alloc(packageName))
                {
                    var nativePackageName = new NativeMethods.LSA_STRING
                    {
                        Buffer = packageNameHandle.DangerousGetHandle(),
                        Length = (ushort)packageName.Length,
                        MaximumLength = (ushort)packageName.Length
                    };

                    var status = NativeMethods.LsaLookupAuthenticationPackage(
                        this.handle,
                        ref nativePackageName,
                        out var package);

                    if (status == 0)
                    {
                        return package;
                    }
                    else
                    {
                        throw new Win32Exception(
                            NativeMethods.LsaNtStatusToWinError(status));
                    }
                }
            }

            private class NativeMethods
            {
                public struct LSA_STRING
                {
                    public ushort Length;
                    public ushort MaximumLength;
                    public /*PCHAR*/ IntPtr Buffer;
                }

                [DllImport("secur32.dll", SetLastError = false)]
                public static extern uint LsaConnectUntrusted([Out] out LsaSafeHandle LsaHandle);

                [DllImport("secur32.dll", SetLastError = false)]
                public static extern uint LsaDeregisterLogonProcess([In] IntPtr LsaHandle);

                [DllImport("advapi32.dll", SetLastError = false)]
                public static extern int LsaNtStatusToWinError(uint status);

                [DllImport("secur32.dll", SetLastError = false)]
                public static extern uint LsaLookupAuthenticationPackage(
                    [In] LsaSafeHandle LsaHandle,
                    [In] ref LSA_STRING PackageName,
                    [Out] out uint AuthenticationPackage);
            }

            private class LsaSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
            {
                public LsaSafeHandle() : base(true)
                {
                }

                protected override bool ReleaseHandle()
                {
                    return NativeMethods.LsaDeregisterLogonProcess(this.handle) == 0;
                }
            }
        }
    }
}
