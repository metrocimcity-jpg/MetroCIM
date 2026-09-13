using Autodesk.Revit.DB;
using MetroCIM.Models;
using RevitColor = Autodesk.Revit.DB.Color;

namespace MetroCIM.Services;

/// <summary>
/// Resolves display color from <see cref="PipingSystemType"/> / <see cref="MEPSystemType"/> only.
/// Never reads pipe segment, pipe type, or <c>RBS_PIPE_MATERIAL_PARAM</c> materials.
/// </summary>
public sealed class MepSystemTypeColor
{
    private static readonly BuiltInParameter[] SystemTypeParameters =
    [
        BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM,
        BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM
    ];

    private static readonly BuiltInParameter[] PhysicalMaterialParameters =
    [
        BuiltInParameter.RBS_PIPE_MATERIAL_PARAM,
        BuiltInParameter.MATERIAL_ID_PARAM
    ];

    private readonly Dictionary<ElementId, ResolvedAppearance?> _bySystemType = [];
    private readonly Dictionary<ElementId, ElementId?> _systemTypeByElement = [];

    public static bool IsPhysicalMaterialParameter(ElementId parameterId)
    {
        if (parameterId == ElementId.InvalidElementId)
            return false;

        long value = parameterId.Value;
        foreach (BuiltInParameter builtIn in PhysicalMaterialParameters)
        {
            if (value == (long)builtIn)
                return true;
        }

        return false;
    }

    public bool TryGet(Element element, out ResolvedAppearance appearance)
    {
        appearance = default;
        MEPSystemType? systemType = FindSystemType(element);
        if (systemType is null)
            return false;

        if (_bySystemType.TryGetValue(systemType.Id, out ResolvedAppearance? cached))
        {
            if (cached is null)
                return false;
            appearance = cached.Value;
            return true;
        }

        bool found = TryFromSystemType(systemType, out appearance);
        _bySystemType[systemType.Id] = found ? appearance : null;
        return found;
    }

    public MEPSystemType? ResolveSystemType(Element element) => FindSystemType(element);

    private MEPSystemType? FindSystemType(Element element)
    {
        if (_systemTypeByElement.TryGetValue(element.Id, out ElementId? cachedId))
        {
            return cachedId is null ? null : element.Document.GetElement(cachedId) as MEPSystemType;
        }

        MEPSystemType? systemType = FindSystemTypeUncached(element);
        _systemTypeByElement[element.Id] = systemType?.Id;
        return systemType;
    }

    private static MEPSystemType? FindSystemTypeUncached(Element element)
    {
        Document doc = element.Document;

        foreach (BuiltInParameter builtIn in SystemTypeParameters)
        {
            try
            {
                Parameter? parameter = element.get_Parameter(builtIn);
                if (parameter is not { HasValue: true, StorageType: StorageType.ElementId })
                    continue;

                ElementId id = parameter.AsElementId();
                if (id == ElementId.InvalidElementId)
                    continue;

                switch (doc.GetElement(id))
                {
                    case MEPSystemType systemType:
                        return systemType;
                    case MEPSystem system:
                        return doc.GetElement(system.GetTypeId()) as MEPSystemType;
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Parameter is not present on this element.
            }
        }

        if (element is MEPCurve curve)
        {
            try
            {
                if (curve.MEPSystem is { } system)
                    return doc.GetElement(system.GetTypeId()) as MEPSystemType;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Unassigned MEP system.
            }
        }

        if (element is FamilyInstance instance)
            return FindSystemTypeFromInstance(instance, doc);

        return null;
    }

    private static MEPSystemType? FindSystemTypeFromInstance(FamilyInstance instance, Document doc)
    {
        var votes = new Dictionary<ElementId, (MEPSystemType Type, int Count)>();

        try
        {
            ConnectorManager? manager = instance.MEPModel?.ConnectorManager;
            if (manager is not null)
            {
                foreach (Connector connector in manager.Connectors.Cast<Connector>())
                    TallyConnector(connector, instance.Id, doc, votes);
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }

        return votes
            .OrderByDescending(pair => pair.Value.Count)
            .Select(pair => pair.Value.Type)
            .FirstOrDefault();
    }

    private static void TallyConnector(
        Connector connector,
        ElementId ownerId,
        Document doc,
        Dictionary<ElementId, (MEPSystemType Type, int Count)> votes)
    {
        try
        {
            try
        {
            TallySystem(connector.MEPSystem, doc, votes);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }

        try
        {
            foreach (Connector other in connector.AllRefs.Cast<Connector>())
            {
                if (other.Owner is null || other.Owner.Id == ownerId)
                    continue;

                TallySystem(other.MEPSystem, doc, votes);
                if (other.Owner is MEPCurve curve)
                    TallySystem(curve.MEPSystem, doc, votes);
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }
    }

    private static void TallySystem(
        MEPSystem? system,
        Document doc,
        Dictionary<ElementId, (MEPSystemType Type, int Count)> votes)
    {
        if (system is null)
            return;

        try
        {
            if (doc.GetElement(system.GetTypeId()) is not MEPSystemType systemType)
                return;

            if (votes.TryGetValue(systemType.Id, out (MEPSystemType Type, int Count) existing))
                votes[systemType.Id] = (existing.Type, existing.Count + 1);
            else
                votes[systemType.Id] = (systemType, 1);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }
    }

    private static bool TryFromSystemType(MEPSystemType systemType, out ResolvedAppearance appearance)
    {
        appearance = default;
        ResolvedAppearance? materialColor = TryGetSystemTypeMaterialColor(systemType);
        TryFromRevitColor(systemType.FillColor, 0, out ResolvedAppearance fill);
        bool hasFill = systemType.FillColor is { IsValid: true };
        TryFromRevitColor(systemType.LineColor, 0, out ResolvedAppearance line);
        bool hasLine = systemType.LineColor is { IsValid: true };

        ResolvedAppearance? selected = SystemTypeAppearancePriority.Select(
            materialColor,
            hasFill ? fill : null,
            hasLine ? line : null);

        if (selected is null)
            return false;

        appearance = selected.Value;
        return true;
    }

    private static ResolvedAppearance? TryGetSystemTypeMaterialColor(MEPSystemType systemType)
    {
        ElementId materialId = GetSystemTypeMaterialId(systemType);
        if (materialId == ElementId.InvalidElementId)
            return null;

        if (systemType.Document.GetElement(materialId) is not Material material)
            return null;

        if (TryFromRevitColor(material.Color, material.Transparency, out ResolvedAppearance appearance))
            return appearance;

        return null;
    }

    private static ElementId GetSystemTypeMaterialId(MEPSystemType systemType)
    {
        if (systemType.MaterialId != ElementId.InvalidElementId)
            return systemType.MaterialId;

        try
        {
            Parameter? parameter = systemType.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
            if (parameter is { HasValue: true, StorageType: StorageType.ElementId })
            {
                ElementId id = parameter.AsElementId();
                if (id != ElementId.InvalidElementId)
                    return id;
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            // Type has no Material parameter.
        }

        return ElementId.InvalidElementId;
    }

    private static bool TryFromRevitColor(RevitColor? color, int transparencyPercent, out ResolvedAppearance appearance)
    {
        appearance = default;
        if (color is null || !color.IsValid)
            return false;

        appearance = ResolvedAppearance.FromRgb(color.Red, color.Green, color.Blue, transparencyPercent);
        return true;
    }
}
