using BeeQ.Grpc;
using System.Reflection;
using ServiceMethodType = (System.Type ServiceType, System.Reflection.MethodInfo MethodInfo, System.Reflection.ParameterInfo[] ParameterInfos);

namespace BeeQ;

public partial class DynamicGrpcService(IServiceProvider provider) : DynamicService.DynamicServiceBase
{
    private static Dictionary<string, ServiceMethodType> ServiceMethods { get; set; } = [];

    /// <summary>
    /// Inicializa los métodos gRPC dinámicos a partir de los ensamblados proporcionados.
    /// </summary>
    /// <param name="assemblies">Listado de Assemblys que contienen los métodos gRPC [GrpcService] y [GrpcMethod]</param>
    internal static void Inicialize(params Assembly[]? assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            return;

        lock (ServiceMethods)
        {
            ServiceMethods.Clear();
            foreach (var ass in assemblies)
            {
                var services = ass.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<GrpcServiceAttribute>() != null);

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
                        ServiceMethods.Add($"{serviceName}.{methodName}.{version}", (service, method, method.GetParameters()));
                    }
                }
            }
        }
    }
}