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

using Google.Solutions.Terminal.Controls;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Google.Solutions.Terminal.TestApp
{
    public static class Program
    {
        private static ClientBase CreateClient(string? commandLineSwitch)
        {

            if (commandLineSwitch == "/rdp")
            {
                return new RdpClient();
            }
            else
            {
                var psClient = new LocalShellClient("powershell.exe");
                psClient.Terminal.ForeColor = Color.LightGray;
                psClient.Terminal.BackColor = Color.Black;

                return psClient;
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            using (var form = new ClientDiagnosticsWindow<ClientBase>(CreateClient(args.FirstOrDefault())))
            {
                Application.Run(form);
            }
        }
    }
}