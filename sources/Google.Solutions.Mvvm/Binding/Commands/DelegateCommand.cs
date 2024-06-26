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

using Google.Solutions.Common.Util;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable VSTHRD100 // Avoid async void methods

namespace Google.Solutions.Mvvm.Binding.Commands
{
    /// <summary>
    /// Command that wraps an arbitrary delegate.
    /// 
    /// DelegateCommands are not observable, but (unlike
    /// ObservableCommands) can for any type of event.
    /// </summary>
    public class DelegateCommand<THandler, TEventArgs> : CommandBase
        where THandler : Delegate
        where TEventArgs : EventArgs
    {
        private readonly EventHandler<TEventArgs> handler;
        public DelegateCommand(
            string text,
            Action<TEventArgs> execute,
            IBindingContext bindingContext) : base(text)
        {
            void invokeHandler(object sender, TEventArgs args)
            {
                try
                {
                    execute(args);

                    bindingContext.OnCommandExecuted(this);
                }
                catch (Exception e) when (e.IsCancellation())
                {
                    // Ignore.
                }
                catch (Exception e)
                {
                    bindingContext.OnCommandFailed(
                        (sender as Control)?.FindForm(),
                        this,
                        e);
                }
            }

            this.handler = invokeHandler;
        }

        public DelegateCommand(
            string text,
            Func<TEventArgs, Task> executeAsync,
            IBindingContext bindingContext) : base(text)
        {
            async void invokeHandler(object sender, TEventArgs args)
            {
                try
                {
                    await executeAsync(args).ConfigureAwait(true);

                    bindingContext.OnCommandExecuted(this);
                }
                catch (Exception e) when (e.IsCancellation())
                {
                    // Ignore.
                }
                catch (Exception e)
                {
                    bindingContext.OnCommandFailed(
                        (sender as Control)?.FindForm(),
                        this,
                        e);
                }
            }

            this.handler = invokeHandler;
        }

        /// <summary>
        /// Delegate to assign to an event handler.
        /// </summary>
        public void Execute(object sender, TEventArgs args)
        {
            this.handler(sender, args);
        }
    }
}
