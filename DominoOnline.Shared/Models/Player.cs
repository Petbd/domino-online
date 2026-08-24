using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class Player
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public List<DominoTile> Hand { get; set; } = new();
    public bool IsAdmin { get; set; }
    public bool IsTurn { get; set; }
    public int Score { get; set; }
}