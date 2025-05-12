using System.Text.RegularExpressions;

namespace StockFish.WebAPI.Utilities;

public static class FenHelper
{
    public static bool IsValidFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            return false;

        string[] parts = fen.Split(' ');
        if (parts.Length != 6)
            return false; // FEN must have 6 fields

        // Validate board representation
        if (!IsValidBoard(parts[0]))
            return false;

        // Validate active color
        if (parts[1] != "w" && parts[1] != "b")
            return false;

        // Validate en passant target square
        if (!Regex.IsMatch(parts[3], @"^(-|[a-h][36])$"))
            return false;

        // Validate halfmove clock (must be a number)
        if (!int.TryParse(parts[4], out _))
            return false;

        // Validate fullmove number (must be a positive number)
        if (!int.TryParse(parts[5], out int fullMove) || fullMove < 1)
            return false;

        return true;
    }

    private static bool IsValidBoard(string board)
    {
        string[] ranks = board.Split('/');
        if (ranks.Length != 8)
            return false;

        foreach (string rank in ranks)
        {
            int fileCount = 0;
            foreach (char c in rank)
            {
                if (char.IsDigit(c))
                {
                    fileCount += c - '0'; // Convert '3' to 3
                }
                else if ("prnbqkPRNBQK".Contains(c))
                {
                    fileCount++;
                }
                else
                {
                    return false; // Invalid character
                }

                if (fileCount > 8)
                    return false; // More than 8 squares in a rank
            }

            if (fileCount != 8)
                return false; // Each rank must have exactly 8 files
        }

        return true;
    }
}
