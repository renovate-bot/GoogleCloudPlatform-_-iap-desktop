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

using Google.Solutions.Apis.Locator;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Core.EntityModel
{
    /// <summary>
    /// Discovers entities by their ancestry.
    /// </summary>
    /// <remarks>
    /// Implementing types must also implement
    /// the generic version of this interface.
    /// </remarks>
    public interface IEntityNavigator
    {
    }

    /// <summary>
    /// Discovers entities by their ancestry.
    /// </summary>
    /// <typeparam name="TLocator">Parent locator type</typeparam>
    /// <typeparam name="TEntity">Entity type</typeparam>
    public interface IEntityNavigator<TLocator, TEntity> : IEntityNavigator
        where TLocator : ILocator
        where TEntity : IEntity<ILocator>
    {
        /// <summary>
        /// List direct descendents.
        /// </summary>
        Task<IEnumerable<TEntity>> ListDescendantsAsync(
            TLocator parent,
            CancellationToken cancellationToken);
    }
}
