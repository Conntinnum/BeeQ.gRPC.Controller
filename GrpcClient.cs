using BeeQ.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MessagePack;
using MessagePack.Resolvers;

namespace BeeQ;

/// <summary>
/// Servicio principal de envío de mensajes vía gRPC
/// </summary>
/// <param name="channel"></param>
public sealed class GrpcClient
{
    public string? Url { get; private set; }
    public GrpcChannelOptions? ChannelOptions { get; private set; }
    private GrpcChannel? Channel;
    private DynamicService.DynamicServiceClient? Client { get; set; }

    private DynamicService.DynamicServiceClient GetClient()
    {
        if (this.Client != null)
            return this.Client;

        if (string.IsNullOrEmpty(Url))
            throw new ApplicationException("Cliente Grpc no inicializado. Llame al método UseUrl() para utilizar este servicio");

        if (Channel == null)
        {
            if (ChannelOptions == null)
                this.Channel = GrpcChannel.ForAddress(Url);
            else
                this.Channel = GrpcChannel.ForAddress(Url, ChannelOptions);
        }

        return this.Client ??= new(this.Channel);
    }


    public GrpcClient() { }
    public GrpcClient(string url, GrpcChannelOptions? options = null)
    {
        this.UseUrl(url, options);
    }
    public GrpcClient(Uri url, GrpcChannelOptions? options = null)
    {
        this.UseUrl(url, options);
    }

    public GrpcClient UseUrl(Uri url, GrpcChannelOptions? options = null)
    {
        return UseUrl(url.ToString(), options);
    }

    public GrpcClient UseUrl(string url, GrpcChannelOptions? options = null)
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",true);
        this.Url = url;
        this.ChannelOptions = options;
        _ = GetClient();    // fuerzo a que se cachee el Channel

        return this;
    }


    /// <summary>
    /// Envía un mensaje gRPC que no posee objeto request
    /// </summary>
    /// <typeparam name="TResponse">Type del objeto a recibir</typeparam>
    /// <param name="service">Nombre del servicio</param>
    /// <param name="method">Nombre del método</param>
    /// <param name="version">Versión del método</param>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version)
    {
        var payload = Array.Empty<byte>();
        return await ExecuteAsync<TResponse>(service, method, version, payload: payload, auth: null, cancellationToken: null);
    }

    /// <summary>
    /// Envía un mensaje gRPC que no posee objeto request
    /// </summary>
    /// <typeparam name="TResponse">Type del objeto a recibir</typeparam>
    /// <param name="service">Nombre del servicio</param>
    /// <param name="method">Nombre del método</param>
    /// <param name="version">Versión del método</param>
    /// <param name="auth">(opcional) JWT de acceso</param>
    /// <param name="cancellationToken">Cancelation Token asociado al Task</param>
    /// <returns></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, string? auth = null, CancellationToken? cancellationToken = null)
    {
        var payload = Array.Empty<byte>();

        return await ExecuteAsync<TResponse>(service, method, version, payload, auth, cancellationToken);
    }

    /// <summary>
    /// Envía un mensaje gRPC
    /// </summary>
    /// <typeparam name="TRequest">Type del objeto a enviar</typeparam>
    /// <typeparam name="TResponse">Type del objeto a recibir</typeparam>
    /// <param name="service">Nombre del servicio</param>
    /// <param name="method">Nombre del método</param>
    /// <param name="version">Versión del método</param>
    /// <param name="request">Objeto a enviar</param>
    /// <param name="auth">(opcional) JWT de acceso</param>
    /// <param name="cancellationToken">(opcional) Cancelation Token asociado al Task</param>
    /// <returns></returns>
    public async Task<TResponse?> ExecuteAsync<TRequest, TResponse>(string service, string method, string version, TRequest request, string? auth = null, CancellationToken? cancellationToken = null)
    {
        byte[]? payload;
        if (cancellationToken == null)
            payload = MessagePackSerializer.Serialize(request);
        else
            payload = MessagePackSerializer.Serialize(request, cancellationToken: cancellationToken.Value);

        return await ExecuteAsync<TResponse>(service, method, version, payload, auth, cancellationToken);
    }

    /// <summary>
    /// Envía un mensaje gRPC
    /// </summary>
    /// <typeparam name="TResponse">Type del objeto a recibir</typeparam>
    /// <param name="service">Nombre del servicio</param>
    /// <param name="method">Nombre del método</param>
    /// <param name="version">Versión del método</param>
    /// <param name="payload">Objeto a enviar</param>
    /// <param name="auth">(opcional) JWT de acceso</param>
    /// <param name="cancellationToken">(opcional) Cancelation Token asociado al Task</param>
    /// <returns></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, byte[]? payload = null, string? auth = null, CancellationToken? cancellationToken = null)
    {
        var metadata = CreateMetadata(auth);

        var request = new DynamicRequest
        {
            Service = service,
            Method = method,
            Version = version,
            Payload = ByteString.CopyFrom(payload ?? [])
        };

        DynamicResponse response;
        if (cancellationToken.HasValue)
            response = await this.GetClient().ExecuteAsync(request, metadata, cancellationToken: cancellationToken.Value);
        else
            response = await this.GetClient().ExecuteAsync(request, metadata);

        if (response.Payload.IsEmpty)
            return default;

        var options = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
        if (cancellationToken.HasValue)
            return MessagePackSerializer.Deserialize<TResponse>(response.Payload.ToByteArray(), options, cancellationToken: cancellationToken.Value);
        return MessagePackSerializer.Deserialize<TResponse>(response.Payload.ToByteArray(), options);
    }

    private static Metadata CreateMetadata(string? jwt)
    {
        var metadata = new Metadata();

        if (!string.IsNullOrWhiteSpace(jwt))
        {
            var tmp = jwt;
            if (tmp.StartsWith("Bearer ", StringComparison.CurrentCultureIgnoreCase))
                tmp = tmp["Bearer ".Length..];

            metadata.Add("authorization", $"Bearer {tmp}");
        }

        return metadata;
    }
}