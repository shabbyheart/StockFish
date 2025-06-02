using StockFish.WebAPI.Models;
using System.Threading.Channels;

namespace StockFish.WebAPI.Services.Stockfish;

public class StockfishRequestQueue : IStockfishRequestQueue
{
    private readonly Channel<StockfishRequest> _requestQueue;
    public StockfishRequestQueue()
    {
        _requestQueue = Channel.CreateBounded<StockfishRequest>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }
    public ValueTask EnqueueAsync(StockfishRequest request, CancellationToken ct)
    {
        return _requestQueue.Writer.WriteAsync(request, ct);
    }
    public ChannelReader<StockfishRequest> Reader => _requestQueue.Reader;

    public void Complete()
    {
        _requestQueue.Writer.Complete();
    }
}
