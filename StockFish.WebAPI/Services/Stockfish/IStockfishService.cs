using StockFish.WebAPI.Models;

namespace StockFish.WebAPI.Services;

public interface IStockfishService
{
    Task<string> GetBestMove(GetBestMoveQuery query);
}
