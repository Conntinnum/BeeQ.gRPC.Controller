namespace BeeQ.Grpc.Controller;

public class GrpcClienteNotInitializedException(string? motive) : Exception(motive)
{
}
