using RoBotClient.Bot.Protocol;

namespace RoBotClient.Tests;

/// <summary>
/// Validates the non-bit-packed login handshake byte-for-byte, in particular the 7-bit-encoded
/// (LEB128) string length prefix that .NET's BinaryWriter/BinaryReader use — different from the
/// ushort length prefix the bit-packed in-game strings use.
/// </summary>
public class LoginHandshakeTests
{
    [Fact]
    public void BuildLogin_MatchesBinaryWriterFraming()
    {
        var bytes = LoginHandshake.BuildLogin(serverVersion: 1, username: "bot", password: "pw", createNewAccount: true);
        var expected = new byte[]
        {
            0x01, 0x00,             // int16 version = 1 (little-endian)
            0x01,                   // bool isNewAccount = true
            0x00,                   // bool isTokenLogin = false
            0x00,                   // bool requestLoginToken = false
            0x03, 0x62, 0x6F, 0x74, // string "bot": 7-bit length 3, then UTF-8 b/o/t
            0x02, 0x70, 0x77        // string "pw":  7-bit length 2, then UTF-8 p/w
        };
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void BuildLogin_LongString_Uses2ByteVarintLength()
    {
        // 200 chars forces a 2-byte LEB128 length: 200 -> 0xC8 0x01.
        var name = new string('a', 200);
        var bytes = LoginHandshake.BuildLogin(1, name, "", createNewAccount: false);
        Assert.Equal(0xC8, bytes[5]); // version(2) + 3 bools(3) = offset 5
        Assert.Equal(0x01, bytes[6]);
        // 5 header + 2 length + 200 name + 1 empty-password-length
        Assert.Equal(5 + 2 + 200 + 1, bytes.Length);
    }
}
