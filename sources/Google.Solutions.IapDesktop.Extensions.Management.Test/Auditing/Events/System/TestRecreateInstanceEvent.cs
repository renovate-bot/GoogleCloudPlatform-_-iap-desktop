﻿//
// Copyright 2019 Google LLC
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
using Google.Solutions.IapDesktop.Extensions.Management.Auditing.Events;
using Google.Solutions.IapDesktop.Extensions.Management.Auditing.Events.System;
using Google.Solutions.IapDesktop.Extensions.Management.Auditing.Logs;
using Google.Solutions.Testing.Application.Test;
using NUnit.Framework;

namespace Google.Solutions.IapDesktop.Extensions.Management.Test.Auditing.Events.System
{
    [TestFixture]
    public class TestRecreateInstanceEvent : ApplicationFixtureBase
    {
        [Test]
        public void ToEvent_WhenSeverityIsInfo()
        {
            var json = @"
               {
               'protoPayload': {
                 '@type': 'type.googleapis.com/google.cloud.audit.AuditLog',
                 'status': {},
                 'authenticationInfo': {
                   'principalEmail': 'system@google.com'
                 },
                 'requestMetadata': {
                   'requestAttributes': {},
                   'destinationAttributes': {}
                 },
                 'serviceName': 'compute.googleapis.com',
                 'methodName': 'compute.instances.repair.recreateInstance',
                 'resourceName': 'projects/project-1/zones/us-central1-a/instances/instance-1',
                 'request': {
                   '@type': 'type.googleapis.com/compute.instances.repair.recreateInstance'
                 }
               },
               'insertId': 'u5trcdfmye',
               'resource': {
                 'type': 'gce_instance',
                 'labels': {
                   'zone': 'us-central1-a',
                   'instance_id': '2162224123123123213',
                   'project_id': 'project-1'
                 }
               },
               'timestamp': '2020-05-03T19:36:19.812Z',
               'severity': 'INFO',
               'logName': 'projects/project-1/logs/cloudaudit.googleapis.com%2Fsystem_event',
               'operation': {
                 'id': 'repair-1588534579592-5a4c387547b81-65d715dc-460e6782',
                 'producer': 'compute.instances.repair.recreateInstance',
                 'first': true,
                 'last': true
               },
               'receiveTimestamp': '2020-05-03T19:36:20.544388587Z'
             }";

            var r = LogRecord.Deserialize(json)!;
            Assert.IsTrue(RecreateInstanceEvent.IsRecreateInstanceEvent(r));

            var e = (RecreateInstanceEvent)r.ToEvent();

            Assert.AreEqual(2162224123123123213, e.InstanceId);
            Assert.AreEqual("instance-1", e.Instance?.Name);
            Assert.AreEqual("us-central1-a", e.Instance?.Zone);
            Assert.AreEqual("project-1", e.Instance?.ProjectId);
            Assert.AreEqual("INFO", e.Severity);
            Assert.IsNull(e.Status);
            Assert.AreEqual(
                new InstanceLocator("project-1", "us-central1-a", "instance-1"),
                e.Instance);
        }
    }
}
