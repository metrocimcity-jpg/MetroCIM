using Autodesk.Revit.UI;
using RevitXKT.Commands;
using System.Reflection;

namespace RevitXKT;

public sealed class App : IExternalApplication
{
    public const string TabName = "RevitXKT";
    public const string PanelName = "Export";

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            application.CreateRibbonTab(TabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Tab already exists from a previous load.
        }

        RibbonPanel panel = GetOrCreatePanel(application);
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        var gltfButton = new PushButtonData(
            "ExportGltf",
            "Export\nglTF",
            assemblyPath,
            typeof(ExportGltfCommand).FullName)
        {
            ToolTip = "Export glTF (View Colors)",
            LongDescription =
                "Exports the active 3D view to glTF (.glb/.gltf) using on-screen visibility and graphic overrides."
        };

        var xktButton = new PushButtonData(
            "ExportXkt",
            "Export\nXKT",
            assemblyPath,
            typeof(ExportXktCommand).FullName)
        {
            ToolTip = "Export XKT (View Colors)",
            LongDescription =
                "Exports the active 3D view to XKT via an intermediate glTF, using xeokit-convert. " +
                "Requires Node.js and `npm install -g @xeokit/xeokit-convert`."
        };

        panel.AddItem(gltfButton);
        panel.AddItem(xktButton);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private static RibbonPanel GetOrCreatePanel(UIControlledApplication application)
    {
        foreach (RibbonPanel existing in application.GetRibbonPanels(TabName))
        {
            if (existing.Name == PanelName)
                return existing;
        }

        return application.CreateRibbonPanel(TabName, PanelName);
    }
}
