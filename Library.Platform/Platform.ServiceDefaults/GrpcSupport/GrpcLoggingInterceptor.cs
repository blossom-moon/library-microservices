using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults.GrpcSupport;

/// <summary>
/// Серверний interceptor для логування gRPC-викликів (Лаб. 06, частина 6.2): метод, адреса клієнта (peer),
/// статус і тривалість — структурованими властивостями; помилки зі статусом і stack trace.
/// Непередбачені винятки перетворюються на Internal без витоку деталей клієнту.
/// Підключення: services.AddGrpc(o => o.Interceptors.Add&lt;GrpcLoggingInterceptor&gt;()).
/// </summary>
public class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await continuation(request, context);
            logger.LogInformation("gRPC {Method} from {Peer}: {GrpcStatus} in {Elapsed} ms",
                context.Method, context.Peer, nameof(Grpc.Core.StatusCode.OK), Elapsed(started));
            return response;
        }
        catch (RpcException ex)
        {
            // NotFound / InvalidArgument — очікувані відповіді (Warning), решта — помилки сервера
            var level = ex.StatusCode is Grpc.Core.StatusCode.NotFound or Grpc.Core.StatusCode.InvalidArgument
                ? LogLevel.Warning : LogLevel.Error;
            logger.Log(level, ex, "gRPC {Method} from {Peer}: {GrpcStatus} in {Elapsed} ms",
                context.Method, context.Peer, ex.StatusCode.ToString(), Elapsed(started));
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("gRPC {Method} from {Peer}: {GrpcStatus} in {Elapsed} ms",
                context.Method, context.Peer, nameof(Grpc.Core.StatusCode.Cancelled), Elapsed(started));
            throw new RpcException(new Status(Grpc.Core.StatusCode.Cancelled, "The call was cancelled by the client."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "gRPC {Method} from {Peer}: unhandled exception in {Elapsed} ms",
                context.Method, context.Peer, Elapsed(started));
            throw new RpcException(new Status(Grpc.Core.StatusCode.Internal, "Internal server error."));
        }
    }

    private static double Elapsed(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
