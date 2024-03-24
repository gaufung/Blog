<!---
- Title: csproj 中引入包中的内容
- Tags: .NET, C#
- status: draft
- image: https://www.endungen.de/images/file/csproj.png
-->

# 1. 问题

作为应用程序包的开发人员，除了提供应用程序 `API` 接口之外，有时候还需要提供其他资源，比如文本，图片等文件以方便调用者在应用程序中使用。那么该怎么做到呢？


# 2. 内嵌资源

C# 对于文件可以选择将其作为 `Embeded Resource` ，这样文件内容会作为应用程序集的一部分被打包到 `dll` 中。比如：

```csharp
<ItemGroup>
    <EmbeddedResource Include="words.txt" />
</ItemGroup>
```

在应用程序中，可以加载 `Assembly` 的资源来读取这个 `words.txt` 文件

```csharp
var assembly = typeof(MyClass).Assembly;
var resourceName = "mylib.words.txt";
using var stream = assembly.GetManifestResourceStream(resourceName);
if (stream != null)
{
    using var reader = new StreamReader(stream);
    string fileContent = reader.ReadToEnd();
    Console.WriteLine(fileContent);
}
```

# 3. 输出目录
如果资源非常多，或者资源的类型不是简单的文本类型，那么上面的方法就显得非常繁琐。那么可以将这些文件输出到 `nuget` 包中的 `content` 资源下，比如：

```xml
<ItemGroup>
    <Content Include="words/**/*">
      <Link>content/words/%(RecursiveDir)%(Filename)%(Extension)</Link>
    </Content>
</ItemGroup>
```

这里，我们会将 `words` 目录下的所有文件作为 `Content` 一部分，而且通过 `%(RecursiveDir)%(Filename)%(Extension)` 配置那他们保持 `words` 目录下原本的文件结构和名字。那么该如何使用它们呢？

```xml
<ItemGroup>
    <PackageReference Include="MyLib" Version="1.0.0" GeneratePathProperty="true" />
</ItemGroup>
<ItemGroup>
    <None Include="$(PkgMyLib)/content/words/**/*">
      <Link>words/%(RecursiveDir)%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

这里我们在 package 引用的地方配置了 `GeneratePathProperty="true"`，这里表明我们接下来可以使用 `$(PkgMyLib)` 这个变量，注意命名格式是 `Pkg<PackageName>`, 而且包名字中的 `.` 要转换成 `_`。这样原本的 `words/words.txt` 文件会被输出到引用程序的生成目录下。之后，在应用程序中访问这些文件就非常简单了

```csharp
var content = File.ReadAllText("words/words.txt");
Console.WriteLine(content);
```

# 4. 结论

对于简单的文件，使用内嵌资源是一种好的方式，而且可以保证内容不会被修改。但是对于复杂的内容，比如一些外部可执行文件，通过输入目录的方式，可以使问题简化。


