﻿//
// Copyright 2023 Google LLC
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

using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Runtime;
using Google.Solutions.Iap;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport.Policies;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol.Rdp;
using Google.Solutions.IapDesktop.Extensions.Session.Protocol.Ssh;
using Google.Solutions.Testing.Apis.Integration;
using System.Net;

namespace Google.Solutions.IapDesktop.Extensions.Session.Test.ToolWindows.Session
{
    /// <summary>
    /// Helper class for managing IAP transports in test cases.
    /// </summary>
    internal class IapTransport : DisposableBase, ITransport
    {
        public IapTunnel Tunnel { get; }

        public IProtocol Protocol { get; }

        public ComputeEngineLocator Target { get; }

        public IPEndPoint Endpoint => this.Tunnel.LocalEndpoint;

        private IapTransport(
            IapTunnel tunnel,
            IProtocol protocol,
            InstanceLocator target)
        {
            this.Tunnel = tunnel;
            this.Protocol = protocol;
            this.Target = target;
        }

        private static IapTransport Create(
            InstanceLocator instance,
            IAuthorization authorization,
            IProtocol protocol,
            ushort port)
        {
            var client = new IapClient(
                IapClient.CreateEndpoint(),
                authorization,
                TestProject.UserAgent);

            var policy = new AllowAllPolicy();
            var listener = new IapListener(
                client.GetTarget(
                    instance,
                    port,
                    IapClient.DefaultNetworkInterface),
                policy,
                null);

            var profile = new IapTunnel.Profile(
                protocol,
                policy,
                instance,
                port,
                listener.LocalEndpoint);

            return new IapTransport(
                new IapTunnel(
                    listener,
                    profile,
                    IapTunnelFlags.None),
                protocol,
                instance);
        }

        public static IapTransport CreateRdpTransport(
            InstanceLocator instance,
            IAuthorization authorization)
        {
            return Create(
                instance,
                authorization,
                RdpProtocol.Protocol,
                3389);
        }

        public static IapTransport CreateSshTransport(
            InstanceLocator instance,
            IAuthorization authorization)
        {
            return Create(
                instance,
                authorization,
                SshProtocol.Protocol,
                22);
        }

        protected override void Dispose(bool disposing)
        {
            this.Tunnel.Dispose();
            base.Dispose(disposing);
        }
    }
}
