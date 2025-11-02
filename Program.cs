using ENet;
using Backdash;
using System.Net.Security;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using System.Net;
using Backdash.Serialization;


public class BackdashSessionHandler : INetcodeSessionHandler
{
        public void AdvanceFrame()
        {
                // TODO: Send request ^
        }

        public void LoadState(in Frame frame, ref readonly BinaryBufferReader reader)
        {
                // TODO: Send request ^
        }

        public void SaveState(in Frame frame, ref readonly BinaryBufferWriter writer)
        {
                // TODO: Send request ^
        }

        public void TimeSync(FrameSpan framesAhead) {}
        public void OnSessionStart() {}
        public void OnSessionClose() {}
        public void OnPeerEvent(NetcodePlayer player, PeerEventInfo evt) { }
}

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
        static IPAddress[] remote_player_ips = new IPAddress[MAX_PLAYERS];
        static byte[][] player_inputs = new byte[MAX_PLAYERS][];
        static uint[] player_inputs_packet_data = new uint[MAX_PLAYERS];

        static Host server = new Host();
        static Packet tick_request_packet = default(Packet);
        static Packet save_game_request_packet = default(Packet);

        public static void HandleReceive(byte[] data)
        {
                switch (data[0])
                {
                        case (byte)PacketType.LOCAL_INPUT:
                                player_inputs[LOCAL_PLAYER_ID] = data[1..];
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
                        Console.WriteLine("USAGE: BackdashDaemon <PORT> <PLAYER_INPUT_STRUCTURE_SIZE> <REMOTE_PLAYER_2_IPv6> [REMOTE_PLAYER_3_IPv6] [REMOTE_PLAYER_4_IPv6]");
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
                        IPAddress? _address;
                        if (!IPAddress.TryParse(args[i], out _address))
                        {
                                Console.WriteLine($"ERROR: Remote player {i}'s IPv6 {args[i]} is invalid");
                                return 1;
                        }
                        remote_player_ips[i - 2] = _address;
                }

                player_inputs_packet_data[0] = (uint)PacketType.SYNC_INPUTS;

                tick_request_packet.Create(new byte[] { (byte)PacketType.TICK });
                save_game_request_packet.Create(new byte[] { (byte)PacketType.SAVE_GAME });

                Address address = new Address();
                address.Port = (ushort)(port + 1);
                server.Create(address, 1);

                var session = RollbackNetcode
                        .WithInputType(t => t.Integer<uint>())
                        .Configure(options =>
                        {
                                options.InputDelayFrames = 0;
                                options.InputQueueLength = 512;
                                options.LocalPort = port;
                                options.NumberOfPlayers = player_count;
                                options.RollbackFramesSmoothFactor = 0.0f;
                                options.UseIPv6 = true;
                        })
                        .Build();

                session.AddPlayer(NetcodePlayer.CreateLocal());
                for (int i = 1; i < player_count; i++)
                {
                        session.AddPlayer(NetcodePlayer.CreateRemote(remote_player_ips[i], port));
                }

                Event netEvent;
                while (true)
                {
                        while (server.Service(0, out netEvent) > 0)
                        {
                                switch (netEvent.Type)
                                {
                                        case EventType.Connect:
                                                session.Start();
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