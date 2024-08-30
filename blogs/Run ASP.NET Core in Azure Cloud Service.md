<!--
{
    "Title": "Run ASP.NET Core in Azure Cloud Service",
    "Image": "https://github.com/user-attachments/assets/7872eb72-ee16-4b2d-a1a2-523645542bd3",
    "Tags": ["ASP.NET Core", "Azure"],
    "Status": "drafted"
}
-->

# Problem

[Azure Cloud Service](https://learn.microsoft.com/en-us/azure/cloud-services-extended-support/) is considered a legacy platform when compared with [Azure App Service](https://azure.microsoft.com/en-au/products/app-service), notably due to its lack of support for modern .NET frameworks—a limitation that will continue into the future. While this [document](https://mzansibytes.com/2023/01/22/supporting-net-6-in-cloud-services-extended-support/)  offers a viable solution to this issue, there are still several concerns that need to be addressed:

- Dependency on External Downloads: The solution requires downloading the ASP.NET Core runtime bundles during deployment. This dependency introduces scalability issues, particularly if Microsoft's download servers are unavailable. Although the likelihood is low, the potential impact is significant.
- Risks of In-Place Upgrades: The approach involves in-place upgrades where existing ASP.NET Framework applications are removed. This method poses risks in business scenarios, particularly if the ASP.NET Core application introduces regressions. A more cautious migration strategy is advisable to minimize potential disruptions.
- Limitations due to IIS: The continued use of IIS as the server restricts our ability to adopt a cross-platform strategy in the future. Moving away from IIS could facilitate greater flexibility and support for diverse operating environments.
Each of these points reflects critical considerations for organizations planning to transition from Azure Cloud Services to more modern and flexible solutions.

# Solution

## ASP.NET Core Application Project

In our `ASP.NET Core` application, we will adopt the advanced [Single File](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview?tabs=cli) deployment approach. Below is an example of how the csproj file should be configured:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
  </PropertyGroup>
</Project>
```

- `SelfContained`: Set to true to eliminate the need for runtime bundle downloads and installations on the target system. This setting requires specifying a `RuntimeIdentifier` to define the target platform.
- `PublishSingleFile`: Set to false to maintain access to cscfg configurations. This is necessary because the `Microsoft.Windows.AzureRuntime` package, which is required for reading these configurations, is not accessible in single-file mode.

## Web Role Build Task

In the `Web Role` project build task, the above ASP.NET Core application will also be compiled.

```xml
<Content Include="..\WebRoleCore\bin\Release\net8.0\win-x64\WebRoleCore.zip" Visible="true">
      <Link>WebRoleCore\WebRoleCore.zip</Link>
</Content>
<Target Name="BuildASPNETCore" BeforeTargets="BeforeBuild">
<Exec Command="dotnet publish $(ProjectDir)..\WebRoleCore\WebRoleCore.csproj -c Release" />
    <Delete Files="$(ProjectDir)..\WebRoleCore\bin\Release\net8.0\win-x64\WebRoleCore.zip" />
    <ZipDirectory SourceDirectory="$(ProjectDir)..\WebRoleCore\bin\Release\net8.0\win-x64\publish" DestinationFile="$(ProjectDir)..\WebRoleCore\bin\Release\net8.0\win-x64\WebRoleCore.zip" />
</Target>
<Target Name="CopyLinkedContentFiles" BeforeTargets="Build">
    <Copy SourceFiles="%(Content.Identity)" DestinationFiles="%(Content.Link)" SkipUnchangedFiles="true" OverwriteReadOnlyFiles="true" Condition="'%(Content.Link)' != ''" />
</Target>
```

- Before `BeforeBuild` event, it will run `dotnet publish` command to build out `ASP.NET Core` artifact and wrap them with `zip` file.
- Link above `zip` file to `Web Role` as content.
- Before `Build` event, copy linked content to the destination file.

You might be puzzled as to why it is necessary to package the artifact as a zip file. The reason is that the output DLLs from the `ASP.NET Core` project could interfere with the references in an ASP.NET Framework project, leading to potential conflicts.

## Deployment

During deployment, we will install the ASP.NET Core application on the same virtual machine as the Web role. This process will be divided into several steps

### Expand Archive file

During the build process, the ASP.NET Core application is packaged into a zip file. Therefore, the first step during deployment is to extract this archive.

```bat
@ECHO OFF
SETLOCAL
set startupLog=WebRoleCoreStartupConfigLog.txt
echo %date% %time:~0,2%:%time:~3,2%:%time:~6,2% Starting web role config ... >> %startupLog%
PowerShell -ExecutionPolicy Unrestricted .\Startup\UnarchiveASPNETCore.ps1 >> %startupLog% 2>&1
EXIT /B 0
```

```powershell
function Unarchive-ASPNETCorePackage {
    param (
        [Parameter(Mandatory=$true)]
        [string]
        $PhysicalPath
    )
    Set-Location "$($physicalPath)\WebRoleCore"
    Get-ChildItem -Exclude @('WebRoleCore.zip') | Remove-Item -Recurse -Force
    Expand-Archive -Path ".\WebRoleCore.zip" -Force
}
if (Test-Path "D:\sitesroot\0\" ) {
    Unarchive-ASPNETCorePackage -PhysicalPath "D:\sitesroot\0\"
} elseif (Test-Path "E:\sitesroot\0\") {
    Unarchive-ASPNETCorePackage -PhysicalPath "E:\sitesroot\0\"
} elseif (Test-Path "F:\sitesroot\0\") {
    Unarchive-ASPNETCorePackage -PhysicalPath "F:\sitesroot\0\"
} else {
    throw "No IIS site path found."
}
```

This task is configured as one `startup` task.

```xml
<?xml version="1.0" encoding="utf-8"?>
<ServiceDefinition name="AzureCloudServiceASPNETCore" xmlns="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" schemaVersion="2015-04.2.6">
  <WebRole name="WebRole" vmsize="Standard_D1_v2">
      <Startup>
          <Task commandLine="Startup\UnarchiveASPNETCore.cmd" executionContext="elevated" taskType="simple" />
      </Startup>
  </WebRole>
</ServiceDefinition>
```

### Lanuch ASP.NET Core Application

```csharp
public class WebRoleCoreLauncher : IDisposable
{
    private const string OnStartCompletedConsoleIndicator = "WebRoleCore started.";
    private const string OnStopCompletedConsoleIndicator = "WebRoleCore stopped.";
    private const string IISSiteName = "WebRole";
    private const int ASPNETCorePort = 8080;
    private static string AssemblyDirectory => Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path));
    private readonly ManualResetEventSlim _onStopCompleted = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _onStartCompleted = new ManualResetEventSlim(false);
    private readonly Process _process;

    public WebRoleCoreLauncher()
    {
        var filePath = Path.Combine(Path.GetPathRoot(AssemblyDirectory), "sitesroot", "0", "WebRoleCore", "WebRoleCore", "WebRoleCore.exe");
        var ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(
            ip => ip.AddressFamily == AddressFamily.InterNetwork
                && ip.ToString() != "127.0.0.1");

        _process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                CreateNoWindow = true,
                FileName = filePath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(filePath),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                EnvironmentVariables =
                    {
                        { "ASPNETCORE_ADDRESS", ipAddress.ToString() },
                        { "ASPNETCORE_PORT", $"{ASPNETCorePort}" },
                    },
            },
            EnableRaisingEvents = true,
        };

        _process.Exited += (sender, e) =>
        {
            _onStartCompleted.Set();
            _onStopCompleted.Set();
        };

        _process.OutputDataReceived += (sender, e) =>
        {
            var data = e.Data ?? string.Empty;
            if (data.Contains(OnStartCompletedConsoleIndicator))
            {
                _onStartCompleted.Set();
            }

            if (data.Contains(OnStopCompletedConsoleIndicator))
            {
                _onStopCompleted.Set();
            }
        };
    }
    public void Run()
    {
        if (HasAdministratorPrivileges)
        {
            TryRemoveIISSitePortBinding();
            SpawnASPNETCoreProcess();
        }
        else
        {
            throw new UnauthorizedAccessException("Administrator privilege is required to start the ASP.NET Core process.");
        }
    }

    private bool HasAdministratorPrivileges =>
                new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);

    private void TryRemoveIISSitePortBinding()
    {
        using var serverManager = new ServerManager();
        var site = serverManager.Sites.FirstOrDefault(s => s.Name.StartsWith(IISSiteName, StringComparison.OrdinalIgnoreCase));
        if (site != null)
        {
            var binding = site.Bindings.FirstOrDefault(b => b.EndPoint?.Port == ASPNETCorePort);
            if (binding != null)
            {
                site.Bindings.Remove(binding);
                serverManager.CommitChanges();
            }
        }
        else
        {
            throw new InvalidOperationException("IIS site is not found or not started.");
        }
    }
    private void SpawnASPNETCoreProcess()
    {
        _process.Start();
        _process.BeginOutputReadLine();
        _onStartCompleted.Wait();
    }
    public void Dispose()
    {
        _process?.StandardInput.WriteLine();
        _onStopCompleted.Wait();
        _onStopCompleted.Dispose();
        _onStartCompleted.Dispose();
        _process?.Dispose();
    }
}
```

1. Create a Process Instance: Initialize a Process instance that points to WebRoleCore.exe. This instance will pass the local IPv4 address and port (8080) as environment variables.
2. Remove IIS Site Port Binding: Delete the IIS site port binding on port 8080, which will then be used for the ASP.NET Core application.
3. Start the ASP.NET Core Process: Launch the ASP.NET Core process and wait for the start-up confirmation message (WebRoleCore started.).

Note:

- ASP.NET Core configures the listen address and port

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    var address = Environment.GetEnvironmentVariable("ASPNETCORE_ADDRESS");
    var port = Environment.GetEnvironmentVariable("ASPNETCORE_PORT");
    if (!string.IsNullOrWhiteSpace(address)
                    && !string.IsNullOrWhiteSpace(port)
                    && int.TryParse(port, out var portNumber))
    {
        options.Listen(IPAddress.Parse(address), portNumber);
    }
});
```

- Configure output when application started or stopped

```csharp
var app = builder.Build();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
                Console.WriteLine("WebRoleCore started.");
});
lifetime.ApplicationStopped.Register(() =>
{
    Console.WriteLine("WebRoleCore stopped.");
});
```

- Remove IIS site port with Administrator Privilege

```xml
<?xml version="1.0" encoding="utf-8"?>
<ServiceDefinition name="AzureCloudServiceASPNETCore" xmlns="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" schemaVersion="2015-04.2.6">
  <WebRole name="WebRole" vmsize="Standard_D1_v2">
      <Runtime executionContext="elevated">
          <EntryPoint>
              <NetFxEntryPoint assemblyName="WebRole.dll" targetFrameworkVersion="v4.8" />
          </EntryPoint>
      </Runtime>
  </WebRole>
</ServiceDefinition>
```

- Invoke `WebRoleCoreLancher`

```csharp
public class WebRole : RoleEntryPoint
{
    private WebRoleCoreLauncher _webRoleCoreLauncher;
    public override bool OnStart()
    {
        _webRoleCoreLauncher = new WebRoleCoreLauncher();
        _webRoleCoreLauncher.Run();
        return base.OnStart();
    }
    public override void OnStop()
    {
        _webRoleCoreLauncher?.Dispose();
        base.OnStop();
    }
}
```

# Conclusion

After setup, your ASP.NET Core application will coexist with the ASP.NET Framework web role on the same VM, but they will listen on different ports. During the migration, you can gradually redirect web API requests to the ASP.NET Core application. This proxying of requests can occur over the local network (loopback) rather than through the public internet.

Sample code: https://github.com/gaufung/AzureCloudService-ASPNETCore
