# BeeQ.gRPC.Controller

Simplifies the usage of gRPC by allowing a Controller-like approach similar to and compatible with REST.

It also eliminates the need to manually create `.proto` files, allowing you to use standard C# JSON class objects directly.

> [!NOTE]
> This library is intended to simplify communication between **.NET systems**. It is not recommended for formal communication with external third-party systems where `.proto` files are preferred to maintain strict contract typing across different languages. The main goal of this library is to seamlessly connect services that already share the same C# class objects on both sides of the call, avoiding redundant `.proto` definitions.

---

## Configuration

The library requires a 2-step setup process in your project.

The application can be configured either as a **Server** or as a **Client Only**.

> **Important:** When invoking `UseGrpcController`, you must pass the target Assemblies containing the Controllers and Services that use `[GrpcService]` as parameters. Otherwise, they will not be registered as gRPC services, and calling them will throw an `RpcException` with `StatusCode.NotFound`.

### Server

Allows you to configure your application to act as a gRPC Server.

Note that this does not restrict your app from making outbound calls to other gRPC controllers (see the *Standard Approach* section below).

```csharp
using BeeQ;
...
var builder = WebApplication.CreateBuilder(args);
...
builder.Services.AddGrpcControllerServer(port);
...
var app = builder.Build();
...
app.UseGrpcController(typeof(Program).Assembly);
...
app.Run();

```

### Client

Configures the app exclusively as a gRPC Client. In this mode, the app will not listen for incoming gRPC calls; it will only make requests to other gRPC services.

```csharp
using BeeQ;
...
var builder = WebApplication.CreateBuilder(args);
...
builder.Services.AddGrpcControllerClient("https://127.0.0.1:8800");
...
var app = builder.Build();
...
app.UseGrpcController(typeof(Program).Assembly);
...
app.Run();

```

---

### Alternative Ways to Make gRPC Calls

There are alternative approaches depending on whether you know the target service URI at startup or retrieve it dynamically after the application has started.

#### Standard Approach

Register the client service without passing parameters.

The `GrpcClient` instance will be available via Dependency Injection (DI), but you must set the target URL using `UseUrl(...)` before making a request.

> **Program.cs**

```csharp
builder.Services.AddGrpcControllerClient();

```

> **ClientService.cs** *(Example)*

```csharp
public class ClientService
{
    private readonly GrpcClient grpc;

    public ClientService(GrpcClient grpc)
    {
        this.grpc = grpc;
        this.grpc.UseUrl("http://127.0.0.1:8800");
    }

    ...
}

```

> [!NOTE]
> Be cautious when using `UseUrl()`, as calling it creates and opens a network connection to the service, which carries an execution overhead. It should be called sparingly or cached appropriately.
> 
> 

For instance, if you handle multiple connections to different remote services, it is recommended to cache instances in memory (e.g., as static fields or within a Singleton):

```csharp
public class ClientService
{
    private static readonly GrpcClient? grpcService1;
    private static readonly GrpcClient? grpcService2;

    public ClientService()
    {
        if (grpcService1 == null) 
            grpcService1 = new GrpcClient("http://127.0.0.1:8800");

        if (grpcService2 == null) 
            grpcService2 = new GrpcClient("http://127.0.0.1:2200");
    }
}

```

#### Pre-configured URL Approach

Provide the target URL at service registration time.

The `GrpcClient` instance injected via DI will come pre-configured with that URL. You can still override it using `UseUrl()`, though it is generally not necessary.

> **Program.cs**

```csharp
builder.Services.AddGrpcControllerClient("http://127.0.0.1:8080");

```

> **ClientService.cs** *(Example)*

```csharp
public class ClientService(GrpcClient grpc)
{
    ...
}

```

---

## Server Side

### Usage

The library allows you to expose gRPC endpoints using familiar Controller-style attributes and patterns.

It even supports Dual-Mode methods (REST + gRPC simultaneously) using the exact same C# method.

**Example:**

```csharp
[GrpcService("WeatherForecast")]
[ApiController, Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    [GrpcMethod("GetWeatherForecast", "v1")]
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        ...
    }
}

```

In this example, the `Get` method is accessible via both HTTP GET (REST) and gRPC, using the method name and version specified in the `[GrpcMethod]` attribute.

#### gRPC Only

You can also create methods exclusively accessible via gRPC by using `[GrpcMethod]` without adding HTTP attributes like `[HttpGet]`.

```csharp
[GrpcService("WeatherForecast")]
public class WeatherForecastController
{
    [GrpcMethod("GetWeatherForecast", "v1")]
    public IEnumerable<WeatherForecast> Get()
    {
        ...
    }
}

```

---

## Client Side

To execute gRPC calls, simply inject or instantiate `GrpcClient` and call `ExecuteAsync`:

```csharp
public class ClientService(GrpcClient grpc)
{
    public async Task<ClientDto[]?> GetClients(ClientFilterDto filters)
    {
        return await grpc.ExecuteAsync<ClientFilterDto, ClientDto[]>("Clients", "Find", "v1", filters);
    }
}

```

### Authentication

To forward `Authorization` tokens, pass the optional `auth` parameter:

```csharp
public class ClientService(GrpcClient grpc)
{
    public async Task<ClientDto[]?> GetClients(ClientFilterDto filters)
    {
        var auth = Request.Headers["Authorization"];
        return await grpc.ExecuteAsync<ClientFilterDto, ClientDto[]>("Clients", "Find", "v1", filters, auth: auth);
    }
}

```

### Threading & Cancellation

You can control request execution by passing an optional `CancellationToken`:

```csharp
public class ClientService(GrpcClient grpc)
{
    public async Task<ClientDto[]?> GetClients(ClientFilterDto filters)
    {
        var cts = new CancellationTokenSource();
        return await grpc.ExecuteAsync<ClientFilterDto, ClientDto[]>("Clients", "Find", "v1", filters, cancellationToken: cts.Token);
    }
}

```

---

## Auto-Documentation

The library includes built-in auto-documentation tools that generate metadata about available services once the application is running:

```csharp
[ApiController]
public class GrpcDocumenterController : BaseController
{
    [HttpGet("openapi")]
    public string GetOpenApi()
    {
        return BeeQ.DynamicGrpcService.GenerateOpenApi();
    }
}

```

---

## External gRPC Calls

Although the main goal of this library is to abstract gRPC setup and let you work directly with C# objects, you can still call these services from external applications or non-.NET clients.

The underlying `.proto` contract is defined as follows:

```proto
syntax = "proto3";

option csharp_namespace = "BeeQ.Grpc";

package dynamic;

service DynamicService {
    rpc Execute (DynamicRequest) returns (DynamicResponse);
}

message DynamicRequest {
    string service = 1;
    string method = 2;
    string version = 3;
    bytes payload = 4;
}

message DynamicResponse {
    bytes payload = 1;
}

```

When invoking the `Execute` RPC method, set the request parameters as follows:

* `service`: Name of the target gRPC service (matches `[GrpcService("NAME")]`).


* `method` and `version`: Target method and version (matches `[GrpcMethod("METHOD", "VERSION")]`).


* `payload`: The request body serialized into raw binary.



This library uses the **MessagePack** standard for object-to-bytes serialization and deserialization.

---

## Minimal APIs

The library supports ASP.NET Core Minimal APIs.

Since there are no Controller classes in Minimal API setups, you specify the service name directly per endpoint mapping using `[GrpcMinimal]`:

```csharp
...

app.MapControllers();

app.MapPost("v1/test", [GrpcMinimal("tester", "test", "v1")] () =>
{
    return "example";
});

...

app.UseGrpcController(typeof(Program).Assembly);

app.Run();

```

> **Note:** Ensure `UseGrpcController` is called **after** your endpoint `Map` definitions.

