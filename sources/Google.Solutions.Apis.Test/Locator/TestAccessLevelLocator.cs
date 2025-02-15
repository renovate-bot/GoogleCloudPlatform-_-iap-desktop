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
using NUnit.Framework;
using System;

namespace Google.Solutions.Apis.Test.Locator
{
    [TestFixture]
    public class TestAccessLevelLocator
        : TestLocatorFixtureBase<AccessLevelLocator>
    {
        protected override AccessLevelLocator CreateInstance()
        {
            return new AccessLevelLocator("policy-1", "level-1");
        }

        //---------------------------------------------------------------------
        // TryParse.
        //---------------------------------------------------------------------

        [Test]
        public void TryParse_WhenPathIsValid()
        {
            Assert.IsTrue(AccessLevelLocator.TryParse(
                "accessPolicies/policy-1/accessLevels/level-1",
                out var locator));

            Assert.IsNotNull(locator);
            Assert.AreEqual("policy-1", locator!.AccessPolicy);
            Assert.AreEqual("level-1", locator.AccessLevel);
        }

        [Test]
        public void TryParse_WhenPathInvalid()
        {
            Assert.IsFalse(AccessLevelLocator.TryParse(
                "accessPolicies/policy-1/notaccessLevels/level-1",
                out var _));
            Assert.IsFalse(AccessLevelLocator.TryParse(
                "/policy-1/accessLevels/level-1",
                out var _));
            Assert.IsFalse(AccessLevelLocator.TryParse(
                "/",
                out var _));
        }

        //---------------------------------------------------------------------
        // Parse.
        //---------------------------------------------------------------------

        [Test]
        public void Parse_WhenPathIsValid()
        {
            var ref1 = AccessLevelLocator.Parse(
                "accessPolicies/policy-1/accessLevels/level-1");

            Assert.AreEqual("policy-1", ref1.AccessPolicy);
            Assert.AreEqual("level-1", ref1.AccessLevel);
        }

        [Test]
        public void Parse_WhenPathInvalid()
        {
            Assert.Throws<ArgumentException>(() => AccessLevelLocator.Parse(
                "accessPolicies/policy-1/notaccessLevels/level-1"));
            Assert.Throws<ArgumentException>(() => AccessLevelLocator.Parse(
                "/policy-1/accessLevels/level-1"));
            Assert.Throws<ArgumentException>(() => AccessLevelLocator.Parse(
                "/"));
        }

        //---------------------------------------------------------------------
        // Equality.
        //---------------------------------------------------------------------

        [Test]
        public void Equals_WhenObjectsNotEquivalent_ThenEqualsReturnsFalse()
        {
            var ref1 = new AccessLevelLocator("proj-1", "level-1");
            var ref2 = new AccessLevelLocator("proj-2", "level-1");

            Assert.IsFalse(ref1.Equals(ref2));
            Assert.IsFalse(ref1.Equals((object)ref2));
            Assert.IsFalse(ref1 == ref2);
            Assert.IsTrue(ref1 != ref2);
        }

        //---------------------------------------------------------------------
        // ToString.
        //---------------------------------------------------------------------

        [Test]
        public void ToString_WhenCreatedFromPath_ThenToStringReturnsPath()
        {
            var path = "accessPolicies/policy-1/accessLevels/level-1";

            Assert.AreEqual(
                path,
                AccessLevelLocator.Parse(path).ToString());
        }
    }
}
