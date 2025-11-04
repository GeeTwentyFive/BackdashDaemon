BackdashDaemon implements the Backdash rollback netcode library as an IPC (Inter-Process Communication) daemon. This does have a downside of adding less than 1ms of latency, but provides the HUGE benefit of super simple integration into any engine, new application, and/or existing application in any language which provides networking capabilities (so like almost all of them).


# REQUIREMENTS
- 100% determinism (same inputs at same time = same outputs) (For physics: [JoltPhysics](https://github.com/jrouwe/JoltPhysics) supports this)
- Fully encapsulated and serializable game state
- Separate simulation from rendering


# USAGE
1) Implement the following in ENet:
- `PacketType.TICK` (0):
  - Input (bytes): [0] ^ packet type
  - Advance physics simulation by 1 tick
  - After every physics tick: Send a `PacketType.TICK`-type packet [0]
- `PacketType.LOAD_GAME` (1):
  - Input (bytes): [0] ^ packet type, [1-...] game state data
  - Set current game state to the received game state data
- `PacketType.SAVE_GAME` (2):
  - Input bytes(): [0] ^ packet type
  - Serialize current game state and reply with a `PacketType.SAVE_GAME`-type packet [0] with the game state data [1-...]
- `PacketType.LOCAL_INPUT` (3):
  - After every physics tick: Send a `PacketType.LOCAL_INPUT`-type packet [0] with the local player's input data as an uint32 [1-4]
- `PacketType.SYNC_INPUTS` (4):
  - Input (bytes): [0] ^ packet type, [1-4] your inputs (uint32), [5-...] remote players's inputs (uint32's)
- `PacketType.FRAME_BEGIN` (5):
  - At the beginning of every frame: Send a `PacketType.FRAME_BEGIN`-type packet [0]
2) Pre-gather a lobby of users
3) Call `BackdashDaemon <PORT> <REMOTE_PLAYER_2_IP> [REMOTE_PLAYER_3_IP] [REMOTE_PLAYER_4_IP]`