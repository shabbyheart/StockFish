namespace StockFish.WebAPI.Models;

public class GetBestMoveQuery
{
    public string FEN { get; set; } = string.Empty;
    public int BotLevel { get; set; }
    public bool IsWhiteTurn { get; set; }
    public int RemainingTimeInSec { get; set; }
    public int OpponentTimeInSec { get; set; }
    public int IncrementTimeInSec { get; set; }
}
