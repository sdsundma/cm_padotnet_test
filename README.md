# CM ProtectApp .Net Testing

A WPF desktop application for exercising and timing the SafeNet ProtectApp .NET API
against a live CipherTrust / KeySecure server. It runs persistent-session and
dynamic-session encrypt/decrypt loops concurrently, displaying real-time latency
statistics and a scrolling operation log for each session type.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Repository Layout](#repository-layout)
- [Build](#build)
- [Configuration](#configuration)
- [Running](#running)
- [Usage](#usage)

---

## Prerequisites

### Build machine

| Requirement | Details |
|---|---|
| OS | Windows (x64) |
| .NET Framework | 4.7 or later (runtime + developer pack) |
| MSBuild | Included with Visual Studio 2017+ **or** the [Build Tools for Visual Studio](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022) workload |
| **ProtectApp for .NET** | Must be installed — provides `ingdnp.dll`, referenced by the project at `C:\Program Files\SafeNet ProtectApp\DotNet\ingdnp.dll` |

MSBuild must be on `PATH`, or invoke it by full path.  Visual Studio Community
(free) satisfies both the .NET and MSBuild requirements.

### Runtime / deployment machine

| Requirement | Details |
|---|---|
| OS | Windows (x64) |
| .NET Framework | 4.7 or later runtime |
| **ProtectApp for .NET** | Must be installed — provides `ingdnp.dll` and the `ProtectAppForDotNet.properties` configuration file |
| Network access | TCP connectivity to the CipherTrust / KeySecure NAE server on the configured port (default `9002`) |

### Installing ProtectApp for .NET

The ProtectApp for .NET provider is a separate SafeNet / Thales product and must
be obtained and installed independently.  After installation, the default location
for the provider files is:

```
C:\Program Files\SafeNet ProtectApp\DotNet\
```

This directory contains `ingdnp.dll` (the managed API assembly) and
`ProtectAppForDotNet.properties` (the NAE connection configuration file).
Both files must be present and correctly configured before building or running
this application.

---

## Repository Layout

```
cm_padotnet_test/
├── src/                        # Main WPF application source
│   ├── CMPADotNetTest.csproj
│   ├── App.config              # Default startup values and properties-file path
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── StartupDialog.xaml / .cs
│   ├── Controls/
│   │   └── SessionPanel.xaml / .cs   # Reusable per-session UI panel
│   ├── Models/
│   │   ├── OperationStats.cs
│   │   ├── SessionPanelViewModel.cs
│   │   └── StartupConfig.cs
│   └── Services/
│       ├── DynamicSessionTester.cs
│       ├── PersistentSessionTester.cs
│       └── ProtectAppService.cs
├── doc/                        # SafeNet ProtectApp SDK reference PDFs
└── examples/                   # Upstream C# example files from the SDK
```

---

## Build

### 1. Clone the repository

```
git clone <repo-url>
cd cm_padotnet_test
```

### 2. Build (Release, x64)

**Using Visual Studio:**

Open `src\CMPADotNetTest.csproj`, set the configuration to **Release | x64**,
and build.

**Using MSBuild from the command line:**

```
msbuild src\CMPADotNetTest.csproj /p:Configuration=Release /p:Platform=x64
```

If MSBuild is not on `PATH`, invoke it by full path, for example:

```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ^
    src\CMPADotNetTest.csproj /p:Configuration=Release /p:Platform=x64
```

### 3. Build output

A successful build produces the following in `src\bin\Release\`:

```
CMPADotNetTest.exe
CMPADotNetTest.exe.config
ingdnp.dll                     (copied from the ProtectApp installation)
```

---

## Configuration

There are two configuration files that must be reviewed before running.

### `ProtectAppForDotNet.properties`

This file is provided and managed by the ProtectApp for .NET installation.  Its
default location is:

```
C:\Program Files\SafeNet ProtectApp\DotNet\ProtectAppForDotNet.properties
```

It controls how `ingdnp.dll` connects to the CipherTrust / KeySecure NAE server.
The following properties must be configured for a TCP connection:

| Property | Description | Example |
|---|---|---|
| `NAE_IP` | IP address(es) of the CipherTrust Manager / KeySecure server. For load-balanced configurations, separate multiple addresses with a colon. | `192.168.1.33` or `192.168.1.33:192.168.1.34` |
| `NAE_Port` | TCP port the NAE server listens on | `9002` |
| `Protocol` | Transport protocol used for the NAE connection | `tcp` |
| `Log_Level` | Logging verbosity for the ProtectApp provider | `INFO` |
| `Log_File` | Path to the provider log file | `C:\Logs\ProtectApp.log` |

Refer to the ProtectApp for .NET User Guide (see `doc/`) for the full list of
available properties and their valid values.

### `src\App.config` (compile-time defaults)

Pre-populates the startup dialog.  These values can always be overridden at
runtime in the Configuration dialog.

| Key | Description |
|---|---|
| `PropertiesFilePath` | Path to `ProtectAppForDotNet.properties` as seen by the running executable |
| `StaticTestValue` | Plaintext used as encrypt/decrypt input |
| `DefaultUsername` | NAE user pre-filled in the dialog |
| `DefaultPassword` | NAE password pre-filled in the dialog |
| `DefaultKeyName` | Key name pre-filled in the dialog |
| `DefaultDuration` | Test run duration in seconds (default `1800`) |

The default `PropertiesFilePath` points to the standard ProtectApp installation
directory:

```xml
<add key="PropertiesFilePath"
     value="C:\Program Files\SafeNet ProtectApp\DotNet\ProtectAppForDotNet.properties"/>
```

---

## Running

1. Ensure the ProtectApp for .NET provider is installed and `ProtectAppForDotNet.properties`
   is configured for the target NAE server.
2. Ensure the target machine has network access to the configured NAE server.
3. Launch `CMPADotNetTest.exe`.
4. The **Configuration** dialog appears.  Fill in or confirm:
   - **Username / Password** — NAE credentials
   - **Key Name** — name of the symmetric key to use for encrypt/decrypt
   - **Duration (sec)** — how long to run the test
   - **Test Isolation** — which session type(s) to run (see [Usage](#usage))
5. Click **Start Testing**.

---

## Usage

### Main window

Two side-by-side panels are displayed (or one, depending on Test Isolation):

| Panel | Description |
|---|---|
| **Persistent Session** | Opens a single NAE session at startup and reuses it for the entire run, automatically reconnecting on session loss |
| **Dynamic Session** | Opens and closes a new NAE session for every encrypt/decrypt iteration |

Each panel shows:
- A **stats table** with operation counts, current / average / min / max latency (ms), and error counts for Session Open, Encrypt, and Decrypt
- **Live gauges** for the current latency of each operation
- A **scrolling operation log** with per-iteration timestamps and values

The header bar shows elapsed time remaining, overall progress, and a **Restart**
button to return to the Configuration dialog without closing the application.

### Test Isolation

The **Test Isolation** drop-down in the Configuration dialog controls which
session type is active:

| Selection | Behaviour |
|---|---|
| Persistent & Dynamic | Both panels run simultaneously (default) |
| Persistent only | Only the Persistent Session panel runs |
| Dynamic only | Only the Dynamic Session panel runs |

Isolating a single session type simplifies log analysis when comparing session
strategies or diagnosing issues specific to one mode.
