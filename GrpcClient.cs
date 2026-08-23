using BeeQ.Grpc;
using BeeQ.Grpc.Controller;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MessagePack;
using MessagePack.Resolvers;

namespace BeeQ;

/// <summary>
/// Main service for sending messages via gRPC
/// </summary>
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
            throw new GrpcClienteNotInitializedException("Cliente Grpc no inicializado. Llame al método UseUrl() para utilizar este servicio");

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

    #region Llamadas sin Request

    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version)
    {
        var payload = Array.Empty<byte>();
        return await ExecuteAsync<TResponse>(service, method, version, auth: null, payload: payload, cancellationToken: null);
    }
    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="cancellationToken">(optional) CancellationToken associated with the task</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, CancellationToken cancellationToken)
    {
        var payload = Array.Empty<byte>();
        return await ExecuteAsync<TResponse>(service, method, version, auth: null, payload: payload, cancellationToken: cancellationToken);
    }


    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="auth">(optional) Access JWT</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, string auth)
    {
        var payload = Array.Empty<byte>();
        return await ExecuteAsync<TResponse>(service, method, version, auth: auth, payload: payload, cancellationToken: null);
    }
    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="auth">(optional) Access JWT</param>
    /// <param name="cancellationToken">(optional) CancellationToken associated with the task</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, string auth, CancellationToken cancellationToken)
    {
        var payload = Array.Empty<byte>();
        return await ExecuteAsync<TResponse>(service, method, version, auth: auth, payload: payload, cancellationToken: cancellationToken);
    }

    #endregion

    #region payload tipado

    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="request">Object to send</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TRequest, TResponse>(string service, string method, string version, TRequest request)
    {
        byte[]? payload = MessagePackSerializer.Serialize(request);
        return await ExecuteAsync<TResponse>(service, method, version, auth: null, payload: payload, cancellationToken: null);
    }

    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="request">Object to send</param>
    /// <param name="cancellationToken">(optional) CancellationToken associated with the task</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TRequest, TResponse>(string service, string method, string version, TRequest request, CancellationToken cancellationToken)
    {
        byte[]? payload = MessagePackSerializer.Serialize(request, cancellationToken: cancellationToken);
        return await ExecuteAsync<TResponse>(service, method, version, auth: null, payload: payload, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sends a gRPC message
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="request">Object to send</param>
    /// <param name="auth">(optional) Access JWT</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TRequest, TResponse>(string service, string method, string version, string auth, TRequest request)
    {
        byte[]? payload = MessagePackSerializer.Serialize(request);
        return await ExecuteAsync<TResponse>(service, method, version, payload: payload, auth: auth, cancellationToken: null);
    }

    /// <summary>
    /// Envía un mensaje gRPC
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="request">Object to send</param>
    /// <param name="auth">(optional) Access JWT</param>
    /// <param name="cancellationToken">(optional) CancellationToken associated with the task</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TRequest, TResponse>(string service, string method, string version, string auth, TRequest request, CancellationToken cancellationToken)
    {
        byte[]? payload = MessagePackSerializer.Serialize(request, cancellationToken: cancellationToken);
        return await ExecuteAsync<TResponse>(service, method, version, payload: payload, auth: auth, cancellationToken: cancellationToken);
    }

    #endregion

    #region payload bruto

    /// <summary>
    /// Envía un mensaje gRPC
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="payload">Object to send</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, byte[] payload)
    {
        return await ExecuteAsync<TResponse>(service, method, version, auth: null, payload: payload, cancellationToken: null);
    }

    /// <summary>
    /// Envía un mensaje gRPC
    /// </summary>
    /// <typeparam name="TResponse">Type of the object to receive</typeparam>
    /// <param name="service">Service name</param>
    /// <param name="method">Method name</param>
    /// <param name="version">Method version</param>
    /// <param name="payload">Object to send</param>
    /// <param name="cancellationToken">(optional) CancellationToken associated with the task</param>
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, byte[] payload, CancellationToken cancellationToken)
    {
        return await ExecuteAsync<TResponse>(service, method, version, payload: payload, auth: null, cancellationToken: cancellationToken);
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
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, string auth, byte[] payload)
    {
        return await ExecuteAsync<TResponse>(service, method, version, payload: payload, auth: auth, cancellationToken: null);
    }

    #endregion

    #region Funcion Completa

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
    /// <returns><typeparamref name="TResponse"/></returns>
    public async Task<TResponse?> ExecuteAsync<TResponse>(string service, string method, string version, string? auth, byte[] payload, CancellationToken? cancellationToken)
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

    #endregion

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