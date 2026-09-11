namespace RevitXKT.Models;

public readonly record struct ResolvedAppearance(byte R, byte G, byte B, byte A)
{
    public static ResolvedAppearance Default { get; } = new(180, 180, 180, 255);

    public System.Numerics.Vector4 ToBaseColor() =>
        new(R / 255f, G / 255f, B / 255f, A / 255f);

    public static ResolvedAppearance FromRgb(byte r, byte g, byte b, int transparencyPercent = 0)
    {
        int t = Math.Clamp(transparencyPercent, 0, 100);
        byte a = (byte)Math.Clamp((int)Math.Round(255d * (100 - t) / 100d), 0, 255);
        return new ResolvedAppearance(r, g, b, a);
    }
}
