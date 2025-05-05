namespace Client.Models;

/// <summary>
/// 1. Player connects to server.
/// 2. Player IDENTIFIES self with server.
/// 3. Server sends existing players to the new player (IDENTIFY).
/// 4. Server sends the new player info to existing players (IDENTIFY).
/// --------
/// 5. On player move, send new POSITION to server.
/// 6. Server broadcasts player POSITION to all players.
/// --------
/// 7. On DISCONNECT, broadcast to other players.
/// </summary>
public enum ChannelType : byte
{
    Identify,
    Position,
    Disconnect
}
