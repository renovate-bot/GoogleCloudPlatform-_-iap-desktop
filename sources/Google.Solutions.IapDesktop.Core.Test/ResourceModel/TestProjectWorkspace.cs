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

using Google.Apis.Auth.OAuth2.Responses;
using Google.Solutions.Apis;
using Google.Solutions.Apis.Crm;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Linq;
using Google.Solutions.IapDesktop.Core.EntityModel;
using Google.Solutions.IapDesktop.Core.ResourceModel;
using Google.Solutions.Testing.Apis;
using Moq;
using NUnit.Framework;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrmOrganization = Google.Apis.CloudResourceManager.v1.Data.Organization;
using CrmProject = Google.Apis.CloudResourceManager.v1.Data.Project;

namespace Google.Solutions.IapDesktop.Core.Test.ResourceModel
{
    [TestFixture]
    public class TestProjectWorkspace
    {
        private static readonly ProjectLocator SampleProject = new ProjectLocator("project-1");

        //----------------------------------------------------------------------
        // Search organizations.
        //----------------------------------------------------------------------

        [Test]
        public async Task SearchOrganizations_WhenWorkspaceEmpty()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                new Mock<IResourceManagerClient>().Object);

            var organizations = await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            CollectionAssert.IsEmpty(organizations);
        }

        [Test]
        public async Task SearchOrganizations_WhenCalledTwice_ThenUsesCachedState()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());

            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                resourceMamager.Object);

            // Search twice.
            await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);

            // Check that cache loaded once.
            resourceMamager.Verify(
                r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task SearchOrganizations_WhenSettingsChange_ThenInvalidatesCachedState()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());

            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                resourceMamager.Object);

            // Search to load cache.
            await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);

            // Invalidate.
            settings.Raise(
                s => s.PropertyChanged += null,
                new PropertyChangedEventArgs(nameof(IProjectWorkspaceSettings.Projects)));

            // Search again.
            await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);

            // Check that cache loaded once.
            resourceMamager.Verify(
                r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Test]
        public async Task SearchOrganizations_WhenProjectAncestryUnknown()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            // Fail calls to load ancestry.
            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());

            var cache = new Mock<IAncestryCache>();
            var workspace = new ProjectWorkspace(
                settings.Object,
                cache.Object,
                resourceMamager.Object);

            var organizations = await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            CollectionAssert.IsNotEmpty(organizations);
            Assert.AreEqual(
                Organization.Default.Locator,
                organizations.First().Locator);

            cache.Verify(
                c => c.SetAncestry(It.IsAny<ProjectLocator>(), It.IsAny<OrganizationLocator>()),
                Times.Never);
        }

        [Test]
        public async Task SearchOrganizations_WhenProjectAncestryCached()
        {
            var org = new OrganizationLocator(1);

            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var cache = new Mock<IAncestryCache>();
            cache
                .Setup(s => s.TryGetAncestry(SampleProject, out org))
                .Returns(true);

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());
            resourceMamager
                .Setup(r => r.GetOrganizationAsync(org, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmOrganization()
                {
                    DisplayName = "One"
                });

            var workspace = new ProjectWorkspace(
                settings.Object,
                cache.Object,
                resourceMamager.Object);

            var organizations = await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            CollectionAssert.IsNotEmpty(organizations);
            Assert.AreEqual(
                org,
                organizations.First().Locator);
            Assert.AreEqual(
                "One",
                organizations.First().DisplayName);

            resourceMamager
                .Verify(r => r.FindOrganizationAsync(
                    It.IsAny<ProjectLocator>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            cache.Verify(
                c => c.SetAncestry(It.IsAny<ProjectLocator>(), It.IsAny<OrganizationLocator>()),
                Times.Never);
        }

        [Test]
        public async Task SearchOrganizations_WhenProjectAncestryInaccessible()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());
            resourceMamager
                .Setup(r => r.FindOrganizationAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OrganizationLocator?)null);

            var cache = new Mock<IAncestryCache>();
            var workspace = new ProjectWorkspace(
                settings.Object,
                cache.Object,
                resourceMamager.Object);

            var organizations = await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            CollectionAssert.IsNotEmpty(organizations);
            Assert.AreEqual(
                Organization.Default.Locator,
                organizations.First().Locator);

            cache.Verify(
                c => c.SetAncestry(It.IsAny<ProjectLocator>(), It.IsAny<OrganizationLocator>()),
                Times.Never);
        }

        [Test]
        public async Task SearchOrganizations_WhenProjectAncestryCachedButOrganizationInaccessible()
        {
            var org = new OrganizationLocator(1);

            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var cache = new Mock<IAncestryCache>();
            cache
                .Setup(s => s.TryGetAncestry(SampleProject, out org))
                .Returns(true);

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());
            resourceMamager
                .Setup(r => r.GetOrganizationAsync(org, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ResourceAccessDeniedException("mock", new Exception()));

            var workspace = new ProjectWorkspace(
                settings.Object,
                cache.Object,
                resourceMamager.Object);

            var organizations = await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            CollectionAssert.IsNotEmpty(organizations);
            Assert.AreEqual(
                Organization.Default.Locator,
                organizations.First().Locator);

            cache.Verify(
                c => c.SetAncestry(It.IsAny<ProjectLocator>(), It.IsAny<OrganizationLocator>()),
                Times.Never);
        }

        [Test]
        public async Task SearchOrganizations_ResolvesAndCachesAncestry()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var org = new OrganizationLocator(1);
            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject());
            resourceMamager
                .Setup(r => r.FindOrganizationAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(org);
            resourceMamager
                .Setup(r => r.GetOrganizationAsync(org, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmOrganization()
                {
                    DisplayName = "Sample org"
                });

            var cache = new Mock<IAncestryCache>();
            var workspace = new ProjectWorkspace(
                settings.Object,
                cache.Object,
                resourceMamager.Object);

            var organizations = await workspace
                .SearchAsync(WildcardQuery.Instance, CancellationToken.None)
                .ConfigureAwait(false);
            CollectionAssert.IsNotEmpty(organizations);
            Assert.AreEqual(
                org,
                organizations.First().Locator);

            cache.Verify(
                c => c.SetAncestry(SampleProject, org),
                Times.Once);
        }

        //----------------------------------------------------------------------
        // Query organizations.
        //----------------------------------------------------------------------

        [Test]
        public async Task QueryOrganization_WhenNotFound()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                new Mock<IResourceManagerClient>().Object);

            var aspect = await workspace
                .QueryAspectAsync(Organization.Default.Locator, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsNull(aspect);
        }

        //----------------------------------------------------------------------
        // List organization descendants.
        //----------------------------------------------------------------------

        [Test]
        public async Task ListDescendants_ByOrganization_WhenProjectInaccessible()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ResourceAccessDeniedException("mock", new Exception()));

            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                resourceMamager.Object);

            var projects = await workspace
                .ListDescendantsAsync(Organization.Default.Locator, CancellationToken.None)
                .ConfigureAwait(false);

            CollectionAssert.IsNotEmpty(projects);
            Assert.IsFalse(projects.First().IsAccessible);
            Assert.AreEqual(SampleProject, projects.First().Locator);
            Assert.AreEqual(SampleProject.Name, projects.First().DisplayName);
        }

        [Test]
        public void ListDescendants_ByOrganization_WhenReauthTriggered()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            // Trigger reauth.
            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TokenResponseException(new TokenErrorResponse()
                {
                    Error = "invalid_grant"
                }));

            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                resourceMamager.Object);

            ExceptionAssert.ThrowsAggregateException<TokenResponseException>(
                () => workspace
                    .ListDescendantsAsync(Organization.Default.Locator, CancellationToken.None)
                    .Wait());
        }

        [Test]
        public async Task ListDescendants_ByOrganization_WhenAncestryInaccessible()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject()
                {
                    Name = "Project 1"
                });

            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                resourceMamager.Object);

            var projects = await workspace
                .ListDescendantsAsync(Organization.Default.Locator, CancellationToken.None)
                .ConfigureAwait(false);

            CollectionAssert.IsNotEmpty(projects);
            Assert.IsTrue(projects.First().IsAccessible);
            Assert.AreEqual(SampleProject, projects.First().Locator);
            Assert.AreEqual("Project 1", projects.First().DisplayName);
            Assert.AreEqual(Organization.Default.Locator, projects.First().Organization);
        }

        [Test]
        public async Task ListDescendants_ByOrganization()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            settings
                .SetupGet(s => s.Projects)
                .Returns(Enumerables.FromNullable(SampleProject));

            var org = new OrganizationLocator(1);

            var resourceMamager = new Mock<IResourceManagerClient>();
            resourceMamager
                .Setup(r => r.GetProjectAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmProject()
                {
                    Name = "Project 1"
                });
            resourceMamager
                .Setup(r => r.FindOrganizationAsync(SampleProject, It.IsAny<CancellationToken>()))
                .ReturnsAsync(org);
            resourceMamager
                .Setup(r => r.GetOrganizationAsync(org, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CrmOrganization()
                {
                    DisplayName = "One"
                });

            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                resourceMamager.Object);

            var projects = await workspace
                .ListDescendantsAsync(org, CancellationToken.None)
                .ConfigureAwait(false);

            CollectionAssert.IsNotEmpty(projects);
            Assert.IsTrue(projects.First().IsAccessible);
            Assert.AreEqual(SampleProject, projects.First().Locator);
            Assert.AreEqual("Project 1", projects.First().DisplayName);
            Assert.AreEqual(org, projects.First().Organization);
        }

        //----------------------------------------------------------------------
        // Query projects.
        //----------------------------------------------------------------------

        [Test]
        public async Task QueryProject_WhenNotFound()
        {
            var settings = new Mock<IProjectWorkspaceSettings>();
            var workspace = new ProjectWorkspace(
                settings.Object,
                new Mock<IAncestryCache>().Object,
                new Mock<IResourceManagerClient>().Object);

            var aspect = await workspace
                .QueryAspectAsync(SampleProject, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsNull(aspect);
        }
    }
}
