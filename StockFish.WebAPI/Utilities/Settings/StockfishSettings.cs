namespace StockFish.WebAPI.Utilities.Settings;

public record StockfishSettings(
    int Skill,
    int? Depth,
    int? MoveTime,
    int? Contempt,
    int? EloRating = null,
    int? WhiteTime = null,
    int? BlackTime = null,
    int? WhiteIncrement = null,
    int? BlackIncrement = null
);
