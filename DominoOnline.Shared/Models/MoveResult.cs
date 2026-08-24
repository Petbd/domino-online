using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class MoveResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool RoundOver { get; set; }          // Раунд закончился?
    public string? RoundWinnerConnectionId { get; set; }
    public int PointsAwarded { get; set; }       // Сколько очков начислено
    public bool MatchOver { get; set; }          // Вся игра закончена?
    public string? MatchWinnerConnectionId { get; set; }
    public int TargetScore { get; set; }
}