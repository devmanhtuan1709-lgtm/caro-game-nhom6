using System;
using System.Collections.Generic;
using System.Timers;
using Newtonsoft.Json;

namespace Server
{
    public class Room
    {
        public string RoomId { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public GameConfig Config { get; set; }
        public char[,] Board;

        public int TimeLeft;
        public Timer TurnTimer;
        public Player CurrentTurn;

        private Action<Room, string> onLog;
        private Action<Room, Player> onTimeout;

        public Room(string id, GameConfig config, Action<Room, string> logCallback, Action<Room, Player> timeoutCallback)
        {
            RoomId = id;
            Config = config;
            onLog = logCallback;
            onTimeout = timeoutCallback;
            Board = new char[Config.BoardSize, Config.BoardSize];
            TimeLeft = Config.TurnTime;

            TurnTimer = new Timer(1000);
            TurnTimer.Elapsed += TurnTimer_Elapsed;
        }

        void TurnTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeLeft--;
            BroadcastTimeLeft();

            if (TimeLeft <= 0)
            {
                onLog?.Invoke(this, $"{CurrentTurn.Name} hết giờ!");
                onTimeout?.Invoke(this, CurrentTurn);
                SwitchTurn();
            }
        }

        public bool IsFull() => Players.Count >= 2;

        public void AddPlayer(Player p)
        {
            if (!IsFull())
            {
                Players.Add(p);
                p.CurrentRoom = this;
            }
        }

        public bool MakeMove(Player p, int x, int y)
        {
            if (p != CurrentTurn) return false;

            if (x < 0 || x >= Config.BoardSize || y < 0 || y >= Config.BoardSize)
                return false;

            if (Board[x, y] != '\0')
                return false;

            Board[x, y] = p.Symbol[0];

            onLog?.Invoke(this, $"{p.Name} đánh ({x},{y})");

            TimeLeft = Config.TurnTime;
            SwitchTurn();

            return true;
        }

        public void SwitchTurn()
        {
            TimeLeft = Config.TurnTime;
            CurrentTurn = Players.Find(p => p != CurrentTurn);
            BroadcastTurn();
        }

        void BroadcastTurn()
        {
            var obj = new
            {
                type = "turn",
                data = new
                {
                    currentPlayer = CurrentTurn.Name,
                    symbol = CurrentTurn.Symbol,
                    timeLeft = TimeLeft
                }
            };

            string json = JsonConvert.SerializeObject(obj);

            foreach (var p in Players)
                p.Send(json);
        }

        void BroadcastTimeLeft()
        {
            var obj = new
            {
                type = "time",
                data = new { timeLeft = TimeLeft }
            };

            string json = JsonConvert.SerializeObject(obj);

            foreach (var p in Players)
                p.Send(json);
        }

        public bool CheckWin(int x, int y, string symbol)
        {
            char mark = symbol[0];
            return CheckDirection(x, y, mark, 1, 0)
                || CheckDirection(x, y, mark, 0, 1)
                || CheckDirection(x, y, mark, 1, 1)
                || CheckDirection(x, y, mark, 1, -1);
        }

        bool CheckDirection(int x, int y, char mark, int dx, int dy)
        {
            int count = 1;
            count += Count(x, y, mark, dx, dy);
            count += Count(x, y, mark, -dx, -dy);

            return count >= Config.WinCount;
        }

        int Count(int x, int y, char mark, int dx, int dy)
        {
            int c = 0;
            int i = x + dx;
            int j = y + dy;

            while (i >= 0 && i < Config.BoardSize &&
                   j >= 0 && j < Config.BoardSize &&
                   Board[i, j] == mark)
            {
                c++;
                i += dx;
                j += dy;
            }
            return c;
        }

        public void Stop()
        {
            TurnTimer?.Stop();
            TurnTimer?.Dispose();
        }
    }
}
