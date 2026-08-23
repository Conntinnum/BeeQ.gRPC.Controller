namespace BeeQ;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GrpcMinimalAttribute : Attribute
{
    public string Service { get; }

    public string Method { get; }

    public string Version { get; }

    public GrpcMinimalAttribute(string service, string method, string version)
    {
        if (string.IsNullOrWhiteSpace(service))
            throw new ArgumentException("El nombre del servicio no puede estar vacío.", nameof(service));

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("El nombre del método no puede estar vacío.", nameof(method));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("La versión no puede estar vacía.", nameof(version));

        Service = service;
        Method = method;
        Version = version;
    }
}