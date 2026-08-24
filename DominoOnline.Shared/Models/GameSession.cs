using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class GameSession
{
    public string GameId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public List<Player> Players { get; set; } = new();
    public List<DominoTile> Board { get; set; } = new();
    public List<DominoTile> Boneyard { get; set; } = new();
    public bool IsStarted { get; set; }
    public string? CurrentTurnConnectionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsFull => Players.Count >= 2;

    // Новые поля для концов стола
    public int LeftEnd { get; set; } = -1;
    public int RightEnd { get; set; } = -1;
    public int TargetScore { get; set; } = 101;
    public int CurrentRound { get; set; } = 1;
    public bool IsMatchOver { get; set; }
    public string? MatchWinnerConnectionId { get; set; }
}