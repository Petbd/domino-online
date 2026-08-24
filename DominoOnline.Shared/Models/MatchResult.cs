using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class MatchResult
{
    public string GameId { get; set; } = string.Empty;
    public DateTime FinishedAt { get; set; }
    public string WinnerName { get; set; } = string.Empty;
    public int TargetScore { get; set; }
    public List<PlayerScore> PlayerScores { get; set; } = new();
}

public class PlayerScore
{
    public string Nickname { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsWinner { get; set; }
}