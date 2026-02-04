using System.Net.Sockets;

namespace Server
{
    public class Player
    {
        public TcpClient Client { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string IconType { get; set; } = "default";
        public string Color { get; set; } = "#000000";
        public Room CurrentRoom { get; set; }

        public Player(TcpClient client, string name)
        {
            Client = client;
            Name = name;
        }

        public void Send(string json)
        {
            try
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                Client.GetStream().Write(buffer, 0, buffer.Length);
            }
            catch
            {
                // Client disconnected
            }
        }
    }
}
