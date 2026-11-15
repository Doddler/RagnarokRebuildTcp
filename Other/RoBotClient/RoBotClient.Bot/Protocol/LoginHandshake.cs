using System.Text;

namespace RoBotClient.Bot.Protocol;

/// <summary>
/// The one packet that is NOT bit-packed: the initial login/version handshake sent on socket open.
/// The server reads it with a .NET <see cref="BinaryReader"/>, so we must match
/// <see cref="BinaryWriter"/>'s framing exactly — little-endian primitives, bool as one byte, and
/// strings as a 7-bit-encoded (LEB128) length prefix followed by UTF-8 bytes. After the server
/// replies with ConnectionApproved, all subsequent traffic is bit-packed (see <see cref="PacketWriter"/>).
/// </summary>
public static class LoginHandshake
{
    /// <param name="serverVersion">Must equal the server's version (from ServerVersion.txt) or the
    /// connection is denied.</param>
    /// <param name="createNewAccount">When true the server creates the account inline, then logs in.</param>
    public static byte[] BuildLogin(short serverVersion, string username, string password, bool createNewAccount)
    {
        using var ms = new MemoryStream();
        // Default BinaryWriter encoding is UTF-8 without a BOM, matching the Unity client and the
        // server-side BinaryReader. leaveOpen so we can read the buffer back after the writer is disposed.
        using (var bw = new BinaryWriter(ms, new UTF8Encoding(false), leaveOpen: true))
        {
            bw.Write(serverVersion);     // int16
            bw.Write(createNewAccount);  // bool isNewAccount
            bw.Write(false);             // bool isTokenLogin (the bot uses password login)
            bw.Write(false);             // bool requestLoginToken
            bw.Write(username ?? string.Empty);
            bw.Write(password ?? string.Empty);
        }
        return ms.ToArray();
    }
}
