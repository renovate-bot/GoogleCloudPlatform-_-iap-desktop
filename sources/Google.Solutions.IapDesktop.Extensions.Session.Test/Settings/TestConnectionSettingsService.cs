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
using Google.Solutions.IapDesktop.Application.Profile.Settings;
using Google.Solutions.IapDesktop.Core.ProjectModel;
using Google.Solutions.IapDesktop.Extensions.Session.Settings;
using Google.Solutions.Testing.Apis.Platform;
using Moq;
using NUnit.Framework;
using System;

namespace Google.Solutions.IapDesktop.Extensions.Session.Test.Settings
{
    [TestFixture]
    public class TestConnectionSettingsService
    {
        private static readonly ProjectLocator SampleProject = new ProjectLocator("project-1");

        private static ConnectionSettingsService CreateConnectionSettingsService()
        {
            var projectRepository = new ProjectRepository(
                RegistryKeyPath.ForCurrentTest().CreateKey());
            var settingsRepository = new ConnectionSettingsRepository(projectRepository);

            // Set some initial project settings.
            projectRepository.AddProject(SampleProject);

            var projectSettings = settingsRepository.GetProjectSettings(SampleProject);
            projectSettings.RdpDomain.Value = "project-domain";
            settingsRepository.SetProjectSettings(projectSettings);

            return new ConnectionSettingsService(settingsRepository);
        }

        private IProjectModelProjectNode CreateProjectNode()
        {
            var projectNode = new Mock<IProjectModelProjectNode>();
            projectNode.SetupGet(n => n.Project).Returns(SampleProject);

            return projectNode.Object;
        }

        private IProjectModelZoneNode CreateZoneNode()
        {
            var zoneNode = new Mock<IProjectModelZoneNode>();
            zoneNode.SetupGet(n => n.Zone).Returns(new ZoneLocator(SampleProject.ProjectId, "zone-1"));

            return zoneNode.Object;
        }

        private IProjectModelInstanceNode CreateVmInstanceNode(bool isWindows = false)
        {
            var vmNode = new Mock<IProjectModelInstanceNode>();
            vmNode.SetupGet(n => n.Instance).Returns(
                new InstanceLocator(SampleProject, "zone-1", "instance-1"));
            vmNode.SetupGet(n => n.OperatingSystem).Returns(
                isWindows ? OperatingSystems.Windows : OperatingSystems.Linux);

            return vmNode.Object;
        }

        //---------------------------------------------------------------------
        // IsConnectionSettingsAvailable.
        //---------------------------------------------------------------------

        [Test]
        public void IsConnectionSettingsAvailable_WhenNodeUnsupported()
        {
            var service = CreateConnectionSettingsService();

            Assert.IsFalse(service.IsConnectionSettingsAvailable(
                new Mock<IProjectModelNode>().Object));
            Assert.IsFalse(service.IsConnectionSettingsAvailable(
                new Mock<IProjectModelCloudNode>().Object));
        }

        //---------------------------------------------------------------------
        // GetConnectionSettings.
        //---------------------------------------------------------------------

        [Test]
        public void GetConnectionSettings_WhenNodeUnsupported()
        {
            var service = CreateConnectionSettingsService();

            Assert.Throws<ArgumentException>(() => service.GetConnectionSettings(
                new Mock<IProjectModelNode>().Object));
        }

        //---------------------------------------------------------------------
        // GetConnectionSettings - Project.
        //---------------------------------------------------------------------

        [Test]
        public void GetConnectionSettings_WhenReadingProjectSettings_ThenExistingProjectSettingIsVisible()
        {
            var service = CreateConnectionSettingsService();
            var projectNode = CreateProjectNode();

            var settings = service.GetConnectionSettings(projectNode);
            Assert.AreEqual("project-domain", settings.TypedCollection.RdpDomain.Value);
        }

        [Test]
        public void GetConnectionSettings_WhenChangingProjectSetting_ThenSettingIsSaved()
        {
            var service = CreateConnectionSettingsService();
            var projectNode = CreateProjectNode();

            var firstSettings = service.GetConnectionSettings(projectNode);
            firstSettings.TypedCollection.RdpUsername.Value = "bob";
            firstSettings.Save();

            var secondSettings = service.GetConnectionSettings(projectNode);
            Assert.AreEqual("bob", secondSettings.TypedCollection.RdpUsername.Value);
        }

        //---------------------------------------------------------------------
        // GetConnectionSettings  - Zone.
        //---------------------------------------------------------------------

        [Test]
        public void GetConnectionSettings_WhenReadingZoneSettings_ThenExistingProjectSettingIsVisible()
        {
            var service = CreateConnectionSettingsService();
            var zoneNode = CreateZoneNode();

            var settings = service.GetConnectionSettings(zoneNode);
            Assert.AreEqual("project-domain", settings.TypedCollection.RdpDomain.Value);
        }

        [Test]
        public void GetConnectionSettings_WhenChangingZoneSetting_ThenSettingIsSaved()
        {
            var service = CreateConnectionSettingsService();
            var zoneNode = CreateZoneNode();

            var firstSettings = service.GetConnectionSettings(zoneNode);
            firstSettings.TypedCollection.RdpUsername.Value = "bob";
            firstSettings.Save();

            var secondSettings = service.GetConnectionSettings(zoneNode);
            Assert.AreEqual("bob", secondSettings.TypedCollection.RdpUsername.Value);
        }

        //---------------------------------------------------------------------
        // GetConnectionSettings - VM.
        //---------------------------------------------------------------------

        [Test]
        public void GetConnectionSettings_WhenReadingVmInstanceSettings_ThenExistingProjectSettingIsVisible()
        {
            var service = CreateConnectionSettingsService();
            var vmNode = CreateVmInstanceNode();

            var settings = service.GetConnectionSettings(vmNode);
            Assert.AreEqual("project-domain", settings.TypedCollection.RdpDomain.Value);
        }

        [Test]
        public void GetConnectionSettings_WhenChangingVmInstanceSetting_ThenSettingIsSaved()
        {
            var service = CreateConnectionSettingsService();
            var vmNode = CreateVmInstanceNode();

            var firstSettings = service.GetConnectionSettings(vmNode);
            firstSettings.TypedCollection.RdpUsername.Value = "bob";
            firstSettings.Save();

            var secondSettings = service.GetConnectionSettings(vmNode);
            Assert.AreEqual("bob", secondSettings.TypedCollection.RdpUsername.Value);
        }

        //---------------------------------------------------------------------
        // GetConnectionSettings - Inheritance.
        //---------------------------------------------------------------------

        [Test]
        public void GetConnectionSettings_WhenUsernameSetInProject_ProjectValueIsInheritedDownToVm(
            [Values("user", null)]
                string username)
        {
            var service = CreateConnectionSettingsService();

            var projectSettings = service.GetConnectionSettings(CreateProjectNode());
            projectSettings.TypedCollection.RdpUsername.Value = username;
            projectSettings.Save();

            // Inherited value is shown...
            var instanceSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            Assert.AreEqual(username, instanceSettings.TypedCollection.RdpUsername.Value);
            Assert.IsTrue(instanceSettings.TypedCollection.RdpUsername.IsDefault);
        }

        [Test]
        public void GetConnectionSettings_WhenUsernameSetInProjectAndZone_ZoneValueIsInheritedDownToVm()
        {
            var service = CreateConnectionSettingsService();

            var projectSettings = service.GetConnectionSettings(CreateProjectNode());
            projectSettings.TypedCollection.RdpUsername.Value = "root-value";
            projectSettings.Save();

            var zoneSettings = service.GetConnectionSettings(CreateZoneNode());
            zoneSettings.TypedCollection.RdpUsername.Value = "overriden-value";
            zoneSettings.Save();

            // Inherited value is shown...
            zoneSettings = service.GetConnectionSettings(CreateZoneNode());
            Assert.AreEqual("overriden-value", zoneSettings.TypedCollection.RdpUsername.Value);
            Assert.IsFalse(zoneSettings.TypedCollection.RdpUsername.IsDefault);

            var instanceSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            Assert.AreEqual("overriden-value", instanceSettings.TypedCollection.RdpUsername.Value);
            Assert.IsTrue(instanceSettings.TypedCollection.RdpUsername.IsDefault);
        }

        [Test]
        public void GetConnectionSettings_WhenPortSetInProject_ProjectValueIsInheritedDownToVm()
        {
            var service = CreateConnectionSettingsService();

            var projectSettings = service.GetConnectionSettings(CreateProjectNode());
            projectSettings.TypedCollection.RdpPort.Value = 13389;
            projectSettings.Save();

            // Inherited value is shown...
            var instanceSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            Assert.AreEqual(13389, instanceSettings.TypedCollection.RdpPort.Value);
            Assert.IsTrue(instanceSettings.TypedCollection.RdpPort.IsDefault);
        }

        [Test]
        public void GetConnectionSettings_WhenPortSetInZoneAndResetInVm_ZoneVmValueApplies()
        {
            var service = CreateConnectionSettingsService();

            var zoneSettings = service.GetConnectionSettings(CreateZoneNode());
            zoneSettings.TypedCollection.RdpPort.Value = 13389;
            zoneSettings.Save();

            // Reset to default...
            var instanceSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            instanceSettings.TypedCollection.RdpPort.Value = 3389;
            instanceSettings.Save();

            // Own value is shown...
            var effectiveSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            Assert.AreEqual(3389, effectiveSettings.TypedCollection.RdpPort.Value);
            Assert.IsFalse(effectiveSettings.TypedCollection.RdpPort.IsDefault);
        }

        [Test]
        public void GetConnectionSettings_WhenPortSetInProjectAndResetInVm_ZoneVmValueApplies()
        {
            var service = CreateConnectionSettingsService();

            var projectSettings = service.GetConnectionSettings(CreateProjectNode());
            projectSettings.TypedCollection.RdpPort.Value = 13389;
            projectSettings.Save();

            // Reset to default...
            var instanceSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            instanceSettings.TypedCollection.RdpPort.Value = 3389;
            instanceSettings.Save();

            // Own value is shown...
            var effectiveSettings = service.GetConnectionSettings(CreateVmInstanceNode());
            Assert.AreEqual(3389, effectiveSettings.TypedCollection.RdpPort.Value);
            Assert.IsFalse(effectiveSettings.TypedCollection.RdpPort.IsDefault);
        }

        //---------------------------------------------------------------------
        // GetConnectionSettings - Filtering.
        //---------------------------------------------------------------------

        [Test]
        public void GetConnectionSettings_WhenInstanceIsWindows_ThenSettingsContainRdpAndAppSettings()
        {
            var service = CreateConnectionSettingsService();
            var vmNode = CreateVmInstanceNode(true);

            var settings = service.GetConnectionSettings(vmNode);

            CollectionAssert.IsSupersetOf(
                settings.Settings,
                settings.TypedCollection.RdpSettings);
            CollectionAssert.IsSupersetOf(
                settings.Settings,
                settings.TypedCollection.AppSettings);

            CollectionAssert.IsNotSupersetOf(
                settings.Settings,
                settings.TypedCollection.SshSettings);
        }

        [Test]
        public void GetConnectionSettings_WhenInstanceIsLinux_ThenSettingsContainSshAndAppSettings()
        {
            var service = CreateConnectionSettingsService();
            var vmNode = CreateVmInstanceNode(false);

            var settings = service.GetConnectionSettings(vmNode);

            CollectionAssert.IsSupersetOf(
                settings.Settings,
                settings.TypedCollection.SshSettings);
            CollectionAssert.IsSupersetOf(
                settings.Settings,
                settings.TypedCollection.AppSettings);

            CollectionAssert.IsNotSupersetOf(
                settings.Settings,
                settings.TypedCollection.RdpSettings);
        }

        //---------------------------------------------------------------------
        // AppSettings.
        //---------------------------------------------------------------------

        [Test]
        public void AppUsername()
        {
            var service = CreateConnectionSettingsService();
            var vmNode = CreateVmInstanceNode(false);

            var settings = service.GetConnectionSettings(vmNode);

            settings.TypedCollection.AppUsername.Value = "sa";
            settings.TypedCollection.AppUsername.Value = null;
            settings.TypedCollection.AppUsername.Value = string.Empty;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.TypedCollection.AppUsername.Value = "has spaces");
        }
    }
}
