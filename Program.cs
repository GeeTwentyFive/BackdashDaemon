using ENet;
using Backdash;
using System.Net.Security;
using System.Runtime.InteropServices;


public static class BackdashDaemon
{
        const int LOCAL_PLAYER_ID = 0;
        const int MAX_PLAYERS = 4;


        public enum PacketType
        {
                TICK = 0,
                // OUT = synchronize inputs -> tick request
                // IN = backdash advance frame
                LOAD_GAME = 1,
                // OUT = load game state request + data
                // IN = ^ confirmation
                SAVE_GAME = 2,
                // OUT = save game request
                // IN = (^ response) game state data
                LOCAL_INPUT = 3,
                // IN = local player input data
                SYNC_INPUTS = 4
                // OUT = synchronized input data (for *all* players)
        }


        static int player_count = 0;
        static string[] remote_player_ips = new string[MAX_PLAYERS];
        static byte[][] player_inputs = new byte[MAX_PLAYERS][];
        static List<byte> player_inputs_packet_data = new List<byte>();

        static Host server = new Host();
        static Packet tick_request_packet = default(Packet);
        static Packet save_game_request_packet = default(Packet);

        public static void HandleReceive(byte[] data)
        {
                switch(data[0])
                {
                        case (byte)PacketType.LOCAL_INPUT:
                                player_inputs[LOCAL_PLAYER_ID] = data;
                                break;

                        case (byte)PacketType.TICK:
                                // TODO: Advance frame
                                break;
                }
        }

        public static int Main(string[] args)
        {
                if (args.Length < 3)
                {
                        Console.WriteLine("USAGE: BackdashDaemon <PORT> <REMOTE_PLAYER_2_IP> [REMOTE_PLAYER_3_IP] [REMOTE_PLAYER_4_IP]");
                        return 1;
                }

                ushort port;
                if (!ushort.TryParse(args[1], out port))
                {
                        Console.WriteLine($"ERROR: Provided port {args[1]} is invalid");
                        return 1;
                }

                player_count = args.Length - 2 + 1; // the "+ 1" is local player
                if (player_count > MAX_PLAYERS)
                {
                        Console.WriteLine($"ERROR: Number of players exceeds max {MAX_PLAYERS}");
                        return 1;
                }

                for (int i = 2; i < player_count; i++)
                {
                        remote_player_ips.Append(args[i]);
                }

                player_inputs_packet_data[0] = (byte)PacketType.SYNC_INPUTS;

                tick_request_packet.Create(new byte[] { (byte)PacketType.TICK });
                save_game_request_packet.Create(new byte[] { (byte)PacketType.SAVE_GAME });

                Address address = new Address();
                address.Port = (ushort)(port+1);
                server.Create(address, 1);

                Event netEvent;
                while (true)
                {
                        while (server.Service(0, out netEvent) > 0)
                        {
                                switch (netEvent.Type)
                                {
                                        case EventType.Connect:
                                                // TODO: Start Backdash connection
                                                break;

                                        case EventType.Receive:
                                                byte[] packet_data = new byte[netEvent.Packet.Length];
                                                netEvent.Packet.CopyTo(packet_data);
                                                HandleReceive(packet_data);
                                                break;

                                        case EventType.Disconnect:
                                                return 0;
                                }
                        }

                        server.Flush();
                }
        }
}