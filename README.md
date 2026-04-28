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
  - [Test Isolation](#test-isolation)
  - [Key Caching](#key-caching)
  - [Loop Delay](#loop-delay)
  - [Provider Property Overrides](#provider-property-overrides)

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
│   ├── PropertiesOverrideDialog.xaml / .cs   # Runtime property override dialog
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
| `DefaultDuration` | Test run duration in seconds (default `60`) |
| `DefaultCachePassphrase` | Passphrase pre-filled for disk key-cache encryption (default `asdf1234`; change before deployment) |

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
   - **Test Isolation** — which session type(s) to run (see [Test Isolation](#test-isolation))
   - **Key Caching** — key caching strategy (see [Key Caching](#key-caching))
   - **Loop Delay** — pause inserted between encrypt/decrypt iterations (see [Loop Delay](#loop-delay))
   - **Customize...** — optionally override provider connection properties at runtime (see [Provider Property Overrides](#provider-property-overrides))
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

### Key Caching

Selects how the ProtectApp provider caches symmetric keys between operations.
Caching trades memory or disk usage for reduced round-trips to the NAE server,
which lowers encrypt/decrypt latency once the cache is warm.

| Selection | Effect |
|---|---|
| **None** (default) | No key caching. Every operation contacts the NAE server. Highest network dependency; provides the clearest view of raw round-trip latency. |
| **Memory** | Keys are cached in process memory (`Symmetric_Key_Cache_Enabled = tcp_ok`). Cached keys survive individual operations for the life of the session, eliminating per-operation key-fetch round-trips. The cache is lost when the session closes or the application exits. |
| **Disk** | All memory-cache behaviour plus disk persistence (`Persistent_Cache_Enabled = yes`). The cache survives session close and application restart. The cache file is encrypted using the **Cache Passphrase** entered in the Configuration dialog. Requires a non-empty Cache Passphrase. |

**Cache Passphrase** is used only when **Key Caching** is **Disk**.  It is
pre-populated from `DefaultCachePassphrase` in `App.config`; the default value
(`asdf1234`) should be replaced with a site-specific passphrase before
deployment.

When a caching mode is active, the NAE properties it controls
(`Symmetric_Key_Cache_Enabled`, `Persistent_Cache_Enabled`) appear locked in
the Provider Property Overrides dialog and cannot be manually overridden.

---

### Loop Delay

Inserts a fixed pause after each encrypt/decrypt pair, allowing controlled
throughput testing and long-running soak tests without saturating the server.

| Selection | Pause |
|---|---|
| **None** (default) | No pause — iterations run as fast as possible |
| **1/10 sec** | 100 ms after each encrypt/decrypt pair |
| **1 sec** | 1 000 ms after each encrypt/decrypt pair |
| **5 sec** | 5 000 ms after each encrypt/decrypt pair |

**Log verbosity** scales automatically: when the loop delay is 1 second or
longer, every iteration is written to the operation log.  At shorter delays,
only every 50th iteration is logged to keep the display readable at high
throughput.

---

### Provider Property Overrides

Clicking **Customize...** in the Configuration dialog opens the Provider
Property Overrides panel.  This lets you change selected NAE connection
properties for the duration of a test run without editing
`ProtectAppForDotNet.properties` on disk.

**How overrides are applied:**

1. The panel reads the current `ProtectAppForDotNet.properties` file and
   displays each recognized property's current on-disk value.
2. An editable **Override** column accepts replacement values for any unlocked
   property.
3. On **Start Testing**, the overrides are merged with the original file and
   written to a temporary copy at:
   ```
   %TEMP%\ProtectAppForDotNet_overrides.properties
   ```
   The NAE provider is initialised from this merged file.  The original
   `ProtectAppForDotNet.properties` is never modified.

**Overridable properties:**

| Property | Description |
|---|---|
| `NAE_IP` | Primary NAE server IP address |
| `NAE_IP.1` – `NAE_IP.9` | Additional addresses for load-balanced / multi-tier configurations |
| `NAE_Port` | NAE server TCP port |
| `Protocol` | Transport protocol (`tcp`, `ssl`, etc.) |
| `Use_Persistent_Connections` | Persistent connection pool setting |
| `Connection_Timeout` | Connection establishment timeout |
| `Connection_Read_Timeout` | Read timeout for established connections |
| `Connection_Retry_Interval` | Delay between connection retry attempts |
| `Symmetric_Key_Cache_Enabled` | Key memory-cache setting (locked when Key Caching ≠ None) |
| `Persistent_Cache_Enabled` | Disk persistence for the key cache (locked when Key Caching = Disk) |
| `Log_File` | Path for the ProtectApp provider log |

Properties that are managed by the **Key Caching** selection are displayed in
italics with a cyan highlight and cannot be manually overridden — their values
are set automatically based on the chosen caching mode.
