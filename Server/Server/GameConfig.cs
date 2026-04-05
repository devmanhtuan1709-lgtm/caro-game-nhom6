using System;
using System.Collections.Generic;

namespace Server
{
    public class GameConfig
    {
        public int BoardSize { get; set; }
        public int WinCount { get; set; }
        public int TurnTime { get; set; }

        private static Dictionary<int, int> StandardRules = new Dictionary<int, int>
        {
            { 9, 4 },
            { 10, 4 },
            { 13, 5 },
            { 15, 5 },
            { 19, 5 },
            { 20, 5 }
        };

        public GameConfig(int boardSize = 15, int? winCount = null, int turnTime = 30)
        {
            BoardSize = boardSize;
            WinCount = winCount ?? GetDefaultWinCount(boardSize);
            TurnTime = turnTime;

            if (!IsValid())
            {
                throw new ArgumentException("Cấu hình game không hợp lệ!");
            }
        }

        private int GetDefaultWinCount(int size)
        {
            if (StandardRules.ContainsKey(size))
                return StandardRules[size];

            if (size <= 10) return 4;
            if (size <= 20) return 5;
            return 6;
        }

        public bool IsValid()
        {
            return BoardSize >= 5 &&
                   BoardSize <= 50 &&
                   WinCount >= 3 &&
                   WinCount <= BoardSize - 2 &&
                   TurnTime >= 5;
        }

        public override string ToString()
        {
            return $"{BoardSize}x{BoardSize}, {WinCount} quân, {TurnTime}s";
        }
    }
}