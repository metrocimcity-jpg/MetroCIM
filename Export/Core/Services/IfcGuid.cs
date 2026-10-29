namespace MetroCIM.Services;

public static class IfcGuid
{
    private const string ConversionTable = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    public static string NewIfcGlobalId()
    {
        char[] chars = From(Guid.NewGuid()).ToCharArray();
        if (chars[0] is < '0' or > '3')
            chars[0] = '2';
        return new string(chars);
    }

    public static string From(Guid guid)
    {
        byte[] bytes = guid.ToByteArray();
        ulong[] num =
        [
            bytes[3],
            bytes[2] * 65536UL + bytes[1] * 256UL + bytes[0],
            bytes[5] * 65536UL + bytes[4] * 256UL + bytes[7],
            bytes[6] * 65536UL + bytes[8] * 256UL + bytes[9],
            bytes[10] * 65536UL + bytes[11] * 256UL + bytes[12],
            bytes[13] * 65536UL + bytes[14] * 256UL + bytes[15]
        ];

        char[] buffer = new char[22];
        int offset = 0;
        for (int i = 0; i < 6; i++)
        {
            int length = i == 0 ? 2 : 4;
            for (int j = 0; j < length; j++)
            {
                buffer[offset + length - j - 1] = ConversionTable[(int)(num[i] % 64)];
                num[i] /= 64;
            }

            offset += length;
        }

        return new string(buffer);
    }
}
