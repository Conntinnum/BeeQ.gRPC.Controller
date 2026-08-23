using BeeQ.Grpc;
using BeeQ.Grpc.Controller;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using ServiceMethodType = (System.Type ServiceType, System.Reflection.MethodInfo MethodInfo, System.Reflection.ParameterInfo[] ParameterInfos);

namespace BeeQ;

public partial class DynamicGrpcService(IServiceProvider provider) : DynamicService.DynamicServiceBase
{
    private static Dictionary<string, ServiceMethodType> ServiceMethods { get; set; } = [];

    /// <summary>
    /// Initializes the dynamic gRPC methods from the provided assemblies.
    /// </summary>
    /// <param name="assemblies">List of assemblies that contain gRPC methods marked with [GrpcService] and [GrpcMethod]</param>
    internal static void Inicialize(ICollection<Microsoft.AspNetCore.Routing.EndpointDataSource> dataSources, Assembly[]? assemblies)
    {
        lock (ServiceMethods)
        {
            ServiceMethods.Clear();
            InternalInicialize(assemblies);
            InternalInicialize(dataSources);
        }
    }
    /// <summary>
    /// Initializes the dynamic gRPC methods from the provided assemblies.
    /// </summary>
    /// <param name="assemblies">List of assemblies that contain gRPC methods marked with [GrpcService] and [GrpcMethod]</param>
    internal static void InternalInicialize(Assembly[]? assemblies)
    {
        ServiceMethods.Clear();

        // Controllers / Services
        foreach (var ass in assemblies ?? [])
        {
            var services = ass.GetTypes().Where(t => t.IsClass && t.GetCustomAttribute<GrpcServiceAttribute>() != null);

            foreach (var service in services)
            {
                var serviceAttr = service.GetCustomAttribute<GrpcServiceAttribute>()!;
                var methods = service.GetMethods().Where(m => m.GetCustomAttribute<GrpcMethodAttribute>() != null);
                foreach (var method in methods)
                {
                    var methodAttr = method.GetCustomAttribute<GrpcMethodAttribute>()!;

                    var serviceName = serviceAttr.Name;
                    var methodName = methodAttr.Name;
                    var version = methodAttr.Version;
                    if (!ServiceMethods.TryAdd($"{serviceName}.{methodName}.{version}", (service, method, method.GetParameters())))
                        throw new GrpcMethodDuplicatedException($"{serviceName}.{methodName}.{version}");
                }
            }
        }
    }

    /// <summary>
    /// Initializes the dynamic gRPC methods from the provided assemblies.
    /// </summary>
    /// <param name="assemblies">List of assemblies that contain gRPC methods marked with [GrpcService] and [GrpcMethod]</param>
    internal static void InternalInicialize(ICollection<Microsoft.AspNetCore.Routing.EndpointDataSource> dataSources)
    {
        // Minimal API
        foreach (var dss in dataSources ?? [])
        {
            foreach (var ep in dss.Endpoints)
            {
                var data = GetMethodFromEndpoint(ep);
                if (data == null)
                    continue;

                var method = data.Value.Method;
                var methodAttr = data.Value.Attr;

                var serviceName = methodAttr.Service;
                var methodName = methodAttr.Method;
                var version = methodAttr.Version;
                if (!ServiceMethods.TryAdd($"{serviceName}.{methodName}.{version}", (method.DeclaringType!, method, method.GetParameters())))
                    throw new GrpcMethodDuplicatedException($"{serviceName}.{methodName}.{version}");
            }
        }
    }

    private static (MethodInfo Method, GrpcMinimalAttribute Attr)? GetMethodFromEndpoint(Endpoint endpoint)
    {
        if (endpoint.RequestDelegate == null)
            return null;

        var methodAttr = endpoint.Metadata.GetMetadata<GrpcMinimalAttribute>();
        if (methodAttr == null)
            return null;

        var target = endpoint.RequestDelegate.Target;
        if (target == null)
            return null;

        var pHandler = target.GetType().GetFields().SingleOrDefault(p => p.Name == "handler");
        if (pHandler == null)
            return null;

        var handler = pHandler.GetValue(target);
        if (handler == null)
            return null;

        var method = ((Delegate)handler).GetMethodInfo();
        if (method?.DeclaringType == null)
            return null;

        return (method, methodAttr);
    }
}