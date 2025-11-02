using Backdash;
using ENet;


class BackdashDaemon
{
        const int LOCAL_PLAYER_ID = 0;

        public enum PacketType
        {
                PACKET_TYPE_TICK = 0,
                // OUT = synchronize inputs -> tick request
                // IN = backdash advance frame
                PACKET_TYPE_LOAD_GAME = 1,
                // OUT = load game state request + data
                // IN = ^ confirmation
                PACKET_TYPE_SAVE_GAME = 2,
                // OUT = save game request
                // IN = (^ response) game state data
                PACKET_TYPE_LOCAL_INPUT = 3,
                // IN = local player input data
                PACKET_TYPE_SYNC_INPUTS = 4
                // OUT = synchronized input data (for *all* players)
        }

        public static int Main(string[] args)
        {
                if (args.Length < 3)
                {
                        Console.WriteLine("USAGE: BackdashDaemon <PORT> <REMOTE_PLAYER_2_IP> <REMOTE_PLAYER_3_IP> ...");
                        return 1;
                }

                int port;
                if (!int.TryParse(args[1], out port))
                {
                        Console.WriteLine($"ERROR: Provided port {args[1]} is invalid");
                        return 1;
                }

                int player_count = args.Length-2 + 1; // the "+ 1" is local player

                //

                return 0;
        }
}