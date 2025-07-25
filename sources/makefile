#
# Copyright 2019 Google LLC
#
# Licensed to the Apache Software Foundation (ASF) under one
# or more contributor license agreements.  See the NOTICE file
# distributed with this work for additional information
# regarding copyright ownership.  The ASF licenses this file
# to you under the Apache License, Version 2.0 (the
# "License"); you may not use this file except in compliance
# with the License.  You may obtain a copy of the License at
# 
#   http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing,
# software distributed under the License is distributed on an
# "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
# KIND, either express or implied.  See the License for the
# specific language governing permissions and limitations
# under the License.
#

#------------------------------------------------------------------------------
# Basic configuration
#------------------------------------------------------------------------------

PRODUCT_VERSION = 2.47
CONFIGURATION = Release
PLATFORM = x86
TARGET_FRAMEWORK = net47

!if ( "$(KOKORO_BUILD_NUMBER)" != "" ) 
BUILD_NUMBER = $(KOKORO_BUILD_NUMBER)
!else
BUILD_NUMBER = 1
!endif

BINARY_VERSION = $(PRODUCT_VERSION).$(BUILD_NUMBER)

MAIN_ASSEMBLY = Google.Solutions.IapDesktop\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\IapDesktop.exe

INSTALLER_SNAPSHOT = dist\$(PLATFORM)\InstallerSnapshot-$(BINARY_VERSION)-$(PLATFORM)-$(CONFIGURATION).zip
INSTALLER_PACKAGE  = dist\$(PLATFORM)\IapDesktop-$(BINARY_VERSION)-$(PLATFORM)-$(CONFIGURATION).msi
SYMBOLS_PACKAGE    = dist\$(PLATFORM)\Symbols-$(BINARY_VERSION)-$(PLATFORM)-$(CONFIGURATION).zip
POLICIES_PACKAGE   = dist\$(PLATFORM)\PolicyTemplates-$(BINARY_VERSION)-$(PLATFORM)-$(CONFIGURATION).zip

NUNIT_ASSEMBLIES = \
	Google.Solutions.Apis.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Apis.Test.dll	\
	Google.Solutions.Platform.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Platform.Test.dll	\
	Google.Solutions.Common.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Common.Test.dll	\
	Google.Solutions.Iap.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Iap.Test.dll \
	Google.Solutions.Ssh.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Ssh.Test.dll \
	Google.Solutions.Mvvm.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Mvvm.Test.dll \
	Google.Solutions.Settings.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Settings.Test.dll \
	Google.Solutions.Terminal.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.Terminal.Test.dll \
	Google.Solutions.IapDesktop.Core.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.IapDesktop.Core.Test.dll \
	Google.Solutions.IapDesktop.Application.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.IapDesktop.Application.Test.dll \
	Google.Solutions.IapDesktop.Extensions.Management.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.IapDesktop.Extensions.Management.Test.dll \
	Google.Solutions.IapDesktop.Extensions.Session.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.IapDesktop.Extensions.Session.Test.dll \
	Google.Solutions.IapDesktop.Extensions.Explorer.Test\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\Google.Solutions.IapDesktop.Extensions.Explorer.Test.dll

NUNIT_COVERAGE_FILTER = \
	+[Google.Solutions.Apis]* \
	+[Google.Solutions.Platform]* \
	+[Google.Solutions.Common]* \
	+[Google.Solutions.Iap]* \
	+[Google.Solutions.Ssh]* \
	+[Google.Solutions.Mvvm]* \
	+[Google.Solutions.Settings]* \
	+[Google.Solutions.Terminal]* \
	+[Google.Solutions.IapDesktop.Core]* \
	+[Google.Solutions.IapDesktop.Application]* \
	+[Google.Solutions.IapDesktop.Extensions.Management]* \
	+[Google.Solutions.IapDesktop.Extensions.Session]* \
	+[Google.Solutions.IapDesktop.Extensions.Explorer]*
    
NUNIT_FILTER = cat!=InteractiveTest and cat!=RdpTest and cat!=ProxyAutoconfigTest
!if ( "$(SKIP_INTEGRATION_TESTS)" != "" )
NUNIT_FILTER = $(NUNIT_FILTER) and cat!=CloudIntegrationTest
!endif

REMOTE_SIGNTOOL = ksigntool
REMOTE_SIGNTOOL_FLAGS = GOOGLE_EXTERNAL /v /debug /t http://timestamp.digicert.com
SIGNTOOL = signtool

NUGET_PACKAGES = $(USERPROFILE)\.nuget\packages

#------------------------------------------------------------------------------
# Ancillary variables
#------------------------------------------------------------------------------

!if ( "$(__BUILD_ENV_INITIALIZED)" == "" ) 
!	ERROR "build must be invoked by build.ps1"
!endif

POWERSHELL = powershell -NoProfile -ExecutionPolicy Unrestricted

#------------------------------------------------------------------------------
# Output targets
#------------------------------------------------------------------------------

default: installer

themes: Google.Solutions.IapDesktop.Application\Theme\Resources\Dark.vstheme \
        Google.Solutions.IapDesktop.Application\Theme\Resources\Light.vstheme
    !$(POWERSHELL) $(MAKEDIR)\scripts\gzip.ps1 -File $**

Google.Solutions.IapDesktop\OAuthClient.cs:
	@echo "========================================================"
	@echo "=== Patching OAuth credentials                      ==="
	@echo "========================================================"

	copy "$(KOKORO_GFILE_DIR)\OAuthClient.cs" $@

$(MAIN_ASSEMBLY) $(NUNIT_ASSEMBLIES): \
			check-copyright-headers \
			themes \
			Google.Solutions.IapDesktop\OAuthClient.cs
	@echo "========================================================"
	@echo "=== Building solution                                ==="
	@echo "========================================================"
    
	msbuild /t:Rebuild /p:Configuration=$(CONFIGURATION);Platform=$(PLATFORM);Version=$(BINARY_VERSION)
	
!if ( "$(SKIP_ASSEMBLY_BUILD)" == "" ) 
$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION): $(MAIN_ASSEMBLY)
	@echo "========================================================"
	@echo "=== Creating staging area for installer              ==="
	@echo "========================================================"
    
    if not exist $(MAKEDIR)\installer\obj \
        md $(MAKEDIR)\installer\obj
    if not exist $(MAKEDIR)\installer\obj\$(PLATFORM) \
        md $(MAKEDIR)\installer\obj\$(PLATFORM)
    if not exist $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION) \
        md $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
		
	copy $(MAKEDIR)\installer\*.* \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
	copy $(MAKEDIR)\Google.Solutions.IapDesktop\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\*.exe* \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
	copy $(MAKEDIR)\Google.Solutions.IapDesktop\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\*.dll \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
	copy $(MAKEDIR)\Google.Solutions.IapDesktop\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\*.ps1 \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
	copy $(MAKEDIR)\Google.Solutions.IapDesktop\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\*.cmd \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
!endif

$(INSTALLER_PACKAGE).wixobj: \
			$(MAKEDIR)\installer\Product.wxs \
			$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
	@echo "========================================================"
	@echo "=== Compiling installer                              ==="
	@echo "========================================================"

	candle \
		-nologo \
		-out $(INSTALLER_PACKAGE).wixobj \
		-dCONFIGURATION=$(CONFIGURATION) \
		-dPLATFORM=$(PLATFORM) \
		-dVERSION=$(BINARY_VERSION) \
		-dBASEDIR=$(MAKEDIR)\installer \
		-arch x86 \
		-ext WixUIExtension.dll \
		-ext WixUtilExtension.dll \
		 $(MAKEDIR)\installer\Product.wxs

$(INSTALLER_PACKAGE): $(INSTALLER_PACKAGE).wixobj
	@echo "========================================================"
	@echo "=== Linking installer                                ==="
	@echo "========================================================"

!if ( "$(CODESIGN)" != "" ) 
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\Google.*.dll
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\libssh2.dll
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\WeifenLuo.WinFormsUI.Docking*.dll
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\*Interop*.dll
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\IapDesktop.exe
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\icu.dll
	
	$(SIGNTOOL) verify /pa $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\*.exe
	$(SIGNTOOL) verify /pa $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\*.dll
!endif

	light \
		-nologo \
		-out $@ \
		-sw1076 \
		-cultures:null \
		-loc $(MAKEDIR)\installer\Product.wxl \
		-ext WixUIExtension.dll \
		-ext WixUtilExtension.dll \
		$(INSTALLER_PACKAGE).wixobj

!if ( "$(CODESIGN)" != "" ) 
	$(REMOTE_SIGNTOOL) sign $(REMOTE_SIGNTOOL_FLAGS) $(INSTALLER_PACKAGE)
!endif

# NB. The installer snapshot must contain all tools necessary to build the installer.
$(INSTALLER_SNAPSHOT): $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)
	@echo "========================================================"
	@echo "=== Packaging installer snapshot                     ==="
	@echo "========================================================"
	
    if not exist $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\tools \
        md $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\tools

    copy $(MAKE) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\tools
    copy $(NUGET) $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\tools

	$(POWERSHELL) $(MAKEDIR)\scripts\create-zip.ps1 \
		-ZipFile $(INSTALLER_SNAPSHOT) \
		-Path $(MAKEDIR)\makefile, \
			  $(MAKEDIR)\build.ps1, \
			  $(MAKEDIR)\installer, \
              $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\tools, \
			  $(NUGET_PACKAGES)\Wix\$(WIX)\tools

$(SYMBOLS_PACKAGE): $(MAIN_ASSEMBLY)
	@echo "========================================================"
	@echo "=== Packaging symbols                                ==="
	@echo "========================================================"

	if not exist $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\symbols \
		md $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\symbols

	copy $(MAKEDIR)\Google.Solutions.IapDesktop\bin\$(PLATFORM)\$(CONFIGURATION)\$(TARGET_FRAMEWORK)\*.pdb \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\symbols

	$(POWERSHELL) $(MAKEDIR)\scripts\create-zip.ps1 $(SYMBOLS_PACKAGE) \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\symbols\*
        
$(POLICIES_PACKAGE):
	@echo "========================================================"
	@echo "=== Packaging policy templates                       ==="
	@echo "========================================================"

	if not exist $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies \
		md $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies
	if not exist $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies\en-us \
		md $(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies\en-us

	copy $(MAKEDIR)\installer\policies\*.admx \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies
	copy $(MAKEDIR)\installer\policies\*.adml \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies\en-us

	$(POWERSHELL) $(MAKEDIR)\scripts\create-zip.ps1 $(POLICIES_PACKAGE) \
		$(MAKEDIR)\installer\obj\$(PLATFORM)\$(CONFIGURATION)\policies\*

#------------------------------------------------------------------------------
# Main targets
#------------------------------------------------------------------------------

cmd:
	@echo "========================================================"
	@echo "=== Opening command prompt                           ==="
	@echo "========================================================"

	cmd

clean:
	@echo "========================================================"
	@echo "=== Cleaning up                                      ==="
	@echo "========================================================"

	$(POWERSHELL) $(MAKEDIR)\scripts\clean.ps1

restore:
	@echo "========================================================"
	@echo "=== Restoring nuget packages                         ==="
	@echo "========================================================"

	nuget restore

check-copyright-headers:
	@echo "========================================================"
	@echo "=== Checking copyright headers                       ==="
	@echo "========================================================"

	$(POWERSHELL) $(MAKEDIR)\scripts\check-copyright-headers.ps1

build: $(MAIN_ASSEMBLY)

# NB. The OpenCover version must match the version of the CLR installed
test: $(NUNIT_ASSEMBLIES)
	@echo "========================================================"
	@echo "=== Testing solution                                 ==="
	@echo "========================================================"

	OpenCover.Console.exe \
		-register:user \
		-returntargetcode \
		-target:nunit3-console.exe \
		"-targetargs:$(NUNIT_ASSEMBLIES) --result=sponge_log.xml;transform=kokoro\nunit-to-sponge.xsl --workers=1 --process=Separate --agents=15 --where ""$(NUNIT_FILTER)""" \
		-filter:"$(NUNIT_COVERAGE_FILTER)" \
		"-excludebyattribute:*.SkipCodeCoverage*;*CompilerGenerated*" \
		-output:opencovertests.xml

	$(NUGET_PACKAGES)\ReportGenerator\$(REPORTGENERATOR)\tools\net47\ReportGenerator.exe \
		-reports:opencovertests.xml \
		-targetdir:coveragereport \
		-reporttypes:HTML

installer: $(INSTALLER_PACKAGE)
installer-snapshot: $(INSTALLER_SNAPSHOT) 
symbols: $(SYMBOLS_PACKAGE)
policies: $(POLICIES_PACKAGE)

#------------------------------------------------------------------------------
# Kokoro specific targets
#------------------------------------------------------------------------------
#
# NB. Run integration tests for single platform only to save time, and
#     skip arm64 tests entirely because Kokoro is x64.
#
kokoro-continuous-integration:
    $(MAKE) $(MAKEFLAGS) PLATFORM=x86 SKIP_INTEGRATION_TESTS=1 \
        installer \
        installer-snapshot \
        symbols \
        policies \
        test
		
    $(MAKE) $(MAKEFLAGS) PLATFORM=arm64 \
        installer \
        installer-snapshot \
        symbols \
        policies

    $(MAKE) $(MAKEFLAGS) PLATFORM=x64 \
        installer \
        installer-snapshot \
        symbols \
        policies \
        test
