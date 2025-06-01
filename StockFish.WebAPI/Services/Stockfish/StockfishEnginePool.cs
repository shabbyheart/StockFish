using System.Collections.Concurrent;

namespace StockFish.WebAPI.Services.Stockfish;

public class StockfishEnginePool : IAsyncDisposable
{
    private readonly ConcurrentQueue<StockfishEngine> _availableEngines;
    private readonly List<StockfishEngine> _allEngines = new();
    private volatile bool _disposed = false;
    private readonly SemaphoreSlim _disposeSemaphore = new(1, 1);

    public StockfishEnginePool(string enginePath, int poolSize)
    {
        _availableEngines = new ConcurrentQueue<StockfishEngine>();
        for (int i = 0; i < poolSize; i++)
        {
            var engine = new StockfishEngine(enginePath);
            _availableEngines.Enqueue(engine);
            _allEngines.Add(engine); // No lock needed
        }
    }

    public void ReleaseEngine(StockfishEngine engine)
    {
        if (!_disposed)
        {
            _availableEngines.Enqueue(engine);
        }
    }

    public async Task<StockfishEngine> AcquireEngineAsync(CancellationToken cancellationToken)
    {
        while (!_disposed && !cancellationToken.IsCancellationRequested)
        {
            if (_availableEngines.TryDequeue(out var engine))
            {
                return engine;
            }
            await Task.Delay(50, cancellationToken);
        }

        throw new ObjectDisposedException(nameof(StockfishEnginePool));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _disposeSemaphore.WaitAsync();
        try
        {
            if (_disposed) return;

            _disposed = true; // Mark as disposed first to prevent new acquisitions

            // Clear the queue to prevent new engines from being returned
            while (_availableEngines.TryDequeue(out _)) { }

            // Dispose all engines concurrently with timeout
            var enginesToDispose = _allEngines.ToList();
            using var cts = new CancellationTokenSource(10000); // 10 second total timeout

            var disposeTasks = enginesToDispose.Select(async engine =>
            {
                try
                {
                    await engine.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error disposing engine: {ex.Message}");
                }
            });

            try
            {
                await Task.WhenAll(disposeTasks).WaitAsync(cts.Token);
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine("Engine pool disposal timed out");
            }
        }
        finally
        {
            _disposeSemaphore.Release();
            _disposeSemaphore.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
