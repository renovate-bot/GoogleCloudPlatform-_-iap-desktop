﻿//
// Copyright 2020 Google LLC
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

using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Util;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ProjectModel;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol.Rdp;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol.Ssh;
using Google.Solutions.Settings;
using Google.Solutions.Settings.Collection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;

#pragma warning disable CA1027 // Mark enums with FlagsAttribute

namespace Google.Solutions.IapDesktop.Extensions.Session.Settings
{
    public class ConnectionSettings : ISettingsCollection
    {
        /// <summary>
        /// Resource (instance, zone, project) that these settings apply to.
        /// </summary>
        public ComputeEngineLocator Resource { get; }

        /// <summary>
        /// Create new empty settings.
        /// </summary>
        /// <param name="resource"></param>
        internal ConnectionSettings(ComputeEngineLocator resource)
            : this(resource, new DictionarySettingsStore(new Dictionary<string, string>()))
        {
        }

        public ConnectionSettings(
            ComputeEngineLocator resource,
            ISettingsStore store)
        {
            this.Resource = resource.ExpectNotNull(nameof(resource));

            //
            // RDP Settings.
            //
            this.RdpUsername = store.Read<string?>(
                "Username",
                "Username",
                "Username of a local user, SAM account name of a domain user, or UPN (user@domain).",
                Categories.WindowsCredentials,
                null,
                _ => true);
            this.RdpPassword = store.Read<SecureString?>(
                "Password",
                "Password",
                "Windows logon password.",
                Categories.WindowsCredentials,
                null);
            this.RdpDomain = store.Read<string?>(
                "Domain",
                "Domain",
                "NetBIOS domain name or computer name. Leave blank when using UPN as username.",
                Categories.WindowsCredentials,
                null,
                _ => true);
            this.RdpConnectionBar = store.Read<RdpConnectionBarState>(
                "ConnectionBar",
                "Connection bar",
                "Show connection bar in full-screen mode.",
                Categories.RdpDisplay,
                RdpConnectionBarState._Default);
            this.RdpAuthenticationLevel = store.Read<RdpAuthenticationLevel>(
                "AuthenticationLevel",
                null,
                null,
                null,
                Protocol.Rdp.RdpAuthenticationLevel._Default);
            this.RdpColorDepth = store.Read<RdpColorDepth>(
                "ColorDepth",
                "Color depth",
                "Color depth of remote desktop.",
                Categories.RdpDisplay,
                Protocol.Rdp.RdpColorDepth._Default);
            this.RdpAudioMode = store.Read<RdpAudioMode>(
                "AudioMode",
                "Audio mode",
                "Redirect audio when playing on server.",
                Categories.RdpResources,
                Protocol.Rdp.RdpAudioMode._Default);
            this.RdpAutomaticLogon = store.Read<RdpAutomaticLogon>(
                "RdpUserAuthenticationBehavior",
                "Automatic logon",
                "Log on automatically using saved credentials if possible. Disable if the VM " +
                    "is configured to always prompt for passwords upon connection (a server-side " +
                    "group policy)",
                Categories.RdpSecurity,

                //
                // Adjust default based on local RDP policy. If the local policy
                // disables saving, then there's a good chance that VMs might be
                // configured to do the same.
                //
                // We don't treat this as a policy though, so users can change
                // the setting.
                //

                LocalRdpPolicy.IsPasswordSavingDisabled
                    ? Protocol.Rdp.RdpAutomaticLogon.Disabled
                    : Protocol.Rdp.RdpAutomaticLogon._Default);
            this.RdpNetworkLevelAuthentication = store.Read<RdpNetworkLevelAuthentication>(
                "NetworkLevelAuthentication",
                "Network level authentication",
                "Secure connection using network level authentication (NLA). " +
                    "Disable NLA only if the server uses a custom credential service provider." +
                    "Disabling NLA automatically enables server authentication.",
                Categories.RdpSecurity,
                Protocol.Rdp.RdpNetworkLevelAuthentication._Default);
            this.RdpConnectionTimeout = store.Read<int>(
                "ConnectionTimeout",
                "Connection timeout",
                "Timeout for establishing a Remote Desktop connection, in seconds. " +
                    "Use a timeout that allows sufficient time for credential prompts.",
                Categories.RdpConnection,
                (int)RdpParameters.DefaultConnectionTimeout.TotalSeconds,
                Predicate.InRange(0, 300));
            this.RdpPort = store.Read<int>(
                "RdpPort",
                "Server port",
                "Server port.",
                Categories.RdpConnection,
                RdpParameters.DefaultPort,
                Predicate.InRange(1, ushort.MaxValue));
            this.RdpTransport = store.Read<SessionTransportType>(
                "TransportType",
                "Connect via",
                $"Type of transport. Use {SessionTransportType.IapTunnel} unless " +
                    "you need to connect to a VM's internal IP address via " +
                    "Cloud VPN or Interconnect.",
                Categories.RdpConnection,
                SessionTransportType._Default);
            this.RdpRedirectClipboard = store.Read<RdpRedirectClipboard>(
                "RedirectClipboard",
                "Redirect clipboard",
                "Allow clipboard contents to be shared with remote desktop.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectClipboard._Default);
            this.RdpRedirectPrinter = store.Read<RdpRedirectPrinter>(
                "RdpRedirectPrinter",
                "Redirect printers",
                "Share local printers with remote desktop.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectPrinter._Default);
            this.RdpRedirectSmartCard = store.Read<RdpRedirectSmartCard>(
                "RdpRedirectSmartCard",
                "Redirect smart cards",
                "Share local smart carrds with remote desktop.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectSmartCard._Default);
            this.RdpRedirectPort = store.Read<RdpRedirectPort>(
                "RdpRedirectPort",
                "Redirect local ports",
                "Share local ports (COM, LPT) with remote desktop.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectPort._Default);
            this.RdpRedirectDrive = store.Read<RdpRedirectDrive>(
                "RdpRedirectDrive",
                "Redirect drives",
                "Share local drives with remote desktop.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectDrive._Default);
            this.RdpRedirectDevice = store.Read<RdpRedirectDevice>(
                "RdpRedirectDevice",
                "Redirect devices",
                "Share local devices with remote desktop.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectDevice._Default);
            this.RdpRedirectWebAuthn = store.Read<RdpRedirectWebAuthn>(
                "RdpRedirectWebAuthn",
                "Redirect WebAuthn authenticators",
                "Use local security key or Windows Hello device for WebAuthn authentication.",
                Categories.RdpResources,
                Protocol.Rdp.RdpRedirectWebAuthn._Default);
            this.RdpHookWindowsKeys = store.Read<RdpHookWindowsKeys>(
                "RdpHookWindowsKeys",
                "Apply Windows shortcuts",
                "Enable Windows shortcuts (like Win+R)",
                Categories.RdpResources,
                Protocol.Rdp.RdpHookWindowsKeys._Default);
            this.RdpRestrictedAdminMode = store.Read<RdpRestrictedAdminMode>(
                "RdpRestrictedAdminMode",
                "Restricted Admin mode",
                "Disable the transmission of reusable credentials to the VM. This mode requires " +
                    "a user account with local administrator privileges on the VM, and the " +
                    "VM must be configured to permit Restricted Admin mode.",
                Categories.RdpSecurity,
                Protocol.Rdp.RdpRestrictedAdminMode._Default);
            this.RdpSessionType = store.Read<RdpSessionType>(
                "RdpSessionType",
                "Session type",
                "Type of RDP session. Use an Admin session to administer an RDS server without " +
                    "consuming a CAL, otherwise use a User session.",
                Categories.RdpSecurity,
                Protocol.Rdp.RdpSessionType._Default);
            this.RdpDpiScaling = store.Read<RdpDpiScaling>(
                "RdpDpiScaling",
                "Display scaling",
                "Scale remote display to match local scaling settings.",
                Categories.RdpDisplay,
                Protocol.Rdp.RdpDpiScaling._Default);
            this.RdpDesktopSize = store.Read<RdpDesktopSize>(
                "DesktopSize",
                "Display resolution",
                "Display resolution of remote desktop.",
                Categories.RdpDisplay,
                Protocol.Rdp.RdpDesktopSize._Default);

            //
            // SSH Settings.
            //
            this.SshPort = store.Read<int>(
                "SshPort",
                "Server port",
                "Server port",
                Categories.SshConnection,
                SshParameters.DefaultPort,
                Predicate.InRange(1, ushort.MaxValue));
            this.SshTransport = store.Read<SessionTransportType>(
                "TransportType",
                "Connect via",
                $"Type of transport. Use {SessionTransportType.IapTunnel} unless " +
                    "you need to connect to a VM's internal IP address via " +
                    "Cloud VPN or Interconnect.",
                Categories.SshConnection,
                SessionTransportType._Default);
            this.SshPublicKeyAuthentication = store.Read<SshPublicKeyAuthentication>(
                "SshPublicKeyAuthentication",
                "Public key authentication",
                "Automatically create an SSH key pair and publish it using OS Login or metadata keys.",
                Categories.SshCredentials,
                Protocol.Ssh.SshPublicKeyAuthentication._Default);
            this.SshUsername = store.Read<string?>(
                "SshUsername",
                "Username",
                "Linux username, optional",
                Categories.SshCredentials,
                null,
                username => username == null ||
                            string.IsNullOrEmpty(username) ||
                            LinuxUser.IsValidUsername(username));
            this.SshPassword = store.Read<SecureString?>(
                "SshPassword",
                "Password",
                "Password, only applicable if public key authentication is disabled",
                Categories.SshCredentials,
                null);
            this.SshConnectionTimeout = store.Read<int>(
                "SshConnectionTimeout",
                "Connection timeout",
                "Timeout for establishing SSH connections, in seconds.",
                Categories.SshConnection,
                (int)SshParameters.DefaultConnectionTimeout.TotalSeconds,
                Predicate.InRange(0, 300));

            //
            // App Settings.
            //
            this.AppUsername = store.Read<string?>(
                "AppUsername",
                null, // Hidden.
                null, // Hidden.
                null, // Hidden.
                null,
                username => string.IsNullOrEmpty(username) || !username.Contains(' '));
            this.AppNetworkLevelAuthentication = store.Read<AppNetworkLevelAuthenticationState>(
                "AppNetworkLevelAuthentication",
                "Windows authentication",
                "Use Windows authentication for SQL Server connections.",
                Categories.AppCredentials,
                AppNetworkLevelAuthenticationState._Default);

            Debug.Assert(this.Settings.All(s => s != null));
        }

        //---------------------------------------------------------------------
        // RDP settings.
        //---------------------------------------------------------------------

        public ISetting<string?> RdpUsername { get; }
        public ISetting<SecureString?> RdpPassword { get; }
        public ISetting<string?> RdpDomain { get; }
        public ISetting<RdpConnectionBarState> RdpConnectionBar { get; }
        public ISetting<RdpAuthenticationLevel> RdpAuthenticationLevel { get; }
        public ISetting<RdpColorDepth> RdpColorDepth { get; }
        public ISetting<RdpAudioMode> RdpAudioMode { get; }
        public ISetting<RdpAutomaticLogon> RdpAutomaticLogon { get; }
        public ISetting<RdpNetworkLevelAuthentication> RdpNetworkLevelAuthentication { get; }
        public ISetting<int> RdpConnectionTimeout { get; }
        public ISetting<int> RdpPort { get; }
        public ISetting<SessionTransportType> RdpTransport { get; }
        public ISetting<RdpRedirectClipboard> RdpRedirectClipboard { get; }
        public ISetting<RdpRedirectPrinter> RdpRedirectPrinter { get; }
        public ISetting<RdpRedirectSmartCard> RdpRedirectSmartCard { get; }
        public ISetting<RdpRedirectPort> RdpRedirectPort { get; }
        public ISetting<RdpRedirectDrive> RdpRedirectDrive { get; }
        public ISetting<RdpRedirectDevice> RdpRedirectDevice { get; }
        public ISetting<RdpRedirectWebAuthn> RdpRedirectWebAuthn { get; }
        public ISetting<RdpHookWindowsKeys> RdpHookWindowsKeys { get; }
        public ISetting<RdpRestrictedAdminMode> RdpRestrictedAdminMode { get; }
        public ISetting<RdpSessionType> RdpSessionType { get; }
        public ISetting<RdpDpiScaling> RdpDpiScaling { get; }
        public ISetting<RdpDesktopSize> RdpDesktopSize { get; private set; }

        internal IEnumerable<ISetting> RdpSettings => new ISetting[]
        {
            //
            // NB. The order determines the default order in the PropertyGrid
            // (assuming the PropertyGrid doesn't force alphabetical order).
            //
            this.RdpTransport,
            this.RdpConnectionTimeout,
            this.RdpPort,

            this.RdpUsername,
            this.RdpPassword,
            this.RdpDomain,

            this.RdpColorDepth,
            this.RdpConnectionBar,
            this.RdpDesktopSize,
            this.RdpDpiScaling,

            this.RdpAudioMode,
            this.RdpHookWindowsKeys,
            this.RdpRedirectClipboard,
            this.RdpRedirectPrinter,
            this.RdpRedirectSmartCard,
            this.RdpRedirectPort,
            this.RdpRedirectDrive,
            this.RdpRedirectDevice,
            this.RdpRedirectWebAuthn,

            this.RdpAutomaticLogon,
            this.RdpNetworkLevelAuthentication,
            this.RdpAuthenticationLevel,
            this.RdpRestrictedAdminMode,
            this.RdpSessionType,
        };

        //---------------------------------------------------------------------
        // SSH settings.
        //---------------------------------------------------------------------

        public ISetting<int> SshPort { get; private set; }
        public ISetting<SessionTransportType> SshTransport { get; private set; }
        public ISetting<string?> SshUsername { get; private set; }
        public ISetting<SecureString?> SshPassword { get; private set; }
        public ISetting<int> SshConnectionTimeout { get; private set; }
        public ISetting<SshPublicKeyAuthentication> SshPublicKeyAuthentication { get; private set; }

        internal IEnumerable<ISetting> SshSettings => new ISetting[]
        {
            //
            // NB. The order determines the default order in the PropertyGrid
            // (assuming the PropertyGrid doesn't force alphabetical order).
            //
            this.SshTransport,
            this.SshConnectionTimeout,
            this.SshPort,
            this.SshPublicKeyAuthentication,
            this.SshUsername,
            this.SshPassword,
        };

        //---------------------------------------------------------------------
        // App settings.
        //---------------------------------------------------------------------

        public ISetting<string?> AppUsername { get; private set; }
        public ISetting<AppNetworkLevelAuthenticationState> AppNetworkLevelAuthentication { get; private set; }

        internal IEnumerable<ISetting> AppSettings => new ISetting[]
        {
            //
            // NB. The order determines the default order in the PropertyGrid
            // (assuming the PropertyGrid doesn't force alphabetical order).
            //
            this.AppUsername,
            this.AppNetworkLevelAuthentication
        };

        //---------------------------------------------------------------------
        // Filtering.
        //---------------------------------------------------------------------

        internal bool AppliesTo(
            ISetting setting,
            IProjectModelInstanceNode node)
        {
            if (this.SshSettings.Contains(setting))
            {
                return node.IsSshSupported();
            }
            else if (this.RdpSettings.Contains(setting))
            {
                return node.IsRdpSupported();
            }
            else
            {
                return true;
            }
        }

        //---------------------------------------------------------------------
        // ISettingsCollection.
        //---------------------------------------------------------------------

        public IEnumerable<ISetting> Settings
        {
            get => this.RdpSettings
                .Concat(this.SshSettings)
                .Concat(this.AppSettings);
        }

        private static class Categories
        {
            private const ushort MaxIndex = 7;

            private static string Order(ushort order, string name)
            {
                //
                // The PropertyGrid control doesn't let us explicitly specify the
                // order of categories. To work around that limitation, prefix 
                // category names with zero-width spaces so that alphabetical 
                // sorting yields the desired result.
                //

                Debug.Assert(order <= MaxIndex);
                return new string('\u200B', MaxIndex - order) + name;
            }

            public static readonly string WindowsCredentials = Order(0, "Windows Credentials");

            public static readonly string RdpConnection = Order(1, "Remote Desktop Connection");
            public static readonly string RdpDisplay = Order(2, "Remote Desktop Display");
            public static readonly string RdpResources = Order(3, "Remote Desktop Resources");
            public static readonly string RdpSecurity = Order(4, "Remote Desktop Security Settings");

            public static readonly string SshConnection = Order(5, "SSH Connection");
            public static readonly string SshCredentials = Order(6, "SSH Credentials");

            public static readonly string AppCredentials = Order(7, "SQL Server");
        }
    }
}
