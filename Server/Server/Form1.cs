using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Server
{
    public partial class Form1 : Form
    {
        TcpListener server;
        List<TcpClient> clients = new List<TcpClient>();
        List<Room> rooms = new List<Room>();
        Dictionary<TcpClient, Player> clientToPlayer = new Dictionary<TcpClient, Player>();
        int roomIndex = 1;
        object lockObj = new object();

        GameConfig defaultConfig = new GameConfig(15, 5, 30);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            server = new TcpListener(IPAddress.Any, 8386);
            server.Start();
            Log($"Server started on port 8386");
            Log($"Config mặc định: {defaultConfig}");

            Thread listenThread = new Thread(ListenClient);
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        void ListenClient()
        {
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();

                    lock (lockObj)
                    {
                        clients.Add(client);
                    }

                    Log("Client connected");

                    Thread t = new Thread(() => ReceiveData(client));
                    t.IsBackground = true;
                    t.Start();
                }
                catch (Exception ex)
                {
                    Log($"Error accepting client: {ex.Message}");
                }
            }
        }

        void ReceiveData(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[2048];

            try
            {
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log("Received: " + json);

                    dynamic msg = JsonConvert.DeserializeObject(json);
                    string type = msg.type;

                    switch (type)
                    {
                        case "join":
                            HandleJoin(client, msg.data);
                            break;

                        case "config":
                            HandleConfig(client, msg.data);
                            break;

                        case "move":
                            HandleMove(client, msg.data);
                            break;

                        case "icon":
                            HandleIcon(client, msg.data);
                            break;

                        case "color":
                            HandleColor(client, msg.data);
                            break;

                        default:
                            SendError(client, "Unknown type");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Client error: {ex.Message}");
            }
            finally
            {
                HandleDisconnect(client);
            }
        }

        // -------- JOIN --------
        void HandleJoin(TcpClient client, dynamic data)
        {
            string name = data.playerName;
            Player player = new Player(client, name);

            lock (lockObj)
            {
                clientToPlayer[client] = player;
            }

            Room room = FindOrCreateRoom();
            room.AddPlayer(player);

            Log($"{name} joined {room.RoomId}");

            if (room.Players.Count == 1)
            {
                player.Symbol = "X";
                SendAssign(player, "X", room.Config);
                SendWaiting(player);
            }
            else if (room.Players.Count == 2)
            {
                room.Players[0].Symbol = "X";
                room.Players[1].Symbol = "O";
                room.CurrentTurn = room.Players[0];
                room.TimeLeft = room.Config.TurnTime;

                SendAssign(room.Players[0], "X", room.Config);
                SendAssign(room.Players[1], "O", room.Config);

                SendGameStart(room);
                room.TurnTimer.Start();

                Log($"{room.RoomId} is ready ({room.Config})");
            }
        }

        void HandleConfig(TcpClient client, dynamic data)
        {
            int boardSize = data.boardSize ?? 15;
            int winCount = data.winCount ?? 5;
            int turnTime = data.turnTime ?? 30;

            Log($"Client requested config: {boardSize}x{boardSize}, {winCount} quân, {turnTime}s");
        }

        void HandleIcon(TcpClient client, dynamic data)
        {
            lock (lockObj)
            {
                if (clientToPlayer.ContainsKey(client))
                {
                    string iconType = data.iconType;
                    clientToPlayer[client].IconType = iconType;
                    Log($"{clientToPlayer[client].Name} chọn icon: {iconType}");
                }
            }
        }

        void HandleColor(TcpClient client, dynamic data)
        {
            lock (lockObj)
            {
                if (clientToPlayer.ContainsKey(client))
                {
                    string color = data.color;
                    clientToPlayer[client].Color = color;
                    Log($"{clientToPlayer[client].Name} chọn màu: {color}");
                }
            }
        }

        // -------- SEND --------
        void SendWaiting(Player p)
        {
            var obj = new
            {
                type = "waiting",
                data = new { message = "Đang chờ đối thủ..." }
            };

            p.Send(JsonConvert.SerializeObject(obj));
        }

        void SendGameStart(Room room)
        {
            var obj = new
            {
                type = "gameStart",
                data = new
                {
                    player1 = room.Players[0].Name,
                    player2 = room.Players[1].Name,
                    currentTurn = room.CurrentTurn.Name
                }
            };

            string json = JsonConvert.SerializeObject(obj);

            foreach (var p in room.Players)
                p.Send(json);
        }

        void SendAssign(Player p, string symbol, GameConfig config)
        {
            var obj = new
            {
                type = "assign",
                data = new
                {
                    symbol,
                    boardSize = config.BoardSize,
                    winCount = config.WinCount,
                    turnTime = config.TurnTime
                }
            };

            p.Send(JsonConvert.SerializeObject(obj));
        }

        // -------- MOVE --------
        void HandleMove(TcpClient client, dynamic data)
        {
            int x = data.x;
            int y = data.y;

            Room room = FindRoomByClient(client);
            if (room == null)
            {
                SendError(client, "Room not found");
                return;
            }

            Player player;
            lock (lockObj)
            {
                if (!clientToPlayer.ContainsKey(client))
                {
                    SendError(client, "Player not found");
                    return;
                }
                player = clientToPlayer[client];
            }

            if (room.MakeMove(player, x, y))
            {
                BroadcastMove(room, x, y, player.Symbol);

                if (room.CheckWin(x, y, player.Symbol))
                {
                    BroadcastWin(room, player);
                    CloseRoom(room);
                }
            }
            else
            {
                SendError(client, "Invalid move");
            }
        }

        void BroadcastMove(Room room, int x, int y, string symbol)
        {
            var obj = new
            {
                type = "move",
                data = new { x, y, symbol }
            };

            string json = JsonConvert.SerializeObject(obj);

            foreach (var p in room.Players)
                p.Send(json);
        }

        void BroadcastWin(Room room, Player winner)
        {
            var obj = new
            {
                type = "win",
                data = new
                {
                    winner = winner.Name,
                    symbol = winner.Symbol
                }
            };

            string json = JsonConvert.SerializeObject(obj);

            foreach (var p in room.Players)
                p.Send(json);

            Log($"{winner.Name} WIN in {room.RoomId}");
        }

        // -------- DISCONNECT --------
        void HandleDisconnect(TcpClient client)
        {
            lock (lockObj)
            {
                clients.Remove(client);

                if (clientToPlayer.ContainsKey(client))
                {
                    Player player = clientToPlayer[client];
                    Room room = player.CurrentRoom;

                    if (room != null)
                    {
                        Log($"{player.Name} disconnected from {room.RoomId}");

                        Player opponent = room.Players.Find(p => p != player);
                        if (opponent != null)
                            SendOpponentLeft(opponent);

                        CloseRoom(room);
                    }

                    clientToPlayer.Remove(client);
                }
            }

            client.Close();
            Log("Client disconnected");
        }

        void SendOpponentLeft(Player p)
        {
            var obj = new
            {
                type = "opponentLeft",
                data = new { message = "Đối thủ đã thoát" }
            };

            p.Send(JsonConvert.SerializeObject(obj));
        }

        void CloseRoom(Room room)
        {
            lock (lockObj)
            {
                room.Stop();
                rooms.Remove(room);
                Log($"Closed {room.RoomId}");
            }
        }

        Room FindRoomByClient(TcpClient client)
        {
            lock (lockObj)
            {
                return clientToPlayer.ContainsKey(client)
                    ? clientToPlayer[client].CurrentRoom
                    : null;
            }
        }

        void SendError(TcpClient client, string msg)
        {
            var obj = new
            {
                type = "error",
                data = new { message = msg }
            };

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
                client.GetStream().Write(buffer, 0, buffer.Length);
            }
            catch { }
        }

        void Log(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), msg);
                return;
            }

            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        Room FindOrCreateRoom()
        {
            lock (lockObj)
            {
                foreach (var r in rooms)
                    if (!r.IsFull()) return r;

                Room newRoom = new Room(
                    "Room " + roomIndex++,
                    defaultConfig,
                    (room, log) => Log($"[{room.RoomId}] {log}"),
                    (room, player) => BroadcastTimeout(room, player)
                );

                rooms.Add(newRoom);
                Log($"Created {newRoom.RoomId} with config: {defaultConfig}");
                return newRoom;
            }
        }

        void BroadcastTimeout(Room room, Player timeoutPlayer)
        {
            var obj = new
            {
                type = "timeout",
                data = new
                {
                    timeoutPlayer = timeoutPlayer.Name,
                    nextPlayer = room.CurrentTurn.Name
                }
            };

            string json = JsonConvert.SerializeObject(obj);

            foreach (var p in room.Players)
                p.Send(json);

            Log($"{room.RoomId}: {timeoutPlayer.Name} timeout");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            lock (lockObj)
            {
                foreach (var room in rooms)
                    room.Stop();

                server?.Stop();
            }

            base.OnFormClosing(e);
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
