namespace BeeQ.Grpc.Controller;

public class GrpcMethodDuplicatedException(string? motive) : Exception(motive)
{
}
