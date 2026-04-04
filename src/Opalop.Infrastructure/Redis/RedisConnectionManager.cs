namespace Opalop.Infrastructure.Redis;

using StackExchange.Redis;

public class RedisConnectionManager : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisConnectionManager(string connectionString)
    {
        _connection = new Lazy<ConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(connectionString));
    }

    public IDatabase GetDatabase() => _connection.Value.GetDatabase();
    public ISubscriber GetSubscriber() => _connection.Value.GetSubscriber();
    public IServer GetServer() => _connection.Value.GetServer(_connection.Value.GetEndPoints()[0]);

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}
