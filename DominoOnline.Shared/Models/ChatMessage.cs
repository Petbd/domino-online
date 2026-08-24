using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class ChatMessage
{
    public string SenderNickname { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}