using StockFish.WebAPI.Models;
using StockFish.WebAPI.Utilities;

namespace StockFish.WebAPI.Services;

public class StockfishService : IStockfishService
{
    public async Task<string> GetBestMove(GetBestMoveQuery query)
    {
        ValidateQuery(query);
        return "";
    }

    private void ValidateQuery(GetBestMoveQuery query)
    {
        if (!FenHelper.IsValidFen(query.FEN))
            throw new ArgumentException("Invalid FEN string.", nameof(query.FEN));

        if (query.BotLevel is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(query.BotLevel), "BotLevel must be between 1 and 10.");
    }
}
