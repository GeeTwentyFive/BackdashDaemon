using ENet;
using Backdash;
using System.Net.Security;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using System.Net;
using Backdash.Serialization;
using Backdash.Data;
using System.Buffers.Binary;


public class BackdashSessionHandler : INetcodeSessionHandler
{
        public void AdvanceFrame()
        {
                if (BackdashDaemon.SynchronizeInputs() != 0)
                {
                        Console.WriteLine("ERROR: Failed to synchronize inputs");
                        Environment.Exit(1);
                }

                Packet tick_request_packet = default(Packet);
                tick_request_packet.Create(
                        new byte[] { (byte)BackdashDaemon.PacketType.TICK },
                        PacketFlags.Reliable
                );
                BackdashDaemon.server.Broadcast(0, ref tick_request_packet);
        }

        public void LoadState(in Frame frame, ref readonly BinaryBufferReader reader)
        {
                byte[] game_state_data = reader.Buffer.ToArray();
                byte[] load_game_request_packet_data = new byte[1 + game_state_data.Length];
                load_game_request_packet_data[0] = (byte)BackdashDaemon.PacketType.LOAD_GAME;
                Array.Copy(game_state_data, 0, load_game_request_packet_data, 1, game_state_data.Length);
                Packet load_game_request_packet = default(Packet);
                load_game_request_packet.Create(
                        load_game_request_packet_data,
                        PacketFlags.Reliable
                );
                BackdashDaemon.server.Broadcast(0, ref load_game_request_packet);
        }

        public void SaveState(in Frame frame, ref readonly BinaryBufferWriter writer)
        {
                // Send request for game state
                Packet save_game_request_packet = default(Packet);
                save_game_request_packet.Create(
                        new byte[] { (byte)BackdashDaemon.PacketType.SAVE_GAME },
                        PacketFlags.Reliable
                );
                BackdashDaemon.server.Broadcast(0, ref save_game_request_packet);

                // Wait for receive (partial copy of main loop)
                Event netEvent;
                while (BackdashDaemon.server.Service(0, out netEvent) > 0)
                {
                        switch (netEvent.Type)
                        {
                                case EventType.Receive:
                                        byte[] packet_data = new byte[netEvent.Packet.Length];
                                        netEvent.Packet.CopyTo(packet_data);
                                        if (packet_data[0] == (byte)BackdashDaemon.PacketType.SAVE_GAME)
                                        {
                                                writer.Write(packet_data);
                                                return;
                                        }
                                        else BackdashDaemon.HandleReceive(packet_data);
                                        break;

                                case EventType.Disconnect:
                                        BackdashDaemon.server.Dispose();
                                        Environment.Exit(0);
                                        break;
                        }
                }

                if (BackdashDaemon.SynchronizeInputs() != 0)
                {
                        Console.WriteLine("ERROR: Failed to synchronize inputs");
                        Environment.Exit(1);
                }

                BackdashDaemon.server.Flush();
        }

        public void TimeSync(FrameSpan framesAhead) {}
        public void OnSessionStart() {}
        public void OnSessionClose() {}
        public void OnPeerEvent(NetcodePlayer player, PeerEventInfo evt) { }
}

public static class BackdashDaemon
{
        const int MAX_PLAYERS = 4;


        public enum PacketType
        {
                TICK = 0,
                // OUT = synchronize inputs -> tick request
                // IN = backdash advance frame

                LOAD_GAME = 1,
                // OUT = load game state request + data

                SAVE_GAME = 2,
                // OUT = save game request
                // IN = (^ response) game state data

                LOCAL_INPUT = 3,
                // IN = uint32 local player input data

                SYNC_INPUTS = 4,
                // OUT = uint32[] synchronized input data (for *all* players)

                FRAME_BEGIN = 5
                // IN = backdash begin frame
        }


        static int player_count = 0;
        static IPAddress[] remote_player_ips = new IPAddress[MAX_PLAYERS];
        static byte[] player_inputs_packet_data = new byte[1 + sizeof(uint)*MAX_PLAYERS];

        public static Host server = new Host();

        public static INetcodeSession<uint>? session;
        static NetcodePlayer local_player = NetcodePlayer.CreateLocal();

        public static void HandleReceive(byte[] data)
        {
                switch (data[0])
                {
                        case (byte)PacketType.FRAME_BEGIN:
                                if (session == null) break;
                                session.BeginFrame();
                                break;

                        case (byte)PacketType.LOCAL_INPUT:
                                if (session == null) break;
                                session.AddLocalInput(local_player, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(1)));
                                break;

                        case (byte)PacketType.TICK:
                                if (session == null) break;
                                session.AdvanceFrame();
                                break;
                }
        }

        public static int SynchronizeInputs()
        {
                if (session == null) return 1;

                ResultCode result = session.SynchronizeInputs();
                if (result != ResultCode.Ok)
                {
                        Console.WriteLine($"ERROR: Failed to synchronize inputs, with error code {result}");
                        return 1;
                }

                for (int i = 1; i < player_count + 1; i++)
                {
                        BinaryPrimitives.WriteUInt32LittleEndian(
                                player_inputs_packet_data.AsSpan(1 + (i-1)*sizeof(uint)),
                                session.CurrentSynchronizedInputs[i-1].Input
                        );
                }
                Packet player_inputs_packet = default(Packet);
                player_inputs_packet.Create(
                        player_inputs_packet_data,
                        PacketFlags.Reliable
                );
                server.Broadcast(0, ref player_inputs_packet);

                return 0;
        }

        public static int Main(string[] args)
        {
                if (args.Length < 3)
                {
                        Console.WriteLine("USAGE: BackdashDaemon <PORT> <REMOTE_PLAYER_2_IPv6> [REMOTE_PLAYER_3_IPv6] [REMOTE_PLAYER_4_IPv6]");
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

                for (int i = 2; i < args.Length; i++)
                {
                        IPAddress? _address;
                        if (!IPAddress.TryParse(args[i], out _address))
                        {
                                Console.WriteLine($"ERROR: Remote player {i}'s IPv6 {args[i]} is invalid");
                                return 1;
                        }
                        remote_player_ips[i - 2] = _address;
                }

                player_inputs_packet_data[0] = (byte)PacketType.SYNC_INPUTS;

                Address address = new Address();
                address.Port = (ushort)(port + 1);
                server.Create(address, 1);

                session = RollbackNetcode
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
                if (session == null)
                {
                        Console.WriteLine("ERROR: Failed to create RollbackNetcode session");
                        return 1;
                }

                session.SetHandler(new BackdashSessionHandler());

                session.AddPlayer(local_player);
                for (int i = 1; i < player_count; i++)
                {
                        session.AddPlayer(NetcodePlayer.CreateRemote(remote_player_ips[i-1], port));
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
                                                server.Dispose();
                                                return 0;
                                }
                        }

                        if (SynchronizeInputs() != 0)
                        {
                                Console.WriteLine("ERROR: Failed to synchronize inputs");
                                return 1;
                        }

                        server.Flush();
                }
        }
}