using BeeQ.Grpc;
using Google.Protobuf;
using Grpc.Core;
using MessagePack;
using MessagePack.Resolvers;
using System.Reflection;
using ServiceMethodType = (System.Type ServiceType, System.Reflection.MethodInfo MethodInfo, System.Reflection.ParameterInfo[] ParameterInfos);

namespace BeeQ;

public partial class DynamicGrpcService
{
    /// <summary>
    /// Executes a dynamic gRPC method based on the provided request.
    /// </summary>
    /// <param name="request">gRPC request</param>
    /// <param name="context">gRPC call context</param>
    /// <returns>gRPC request response</returns>
    /// <exception cref="RpcException"></exception>
    public override async Task<DynamicResponse> Execute(DynamicRequest request, ServerCallContext context)
    {
        ServiceMethodType service;
        lock (ServiceMethods)
        {
            ServiceMethods.TryGetValue($"{request.Service}.{request.Method}.{request.Version}", out service);
        }
        if (service.ServiceType == null || service.MethodInfo == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Service {request.Service}.{request.Method}.{request.Version} not found"));

        // intento obtenerlo del ServiceProvider, sino busco un constructor sin parámetros, sino es error
        var svc = provider.GetService(service.ServiceType) ?? service.ServiceType.GetConstructor([])?.Invoke([]) ?? throw new RpcException(new Status(StatusCode.NotFound, $"Service {request.Service}.{request.Method}.{request.Version} not found"));

        // convertir el payload en parametros
        var parametros = ConvertFromGrpc(request.Payload, service.ParameterInfos);
        var result = await AwaitResult(service.MethodInfo.Invoke(svc, parametros));

        // convertir el resultado en payload
        var payloadFinal = ConvertToGrpc(service.MethodInfo.ReturnType, result);

        return new DynamicResponse
        {
            Payload = payloadFinal
        };
    }

    private static async Task<object?> AwaitResult(object? result)
    {
        if (result == null)
            return null;

        if (result is Task task)
        {
            await task;
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }

        return result;
    }

    private static object?[] ConvertFromGrpc(ByteString payload, ParameterInfo[] parameters)
    {
        if (parameters.Length == 0)
            return [];

        if (parameters.Length != 1)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Los métodos gRPC dinámicos deben tener cero o un parámetro de entrada."));

        var parameterType = parameters[0].ParameterType;

        var value = MessagePackSerializer.Deserialize(parameterType, payload.ToByteArray());

        return [value];
    }

    private static ByteString ConvertToGrpc(Type type, object? value)
    {
        if (value == null)
            return ByteString.Empty;
        var options = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
        var bytes = MessagePackSerializer.Serialize(type, value, options);
        return ByteString.CopyFrom(bytes);
    }
}