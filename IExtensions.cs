using Grpc.AspNetCore.Server;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace BeeQ
{
    public static class IExtensions
    {
        public static IHostApplicationBuilder AddGrpcControllerServer(this IHostApplicationBuilder host, int? serverPort = null, Action<GrpcServiceOptions>? options = null)
        {
            return InternalAddGrpcController(host, null, null, serverPort, options);
        }

        public static IHostApplicationBuilder AddGrpcControllerClient(this IHostApplicationBuilder host, Action<GrpcServiceOptions>? options = null)
        {
            return InternalAddGrpcController(host, null, null, null, options);
        }
        public static IHostApplicationBuilder AddGrpcControllerClient(this IHostApplicationBuilder host, string url, GrpcChannelOptions? channelOptions = null, Action<GrpcServiceOptions>? options = null)
        {
            return InternalAddGrpcController(host, url, channelOptions, null, options);
        }
        public static IHostApplicationBuilder AddGrpcControllerClient(this IHostApplicationBuilder host, Uri url, GrpcChannelOptions? channelOptions = null, Action<GrpcServiceOptions>? options = null)
        {
            return InternalAddGrpcController(host, url.ToString(), channelOptions, null, options);
        }

        public static IHostApplicationBuilder AddGrpcControllerBoth(this IHostApplicationBuilder host, string? url, GrpcChannelOptions? channelOptions = null, int? serverPort = null, Action<GrpcServiceOptions>? options = null)
        {
            return InternalAddGrpcController(host, url, channelOptions, serverPort, options);
        }

        private static IHostApplicationBuilder InternalAddGrpcController(IHostApplicationBuilder host, string? url, GrpcChannelOptions? channelOptions, int? serverPort = null, Action<GrpcServiceOptions>? options = null)
        {
            if (options != null)
                host.Services.AddGrpc(options);
            else
                host.Services.AddGrpc();

            if (string.IsNullOrEmpty(url))
                host.Services.AddTransient<GrpcClient>(provider => new GrpcClient());
            else
                host.Services.AddTransient<GrpcClient>(provider => new GrpcClient(url, channelOptions));


            if (host is WebApplicationBuilder h && serverPort.HasValue)
            {
                h.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(serverPort.Value, listenOptions =>
                    {
                        listenOptions.UseHttps();
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
            }

            return host;
        }

        public static IEndpointRouteBuilder UseGrpcController(this IEndpointRouteBuilder app, params Assembly[]? assemblies)
        {
            app.MapGrpcService<DynamicGrpcService>(); //GRPC
            DynamicGrpcService.Inicialize(app.DataSources, assemblies);

            return app;
        }
    }
}
