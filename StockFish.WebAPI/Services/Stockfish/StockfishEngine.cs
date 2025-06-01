using StockFish.WebAPI.Utilities.Settings;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace StockFish.WebAPI.Services;

public class StockfishEngine : IDisposable
{
    private const int DefaultTimeout = 2000; // 2 seconds

    private readonly Process _engineProcess;
    private readonly StreamWriter _engineInput;
    private readonly StreamReader _engineOutput;

    private volatile bool _disposed = false;
    private readonly SemaphoreSlim _disposeSemaphore = new(1, 1);

    public StockfishEngine(string enginePath)
    {
        _engineProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        _engineProcess.Start();
        _engineInput = _engineProcess.StandardInput;
        _engineOutput = _engineProcess.StandardOutput;

        //Task.Run(async () => await InitializeEngineOptions());
    }

    private async Task InitializeEngineOptions()
    {
        await _engineInput.WriteLineAsync("setoption name Hash value 128");  // Optimize hash table size
        await _engineInput.WriteLineAsync("setoption name Threads value 4"); // Use multiple threads
        await _engineInput.FlushAsync();
    }

    public async Task<List<(string Move, int Score)>> GetMultiMovesAsync(string fen, int depth, int multiPV)
    {
        var moves = new List<(string Move, int Score)>();

        await _engineInput.WriteLineAsync($"setoption name MultiPV value {multiPV}");

        await _engineInput.WriteLineAsync($"position fen {fen}");

        await _engineInput.WriteLineAsync($"go depth {depth}");

        using var cts = new CancellationTokenSource(DefaultTimeout + 5000);
        try
        {
            while (true)
            {
                string? line = await _engineOutput.ReadLineAsync();
                if (line?.StartsWith("info") == true && line.Contains("multipv"))
                {
                    // Parse the move and score
                    var match = Regex.Match(line, @"multipv (\d+) .*score cp (-?\d+) .* pv (\S+)");

                    if (match.Success)
                    {
                        int rank = int.Parse(match.Groups[1].Value);  // Rank of the move
                        int score = int.Parse(match.Groups[2].Value); // Evaluation in centipawns
                        string move = match.Groups[3].Value;          // Best move for this PV

                        moves.Add((move, score));
                    }
                }
                else if (line?.StartsWith("bestmove") == true)
                {
                    break; // Analysis is complete
                }
            }
        }
        catch (OperationCanceledException)
        {
            return moves;
        }

        return moves;
    }

    public async Task<string?> GetBestAnalysisLineAsync(string fen, int depth)
    {
        await StartNewGame();
        await WaitToEngineReady();

        await _engineInput.WriteLineAsync($"position fen {fen}");
        await _engineInput.WriteLineAsync($"go depth {depth}");
        string? bestLine = string.Empty;

        using var cts = new CancellationTokenSource(DefaultTimeout + 5000);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                string? line = await _engineOutput.ReadLineAsync();
                if (line?.StartsWith("bestmove") == true)
                {
                    return bestLine;
                }

                bestLine = line;
            }
        }
        catch (OperationCanceledException)
        {
            return "timeout";
        }

        return bestLine;
    }

    public async Task<string> GetBestMoveAsync(string fen, int botLevel)
    {
        var commands = GetStockfishCommands(botLevel);

        // Configure engine settings
        await ConfigureEngine(commands);

        // Set position
        await _engineInput.WriteLineAsync($"position fen {fen}");

        // Start analysis with appropriate constraints
        string? searchCommand = BuildSearchCommand(commands);
        await _engineInput.WriteLineAsync(searchCommand);
        await _engineInput.FlushAsync();

        // Wait for best move with timeout
        return await WaitForBestMove((commands.MoveTime ?? 0) + DefaultTimeout);
    }

    public async Task<string> GetBestMoveByRemainingTimeAsync(
        string fen,
        int skillLevel,
        bool isWhite,
        int remainingTimeSec,
        int opponentTimeSec,
        int incrementSec)
    {
        int remainingTimeMs = remainingTimeSec * 1000;
        int opponentTimeMs = opponentTimeSec * 1000;
        int incrementMs = incrementSec * 1000;

        var commands = GetStockfishCommandsWithTimeControl(
            skillLevel,
            isWhite,
            remainingTimeMs,
            opponentTimeMs,
            incrementMs);

        await ConfigureEngine(commands);

        await _engineInput.WriteLineAsync($"position fen {fen}");

        string? searchCommand = BuildTimeControlSearchCommand(commands);

        await _engineInput.WriteLineAsync(searchCommand);

        await _engineInput.FlushAsync();

        // Use a reasonable timeout based on remaining time
        int timeoutMs = Math.Min(remainingTimeMs, 20000); // Max 20 seconds timeout
        return await WaitForBestMove(timeoutMs);
    }

    /*public void Dispose()
    {
        _engineInput?.WriteLine("quit");
        _engineProcess?.WaitForExit(1000);
        if (!_engineProcess?.HasExited ?? false)
            _engineProcess?.Kill();

        _engineInput?.Dispose();
        _engineOutput?.Dispose();
        _engineProcess?.Dispose();
    }*/
    #region Disposable Implementation
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _disposeSemaphore.WaitAsync();
        try
        {
            if (_disposed) return;
            await DisposeAsyncCore();
            _disposed = true;
        }
        finally
        {
            _disposeSemaphore.Release();
        }

        GC.SuppressFinalize(this);
    }

    private async Task DisposeAsyncCore()
    {
        try
        {
            // Send quit command if process is still running
            if (_engineProcess?.HasExited == false)
            {
                try
                {
                    await _engineInput.WriteLineAsync("quit");
                    await _engineInput.FlushAsync();
                }
                catch (Exception)
                {
                    // Input stream might be closed, ignore
                }

                // Wait for graceful exit with timeout
                using var cts = new CancellationTokenSource(3000); // 3 second timeout
                try
                {
                    await _engineProcess.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred, force kill
                    try
                    {
                        _engineProcess.Kill();
                        await _engineProcess.WaitForExitAsync(CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        // Process might already be dead
                    }
                }
            }
        }
        finally
        {
            // Always dispose resources
            try { _engineInput?.Dispose(); } catch { }
            try { _engineOutput?.Dispose(); } catch { }
            try { _engineProcess?.Dispose(); } catch { }
            try { _disposeSemaphore?.Dispose(); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposeSemaphore.Wait();
        try
        {
            if (_disposed) return;
            DisposeSync();
            _disposed = true;
        }
        finally
        {
            _disposeSemaphore.Release();
        }
    }

    private void DisposeSync()
    {
        try
        {
            if (_engineProcess?.HasExited == false)
            {
                try
                {
                    _engineInput?.WriteLine("quit");
                    _engineInput?.Flush();
                    _engineProcess?.WaitForExit(2000);
                }
                catch (Exception) { }

                if (_engineProcess?.HasExited == false)
                {
                    try { _engineProcess?.Kill(); } catch { }
                }
            }
        }
        finally
        {
            try { _engineInput?.Dispose(); } catch { }
            try { _engineOutput?.Dispose(); } catch { }
            try { _engineProcess?.Dispose(); } catch { }
            try { _disposeSemaphore?.Dispose(); } catch { }
        }
    }
    #endregion

    #region Private
    private async Task StartNewGame()
    {
        await _engineInput.WriteLineAsync("ucinewgame");
    }

    private async Task WaitToEngineReady()
    {
        await _engineInput.WriteLineAsync("isready");

        // Wait for engine to be ready
        string? line;
        while ((line = await _engineOutput!.ReadLineAsync()) != null)
        {
            if (line.Trim() == "readyok")
                break;
        }
    }

    #region Get Best move

    private static string BuildTimeControlSearchCommand(StockfishSettings commands)
    {
        var constraints = new List<string>();

        if (commands.WhiteTime.HasValue)
            constraints.Add($"wtime {commands.WhiteTime}");
        if (commands.BlackTime.HasValue)
            constraints.Add($"btime {commands.BlackTime}");
        if (commands.WhiteIncrement.HasValue)
            constraints.Add($"winc {commands.WhiteIncrement}");
        if (commands.BlackIncrement.HasValue)
            constraints.Add($"binc {commands.BlackIncrement}");
        if (commands.Depth.HasValue)
            constraints.Add($"depth {commands.Depth}");

        return $"go {string.Join(" ", constraints)}";
    }

    private async Task ConfigureEngine(StockfishSettings commands)
    {
        await StartNewGame();
        await WaitToEngineReady();

        await _engineInput.WriteLineAsync("setoption name UCI_Variant value chess");
        await _engineInput.WriteLineAsync("setoption name Hash value 128");  // Optimize hash table size
        await _engineInput.WriteLineAsync("setoption name Threads value 4"); // Use multiple threads
        await _engineInput.WriteLineAsync($"setoption name Skill Level value {commands.Skill}");
        await _engineInput.WriteLineAsync($"setoption name Contempt value {commands.Contempt}");

        if (commands.EloRating.HasValue)
        {
            await _engineInput.WriteLineAsync($"setoption name UCI_LimitStrength value true");
            await _engineInput.WriteLineAsync($"setoption name UCI_Elo value {commands.EloRating}");
        }
        else
        {
            await _engineInput.WriteLineAsync($"setoption name UCI_LimitStrength value false");
        }

        await WaitToEngineReady();
    }

    private static string BuildSearchCommand(StockfishSettings commands)
    {
        var constraints = new List<string>();

        if (commands.MoveTime.HasValue)
            constraints.Add($"movetime {commands.MoveTime}");

        if (commands.Depth.HasValue)
            constraints.Add($"depth {commands.Depth}");

        return $"go {string.Join(" ", constraints)}";
    }

    private async Task<string> WaitForBestMove(int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                string? line = await _engineOutput.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("bestmove"))
                {
                    string[] parts = line.Split(' ');
                    return parts.Length >= 2 ? parts[1] : "timeout";
                }
            }

            await _engineInput.WriteLineAsync("stop");
            await _engineInput.FlushAsync();
            return "timeout";
        }
        catch (OperationCanceledException)
        {
            return "timeout";
        }
    }

    private static StockfishSettings GetStockfishCommands(int skillLevel)
    {

        static int GetDepth(int level) => level switch
        {
            1 => 6,      // Increased from 5 for better moves
            2 => 8,      // Consistent beginner depth
            3 => 10,     // Stronger early game
            4 => 12,     // Better tactical awareness
            5 => 14,     // Improved middlegame
            6 => 18,     // Enhanced calculation
            7 => 20,     // Strong positional play
            8 => 22,     // Expert analysis
            9 => 24,     // Deep calculation
            10 => 30,    // Maximum depth
            _ => 12
        };

        return skillLevel switch
        {
            1 => new StockfishSettings(0, GetDepth(1), MoveTime: 200, Contempt: 100, EloRating: 1000),
            2 => new StockfishSettings(2, GetDepth(2), MoveTime: 400, Contempt: 75, EloRating: 1200),
            3 => new StockfishSettings(5, GetDepth(3), MoveTime: 600, Contempt: 50, EloRating: 1400),
            4 => new StockfishSettings(8, GetDepth(4), MoveTime: 800, Contempt: 25, EloRating: 1600),
            5 => new StockfishSettings(10, GetDepth(5), MoveTime: 1000, Contempt: 0, EloRating: 1800),
            6 => new StockfishSettings(13, GetDepth(6), MoveTime: 1500, Contempt: -10, EloRating: 2000),
            7 => new StockfishSettings(15, GetDepth(7), MoveTime: 2000, Contempt: -20, EloRating: 2200),
            8 => new StockfishSettings(17, GetDepth(8), MoveTime: 2500, Contempt: -25, EloRating: 2500),
            9 => new StockfishSettings(19, GetDepth(9), MoveTime: 3000, Contempt: -30, EloRating: 2700),
            10 => new StockfishSettings(20, GetDepth(10), MoveTime: 4000, Contempt: -50, EloRating: 2800),
            _ => throw new ArgumentException("Invalid skill level. Must be between 1 and 10.")
        };
    }

    private static StockfishSettings GetStockfishCommandsWithTimeControl(
        int skillLevel,
        bool isWhite,
        int remainingTimeMs,
        int opponentTimeMs,
        int incrementMs)
    {
        static int GetDepth(int level) => level switch
        {
            1 => 6,      // Increased from 5 for better moves
            2 => 8,      // Consistent beginner depth
            3 => 10,     // Stronger early game
            4 => 12,     // Better tactical awareness
            5 => 14,     // Improved middlegame
            6 => 18,     // Enhanced calculation
            7 => 20,     // Strong positional play
            8 => 22,     // Expert analysis
            9 => 24,     // Deep calculation
            10 => 30,    // Maximum depth
            _ => 12
        };

        var baseSettings = skillLevel switch
        {
            1 => new StockfishSettings(0, GetDepth(1), null, Contempt: 100, EloRating: 1000),
            2 => new StockfishSettings(2, GetDepth(2), null, Contempt: 75, EloRating: 1200),
            3 => new StockfishSettings(5, GetDepth(3), null, Contempt: 50, EloRating: 1400),
            4 => new StockfishSettings(8, GetDepth(4), null, Contempt: 25, EloRating: 1600),
            5 => new StockfishSettings(10, GetDepth(5), null, Contempt: 0, EloRating: 1800),
            6 => new StockfishSettings(13, GetDepth(6), null, Contempt: -10, EloRating: 2000),
            7 => new StockfishSettings(15, GetDepth(7), null, Contempt: -20, EloRating: 2200),
            8 => new StockfishSettings(17, GetDepth(8), null, Contempt: -25, EloRating: 2500),
            9 => new StockfishSettings(19, GetDepth(9), null, Contempt: -30, EloRating: 2700),
            10 => new StockfishSettings(20, GetDepth(10), null, Contempt: -50, EloRating: 2800),
            _ => throw new ArgumentException("Invalid skill level. Must be between 1 and 10.")
        };

        return baseSettings with
        {
            WhiteTime = isWhite ? remainingTimeMs : opponentTimeMs,
            BlackTime = isWhite ? opponentTimeMs : remainingTimeMs,
            WhiteIncrement = incrementMs,
            BlackIncrement = incrementMs
        };
    }
    #endregion

    #endregion

}