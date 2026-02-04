using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

class Program
{
    static TcpClient client;
    static NetworkStream stream;

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        try
        {
            client = new TcpClient();
            client.Connect("127.0.0.1", 8386);

            Console.WriteLine("Connected to server!");

            stream = client.GetStream();

            // Thread nhận dữ liệu từ server
            Thread receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // ====== Gửi JOIN ======
            var joinMsg = new
            {
                type = "join",
                data = new
                {
                    playerName = "Player_" + new Random().Next(1000)
                }
            };

            Send(joinMsg);

            // ====== Menu test ======
            while (true)
            {
                Console.WriteLine("\nCommands:");
                Console.WriteLine("1 -> Move");
                Console.WriteLine("2 -> Change Icon");
                Console.WriteLine("3 -> Change Color");
                Console.WriteLine("q -> Quit");

                string cmd = Console.ReadLine();

                if (cmd == "1")
                {
                    Random rd = new Random();

                    var move = new
                    {
                        type = "move",
                        data = new
                        {
                            x = rd.Next(0, 15),
                            y = rd.Next(0, 15)
                        }
                    };

                    Send(move);
                }
                else if (cmd == "2")
                {
                    var icon = new
                    {
                        type = "icon",
                        data = new { iconType = "star" }
                    };

                    Send(icon);
                }
                else if (cmd == "3")
                {
                    var color = new
                    {
                        type = "color",
                        data = new { color = "#FF0000" }
                    };

                    Send(color);
                }
                else if (cmd == "q")
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    static void Send(object obj)
    {
        string json = JsonConvert.SerializeObject(obj);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        stream.Write(buffer, 0, buffer.Length);
        Console.WriteLine("Sent: " + json);
    }

    static void ReceiveData()
    {
        byte[] buffer = new byte[2048];

        try
        {
            while (true)
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break;

                string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine("Server: " + msg);
            }
        }
        catch
        {
            Console.WriteLine("Disconnected from server.");
        }
    }
}
