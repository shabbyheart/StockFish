namespace StockFish.WebAPI.Models;

public class GetBestMoveDto
{
    public string FEN { get; set; } = string.Empty;
    public int BotLevel { get; set; }
}
