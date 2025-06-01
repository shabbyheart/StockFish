using StockFish.WebAPI.Models;
using System.Threading.Channels;

namespace StockFish.WebAPI.Services.Stockfish;

public class StockfishRequestProcessor : BackgroundService
{
    private readonly Channel<StockfishRequest> _requestQueue;
    private readonly StockfishEnginePool _enginePool;
    private readonly int _poolSize;

    public StockfishRequestProcessor(StockfishEnginePool enginePool, IConfiguration config)
    {
        _enginePool = enginePool;
        _poolSize = int.Parse(config["Stockfish:InstanceCount"]);
        _requestQueue = Channel.CreateBounded<StockfishRequest>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processingTasks = Enumerable.Range(0, _poolSize)
            .Select(_ => ProcessQueueAsync(stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(processingTasks);
        }
        catch (Exception)
        {
            // Console.WriteLine($"Error in background processing: {ex.Message}");  
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _requestQueue.Writer.Complete(); // no more requests will be accepted
        await _enginePool.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    public async Task<string> GetBestMoveAsync(string fen, int botlevel, CancellationToken ct)
    {
        var request = new BestMoveRequest
        {
            FEN = fen,
            BotLevel = botlevel,
            CompletionSource = new TaskCompletionSource<object>()
        };

        await _requestQueue.Writer.WriteAsync(request);

        using (ct.Register(() => request.CompletionSource.TrySetCanceled()))
        {
            return (string)await request.CompletionSource.Task;
        }
    }

    /*private async Task ProcessQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var request = await _requestQueue.Reader.ReadAsync(ct);

                // Acquire an available engine  
                var engine = await _enginePool.AcquireEngineAsync(ct);

                try
                {
                    // Process the request  
                    await ProcessRequestAsync(engine, request);
                }
                finally
                {
                    _enginePool.ReleaseEngine(engine);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break; // Graceful exit  
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing request: {ex.Message}");
            }
        }
    }*/

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var request in _requestQueue.Reader.ReadAllAsync(ct))
            {
                StockfishEngine? engine = null;

                try
                {
                    engine = await _enginePool.AcquireEngineAsync(ct);

                    await ProcessRequestAsync(engine, request);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing request: {ex}");
                }
                finally
                {
                    if (engine != null)
                    {
                        _enginePool.ReleaseEngine(engine);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }


    private async Task ProcessRequestAsync(StockfishEngine engine, StockfishRequest request)
    {
        switch (request)
        {
            case BestMoveRequest bestMoveRequest:
                string bestMove = await engine.GetBestMoveAsync(
                        bestMoveRequest.FEN,
                        bestMoveRequest.BotLevel);
                bestMoveRequest.CompletionSource.SetResult(bestMove);
                break;
        }
    }
}
