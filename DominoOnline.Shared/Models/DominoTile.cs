using System;
using System.Collections.Generic;
using System.Text;

namespace DominoOnline.Shared.Models;

public class DominoTile
{
    public int Left { get; set; }
    public int Right { get; set; }
    public bool IsDouble => Left == Right;

    public DominoTile(int left, int right)
    {
        Left = left;
        Right = right;
    }

    public override string ToString() => $"[{Left}|{Right}]";
}
