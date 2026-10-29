using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using MetroCIM.Models;
using System.Globalization;

namespace MetroCIM.Navisworks.Services;

public sealed class ColorResolver
{
    public ResolvedAppearance Resolve(ModelItem item)
    {
        if (TryGetFragmentAppearance(item, out ResolvedAppearance fragmentColor))
            return fragmentColor;

        if (TryGetPropertyColor(item, out ResolvedAppearance propertyColor))
            return propertyColor;

        return ResolvedAppearance.Default;
    }

    private static bool TryGetFragmentAppearance(ModelItem item, out ResolvedAppearance appearance)
    {
        appearance = default;
        try
        {
            InwOaPath? path = ComApiBridge.ToInwOaPath(item);
            if (path is null)
                return false;

            foreach (InwOaFragment3 fragment in path.Fragments())
            {
                object? raw = fragment.Appearance;
                if (raw is not Array array || array.Length < 6)
                    continue;

                // Appearance: Ambient RGB, Diffuse RGB, Specular RGB, Emissive RGB...
                byte r = ToByte(Convert.ToDouble(array.GetValue(3), CultureInfo.InvariantCulture));
                byte g = ToByte(Convert.ToDouble(array.GetValue(4), CultureInfo.InvariantCulture));
                byte b = ToByte(Convert.ToDouble(array.GetValue(5), CultureInfo.InvariantCulture));
                appearance = ResolvedAppearance.FromRgb(r, g, b);
                if (!appearance.IsNearBlack)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryGetPropertyColor(ModelItem item, out ResolvedAppearance appearance)
    {
        appearance = default;
        try
        {
            foreach (PropertyCategory category in item.PropertyCategories)
            {
                foreach (DataProperty property in category.Properties)
                {
                    string name = property.DisplayName ?? property.Name ?? string.Empty;
                    if (!LooksLikeColorProperty(name))
                        continue;

                    if (TryParseColorVariant(property.Value, out appearance) && !appearance.IsNearBlack)
                        return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool LooksLikeColorProperty(string name)
    {
        return name.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0
               || name.IndexOf("colour", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryParseColorVariant(VariantData value, out ResolvedAppearance appearance)
    {
        appearance = default;
        try
        {
            if (value.DataType == VariantDataType.DisplayString || value.DataType == VariantDataType.IdentifierString)
            {
                string? text = value.ToDisplayString();
                return TryParseRgbText(text, out appearance);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryParseRgbText(string? text, out ResolvedAppearance appearance)
    {
        appearance = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text!.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal) && text.Length >= 7)
        {
            if (byte.TryParse(text.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(text.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(text.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                appearance = ResolvedAppearance.FromRgb(r, g, b);
                return true;
            }
        }

        string[] parts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r2) &&
            byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g2) &&
            byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b2))
        {
            appearance = ResolvedAppearance.FromRgb(r2, g2, b2);
            return true;
        }

        return false;
    }

    private static byte ToByte(double channel)
    {
        double scaled = channel <= 1.0 ? channel * 255.0 : channel;
        return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(scaled)));
    }
}
