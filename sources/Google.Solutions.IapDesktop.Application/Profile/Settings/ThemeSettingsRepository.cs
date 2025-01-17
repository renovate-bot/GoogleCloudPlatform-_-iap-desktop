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

using Google.Solutions.Settings;
using Google.Solutions.Settings.Collection;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;

namespace Google.Solutions.IapDesktop.Application.Profile.Settings
{
    /// <summary>
    /// Application theme.
    /// </summary>
    public enum ApplicationTheme
    {
        [Description("Light theme")]
        Light = 0,

        [Description("Current Windows theme")]
        System = 1,

        [Description("Dark theme")]
        Dark = 2,
        _Default = System
    }

    /// <summary>
    /// Mode to use for high-DPI scaling.
    /// </summary>
    public enum ScalingMode
    {
        /// <summary>
        /// Default (blurry) scaling.
        /// </summary>
        [Description("Disabled")]
        None = 0,

        /// <summary>
        /// Enable GDI scaling.
        /// </summary>
        [Browsable(false)]
        LegacyGdi = 1,

        /// <summary>
        /// System DPI aware scaling.
        /// </summary>
        [Description("Match primary display")]
        SystemDpiAware = 2,

        _Default = SystemDpiAware
    }

    /// <summary>
    /// Theme-related settings.
    /// </summary>
    public interface IThemeSettings : ISettingsCollection
    {
        /// <summary>
        /// Current theme.
        /// </summary>
        ISetting<ApplicationTheme> Theme { get; }

        /// <summary>
        /// Enable GDI scaling for high-DPI monitors.
        /// </summary>
        ISetting<ScalingMode> ScalingMode { get; }
    }

    public class ThemeSettingsRepository : RepositoryBase<IThemeSettings>
    {
        public ThemeSettingsRepository(RegistryKey key)
            : this(new RegistrySettingsStore(key))
        {
        }

        public ThemeSettingsRepository(ISettingsStore store) : base(store)
        {
        }

        protected override IThemeSettings LoadSettings(ISettingsStore store)
            => new ThemeSettings(store);

        //---------------------------------------------------------------------
        // Inner class.
        //---------------------------------------------------------------------

        private class ThemeSettings : IThemeSettings
        {
            public ISetting<ApplicationTheme> Theme { get; }
            public ISetting<ScalingMode> ScalingMode { get; }

            public IEnumerable<ISetting> Settings => new ISetting[]
            {
                this.Theme,
                this.ScalingMode
            };

            internal ThemeSettings(ISettingsStore store)
            {
                this.Theme = store.Read<ApplicationTheme>(
                    "Theme",
                    "Theme",
                    null,
                    null,
                    ApplicationTheme._Default);
                this.ScalingMode = store.Read<ScalingMode>(
                    "IsGdiScalingEnabled", // Use previous setting for compatibility.
                    "IsGdiScalingEnabled",
                    null,
                    null,
                    Profile.Settings.ScalingMode._Default);
            }
        }
    }
}
