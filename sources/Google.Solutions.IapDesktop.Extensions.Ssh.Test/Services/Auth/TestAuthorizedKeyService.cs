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

using Google.Apis.CloudOSLogin.v1.Data;
using Google.Apis.Compute.v1.Data;
using Google.Solutions.Common.ApiExtensions.Instance;
using Google.Solutions.Common.Auth;
using Google.Solutions.Common.Locator;
using Google.Solutions.Common.Test;
using Google.Solutions.IapDesktop.Application.Services.Adapters;
using Google.Solutions.IapDesktop.Application.Test;
using Google.Solutions.IapDesktop.Extensions.Ssh.Services.Auth;
using Google.Solutions.Ssh;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Extensions.Ssh.Test.Services.Auth
{
    [TestFixture]
    public class TestAuthorizedKeyService : ApplicationFixtureBase
    {
        private readonly static InstanceLocator SampleLocator
            = new InstanceLocator("project-1", "zone-1", "instance-1");

        private static Mock<IAuthorizationAdapter> CreateAuthorizationAdapterMock()
        {
            var authorization = new Mock<IAuthorization>();
            authorization
                .SetupGet(a => a.Email)
                .Returns("bob@example.com");

            var adapter = new Mock<IAuthorizationAdapter>();
            adapter
                .SetupGet(a => a.Authorization)
                .Returns(authorization.Object);

            return adapter;
        }

        private Mock<IComputeEngineAdapter> CreateComputeEngineAdapterMock(
            bool? osLoginEnabledForProject,
            bool? osLoginEnabledForInstance,
            bool osLogin2fa,
            bool legacySshKeyPresent,
            bool projectWideKeysBlockedForProject,
            bool projectWideKeysBlockedForInstance)
        {
            var projectMetadata = new Metadata();
            if (osLoginEnabledForProject.HasValue)
            {
                projectMetadata.Add("enable-oslogin", 
                    osLoginEnabledForProject.Value.ToString());
            }

            if (osLoginEnabledForProject.HasValue && osLogin2fa)
            {
                projectMetadata.Add("enable-oslogin-2fa", "true");
            }

            if (projectWideKeysBlockedForProject)
            {
                projectMetadata.Add("block-project-ssh-keys", "true");
            }


            var instanceMetadata = new Metadata();
            if (osLoginEnabledForInstance.HasValue)
            {
                instanceMetadata.Add("enable-oslogin", 
                    osLoginEnabledForInstance.Value.ToString());
            }

            if (osLoginEnabledForInstance.HasValue && osLogin2fa)
            {
                instanceMetadata.Add("enable-oslogin-2fa", "true");
            }

            if (legacySshKeyPresent)
            {
                instanceMetadata.Add("sshKeys", "somedata");
            }

            if (projectWideKeysBlockedForInstance)
            {
                instanceMetadata.Add("block-project-ssh-keys", "true");
            }

            var adapter = new Mock<IComputeEngineAdapter>();
            adapter
                .Setup(a => a.GetProjectAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Project()
                {
                    CommonInstanceMetadata = projectMetadata
                });
            adapter
                .Setup(a => a.GetInstanceAsync(
                    It.IsAny<InstanceLocator>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Instance()
                {
                    Metadata = instanceMetadata
                });
            return adapter;
        }

        private static Mock<IOsLoginService> CreateOsLoginServiceMock()
        {
            var osLoginService = new Mock<IOsLoginService>();
            osLoginService
                .Setup(s => s.AuthorizeKeyAsync(
                        It.IsAny<string>(),
                        It.Is((OsLoginSystemType os) => os == OsLoginSystemType.Linux),
                        It.IsAny<ISshKey>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(AuthorizedKey.ForOsLoginAccount(
                    new Mock<ISshKey>().Object,
                    new PosixAccount()
                    {
                        Username = "bob"
                    }));
            return osLoginService;
        }

        private static Mock<IResourceManagerAdapter> CreateResourceManagerAdapterMock(
            bool allowSetCommonInstanceMetadata)
        {
            var adapter = new Mock<IResourceManagerAdapter>();
            adapter
                .Setup(a => a.IsGrantedPermission(
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                .ReturnsAsync(allowSetCommonInstanceMetadata);

            return adapter;
        }

        //---------------------------------------------------------------------
        // Os Login.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenOsLoginEnabledForProject_ThenAuthorizeKeyAsyncUsesOsLogin()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: true,
                    osLoginEnabledForInstance: null,
                    osLogin2fa: false,
                    legacySshKeyPresent: true,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            var authorizedKey = await service.AuthorizeKeyAsync(
                SampleLocator,
                new Mock<ISshKey>().Object,
                TimeSpan.FromMinutes(1),
                null,
                AuthorizeKeyMethods.All,
                CancellationToken.None);

            Assert.IsNotNull(authorizedKey);
            Assert.AreEqual(AuthorizeKeyMethods.Oslogin, authorizedKey.AuthorizationMethod);
            Assert.AreEqual("bob", authorizedKey.Username);
        }

        [Test]
        public async Task WhenOsLoginEnabledForInstance_ThenAuthorizeKeyAsyncUsesOsLogin()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: null,
                    osLoginEnabledForInstance: true,
                    osLogin2fa: false,
                    legacySshKeyPresent: true,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            var authorizedKey = await service.AuthorizeKeyAsync(
                SampleLocator,
                new Mock<ISshKey>().Object,
                TimeSpan.FromMinutes(1),
                null,
                AuthorizeKeyMethods.All,
                CancellationToken.None);

            Assert.IsNotNull(authorizedKey);
            Assert.AreEqual(AuthorizeKeyMethods.Oslogin, authorizedKey.AuthorizationMethod);
            Assert.AreEqual("bob", authorizedKey.Username);
        }

        [Test]
        public async Task WhenOsLoginDisabledForProjectButEnabledForInstance_ThenAuthorizeKeyAsyncUsesOsLogin()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: false,
                    osLoginEnabledForInstance: true,
                    osLogin2fa: false,
                    legacySshKeyPresent: true,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            var authorizedKey = await service.AuthorizeKeyAsync(
                SampleLocator,
                new Mock<ISshKey>().Object,
                TimeSpan.FromMinutes(1),
                null,
                AuthorizeKeyMethods.All,
                CancellationToken.None);

            Assert.IsNotNull(authorizedKey);
            Assert.AreEqual(AuthorizeKeyMethods.Oslogin, authorizedKey.AuthorizationMethod);
            Assert.AreEqual("bob", authorizedKey.Username);
        }

        [Test]
        public async Task WhenOsLoginEnabledForProjectButDisabledForInstance_ThenAuthorizeKeyAsyncPushesKeyToProjectMetadata()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: true,
                osLoginEnabledForInstance: false,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: false,
                projectWideKeysBlockedForInstance: false);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                var authorizedKey = await service.AuthorizeKeyAsync(
                    SampleLocator,
                    key,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None);

                Assert.IsNotNull(authorizedKey);
                Assert.AreEqual(AuthorizeKeyMethods.ProjectMetadata, authorizedKey.AuthorizationMethod);
                Assert.AreEqual("bob", authorizedKey.Username);

                computeEngineAdapter.Verify(a => a.UpdateCommonInstanceMetadataAsync(
                    It.IsAny<string>(),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public void WhenOsLoginEnabledForProjectButOsLoginNotAllowed_ThenAuthorizeKeyThrowsInvalidOperationException()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: true,
                    osLoginEnabledForInstance: null,
                    osLogin2fa: false,
                    legacySshKeyPresent: false,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            AssertEx.ThrowsAggregateException<InvalidOperationException>(
                () => service.AuthorizeKeyAsync(
                    SampleLocator,
                    new Mock<ISshKey>().Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.InstanceMetadata,
                    CancellationToken.None).Wait());
        }

        [Test]
        public void WhenOsLoginEnabledForInstanceButOsLoginNotAllowed_ThenAuthorizeKeyThrowsInvalidOperationException()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: null,
                    osLoginEnabledForInstance: true,
                    osLogin2fa: false,
                    legacySshKeyPresent: false,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            AssertEx.ThrowsAggregateException<InvalidOperationException>(
                () => service.AuthorizeKeyAsync(
                    SampleLocator,
                    new Mock<ISshKey>().Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.InstanceMetadata,
                    CancellationToken.None).Wait());
        }

        [Test]
        public void WhenOsLogin2faEnabled_ThenAuthorizeKeyThrowsNotImplementedException()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: true,
                    osLoginEnabledForInstance: null,
                    osLogin2fa: true,
                    legacySshKeyPresent: false,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            AssertEx.ThrowsAggregateException<NotImplementedException>(
                () => service.AuthorizeKeyAsync(
                    SampleLocator,
                    new Mock<ISshKey>().Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None).Wait());
        }

        //---------------------------------------------------------------------
        // Metadata.
        //---------------------------------------------------------------------

        [Test]
        public void WhenLegacySshKeyPresent_ThenAuthorizeKeyAsyncThrowsUnsupportedLegacySshKeyEncounteredException()
        {
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                CreateComputeEngineAdapterMock(
                    osLoginEnabledForProject: null,
                    osLoginEnabledForInstance: null,
                    osLogin2fa: false,
                    legacySshKeyPresent: true,
                    projectWideKeysBlockedForProject: false,
                    projectWideKeysBlockedForInstance: false).Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            AssertEx.ThrowsAggregateException<UnsupportedLegacySshKeyEncounteredException>(
                () => service.AuthorizeKeyAsync(
                    SampleLocator,
                    new Mock<ISshKey>().Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenProjectWideSshKeysBlockedInProject_ThenAuthorizeKeyAsyncPushesKeyToInstanceMetadata()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: true,
                projectWideKeysBlockedForInstance: false);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                var authorizedKey = await service.AuthorizeKeyAsync(
                    SampleLocator,
                    key,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None);

                Assert.IsNotNull(authorizedKey);
                Assert.AreEqual(AuthorizeKeyMethods.InstanceMetadata, authorizedKey.AuthorizationMethod);
                Assert.AreEqual("bob", authorizedKey.Username);

                computeEngineAdapter.Verify(a => a.UpdateMetadataAsync(
                    It.Is((InstanceLocator loc) => loc == SampleLocator),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task WhenProjectWideSshKeysBlockedInInstance_ThenAuthorizeKeyAsyncPushesKeyToInstanceMetadata()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: false,
                projectWideKeysBlockedForInstance: true);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                var authorizedKey = await service.AuthorizeKeyAsync(
                    SampleLocator,
                    key,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None);

                Assert.IsNotNull(authorizedKey);
                Assert.AreEqual(AuthorizeKeyMethods.InstanceMetadata, authorizedKey.AuthorizationMethod);
                Assert.AreEqual("bob", authorizedKey.Username);

                computeEngineAdapter.Verify(a => a.UpdateMetadataAsync(
                    It.Is((InstanceLocator loc) => loc == SampleLocator),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task WhenProjectMetadataNotWritable_ThenAuthorizeKeyAsyncPushesKeyToInstanceMetadata()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: false,
                projectWideKeysBlockedForInstance: false);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(false).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                var authorizedKey = await service.AuthorizeKeyAsync(
                    SampleLocator,
                    key,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None);

                Assert.IsNotNull(authorizedKey);
                Assert.AreEqual(AuthorizeKeyMethods.InstanceMetadata, authorizedKey.AuthorizationMethod);
                Assert.AreEqual("bob", authorizedKey.Username);

                computeEngineAdapter.Verify(a => a.UpdateMetadataAsync(
                    It.Is((InstanceLocator loc) => loc == SampleLocator),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public void WhenProjectWideSshKeysBlockedButInstanceMetadataNotAllowed_ThenAuthorizeKeyThrowsInvalidOperationException()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: true,
                projectWideKeysBlockedForInstance: false);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            AssertEx.ThrowsAggregateException<InvalidOperationException>(
                () => service.AuthorizeKeyAsync(
                    SampleLocator,
                    new Mock<ISshKey>().Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.Oslogin | AuthorizeKeyMethods.ProjectMetadata,
                    CancellationToken.None).Wait());
        }

        [Test]
        public async Task WhenProjectMetadataNotAllowed_ThenAuthorizeKeyAsyncPushesKeyToInstanceMetadata()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: false,
                projectWideKeysBlockedForInstance: false);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                var authorizedKey = await service.AuthorizeKeyAsync(
                    SampleLocator,
                    key,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.InstanceMetadata,
                    CancellationToken.None);

                Assert.IsNotNull(authorizedKey);
                Assert.AreEqual(AuthorizeKeyMethods.InstanceMetadata, authorizedKey.AuthorizationMethod);
                Assert.AreEqual("bob", authorizedKey.Username);

                computeEngineAdapter.Verify(a => a.UpdateMetadataAsync(
                    It.Is((InstanceLocator loc) => loc == SampleLocator),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task WhenProjectAndInstanceMetadataAllowed_ThenAuthorizeKeyAsyncPushesKeyToProjectMetadata()
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: false,
                projectWideKeysBlockedForInstance: false);
            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                var authorizedKey = await service.AuthorizeKeyAsync(
                    SampleLocator,
                    key,
                    TimeSpan.FromMinutes(1),
                    null,
                    AuthorizeKeyMethods.All,
                    CancellationToken.None);

                Assert.IsNotNull(authorizedKey);
                Assert.AreEqual(AuthorizeKeyMethods.ProjectMetadata, authorizedKey.AuthorizationMethod);
                Assert.AreEqual("bob", authorizedKey.Username);

                computeEngineAdapter.Verify(a => a.UpdateCommonInstanceMetadataAsync(
                    It.IsAny<string>(),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public void WhenMetadataUpdatesFails_ThenAuthorizeKeyAsyncThrowsSshKeyPushFailedException(
            [Values(
                HttpStatusCode.Forbidden, 
            HttpStatusCode.BadRequest)] HttpStatusCode httpStatus)
        {
            var computeEngineAdapter = CreateComputeEngineAdapterMock(
                osLoginEnabledForProject: null,
                osLoginEnabledForInstance: null,
                osLogin2fa: false,
                legacySshKeyPresent: false,
                projectWideKeysBlockedForProject: false,
                projectWideKeysBlockedForInstance: false);
            computeEngineAdapter
                .Setup(a => a.UpdateCommonInstanceMetadataAsync(
                    It.IsAny<string>(),
                    It.IsAny<Action<Metadata>>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new GoogleApiException("GCE", "mock-error")
                {
                    HttpStatusCode = httpStatus
                });

            var service = new AuthorizedKeyService(
                CreateAuthorizationAdapterMock().Object,
                computeEngineAdapter.Object,
                CreateResourceManagerAdapterMock(true).Object,
                CreateOsLoginServiceMock().Object);

            using (var key = RsaSshKey.NewEphemeralKey())
            {
                AssertEx.ThrowsAggregateException<SshKeyPushFailedException>(
                    () => service.AuthorizeKeyAsync(
                        SampleLocator,
                        key,
                        TimeSpan.FromMinutes(1),
                        null,
                        AuthorizeKeyMethods.All,
                        CancellationToken.None).Wait());
            }
        }
    }
}
