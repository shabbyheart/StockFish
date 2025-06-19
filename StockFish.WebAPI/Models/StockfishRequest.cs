namespace StockFish.WebAPI.Models;

public class StockfishRequest
{
    public TaskCompletionSource<object> CompletionSource { get; set; } = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken CancellationToken { get; set; }
}
