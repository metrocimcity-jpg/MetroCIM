using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
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
                    case MEPSystemType systemType when !IsElectrical(systemType):
                        return systemType;
                    case MEPSystem system:
                        if (AsNonElectricalSystemType(system, doc) is { } fromSystem)
                            return fromSystem;
                        break;
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
                if (AsNonElectricalSystemType(curve.MEPSystem, doc) is { } fromCurve)
                    return fromCurve;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Unassigned MEP system.
            }
        }

        if (element is FamilyInstance instance && !IsEquipmentFamily(instance))
            return FindSystemTypeFromInstance(instance, doc);

        return null;
    }

    private static MEPSystemType? FindSystemTypeFromInstance(FamilyInstance instance, Document doc)
    {
        var pipeVotes = new Dictionary<ElementId, (MEPSystemType Type, int Count)>();
        var otherVotes = new Dictionary<ElementId, (MEPSystemType Type, int Count)>();
        var visited = new HashSet<ElementId> { instance.Id };
        CollectFromInstance(instance, doc, pipeVotes, otherVotes, visited, 0);

        return PreferNonElectrical(pipeVotes) ?? PreferNonElectrical(otherVotes);
    }

    private static void CollectFromInstance(
        FamilyInstance instance,
        Document doc,
        Dictionary<ElementId, (MEPSystemType Type, int Count)> pipeVotes,
        Dictionary<ElementId, (MEPSystemType Type, int Count)> otherVotes,
        HashSet<ElementId> visited,
        int depth)
    {
        if (depth > 6)
            return;

        ConnectorManager? manager;
        try
        {
            manager = instance.MEPModel?.ConnectorManager;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return;
        }

        if (manager is null)
            return;

        foreach (Connector connector in manager.Connectors.Cast<Connector>())
        {
            if (IsElectricalDomain(connector))
                continue;

            try
            {
                TallySystem(connector.MEPSystem, doc, otherVotes);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
            }

            IEnumerable<Connector> refs;
            try
            {
                refs = connector.AllRefs.Cast<Connector>();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                continue;
            }

            foreach (Connector other in refs)
            {
                Element? owner;
                try
                {
                    owner = other.Owner;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    continue;
                }

                if (owner is null || !visited.Add(owner.Id))
                    continue;

                if (owner is MEPCurve curve)
                {
                    try
                    {
                        TallySystem(other.MEPSystem, doc, pipeVotes);
                        TallySystem(curve.MEPSystem, doc, pipeVotes);
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException)
                    {
                    }

                    continue;
                }

                if (owner is FamilyInstance nested)
                    CollectFromInstance(nested, doc, pipeVotes, otherVotes, visited, depth + 1);
            }
        }
    }

    private static void TallySystem(
        MEPSystem? system,
        Document doc,
        Dictionary<ElementId, (MEPSystemType Type, int Count)> votes)
    {
        if (system is null || IsElectrical(system, doc))
            return;

        try
        {
            if (doc.GetElement(system.GetTypeId()) is not MEPSystemType systemType || IsElectrical(systemType))
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

        if (IfcAppearanceApplier.IsTemporaryMaterialName(material.Name))
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

    private static MEPSystemType? PreferNonElectrical(
        Dictionary<ElementId, (MEPSystemType Type, int Count)> votes)
    {
        return votes.Values
            .Where(vote => !IsElectrical(vote.Type))
            .OrderByDescending(vote => vote.Count)
            .Select(vote => vote.Type)
            .FirstOrDefault();
    }

    private static MEPSystemType? AsNonElectricalSystemType(MEPSystem? system, Document doc)
    {
        if (system is null || IsElectrical(system, doc))
            return null;

        return doc.GetElement(system.GetTypeId()) is MEPSystemType systemType && !IsElectrical(systemType)
            ? systemType
            : null;
    }

    private static bool IsElectrical(MEPSystem system, Document doc)
    {
        if (system is ElectricalSystem)
            return true;

        try
        {
            return doc.GetElement(system.GetTypeId()) is MEPSystemType systemType && IsElectrical(systemType);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool IsElectrical(MEPSystemType systemType)
    {
        try
        {
            if (IsElectricalClassification(systemType.SystemClassification))
                return true;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }

        try
        {
            return systemType.Category?.BuiltInCategory is
                BuiltInCategory.OST_ElecDistributionSys or
                BuiltInCategory.OST_ElectricalCircuit;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool IsElectricalDomain(Connector connector)
    {
        try
        {
            return connector.Domain is Domain.DomainElectrical or Domain.DomainCableTrayConduit;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    internal static bool IsElectricalClassification(MEPSystemClassification classification) =>
        classification is
            MEPSystemClassification.PowerCircuit or
            MEPSystemClassification.DataCircuit or
            MEPSystemClassification.Telephone or
            MEPSystemClassification.Security or
            MEPSystemClassification.FireAlarm or
            MEPSystemClassification.NurseCall or
            MEPSystemClassification.Controls or
            MEPSystemClassification.Communication or
            MEPSystemClassification.PowerBalanced or
            MEPSystemClassification.PowerUnBalanced or
            MEPSystemClassification.CableTrayConduit;

    /// <summary>
    /// Equipment keeps its own system-type parameter if it has one, but does not inherit
    /// color from connected ducts/pipes. Otherwise an AHU would pick up Supply Air / insulation colors.
    /// </summary>
    private static bool IsEquipmentFamily(FamilyInstance instance)
    {
        BuiltInCategory category = BuiltInCategory.INVALID;
        try
        {
            if (instance.Category is not null)
                category = instance.Category.BuiltInCategory;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            category = BuiltInCategory.INVALID;
        }

        return category is
            BuiltInCategory.OST_MechanicalEquipment or
            BuiltInCategory.OST_ElectricalEquipment or
            BuiltInCategory.OST_SpecialityEquipment or
            BuiltInCategory.OST_PlumbingEquipment or
            BuiltInCategory.OST_MedicalEquipment;
    }
}
