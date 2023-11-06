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
using Google.Solutions.Ssh.Cryptography;
using Google.Solutions.Ssh.Native;
using Google.Solutions.Testing.Apis.Integration;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Google.Solutions.Ssh.Test.Native
{
    [TestFixture]
    [UsesCloudResources]
    public class TestSshConnectedSession : SshFixtureBase
    {
        //---------------------------------------------------------------------
        // Banner.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenConnected_ThenGetRemoteBannerReturnsBanner(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var banner = connection.GetRemoteBanner();
                Assert.AreEqual(LIBSSH2_ERROR.NONE, session.LastError);
                Assert.IsNotNull(banner);
            }
        }


        //---------------------------------------------------------------------
        // Algorithms.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenConnected_ThenActiveAlgorithmsAreSet(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.KEX));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.HOSTKEY));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.CRYPT_CS));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.CRYPT_SC));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.MAC_CS));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.MAC_SC));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.COMP_CS));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.COMP_SC));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.LANG_CS));
                Assert.IsNotNull(connection.GetActiveAlgorithms(LIBSSH2_METHOD.LANG_SC));
            }
        }

        [Test]
        public async Task WhenRequestedAlgorithmInvalid_ThenActiveAlgorithmsThrowsArgumentException(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                Assert.Throws<ArgumentException>(
                    () => connection.GetActiveAlgorithms((LIBSSH2_METHOD)9999999));
            }
        }

        //---------------------------------------------------------------------
        // Host Key.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenConnected_ThenGetRemoteHostKeyReturnsKey(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var key = connection.GetRemoteHostKey();
                Assert.AreEqual(LIBSSH2_ERROR.NONE, session.LastError);
                Assert.IsNotNull(key);
            }
        }

        [Test]
        public async Task WhenConnected_ThenGetRemoteHostKeyTypeReturnsEcdsa256(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var keyType = connection.GetRemoteHostKeyType();
                Assert.AreEqual(LIBSSH2_ERROR.NONE, session.LastError);
                Assert.IsTrue(
                    keyType == LIBSSH2_HOSTKEY_TYPE.ECDSA_256 ||
                    keyType == LIBSSH2_HOSTKEY_TYPE.RSA);
            }
        }

        [Test]
        public async Task WhenConnected_ThenGetRemoteHostKeyHashReturnsKeyHash(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                Assert.IsNotNull(connection.GetRemoteHostKeyHash(LIBSSH2_HOSTKEY_HASH.MD5), "MD5");
                Assert.IsNotNull(connection.GetRemoteHostKeyHash(LIBSSH2_HOSTKEY_HASH.SHA1), "SHA1");

                // SHA256 is not always available.
                // Assert.IsNotNull(connection.GetRemoteHostKeyHash(LIBSSH2_HOSTKEY_HASH.SHA256), "SHA256");
            }
        }

        [Test]
        public async Task WhenRequestedAlgorithmInvalid_ThennGetRemoteHostKeyHashThrowsArgumentException(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                Assert.Throws<ArgumentException>(
                    () => connection.GetRemoteHostKeyHash((LIBSSH2_HOSTKEY_HASH)9999999));
            }
        }

        [Test]
        public async Task WhenNeitherEcdsaNorRsaHostKeyAlgorithmAllowed_ThenConnectThrowsException(
            [LinuxInstance(InitializeScript = InitializeScripts.AllowNeitherEcdsaNorRsaForHostKey)]
            ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            {
                //
                // NB. Connect should throw an exception, but libssh doesn't
                // set the last error. So we have to check the exception
                // message.
                //

                try
                {
                    session.Connect(endpoint);
                    Assert.Fail("Expected exception");
                }
                catch (SshNativeException e)
                {
                    Assert.AreEqual("Unable to exchange encryption keys", e.Message);
                }
            }
        }

        //---------------------------------------------------------------------
        // Banner.
        //---------------------------------------------------------------------

        [Test]
        public void WhenCustomBannerHasWrongPrefix_ThenSetLocalBannerThrowsArgumentException()
        {
            using (var session = CreateSession())
            {
                Assert.Throws<ArgumentException>(
                    () => session.SetLocalBanner("SSH-test-123"));
            }
        }

        [Test]
        public async Task WhenCustomBannerSet_ThenConnectionSucceeds(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            {
                session.SetLocalBanner("SSH-2.0-test-123");
                using (var connection = session.Connect(endpoint))
                {
                    Assert.IsFalse(connection.IsAuthenticated);
                }
            }

        }

        [Test]
        public async Task WhenConnected_GetRemoteBannerReturnsBanner(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            {
                using (var connection = session.Connect(endpoint))
                {
                    StringAssert.StartsWith("SSH", connection.GetRemoteBanner());
                }
            }
        }

        //---------------------------------------------------------------------
        // User auth.
        //---------------------------------------------------------------------

        [Test]
        public async Task WhenConnected_ThenIsAuthenticatedIsFalse(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                Assert.IsFalse(connection.IsAuthenticated);
            }
        }

        [Test]
        public async Task WhenConnected_ThenGetAuthenticationMethodsReturnsPublicKey(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var methods = connection.GetAuthenticationMethods(string.Empty);
                Assert.IsNotNull(methods);
                Assert.AreEqual(1, methods.Length);
                Assert.AreEqual("publickey", methods.First());
            }
        }

        [Test]
        public async Task WhenPublicKeyValidButUnrecognized_ThenAuthenticateThrowsAuthenticationFailed(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask,
            [Values(SshKeyType.Rsa3072, SshKeyType.EcdsaNistp256)] SshKeyType keyType)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            using (var signer = AsymmetricKeySigner.CreateEphemeral(keyType))
            {
                SshAssert.ThrowsNativeExceptionWithError(
                    session,
                    LIBSSH2_ERROR.AUTHENTICATION_FAILED,
                    () => connection.Authenticate(
                        new StaticAsymmetricKeyCredential("invaliduser", signer),
                        KeyboardInteractiveHandler.Silent));
            }
        }

        [Test]
        public async Task WhenSessionDisconnected_ThenAuthenticateThrowsSocketSend(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask,
            [Values(SshKeyType.Rsa3072, SshKeyType.EcdsaNistp256)] SshKeyType keyType)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);
            var credential = await GetCredentialForInstanceAsync(
                    instance,
                    keyType)
                .ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                connection.Dispose();

                SshAssert.ThrowsNativeExceptionWithError(
                    session,
                    LIBSSH2_ERROR.SOCKET_SEND,
                    () => connection.Authenticate(
                        credential,
                        KeyboardInteractiveHandler.Silent));
            }
        }

        [Test]
        public async Task WhenPublicKeyValidAndKnownFromMetadata_ThenAuthenticationSucceeds(
            [LinuxInstance] ResourceTask<InstanceLocator> instanceLocatorTask,
            [Values(
                SshKeyType.Rsa3072,
                SshKeyType.EcdsaNistp256,
                SshKeyType.EcdsaNistp384,
                SshKeyType.EcdsaNistp521)] SshKeyType keyType)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);
            var credential = await GetCredentialForInstanceAsync(
                    instance,
                    keyType)
                .ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var authSession = connection.Authenticate(
                    credential,
                    KeyboardInteractiveHandler.Silent);
                Assert.IsNotNull(authSession);
            }
        }

        //---------------------------------------------------------------------
        // 2FA.
        //---------------------------------------------------------------------

        //
        // Service acconts can't use 2FA, so emulate the 2FA prompting behavior
        // by setting up SSHD to require a public key *and* a keyboard-interactive.
        //
        private const string RequireSshPassword =
            "cat << EOF > /etc/ssh/sshd_config\n" +
            "UsePam yes\n" +
            "AuthenticationMethods publickey,keyboard-interactive\n" +
            "EOF\n" +
            "systemctl restart sshd";

        [Test]
        public async Task When2faRequiredAndPromptReturnsWrongValue_ThenPromptIsRetried(
            [LinuxInstance(InitializeScript = RequireSshPassword)] ResourceTask<InstanceLocator> instanceLocatorTask,
            [Values(SshKeyType.Rsa3072, SshKeyType.EcdsaNistp256)] SshKeyType keyType)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);
            var credential = await GetCredentialForInstanceAsync(
                    instance,
                    keyType)
                .ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var twoFaHandler = new KeyboardInteractiveHandler(
                    (name, instruction, prompt, echo) =>
                    {
                        Assert.AreEqual("Password: ", prompt);
                        Assert.IsFalse(echo);

                        return "wrong";
                    });

                SshAssert.ThrowsNativeExceptionWithError(
                    session,
                    LIBSSH2_ERROR.AUTHENTICATION_FAILED,
                    () => connection.Authenticate(credential, twoFaHandler));

                Assert.AreEqual(
                    SshConnectedSession.KeyboardInteractiveRetries,
                    twoFaHandler.PromptCount);
            }
        }

        [Test]
        public async Task When2faRequiredAndPromptReturnsNull_ThenPromptIsRetried(
            [LinuxInstance(InitializeScript = RequireSshPassword)] ResourceTask<InstanceLocator> instanceLocatorTask,
            [Values(SshKeyType.Rsa3072, SshKeyType.EcdsaNistp256)] SshKeyType keyType)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);
            var credential = await GetCredentialForInstanceAsync(
                    instance,
                    keyType)
                .ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var twoFactorHandler = new KeyboardInteractiveHandler(
                    (name, instruction, prompt, echo) =>
                    {
                        Assert.AreEqual("Password: ", prompt);
                        Assert.IsFalse(echo);

                        return null;
                    });

                SshAssert.ThrowsNativeExceptionWithError(
                    session,
                    LIBSSH2_ERROR.AUTHENTICATION_FAILED,
                    () => connection.Authenticate(credential, twoFactorHandler));

                Assert.AreEqual(SshConnectedSession.KeyboardInteractiveRetries, twoFactorHandler.PromptCount);
            }
        }

        [Test]
        public async Task When2faRequiredAndPromptThrowsException_ThenAuthenticationFailsWithoutRetry(
            [LinuxInstance(InitializeScript = RequireSshPassword)] ResourceTask<InstanceLocator> instanceLocatorTask,
            [Values(SshKeyType.Rsa3072, SshKeyType.EcdsaNistp256)] SshKeyType keyType)
        {
            var instance = await instanceLocatorTask;
            var endpoint = await GetPublicSshEndpointAsync(instance).ConfigureAwait(false);
            var credential = await GetCredentialForInstanceAsync(
                    instance,
                    keyType)
                .ConfigureAwait(false);

            using (var session = CreateSession())
            using (var connection = session.Connect(endpoint))
            {
                var twoFactorHandler = new KeyboardInteractiveHandler(
                    (name, instruction, prompt, echo) =>
                    {
                        throw new OperationCanceledException();
                    });

                Assert.Throws<OperationCanceledException>(
                    () => connection.Authenticate(credential, twoFactorHandler));
                Assert.AreEqual(1, twoFactorHandler.PromptCount);
            }
        }
    }
}
