<!--
{
    "Title": "Windows Server 安装 Windbg",
    "Image": "https://cdn.windowsreport.com/wp-content/uploads/2017/08/new-Windbg-tool-released.jpg",
    "Tags": ["Programming"],
    "Status": "Published"
}
-->

# 1. 问题

当我们的应用程序出现内存泄漏的时候，通常需要通过 `Windbg` 来分析 dump 文件。虽然官方提供了[下载途径](https://learn.microsoft.com/en-gb/windows-hardware/drivers/debugger/#install-windbg-directly)，但是如果我们的服务器是 Windows Server 而且不方便将  dump 文件从服务器中下载下来，那么 Windows Server 将无法通过 `Windows Store` 来安装它。

# 2. Winget 安装
 
 `winget` 是微软推出的包安装工具，通过它也可以安装相应的应用软件。但是它的安装包也无法在 Windows Server 上安装，所以我们需要通过其他方式先安装 `winget`. 
 
 - 安装 `winget-install` 脚本

```powershell
Install-Script -Name winget-install -RequiredVersion 4.0.4
```
Powershell 仓库提供了一个 `winget-install` 脚本。安装完毕后，直接执行 `winget-install`，这样 `winget` 命令行工具就安装到 Server 端机器上

- 安装 `Microsoft.Windbg` 软件

```powershell
winget install Microsoft.Windbg
```

安装完毕后， 就可以使用 `windbg` 工具了。
