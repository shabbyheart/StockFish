using StockFish.WebAPI.Models;

namespace StockFish.WebAPI.Services.Stockfish;

public class StockfishService : IStockfishService
{
    private readonly IStockfishRequestQueue _requestQueue;

    public StockfishService(IStockfishRequestQueue requestQueue)
    {
        _requestQueue = requestQueue;
    }

    public async Task<string> GetBestMoveAsync(string fen, int botlevel, CancellationToken ct)
    {
        var request = new BestMoveRequest
        {
            FEN = fen,
            BotLevel = botlevel,
            CompletionSource = new TaskCompletionSource<object>()
        };

        await _requestQueue.EnqueueAsync(request, ct);

        using (ct.Register(() => request.CompletionSource.TrySetCanceled()))
        {
            try
            {
                return (string)await request.CompletionSource.Task;
            }
            catch (TaskCanceledException)
            {
                // Handle or rethrow
                //throw new OperationCanceledException("Best move request was canceled.", ct);
                return "Request was canceled.";
            }
        }
    }
}
