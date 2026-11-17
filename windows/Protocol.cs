namespace G5Link;

// Port of KeyLinker 3.73 util.e.g/h/k/L; no configuration commands.
internal static class Protocol
{
    static readonly byte[] PayloadMask = [51,99,157,121,242,219,162,26,170,33,139,232,116,211,88];
    static readonly byte[] FrameMask = [161,82,213,163,245,137,246,143,240,157,72,147,234,52,49,186,195,77,198,235,73,96,216,163,218,42,83,141,244,97,24,191,174,215,111,81,228,160,217,146];
    static readonly byte[] CrcTable = [0, 121, 242, 139, 51, 74, 193, 184, 102, 31, 148, 237, 85, 44, 167, 222, 204, 181, 62, 71, 255, 134, 13, 116, 170, 211, 88, 33, 153, 224, 107, 18, 79, 54, 189, 196, 124, 5, 142, 247, 41, 80, 219, 162, 26, 99, 232, 145, 131, 250, 113, 8, 176, 201, 66, 59, 229, 156, 23, 110, 214, 175, 36, 93, 158, 231, 108, 21, 173, 212, 95, 38, 248, 129, 10, 115, 203, 178, 57, 64, 82, 43, 160, 217, 97, 24, 147, 234, 52, 77, 198, 191, 7, 126, 245, 140, 209, 168, 35, 90, 226, 155, 16, 105, 183, 206, 69, 60, 132, 253, 118, 15, 29, 100, 239, 150, 46, 87, 220, 165, 123, 2, 137, 240, 72, 49, 186, 195, 235, 146, 25, 96, 216, 161, 42, 83, 141, 244, 127, 6, 190, 199, 76, 53, 39, 94, 213, 172, 20, 109, 230, 159, 65, 56, 179, 202, 114, 11, 128, 249, 164, 221, 86, 47, 151, 238, 101, 28, 194, 187, 48, 73, 241, 136, 3, 122, 104, 17, 154, 227, 91, 34, 169, 208, 14, 119, 252, 133, 61, 68, 207, 182, 117, 12, 135, 254, 70, 63, 180, 205, 19, 106, 225, 152, 32, 89, 210, 171, 185, 192, 75, 50, 138, 243, 120, 1, 223, 166, 45, 84, 236, 149, 30, 103, 58, 67, 200, 177, 9, 112, 251, 130, 92, 37, 174, 215, 111, 22, 157, 228, 246, 143, 4, 125, 197, 188, 55, 78, 144, 233, 98, 27, 163, 218, 81, 40];
    static int sequence;

    public static byte Checksum(ReadOnlySpan<byte> bytes)
    {
        byte crc = 0;
        foreach (byte value in bytes)
        {
            crc = CrcTable[crc ^ value];
        }
        return crc;
    }
    public static byte[] BuildIdentity()
        => Build(0x91, []);

    public static byte[] Build(byte command, byte[] payload)
    {
        if (payload.Length > 15) throw new ArgumentException("Payload máximo: 15 bytes.");
        int n = Interlocked.Increment(ref sequence);
        var packet = new byte[payload.Length + 5];
        packet[0] = command; packet[1] = (byte)packet.Length;
        packet[2] = (byte)((n % 128) + (n % 2 == 0 ? 128 : 0));
        packet[3] = (byte)Random.Shared.Next(256); payload.CopyTo(packet,4);
        packet[^1] = Checksum(packet.AsSpan(0,packet.Length-1));
        return packet;
    }
    static int chunkSequence, chunkCountdown = 8;
    public static byte[] BuildLightingChunk(byte[] payload, int index)
        => BuildChunk(0x37,payload,index);
    public static byte[] BuildChunk(byte command, byte[] payload, int index)
    {
        if (index < 0 || index > 15 || payload.Length < 1 || payload.Length > 15) throw new ArgumentException("Fragmento inválido.");
        var packet = Build(command, payload);
        if (chunkCountdown == 0) chunkCountdown = 8;
        packet[1] = (byte)((--chunkCountdown % 8) << 5 | packet.Length);
        int n = ++chunkSequence;
        packet[2] = (byte)((n % 2) << 7 | index << 3 | n % 8);
        packet[^1] = Checksum(packet.AsSpan(0,packet.Length-1));
        return packet;
    }
    public static byte[] BuildMacroChunk(byte[] payload,int index,int slot)
    {
        if(slot<0||slot>7)throw new ArgumentOutOfRangeException(nameof(slot));
        var packet=BuildChunk(0x35,payload,index);
        packet[1]=(byte)((slot<<5)|packet.Length); // util.e.m0 uses r(slot,payload), not p(payload).
        packet[^1]=Checksum(packet.AsSpan(0,packet.Length-1));return packet;
    }
    static void Validate(byte[] packet)
    {
        if (packet.Length < 5 || packet.Length > 20) throw new ArgumentException("Comprimento de pacote esperado: 5 a 20 bytes.");
    }
    public static byte[] Encode(byte[] packet)
    {
        Validate(packet);
        var result = new byte[packet.Length];
        int sum = packet[2] + packet[3];
        result[0] = unchecked((byte)(packet[0] ^ (sum - 154)));
        result[1] = unchecked((byte)(packet[1] ^ (sum + 155)));
        result[2] = unchecked((byte)((packet[2] - 173) ^ packet[3]));
        result[3] = unchecked((byte)((packet[3] + 191) ^ 219));
        for (int i=4; i<packet.Length-1; i++) result[i] = unchecked((byte)(packet[i] ^ (sum - PayloadMask[i-4])));
        result[^1] = packet[^1];
        int offset = (result[3] & 2) * 10;
        for (int i=0; i<result.Length; i++) result[i] ^= FrameMask[offset+i];
        return result;
    }
    public static byte[] Decode(byte[] packet)
    {
        Validate(packet);
        var result = new byte[packet.Length];
        int offset = ((packet[3] ^ FrameMask[3]) & 2) != 0 ? 20 : 0;
        for (int i=0; i<packet.Length; i++) result[i] = (byte)(packet[i] ^ FrameMask[offset+i]);
        result[3] = unchecked((byte)((result[3] ^ 219) - 191));
        result[2] = unchecked((byte)((result[2] ^ result[3]) + 173));
        int sum = result[2] + result[3];
        result[1] = unchecked((byte)(result[1] ^ (sum + 155)));
        result[0] = unchecked((byte)(result[0] ^ (sum - 154)));
        for (int i=4; i<result.Length-1; i++) result[i] = unchecked((byte)(result[i] ^ (sum - PayloadMask[i-4])));
        return result;
    }
}
