using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class PlayerDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public int HandCount { get; set; }
    public bool IsTurn { get; set; }
    public int Score { get; set; }
    public bool IsAdmin { get; set; }
}