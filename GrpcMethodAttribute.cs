namespace BeeQ;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GrpcMethodAttribute : Attribute
{
    public string Name { get; }

    public string Version { get; }

    public GrpcMethodAttribute(string name, string version)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del método no puede estar vacío.", nameof(name));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("La versión no puede estar vacía.", nameof(version));

        Name = name;
        Version = version;
    }
}