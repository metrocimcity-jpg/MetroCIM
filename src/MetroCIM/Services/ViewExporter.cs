using Autodesk.Revit.DB;
using MetroCIM.Models;

namespace MetroCIM.Services;

public sealed class ViewExporter
{
    private readonly VisibilityService _visibility = new();
    private readonly ColorResolver _colors = new();
    private readonly GeometryBuilder _geometry = new();

    public int ExportColorBatched(
        Document document,
        View3D view,
        string path,
        Action<int, int, string>? progress = null,
        List<ExportedElement>? exportedElements = null)
    {
        IReadOnlyList<Element> visible = _visibility.GetVisibleElements(
            document,
            view,
            scanned => progress?.Invoke(scanned, Math.Max(scanned, 1), "Collecting visible elements"));
        HashSet<ElementId> skipHosts = HostsCoveredByInsulation(visible);
        _colors.Bind(view);
        _geometry.ClearCache();

        var writer = new GltfSceneAccumulator();
        int exported = 0;
        int index = 0;

        foreach (Element element in visible)
        {
            index++;
            progress?.Invoke(index, visible.Count, "Reading geometry");
            if (skipHosts.Contains(element.Id))
                continue;

            TriangleMesh? mesh = _geometry.BuildMesh(element, view);
            if (mesh is null)
                continue;

            writer.AddBatched(_colors.Resolve(element, view), mesh);
            if (exportedElements is not null)
            {
                string id = string.IsNullOrWhiteSpace(element.UniqueId)
                    ? $"e-{element.Id.Value}"
                    : element.UniqueId;
                exportedElements.Add(new ExportedElement(id, element));
            }
            exported++;
        }

        progress?.Invoke(visible.Count, visible.Count, "Writing glTF");
        writer.Save(path);
        ReleaseGeometryMemory();
        return exported;
    }

    public int ExportPerElement(
        Document document,
        View3D view,
        string path,
        List<ExportedElement> exportedElements,
        Action<int, int, string>? progress = null)
    {
        IReadOnlyList<Element> visible = _visibility.GetVisibleElements(
            document,
            view,
            scanned => progress?.Invoke(scanned, Math.Max(scanned, 1), "Collecting visible elements"));
        HashSet<ElementId> skipHosts = HostsCoveredByInsulation(visible);
        _colors.Bind(view);
        _geometry.ClearCache();

        var writer = new GltfSceneAccumulator();
        int index = 0;

        foreach (Element element in visible)
        {
            index++;
            progress?.Invoke(index, visible.Count, "Reading geometry");
            if (skipHosts.Contains(element.Id))
                continue;

            TriangleMesh? mesh = _geometry.BuildMesh(element, view);
            if (mesh is null)
                continue;

            string id = string.IsNullOrWhiteSpace(element.UniqueId)
                ? $"e-{element.Id.Value}"
                : element.UniqueId;

            writer.Add(id, _colors.Resolve(element, view), mesh);
            exportedElements.Add(new ExportedElement(id, element));
        }

        progress?.Invoke(visible.Count, visible.Count, "Writing glTF");
        writer.Save(path);
        ReleaseGeometryMemory();
        return exportedElements.Count;
    }

    private static void ReleaseGeometryMemory()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
    }

    private static HashSet<ElementId> HostsCoveredByInsulation(IReadOnlyList<Element> visible)
    {
        var hosts = new HashSet<ElementId>();
        foreach (Element element in visible)
        {
            if (element is InsulationLiningBase insulation &&
                insulation.HostElementId != ElementId.InvalidElementId)
            {
                hosts.Add(insulation.HostElementId);
            }
        }

        return hosts;
    }
}
