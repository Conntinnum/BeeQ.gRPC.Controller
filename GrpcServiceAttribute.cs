namespace BeeQ;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GrpcServiceAttribute : Attribute
{
    public string Name { get; }

    public GrpcServiceAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del servicio no puede estar vacío.", nameof(name));

        Name = name;
    }
}