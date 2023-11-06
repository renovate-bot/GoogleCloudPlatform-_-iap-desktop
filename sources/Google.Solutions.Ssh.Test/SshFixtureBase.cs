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

using Google.Apis.Compute.v1.Data;
using Google.Solutions.Apis.Compute;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common;
using Google.Solutions.Common.Util;
using Google.Solutions.Ssh.Cryptography;
using Google.Solutions.Ssh.Native;
using Google.Solutions.Testing.Apis;
using Google.Solutions.Testing.Apis.Integration;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.Ssh.Test
{
    public abstract class SshFixtureBase : FixtureBase
    {
        private static readonly IDictionary<string, IAsymmetricKeyCredential> cachedCredentials =
            new Dictionary<string, IAsymmetricKeyCredential>();

        protected override IEnumerable<TraceSource> Sources
            => base.Sources.Concat(new[]
            {
                CommonTraceSource.Log,
                SshTraceSource.Log
            });

        //---------------------------------------------------------------------
        // Handle tracking.
        //---------------------------------------------------------------------

        [SetUp]
        public void ClearOpenHandles()
        {
            HandleTable.Clear();
        }

        [TearDown]
        public void CheckOpenHandles()
        {
            HandleTable.DumpOpenHandles();
            Assert.AreEqual(0, HandleTable.HandleCount);
        }

        //---------------------------------------------------------------------
        // Helper methods.
        //---------------------------------------------------------------------

        protected static SshSession CreateSession()
        {
            var session = new SshSession();
            session.SetTraceHandler(
                LIBSSH2_TRACE.SOCKET |
                    LIBSSH2_TRACE.ERROR |
                    LIBSSH2_TRACE.CONN |
                    LIBSSH2_TRACE.AUTH |
                    LIBSSH2_TRACE.KEX |
                    LIBSSH2_TRACE.SFTP,
                Console.WriteLine);

            session.Timeout = TimeSpan.FromSeconds(5);

            return session;
        }

        protected static async Task<IPAddress> GetPublicIpAddressForInstanceAsync(
            InstanceLocator instanceLocator)
        {
            using (var service = TestProject.CreateComputeService())
            {
                var instance = await service
                    .Instances.Get(
                            instanceLocator.ProjectId,
                            instanceLocator.Zone,
                            instanceLocator.Name)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
                var ip = instance
                    .NetworkInterfaces
                    .EnsureNotNull()
                    .Where(nic => nic.AccessConfigs != null)
                    .SelectMany(nic => nic.AccessConfigs)
                    .EnsureNotNull()
                    .Where(accessConfig => accessConfig.Type == "ONE_TO_ONE_NAT")
                    .Select(accessConfig => accessConfig.NatIP)
                    .FirstOrDefault();
                return IPAddress.Parse(ip);
            }
        }

        protected static async Task<IPEndPoint> GetPublicSshEndpointAsync(
            InstanceLocator instance)
        {
            return new IPEndPoint(
                await GetPublicIpAddressForInstanceAsync(instance)
                    .ConfigureAwait(false),
                22);
        }

        /// <summary>
        /// Create an authenticator for a given key type, minimizing
        /// server rountrips.
        /// </summary>
        protected static async Task<IAsymmetricKeyCredential> GetCredentialForInstanceAsync(
            InstanceLocator instanceLocator,
            SshKeyType keyType)
        {
            var username = $"testuser";
            var cacheKey = $"{instanceLocator}|{username}|{keyType}";

            if (!cachedCredentials.ContainsKey(cacheKey))
            {
                //
                // Create a set of keys for this user/instance.
                //
                // N.B. We are replacing all existing keys for this
                // user. Therefore, upload keys for all key types.
                // 
                var keysByType = Enum.GetValues(typeof(SshKeyType))
                    .Cast<SshKeyType>()
                    .ToDictionary(
                        k => k,
                        k => AsymmetricKeySigner.CreateEphemeral(k));

                var metadataEntry = string.Join(
                    "\n", 
                    keysByType
                        .Values
                        .Select(k => $"{username}:{k.PublicKey.ToString(PublicKey.Format.OpenSsh)} {username}"));

                using (var service = TestProject.CreateComputeService())
                {
                    await service.Instances
                        .AddMetadataAsync(
                            instanceLocator,
                            new Metadata()
                            {
                                Items = new[]
                                {
                                    new Metadata.ItemsData()
                                    {
                                        Key = "ssh-keys",
                                        Value = metadataEntry
                                    }
                                }
                            },
                            CancellationToken.None)
                    .ConfigureAwait(false);

                }

                foreach (var kvp in keysByType)
                {
                    cachedCredentials[$"{instanceLocator}|{username}|{kvp.Key}"] =
                        new StaticAsymmetricKeyCredential(username, kvp.Value);
                }
            }

            return cachedCredentials[cacheKey];
        }
    }
}
