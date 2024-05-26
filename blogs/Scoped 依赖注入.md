<!--
{
    "Title": "依赖注入中的 Scoped Service",
    "Image": "https://github.com/gaufung/Blog/assets/11272110/8f4bdaa9-ccac-4648-8aa8-d32034d685fa",
    "Tags": ["C#", "依赖注入"],
    "Status": "published"
}
-->

# 1 介绍

在 `Microsoft.Extensions.DependencyInjection` 库中，所有注册的服务可以分为三种类型：

- Singleton
- Scoped
- Transient 

其中 `Singleton` 和 `Transient` 比较容易理解，但是 `Scoped` 这个概念有点抽象，这篇文章将要回答这些问题，以便更加方便理解这个概念

1. 为什么有 `Scoped` 概念？
2. `Scoped` 服务在注册和使用的时候有什么特殊之处？
3. `Scoped` 服务在使用的时候有什么注意的地方？

# 2 为什么有 `Scoped` 概念

`Singleton` 服务在生命周期中只有一个实例，`Transient` 服务在每次使用时候创建一个实例，而 `Scoped` 服务的生命周期在两者之间，比如在网络中，一个请求在处理的生命周期中将共用一个服务实例。
使用 `Scoped` 服务也可以降低资源的使用，比如在一个请求中使用同一个数据库连接实例，不使用全局唯一的连接实例能够保证数据库操作的隔离。在 `ASP.NET Core` 中，每个网络请求都是由一个个中间件 `Middleware` 拼接而成，有时候需要在不同的中间件中分享数据，这样 `Scoped` 服务的唯一性能够保证数据的正确传递。

但是在 `ASP.NET Core` 中，我们并且直接涉及到 `Scoped` 服务的使用，那么框架帮我们做了哪些工作呢？

- `DefaultHttpContextFactory` 

每个 `HTTP` 请求都体现为一个 `HTTPContext`, 每个依赖注入的服务都会从中创建出来，[DefaultHttpContextFactory](https://github.com/dotnet/aspnetcore/blob/6dfaf9e2cff6cfa3aab0b7842fe02fe9f71e0f60/src/Hosting/Hosting/src/Http/DefaultHttpContextFactory.cs#L67) 是其中一个默认实现

```csharp
internal void Initialize(DefaultHttpContext httpContext, IFeatureCollection featureCollection)
{
   httpContext.Initialize(featureCollection);
   if (_httpContextAccessor != null)
   {
        _httpContextAccessor.HttpContext = httpContext;
   }
   httpContext.FormOptions = _formOptions;
   httpContext.ServiceScopeFactory = _serviceScopeFactory;
}
```


- RequestFeaturesFeatures

`IServiceProviderFeature` 是 `ASP.NET Core` 中依赖注册服务的抽象，默认情况是使用 [`RequestServicesFeature`](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http/src/DefaultHttpContext.cs#L29)

```csharp
private static readonly Func<DefaultHttpContext, IServiceProvidersFeature> _newServiceProvidersFeature = context => new RequestServicesFeature(context, context.ServiceScopeFactory);
```

那么 `RequestServicesFeature` 中，在实现 `IServiceProviderFeature` 的 `RequestServices` 属性的时候，就创建了 [`ScopedServiceProvider`](https://github.com/dotnet/aspnetcore/blob/6dfaf9e2cff6cfa3aab0b7842fe02fe9f71e0f60/src/Http/Http/src/Features/RequestServicesFeature.cs#L38) 

```csharp
 public IServiceProvider RequestServices
    {
        get
        {
            if (!_requestServicesSet && _scopeFactory != null)
            {
                _context.Response.RegisterForDisposeAsync(this);
                _scope = _scopeFactory.CreateScope();
                _requestServices = _scope.ServiceProvider;
                _requestServicesSet = true;
            }
            return _requestServices!;
        }

        set
        {
            _requestServices = value;
            _requestServicesSet = true;
        }
    }

```

我们可以看出返回了一个 `Scoped` 类型的 `IServiceProvider` 对象，这个对象会被每个请求（HTTPContext) 创建，但是在同一个 `HTTPContext`  中不会重复创建。


# 3 `Scoped` 服务的实现

`M.E.DepdendnecyInjection` 库实现太过复杂，这里提供一个[简易版本](https://gist.github.com/gaufung/d72fed06d705c499c526045abfa38376)，通过它可以理解 `Scoped` 服务的特殊之处


- ServiceCollection 字段

```csharp
public class ServiceCollection : IServiceProvider, IDisposable
{
	internal readonly ConcurrentDictionary<Type, ServiceRegistry> _registries;
	internal readonly ConcurrentDictionary<Key, object?> _services;
	
	public ServiceCollection()
	{
		_registries = new ConcurrentDictionary<Type, ServiceRegistry>();
		_root = this;
		_services = new ConcurrentDictionary<Key, object?>();
	}
	
	internal ServiceCollection(ServiceCollection parent)
	{
		_root = parent._root;
		_registries = _root._registries;
       	_services = new ConcurrentDictionary<Key, object?>();
     }
}
```

`ServiceCollection` 中的 `_registeries` 和 `_services` 两个字段分别保留依赖服务的定义和已经创建的服务实例，如果 `ServiceCollection` 由别的 `ServiceCollection` 创建，那么 `_root` 字段指向 `ServiceCollection` 的根实例，而且注入服务的定义都共享根实例的集合。在我们创建一个 `Scoped ServiceCollection` 的时候，我们就会使用这种构造方法。

- GetServicesCore

```csharp
private object? GetServiceCore(ServiceRegistry registry,
                               Type[] genereicArguments) {
  var key = new Key(registry, genereicArguments);

  switch (registry.Lifetime) {
    case Lifetime.Singleton:
      return GetOrCreate(_root._services, _root._disposables);
    case Lifetime.Scoped:
      return GetOrCreate(_services, _disposables);
    default: {
      var service = registry.Factory(this, genereicArguments);
      if (service is IDisposable disposable && disposable != this) {
        _disposables.Add(disposable);
      }

      return service;
    }
  }

  object? GetOrCreate(ConcurrentDictionary<Key, object?> services,
                      ConcurrentBag<IDisposable> disposables) {
    if (services.TryGetValue(key, out var service)) {
      return services;
    }

    service = registry.Factory(this, genereicArguments);
    services[key] = service;
    if (service is IDisposable disposable) {
      disposables.Add(disposable);
    }

    return service;
  }
}
```

创建服务的细节都在 `GetServicesCore` 这个方法中，首先在 `GetOrCreate` 方法中，我们会判断一下是否这个服务实例已经创建，如果没有创建，则调用注册时候定义创建。然后在创建的时候，如果是 `Singleton` ，我们就传入 `_root` 的 `_services`, 我们刚刚提到的所有 `ServiceCollection` 都指向了唯一的根实例，也是`Singleton` 服务的要求；如果是 `Scoped` 服务，则用当前 `ServiceCollection` 实例的 `_service` 去尝试创建，这就说明了如果我们不去创建 `Scoped` 的 `ServiceCollection`, 那么 `Scoped` 服务和 `Singleton` 服务没有任何本质的区别；最后如果这个服务是 `Transient` 的，那么每次创建都会创建新的实例，不管之前是否创建过。

所以注册为 `Scoped` 的服务，在每个 `ServiceCollection` 中都会被缓存下来，以便这个 `ServiceCollection` 后续续续使用；并且子 `ServiceCollection` 或者父 `ServiceCollection` ， 甚至兄弟 `ServiceCollection` 并不能看到这个缓存的依赖服务的实例；如果这个 `ServiceCollection` 是根实例，那么 `Scoped` 和 `Singleton` 没有区别。

# 3 Scoped 使用的注意点

在 `DependnecyInjection` 中如果一个依赖服务出现运行时不匹配，比如一个 `Singleton` 类型服务依赖一个 `Scoped` 类型的服务

```csharp
public class FooService 
{
    public void DoWork()
    {
        Console.WriteLine("FooService is starting.");
    }
}

public class BarService (FooService foo) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foo.DoWork();
        return Task.CompletedTask;
    }
}

builder.Services.AddHostedService<BarService>();
builder.Services.AddScoped<FooService>();
```

`AddHostedService` 会将 `BarService` 注册为 `Singleton` 类型，但是它依赖一个 `FooService` 是 `Scope` 类型的，所以在运行的时候，就会报错。那么我们该如何解决这个问题呢？我们可以让 `BarService` 依赖一个 `IServiceScopeFactory` 接口，因为它是按照 `Singleton` 类型注入到容器中

```csharp
public class BarService (IServiceScopeFactory _factory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _factory.CreateScope();
        var foo = scope.ServiceProvider.GetRequiredService<FooService>();
        foo.DoWork();
        return Task.CompletedTask;
    }
}
``` 

除此之外，如果 `Scoped` 服务没有处理好的话，还有可能会导致内存泄漏。在我们简易版 `ServiceCollection` 实现中，我们忽略了一个字段 `_disposables`, 这是一个集合，它记录了这个 `ServiceCollection` 创建的所有实现 `IDispose` 的对象，这个集合会在 `ServiceCollection` 调用 `Dispose` 方式被处理

```csharp
public void Dispose() {
  _disposed = true;
  foreach (var disposable in _disposables) {
    disposable.Dispose();
  }
  _disposables.Clear();
  _services.Clear();
}
```

在 `ASP.NET Core` 中，每个请求对象的 `ServiceCollection` 对象会在请求处理完毕后调用 `Dispose` 方法

```csharp
public class RequestServicesFeature : IServiceProvidersFeature,
                                      IDisposable,
                                      IAsyncDisposable {
  private readonly IServiceScopeFactory? _scopeFactory;
  private IServiceProvider? _requestServices;
  private IServiceScope? _scope;

  public IServiceProvider RequestServices {
    get {
      if (!_requestServicesSet && _scopeFactory != null) {
        _context.Response.RegisterForDisposeAsync(this);
        //...
      }
      return _requestServices !;
    }
  }

  public ValueTask DisposeAsync() {
    switch (_scope) {
      case IAsyncDisposable asyncDisposable:
        //..
        break;
      case IDisposable disposable:
        disposable.Dispose();
        break;
    }

    _scope = null;
    _requestServices = null;
    //..
  }

  /// <inheritdoc />
  public void Dispose() { DisposeAsync().AsTask().GetAwaiter().GetResult(); }
}
```

这里将 `RequestServicesFeature` 这个对象注册到 `Response.RegisterForDisposeAsync`  回调中，当请求完成的时候， `_scope` 对象就会调用 `Dispose` 方法，其中创建的`Scoped` 和 `Transient` 服务就会被释放。我们这些做的前提都是能够创建 `Scoped` 的 ServiceCollection，因为框架已经帮我们做好相应的工作。但是如果是要给 `ASP.NET Framework` 引入这个依赖注入，就需要特别注意显示的创建 `Scoped` ServiceCollection，具体实现细节可以查看[这篇问答](https://stackoverflow.com/questions/73404569/microsoft-extensions-dependencyinjection-for-net-framework-causing-memory-leak)。



