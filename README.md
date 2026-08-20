# BeeQ.gRPC.Controller
Simplificación del uso de gRPC para usarlo en forma de Controllers similar y compatible con REST. \
Tambien simplifica la creación de los archivos .proto ya que no son necesarios y pueden utilizarse los objetos class típicos de Json.

> [!NOTE]
> Esta librería es para simplificar la comunicación entre sistemas .NET, no es una forma que se recomiende para comunicaciones formales con sistemas externos, donde se recomienda el uso de los archivos proto para mantener el tipado entre sistemas. Esta librería tiene como objetivo comunicar servicios que poseen los mismos objetos class ya creados de ambos lados de la llamada simplificando la creación de los archivos .proto que solo duplicarían código.


## Configuración
La librería requiere 2 pasos de configuración en el proyecto que la use. \
La App puede ser configurada como `Servidor` o `Solo Cliente`. \
Es importante que al invocar el método `UseGrpcController` se envíe como parámetro los Assemblys de los proyectos que tengan los Controllers y Servicios que utilicen [GrpcService] de otro modo no serán incluídos como servicio gRPC y al invocarlos se devolverá un `RpcException` con `StatusCode.NotFound`

### Servidor
La librería permite configurar la app como Servidor de gRPC. \
Sin embargo, esto no limita la capacidad de enviar mensajes a otros gRPC.Controller (ver mas adelante `Forma Standard` de hacer una llamada)
``` csharp
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
app.Run()
```

### Cliente
La librería permite configurar la app como exclusivamente Cliente de gRPC. Es decir que no estará escuchando llamadas, solo hará llamadas a otros Servicios gRPC. \
``` csharp
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
app.Run()
```

### Formas alternativas para realizar Llamadas gRPC
Existen formas alternativas si es que ya se cuenta con la Uri del servicio de destino al momento de crear la app o si se obtiene posterior a que la App haya levantado.

#### Forma Standard
Al momento de Agregar el servicio del Host, se deja la funcion sin parámetros. \
La clase `GrpcClient` será accesible desde Inyector pero esto implica que al momento de usar el método de envío se deberá previamente establecer la url al servicio 

> Program.cs
``` csharp
builder.Services.AddGrpcControllerClient();
```
> ClienteService.cs (ejemplo)
``` csharp
public class ClienteService
{
    private readonly GrpcClient grpc;

    public ClienteService(GrpcClient grpc)
    {
        this.grpc = grpc;
        this.grpc.UseUrl("http://127.0.0.1:8800");
    }

    ...
}
```

> [!NOTE]
> Hay que tener especial cuidado con el `UseUrl()` ya que al momento de usarlo se crea y se abre la conexión con el servicio y eso genera un costo. Debe ser usado la menor cantidad de veces o catchearlo

Por ejemplo, si tenemos múltiples conexiones a diferentes servicios, tal vez sea buena idea guardar las referencias en memoria de forma estática o en Singleton.
``` csharp
public class ClienteService
{
    private static readonly GrpcClient? grpcServicio1;
    private static readonly GrpcClient? grpcServicio2;

    public ClienteService
    {
        if (this.grpcServicio1 == null) 
            grpcServicio1 = new GrpcClient("http://127.0.0.1:8800");

        if (this.grpcServicio2 == null) 
            grpcServicio1 = new GrpcClient("http://127.0.0.1:2200");
    }
}
```


#### Forma con la Url preseteada
Al momento de Agregar el servicio del Host, se indica la url y ya queda configurada de esa forma. \
La clase `GrpcClient` será accesible desde Inyector y ya vendrá con la Url establecida. \
Es válido aclarar que la Url se puede cambiar utilizando el método `UseUrl()` pero no es necesario

> Program.cs
``` csharp
builder.Services.AddGrpcControllerClient("http://127.0.0.1:8080");
```
> ClienteService.cs (ejemplo)
``` csharp
public class ClienteService(GrpcClient grpc)
{
    ...
}
```

## Lado Servidor
### Uso
La librería permite recuperar información de los servicios gRPC de forma similar a como se hace con los controladores REST, usando atributos y métodos similares. \
Incluso permite que un método sea REST y gRPC al mismo tiempo, usando el mismo método para ambos protocolos. \
Ejemplo de Uso:
``` csharp
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
En este ejemplo, el método `Get` es accesible tanto vía gRPC como vía HTTP GET, y se puede acceder a él usando el nombre del método y la versión especificada en el atributo `GrpcMethod`.

#### Solo Grpc
Es posible crear métodos que solo sean accesibles vía gRPC, para ello se puede usar el atributo `GrpcMethod` sin el atributo `HttpGet` o cualquier otro atributo de HTTP.

``` csharp
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


## Lado Cliente
Para realizar las llamadas gRPC solo se debe instanciar la clase `GrpcClient` e invocar al método `ExecuteAsync`
``` csharp
public class ClienteService(GrpcClient grpc)
{
    public async Task<ClienteDto[]?> GetClientes(ClienteFiltrosDto filtros)
    {
        return await grpc.ExecuteAsync<ClienteFiltrosDto, ClienteDto[]>("Clientes", "Find", "v1", filtros);
    }
}
```

### Autentificación
Para enviar la información de `authorization`, existe el parámetro opcional `auth`
``` csharp
public class ClienteService(GrpcClient grpc)
{
    public async Task<ClienteDto[]?> GetClientes(ClienteFiltrosDto filtros)
    {
        var auth = Request.Headers["Authorization"];
        return await grpc.ExecuteAsync<ClienteFiltrosDto, ClienteDto[]>("Clientes", "Find", "v1", filtros, jwt, auth: auth);
    }
}
```

### Manejo de Threads
Es posible controlar el Task con CancellationToken enviandolo opcionalmente a la función.

``` csharp
public class ClienteService(GrpcClient grpc)
{
    public async Task<ClienteDto[]?> GetClientes(ClienteFiltrosDto filtros)
    {
        var cancelationToken = new CancellationTokenSource();
        return await grpc.ExecuteAsync<ClienteFiltrosDto, ClienteDto[]>("Clientes", "Find", "v1", filtros, jwt, cancellationToken: cancelationToken);
    }
}
```