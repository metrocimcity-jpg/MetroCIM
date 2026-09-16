using Autodesk.Navisworks.Api;

namespace MetroCIM.Navisworks.Services;

public sealed class VisibilityService
{
    public IReadOnlyList<ModelItem> GetVisibleGeometryItems(
        Document document,
        Action<int>? heartbeat = null)
    {
        var results = new List<ModelItem>();
        int scanned = 0;

        foreach (ModelItem root in document.Models.RootItems)
        {
            foreach (ModelItem item in root.DescendantsAndSelf)
            {
                scanned++;
                if ((scanned & 127) == 0)
                    heartbeat?.Invoke(scanned);

                if (!IsExportable(item))
                    continue;

                results.Add(item);
            }
        }

        return results;
    }

    private static bool IsExportable(ModelItem item)
    {
        try
        {
            if (item.IsHidden)
                return false;
            if (item.Geometry is null)
                return false;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
