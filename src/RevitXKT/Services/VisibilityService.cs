using Autodesk.Revit.DB;

namespace RevitXKT.Services;

public sealed class VisibilityService
{
    public IReadOnlyList<Element> GetVisibleElements(Document doc, View3D view)
    {
        var results = new List<Element>();

        using var collector = new FilteredElementCollector(doc, view.Id)
            .WherePasses(new VisibleInViewFilter(doc, view.Id))
            .WhereElementIsNotElementType();

        bool tempHideIsolate = view.IsTemporaryHideIsolateActive();

        foreach (Element element in collector)
        {
            if (!IsExportable(element, view, tempHideIsolate))
                continue;

            results.Add(element);
        }

        return results;
    }

    private static bool IsExportable(Element element, View3D view, bool tempHideIsolate)
    {
        if (element is RevitLinkInstance or ElementType)
            return false;

        Category? category = element.Category;
        if (category is null || category.CategoryType != CategoryType.Model)
            return false;

        if (IsHiddenInView(element, view))
            return false;

        if (IsCategoryHidden(view, category.Id))
            return false;

        if (tempHideIsolate &&
            !view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, element.Id))
        {
            return false;
        }

        if (IsHiddenByWorkset(element, view))
            return false;

        return true;
    }

    private static bool IsHiddenInView(Element element, View view)
    {
        try
        {
            return element.IsHidden(view);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool IsCategoryHidden(View view, ElementId categoryId)
    {
        try
        {
            return view.GetCategoryHidden(categoryId);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool IsHiddenByWorkset(Element element, View view)
    {
        WorksetId worksetId = element.WorksetId;
        if (worksetId == WorksetId.InvalidWorksetId)
            return false;

        try
        {
            WorksetVisibility visibility = view.GetWorksetVisibility(worksetId);
            if (visibility == WorksetVisibility.Hidden)
                return true;

            if (visibility == WorksetVisibility.Visible)
                return false;

            Workset workset = element.Document.GetWorksetTable().GetWorkset(worksetId);
            return !workset.IsVisibleByDefault;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }
}
