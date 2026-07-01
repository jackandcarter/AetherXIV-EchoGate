using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Server.Hosting;

public sealed record ServerRuntimeOptions(string ServiceName, ServerEndpoint BindEndpoint, int Backlog = 100);

public sealed record SessionKey(uint Value, string Channel);

public interface ISessionManager<TSession>
{
    ValueTask<TSession> AttachAsync(SessionKey key, CancellationToken cancellationToken = default);

    ValueTask DetachAsync(SessionKey key, string reason, CancellationToken cancellationToken = default);

    ValueTask<TSession?> FindAsync(SessionKey key, CancellationToken cancellationToken = default);
}

public interface IWorldRouteRegistry
{
    ValueTask<ServerEndpoint?> FindMapEndpointAsync(ZoneId zoneId, CancellationToken cancellationToken = default);
}

public interface IPacketDispatcher
{
    ValueTask DispatchAsync(SubPacket packet, CancellationToken cancellationToken = default);
}

public interface IPipelineConnection : IAsyncDisposable
{
    EndPoint? RemoteEndPoint { get; }

    PipeReader Input { get; }

    PipeWriter Output { get; }
}

public sealed class TcpPipelineConnection : IPipelineConnection
{
    private readonly TcpClient client;

    private TcpPipelineConnection(TcpClient client, PipeReader input, PipeWriter output)
    {
        this.client = client;
        Input = input;
        Output = output;
    }

    public EndPoint? RemoteEndPoint => client.Client.RemoteEndPoint;

    public PipeReader Input { get; }

    public PipeWriter Output { get; }

    public static TcpPipelineConnection Create(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        return new TcpPipelineConnection(client, PipeReader.Create(stream), PipeWriter.Create(stream));
    }

    public async ValueTask DisposeAsync()
    {
        await Input.CompleteAsync().ConfigureAwait(false);
        await Output.CompleteAsync().ConfigureAwait(false);
        client.Dispose();
    }
}

public sealed class InMemorySessionManager<TSession> : ISessionManager<TSession>
    where TSession : class, new()
{
    private readonly Dictionary<SessionKey, TSession> sessions = new();

    public ValueTask<TSession> AttachAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        TSession session = new();
        sessions[key] = session;
        return ValueTask.FromResult(session);
    }

    public ValueTask DetachAsync(SessionKey key, string reason, CancellationToken cancellationToken = default)
    {
        sessions.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask<TSession?> FindAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        sessions.TryGetValue(key, out TSession? session);
        return ValueTask.FromResult(session);
    }
}
