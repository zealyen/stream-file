public static class Crc16Modbus
{
    private const ushort Polynomial = 0xA001;
    private static readonly ushort[] Table = new ushort[256];

    static Crc16Modbus()
    {
        ushort value;
        ushort temp;
        for (ushort i = 0; i < Table.Length; ++i)
        {
            value = 0;
            temp = i;
            for (byte j = 0; j < 8; ++j)
            {
                if (((value ^ temp) & 0x0001) != 0)
                {
                    value = (ushort)((value >> 1) ^ Polynomial);
                }
                else
                {
                    value >>= 1;
                }
                temp >>= 1;
            }
            Table[i] = value;
        }
    }

    public static ushort ComputeChecksum(byte[] bytes, ushort previous)
    {
        ushort crc = previous;
        foreach (byte b in bytes)
        {
            crc = (ushort)((crc >> 8) ^ Table[(crc ^ b) & 0xFF]);
        }
        return crc;
    }
}
