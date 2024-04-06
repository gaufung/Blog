<!--
{
    "Title": "编写 GC 友好的代码",
    "Image": "https://github.com/gaufung/Blog/assets/11272110/3fb38ee5-dfc9-4e03-a461-d37e0ad5e2b9",
    "Tags": ["C#", "Performance"],
    "Status": "Published"
}
-->

# 1 工具

> 工欲善其事，必先利其器

好的工具有助于我们写高效的 C# 代码，[dnSpy](https://github.com/0xd4d/dnSpy) 和 [BenchmarkDotNet](jttps://github.com/dotnet/BenchmarkDotNet) 就是不错的选择。

## 1.1 dnSpy

众所周知，C# 代码都会编译成 IL (Intermediate Language) 代码，然后被运行时 (Runtime) 执行，目前使用的运行时主要有 .Net Framework, CoreCLR 和 Mono。通过查看 IL 可以知道 C# 代码在编译器作用下会生成怎样的代码，使用 dnSpy 可以将生成的 dll 或者 exe 文件查看成相应的 IL 代码。

```C#
using System;
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
```

这是最简单的 Hello World 控制台应用程序，使用 `dotnet build` 命令生成 HelloWorld.dll 或者 HelloWorld.exe，这是最小可执行程序，使用 dnSpy 查看编译器究竟为我们生成了怎样的 IL 代码。

![dnSpy](https://github.com/gaufung/Blog/assets/11272110/a38aa541-908b-4b5b-a83d-e5527d24fa37)

文件打开生成的 HelloWorld.dll, 选择 Program 类的 Main 方法，右边就会显示出相应的 IL 代码。IL 是基于栈机器（Stack Machine），基本上所有的操作可以归结于进栈（Push）和出栈（Push），比如 Main 函数中执行逻辑可以这样描述：

1. nop 无操作，主要是为了指令对齐
2. ldstr 将字符串 "Hello World" 进行入栈
3. call 调用 WriteLine 方法，所需要的参数从栈中取即可，即出栈。

IL 还有更多技术上的细节，可以查看更多[资料](https://www.amazon.com/Inside-Microsoft-Assembler-Serge-Lidin/dp/0735615470)。

## 1.2 BenchmarkDotNet

BenchmarkDotNet 是开源的 .Net 应用程序 benchmark 工具，用来测试我们的代码运行时空间和时间效率，从而选择正确的实现方式。

### 1.2.1 安装

BenchmarkDotNet 提供一系列相应的 NuGet 包，主要有：

- BenchmarkDotNet: 运行基础框架和逻辑
- BenchmarkDotNet.Diagnostic.Windows: 提供 Windows 相关的诊断服务
- BenchmarkDotNet.Tool: dotnet 相关工具
- BenchmarkDotNet.Templates: Benchmark 模板

通过 `dotnet add package <package name>` 安装上述相关包。

### 1.2.2 示例

使用 `dotnet new Benchmark --console-app -b MyBecnhmark`, 就会在当前目录下创建两个文件 `Program.cs` 和 `MyBenchmark.cs`， 

```c#
// MyBenchmark.cs
public class MyBenchmark
{
    [Benchmark]
    public void Scenario1()
    {
        // Implement your benchmark here
    }

    [Benchmark]
    public void Scenario2()
    {
        // Implement your benchmark here
    }
}

// Program.cs
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<MyBenchmark>();
    }
}
```

带有 `Benchmark` Attribute 的方法就是要进行测试的方法，里面就是具体实现方式；而 `BenchmarkRunner` 则是 Benchmark 的入口。使用 `dotnet run` ~~就可以在控制台运行然后得到运行结果~~。现在我们测试斐波那契数列迭代和递归的两种不同方式性能差异。

```C#
[SimpleJob(launchCount:1, warmupCount:2, targetCount:5)]
public class MyBenchmark
{
    [Params(5, 10)]
    public int N ;

    [Benchmark]
    public void Iteration()
    {
        Fib_Iter(N);
    }

    private int Fib_Iter(int n)
    {
        int f1 = 0;
        int f2 = 1;
        int fi = 0;
        if (n == 0)
            return 0;
        if (n == 1)
            return 1;
        for(int i = 2; i <= n; i++)
        {
            fi = f1 + f2;
            f1 = f2;
            f2 = fi;
        }
        return fi;
    }

    [Benchmark]
    public void Recursive()
    {
        Fib_Rec(N);
    }

    private int Fib_Rec(int n)
    {
        if( n == 0 || n == 1)
            return n;
        return Fib_Rec(n-1) + Fib_Rec(n-2);
    }
}
```

在 Benchark 中，每个都是由 Job 运行，每个 Job 都有相应的执行策略，主要由 `Throughput`, `ColdStart` 和 `Monitoring`，通常 `Thoughput` 基本上满足要求。`launchCount` 指定运行这个 Benchmark 的次数，每个标记 benchmark 方法调用称为一次操作，一连串操作组成在一起就成为一次迭代。`warmupCount` 指定热身的迭代次数，`targetCount` 表明需要进行测量的 benchmark 的测试。示例中得到的结果如下：


|    Method |  N |       Mean |      Error |    StdDev |
|---------- |--- |-----------:|-----------:|----------:|
| Iteration |  5 |   3.605 ns |  0.4940 ns | 0.1283 ns |
| Recursive |  5 |  28.623 ns |  0.6336 ns | 0.1645 ns |
| Iteration | 10 |   5.929 ns |  0.1143 ns | 0.0297 ns |
| Recursive | 10 | 345.854 ns | 36.4126 ns | 9.4562 ns |

### 1.2.3 内存监控

除了时间运行效率，我们还需要关注内存使用情况，这一点 BenchmarkDotNet 也提供了相应的功能，只需增加 `MemoryDiagnoser` attribute 即可。

```C#
[SimpleJob(launchCount:1, warmupCount:2, targetCount:5)]
[MemoryDiagnoser()]
public class MyBenchmark
{
    // elide
}
```

|    Method |  N |       Mean |     Error |    StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
|---------- |--- |-----------:|----------:|----------:|------:|------:|------:|----------:|
| Iteration |  5 |   3.527 ns | 0.0964 ns | 0.0250 ns |     - |     - |     - |         - |
| Recursive |  5 |  28.493 ns | 0.4153 ns | 0.1078 ns |     - |     - |     - |         - |
| Iteration | 10 |   6.035 ns | 0.5147 ns | 0.1337 ns |     - |     - |     - |         - |
| Recursive | 10 | 332.822 ns | 8.2203 ns | 2.1348 ns |     - |     - |     - |         - |


我们斐波那契两种实现方式都是栈空间分配内存，所以并不会存在堆内存分配情况，所以这些列都是为空。

### 1.1.4 其他功能

- 可以指定不同的 runtime，比如 .net framework, coreclr 或者 mono
- 可以从编译好 assembly, exe 等运行 Benchmark.
- 运行的方法必须要有返回值，否则被编译器优化

# 2 避免内存分配

## 2.1 值类型

和 Java 语言不同，在 C# 中，用户自定义类型除了引用类型（class)，还可以定义值类型（struct)。对于值类型，很多人都存在这样这样的误解：

> 值类型分配在栈上，而引用类型定义分配在堆上

但是在所有的官方文档中并没有明确说明阻止在堆上分配值类型，除了自定义值类型，还有基础类型也是值类型，比如 `int`, `double` 和枚举类型等等。

下面代码为值类型和引用类型使用示例

```C#
class Program
{
    static void Main(string[] args)
    {
        DataStruct data1 = new DataStruct(){Age=20, Salary = 10.0};
        DataClass data2= new DataClass(){Age=20, Salary=10.0};
    }
}
struct DataStruct
{
    public int Age;
    public double Salary;
}
class DataClass
{
    public int Age;
    public double Salary;
}
```

使用 `dnSpy` 工具查看生成的 IL 代码

```IL:n
    /* (8,9)-(8,10) C:\Users\fenga\workspace\DotNetMemoryBenchmark\StructVsClass\Program.cs */
    /* 0x0000025C 00           */ IL_0000: nop
    /* (9,14)-(9,73) C:\Users\fenga\workspace\DotNetMemoryBenchmark\StructVsClass\Program.cs */
    /* 0x0000025D 1202         */ IL_0001: ldloca.s  V_2
    /* 0x0000025F FE1503000002 */ IL_0003: initobj   StructVsClass.DataStruct
    /* 0x00000265 1202         */ IL_0009: ldloca.s  V_2
    /* 0x00000267 1F14         */ IL_000B: ldc.i4.s  20
    /* 0x00000269 7D01000004   */ IL_000D: stfld     int32 StructVsClass.DataStruct::Age
    /* 0x0000026E 1202         */ IL_0012: ldloca.s  V_2
    /* 0x00000270 230000000000002440 */ IL_0014: ldc.r8    10
    /* 0x00000279 7D02000004   */ IL_001D: stfld     float64 StructVsClass.DataStruct::Salary
    /* 0x0000027E 08           */ IL_0022: ldloc.2
    /* 0x0000027F 0A           */ IL_0023: stloc.0
    /* (10,14)-(10,68) C:\Users\fenga\workspace\DotNetMemoryBenchmark\StructVsClass\Program.cs */
    /* 0x00000280 7303000006   */ IL_0024: newobj    instance void StructVsClass.DataClass::.ctor()
    /* 0x00000285 25           */ IL_0029: dup
    /* 0x00000286 1F14         */ IL_002A: ldc.i4.s  20
    /* 0x00000288 7D03000004   */ IL_002C: stfld     int32 StructVsClass.DataClass::Age
    /* 0x0000028D 25           */ IL_0031: dup
    /* 0x0000028E 230000000000002440 */ IL_0032: ldc.r8    10
    /* 0x00000297 7D04000004   */ IL_003B: stfld     float64 StructVsClass.DataClass::Salary
    /* 0x0000029C 0B           */ IL_0040: stloc.1
    /* (11,9)-(11,10) C:\Users\fenga\workspace\DotNetMemoryBenchmark\StructVsClass\Program.cs */
    /* 0x0000029D 2A           */ IL_0041: ret
```

`initbj` 表明在栈上分配空间，而 `newobj` 是堆上分配空间。在栈空间分配空间的话，内存空间管理就交给程序栈管理，而在堆上分配就需要 GC 来管理。除此之外，使用值类型还有以下几点好处：

- 值类型只存储数据而没有其他的元数据
- 值类型数据是紧密存储，有很好的局部性
- 没有 dereference，所以访问值类型更快
- 值类型可以使用按值传递机制，实现不可变性。

接下来通过 benchmark 查看两者在性能上的差距

```C#
[Benchmark]
public List<string> UseDataClass()
{
    int amount = Amount;
    LocationClass location = new LocationClass();
    List<string> result = new List<string>();
    List<PersonDataClass> input = service.GetPersonInBatchClasses(amount);
    DateTime now = DateTime.Now;
    for(int i = 0; i < input.Count; i++)
    {
        PersonDataClass item = input[i];
        if(now.Subtract(item.BirthDate).TotalDays > 18 * 365)
        {
            var employee = service.GetEmployeeClass(item.EmployeeId);
            if(locationService.DistanceWithClass(location, employee.Address) < 10.0)
            {
                string name = $"{item.Firstname} {item.Lastname}";
            result.Add(name);
            }
        }
    }
    return result;
}

[Benchmark]
public List<string> UseDataStruct()
{
    int amount = Amount;
    LocationStruct location = new LocationStruct();
    List<string> result = new List<string>();
    InputDataStruct[] input = service.GetPersonInBatchStructs(amount);
    DateTime now = DateTime.Now;
    for(int i = 0; i < input.Length; i++)
    {
        ref InputDataStruct item = ref input[i];
        if(now.Subtract(item.BirthDate).TotalDays > 18 * 365)
        {
            var employee = service.GetEmployeeStruct(item.EmployeeId);
            if(locationService.DistanceWithStruct(ref location, employee.Address) < 10.0)
            {
                string name = $"{item.Firstname} {item.Lastname}";
                result.Add(name);
            }
        }
    }
    return result;
}
```

结果如下：

|        Method | Amount |     Mean |    Error |    StdDev |  Gen 0 |  Gen 1 | Gen 2 | Allocated |
|-------------- |------- |---------:|---------:|----------:|-------:|-------:|------:|----------:|
|  UseDataClass |    200 | 37.84 us | 2.484 us | 0.6451 us | 3.0518 | 0.0610 |     - |  12.59 KB |
| UseDataStruct |    200 | 40.71 us | 1.961 us | 0.5091 us | 1.8921 |      - |     - |   7.87 KB |

在时间消耗上两者差距不大，但是在使用值类型的方法在内存使用有较大优势，内存分配几乎只有前者的 60%。

## 2.2 使用 ValueTuple

很多时候我们需要返回多个字段，通常采用返回一个 `Tuple` 或者匿名对象，但是它们都是引用类型。在 C# 中引入了 Value Tuple. 使用也非常简单：

```C#
var tuple1 = (1, 4.0);
var tuple2 = (A: 1, B: 4.0);
tuple2.A = 2;
```

接下来通过例子比较两者的在内存分配上的差异：

```C#
[Benchmark]
public Tuple<ResultDesc, ResultData> ReturnTuple()
{
    return new Tuple<ResultDesc, ResultData>(new ResultDesc {Count = 10}, new ResultData(){Average=0.0, Sum = 10.0});
            
}

[Benchmark]
public (ResultDescStruct, ResultDataStruct) ReturnValueTuple()
{
    return (new ResultDescStruct(){Count=10}, new ResultDataStruct(){Average = 0.0, Sum = 10.0});
}
```

结果如下：

|           Method |      Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|----------------- |----------:|----------:|----------:|-------:|------:|------:|----------:|
|      ReturnTuple | 11.083 ns | 1.5716 ns | 0.4081 ns | 0.0210 |     - |     - |      88 B |
| ReturnValueTuple |  4.536 ns | 0.2652 ns | 0.0689 ns |      - |     - |     - |         - |

使用 `ValueTuple` 在时间和内存消耗上有着显著的优势。

## 2.3 使用 ArrayPool

很多情况下我们会多次使用数组，那么为何不将这些数组缓存起来，使它们不被 GC 所回收，这样就减轻了 GC 的压力。`System.Buffer` 包提供了 ArrayPool 相应的功能，基本用法如下：

```C#
int[] buffer = ArrayPool.Shared.Rent(miniLength);
try
{
    consume(buffer)
}
finally
{
    ArrayPool.Shared.Return(buffer);
}
```

在之前值类型和引用的类型比较中，再增加一个 ArrayPool 比较:

```C#
//elide
[GlobalSetup]
public void Setup()
{
    var array = ArrayPool<InputDataStruct>.Shared.Rent(Amount);
    ArrayPool<InputDataStruct>.Shared.Return(array);
}
// elide

[Benchmark]
public List<string> PeopleEmployeeWithInLocation_ArrayPoolStructs()
{
    int amount = Amount;
    LocationStruct location = new LocationStruct();
    List<string> result = new List<string>();
    InputDataStruct[] input = service.GetDataArrayPoolStructs(amount);
    DateTime now = DateTime.Now;
    for(int i = 0; i < input.Length; i++)
    {
        ref InputDataStruct item = ref input[i];
        if(now.Subtract(item.BirthDate).TotalDays > 18 * 365)
        {
            var employee = service.GetEmployeeStruct(item.EmployeeId);
            if(locationService.DistanceWithStruct(ref location, employee.Address) < 10.0)
            {
                string name = $"{item.Firstname} {item.Lastname}";
                        result.Add(name);
            }
        }
    }
    ArrayPool<InputDataStruct>.Shared.Return(input);
    return result;
}
```

`Setup` 方法使 ArrayPool 提前创建好，以便后续的 `Rent` 调用的时候不需要再一次申请内存分配，Benchmark 得到的结果如下：

|              Method | Amount |     Mean |    Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------- |------- |---------:|---------:|----------:|-------:|------:|------:|----------:|
|        UseDataClass |    200 | 39.04 us | 3.724 us | 0.9672 us | 3.0518 |     - |     - |  12.59 KB |
|       UseDataStruct |    200 | 40.48 us | 1.941 us | 0.5042 us | 1.8921 |     - |     - |   7.87 KB |
| UseArrayPoolStructs |    200 | 40.94 us | 1.465 us | 0.3804 us | 0.2441 |     - |     - |   1.12 KB |

## 使用对象池

对象池概念已经被广泛使用，比如数据库连接池，每次数据库操作并不需要重新建立一个连接，只需要选择空闲的数据库连接对象即可。有很多现成库可以选择，比如：[CodeProject.ObjectPool](https://www.nuget.org/packages/CodeProject.ObjectPool)，手动实现对象池也并不是很困难。

```C#
public class Object<T> Where T : class
{
    private T firstItem;
    private readonly T[] items;
    private readonly Func<T> generator

    public ObjectPool(Func<T> generator, int size)
    {
        this.generator = generator ?? throw new ArgumentNullException("genertor");
        this.items = new T[size-1];
    }

    public T Rent()
    {
        T inst = firstItem;
        if (inst == null || inst != Interlocked.CompareExchange(ref firstItem, null, inst))
        {
            inst = RentSlow();
        }
        return inst;
    }
    public void Return(T item)
    {
        if(firstItem == null)
        {
            firstItem = item;
        }
        else
        {
            ReturnSlow(item);
        }
    }
    private T RentSlow()
    {
        for (int i = 0; i < items.Length; i++)
        {
            T inst = inst[i];
            if (inst != null)
            {
                if(inst == Interlocked.ComparedExchange(ref items[i], null, inst))
                {
                    return inst;
                }
            }
        }
        return generator();
    }

    private void ReturnSlow(T obj)
    {
        for(int i =0; i < items.Length; i++)
        {
            if(items[i] == null)
            {
                items[i] = obj;
                break;
            }
        }
    }
}
```

# 3 隐藏内存分配

除了显示使用 `new` 分配内存之外，还有一些隐藏的内存分配情况。

## 3.1 委托

我们代码中包含了大量的委托 `Action`, `Func` 等等，通常为一个委托赋值的方法有一下几种：

```C#
Func<double> action1 = ProgressWithLogging;
Func<double> action2 = new Func<double>(ProgressWithLogging);
Func<double> action3 = () => ProgressWithLogging();
Func<double> action4 = () => 1.0;
```

除了第一种显式使用了 `new` 来分配一个委托，剩下的三种其实都包含了隐藏的内存分配，相关 IL 代码如下：

```IL
/* 0x0000025E FE0602000006 */ IL_0002: ldftn     float64 DelegateAlloc.Program::ProgressWithLogging()
/* 0x00000264 730C00000A   */ IL_0008: newobj    instance void class [System.Runtime]

/* elide */
/* 0x0000026B FE0602000006 */ IL_000F: ldftn     float64 DelegateAlloc.Program::ProgressWithLogging()
/* 0x00000271 730C00000A   */ IL_0015: newobj    instance void class [System.Runtime]System.Func`1<float64>::.ctor(object, native int)
/* elide */
/* 0x00000285 FE0606000006 */ IL_0029: ldftn     instance float64 DelegateAlloc.Program/'<>c'::'<Main>b__0_0'()
/* 0x0000028B 730C00000A   */ IL_002F: newobj    instance void class [System.Runtime]System.Func`1<float64>::.ctor(object, native int)
/* elide */
/* 0x000002A5 FE0607000006 */ IL_0049: ldftn     instance float64 DelegateAlloc.Program/'<>c'::'<Main>b__0_1'()
/* elide */
/* 0x000002AB 730C00000A   */ IL_004F: newobj    instance void class [System.Runtime]System.Func`1<float64>::.ctor(object, native int)
```

每个委托赋值语句都转换为 `newobj` 语句，即堆内存分配操作。

## 3.2 装箱

装箱是指在值类型和引用类型之间的相互转换，.Net 官方文档是这么说的

> 每一个值类型都有相应的引用类型，叫做装箱类型；反过来却不成立，装箱后的引用类型存储了转换之前值类型的值。

当函数或者方法接受的是引用类型，而传递给的参数却是值类型，那么就会引发装箱操作。装箱带来了内存的分配，因此是非常耗时的操作，接下来使用 Benchmark 查看装箱带来的性能损失。

```C#
[Benchmark]
public void UseBox()
{
    for(int i =0; i < 100; i ++)
    {
        Box(i);
    }
}

[Benchmark]
public void UnBox()
{
    for(int i =0; i < 100; i++)
    {
        Unbox(i);
    }
}

public int Box(object obj)
{
    return (int)obj;
}

public int Unbox(int i)
{
    return i;
}
```

结果如下：

| Method |      Mean |      Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------- |----------:|-----------:|----------:|-------:|------:|------:|----------:|
| UseBox | 288.16 ns | 16.9930 ns | 4.4130 ns | 0.5736 |     - |     - |    2400 B |
|  UnBox |  30.16 ns |  0.1829 ns | 0.0475 ns |      - |     - |     - |         - |

不管是时间还是空间效率上，装箱操作都带来不小的性能损失。如果方法接受的参数是接口类型，但是我们传入的是值类型，同样也会引起装箱操作。

```C#
interface ITuple
{
    int Length();
}

class TupleStruct : ITuple
{
    public int Length()
    {
        return 1;
    }
}

// elide
class Program
{
    static void Main(string[] args)
    {
        TupleStruct ts = new TupleStruct();
        FooBar(ts);
    }

    static int FooBar(ITuple tuple)
    {
        return tuple.Length();
    }
}
```

相应的 IL 代码如下:

```IL
/* elide */
/* 0x0000025D 7306000006   */ IL_0001: newobj    instance void StructInteface.TupleStruct::.ctor()
/* 0x00000262 0A           */ IL_0006: stloc.0
/* (10,13)-(10,24) C:\Users\fenga\workspace\DotNetMemoryBenchmark\StructInteface\Program.cs */
/* 0x00000263 06           */ IL_0007: ldloc.0
/* 0x00000264 2802000006   */ IL_0008: call      int32 StructInteface.Program::FooBar(class StructInteface.ITuple)
/* elide */
```

使用泛型和类型约束可以避免装箱操作

```C#
void FooBar<T>(T obj)
{

}

void FooBar<T>(T tuple) where T: ITuple
{

}
```

除此之外，值类型下面的情况也会导致装箱操作：

1. 在值类型没有重写 `GetHashCode()` 和 `ToString()` 方法，如果方法中使用了这些方法，也会导致装箱。
2. 使用 `GetType()` 方法总会导致装箱操作。
3. 从值类型方法中创建委托。

## 3.3 闭包

闭包是一种获取执行环境状态的一种机制，比如下面的例子

```C#
private IEnumnerable<string> Closures(int value)
{
    var filteredList = _list.Where(x => x > value);
    var result = filteredList.Select(x => x.ToString());
    return result;
}
```

通过之前了解到的，`Where` 和 `Select` 的参数都是委托，所以就会有两次对象分配。但是还有个对象分配并不起眼，就是为闭包创建的一个类，它包含了传入的参数 `value`。编译转换后的代码如下：

```C#
private IEnumerable<string> Closure(int value)
{
    Program.<>c__DisplayClass1_0 <>c__DisplayClass1_ = new Program.<>c__DisplayClass1_0();
    <>c__DisplayClass1_.value = value;
    IEnumberable<int> arg_43_0 = this._list.Where(new Func<int, bool>(<>c__DisplayClass1_.<Clousure>b_0));
    Func<int, string> arg_43_1;
    if((arg_43_1 = Program.<>c.<>9__1_1) == null)
    {
        arg_43_1 = (Program.<>c.<>9__1_1 = new Func<int, string>(Program.<>c.<>9.<Clousures>b__1_1);
    }
    return arg_43_0.Select(arg_43_1);
}

[CompilerGenerated]
private sealed class <>c__DisplayClass1_0
{
    public <>C__DisplayClass1_0()
    {

    }
    internal bool <Clousure>b__0(int x)
    {
        return x > this.value;
    }
    public int value;
}
```

类 `<>c__DisplayClass1_0` 就是编译器帮我们创建好的类，它包含了传我们传入的参数，并提供了委托所需要的方法。每次调用 `Closure` 方法的时候，都会引起这个类在堆空间上的分配。

## 3.4 参数数组

从 C# 2.0 开始提供了 `params` 关键字，它允许我们传入可变的调用参数。但是要注意的是这个仅仅是一个语法糖，其实编译器为我们创建了一个对象数组。

```C#
public void MethodWithParams(string str, params object[] args)
{
    Console.WriteLine(str, args);
}
```

为了避免额外的内存分配，可以选择不同的参数的方法重载。

```C#
public void MethodWithParams(string str, object arg1)
{
    // elide
}
public void MethodWithParams(string str, object arg1, object args2)
{
    // elide
}
```

## 3.5 `IEnumbeable<T>` 参数

很多代码设计的规则要求面向接口编程，比如我们的方法的参数和返回值都应当是接口类型，比如：

```C#
public int Sum(IEnumerable<Person> persons)
{
    //elide
}

List<Person> list = new List<Person>();
// elide
Sum(list);
```

`Sum` 方法接受的参数类型接口 `IEnumerable<Person>`，`List<T>` 实现了这个接口，所以将 List 类型传入是没有问题。但是如果查看 `List` 对这个接口的实现，发现它是返回一个 `Enumerator` 对象。

```C#
public List<T> 
{
    //elide

    public Enumerator GetEnumerator()
            => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => new Enumerator(this);

    //elide

    public struct Enumerator : IEnumerator<T>, IEnumerator
    {
        //elide
    }

}
```

可以看出 `Enumerator` 是值类型，根据之前的讨论，这将引起装箱操作，当然这也说明工程上并非仅仅只是性能作为考虑，而是一种平衡的结果。
