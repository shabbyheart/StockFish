using StockFish.WebAPI.Models;
using System.Threading.Channels;

namespace StockFish.WebAPI.Services.Stockfish;

public interface IStockfishRequestQueue
{
    ValueTask EnqueueAsync(StockfishRequest request, CancellationToken ct);
    ChannelReader<StockfishRequest> Reader { get; }
    void Complete();
}
