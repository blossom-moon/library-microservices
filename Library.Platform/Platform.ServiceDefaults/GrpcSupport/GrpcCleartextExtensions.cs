using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;

namespace Platform.ServiceDefaults.GrpcSupport;

/// <summary>
/// Лаб. 10: у Kubernetes TLS немає, а HTTP/2 без TLS (h2c) Kestrel не узгоджує на одному порту з HTTP/1.1.
/// Тому за <c>Grpc:Cleartext=true</c> сервіс слухає ДВА порти: 8080 — REST і health-проби (HTTP/1.1), 8081 — gRPC (HTTP/2).
/// Локально (Aspire, https dev-сертифікат) параметр не задається і поведінка не змінюється.
/// </summary>
public static class GrpcCleartextExtensions
{
    public const int DefaultHttpPort = 8080;
    public const int DefaultGrpcPort = 8081;

    public static WebApplicationBuilder UseCleartextGrpcPort(this WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("Grpc:Cleartext")) return builder;

        var httpPort = builder.Configuration.GetValue("Grpc:HttpPort", DefaultHttpPort);
        var grpcPort = builder.Configuration.GetValue("Grpc:Port", DefaultGrpcPort);

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenAnyIP(httpPort, o => o.Protocols = HttpProtocols.Http1);   // REST + /health/live + /health/ready
            kestrel.ListenAnyIP(grpcPort, o => o.Protocols = HttpProtocols.Http2);   // gRPC (h2c)
        });
        return builder;
    }
}
