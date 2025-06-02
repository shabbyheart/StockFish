namespace StockFish.WebAPI.Services.Stockfish;

public interface IStockfishService
{
    Task<string> GetBestMoveAsync(string fen, int botlevel, CancellationToken ct);
}
