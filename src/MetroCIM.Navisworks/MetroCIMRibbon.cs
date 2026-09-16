using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace MetroCIM.Navisworks;

[Plugin("MetroCIM.Ribbon", "MCIM", DisplayName = "MetroCIM")]
[RibbonLayout("MetroCIMRibbon.xaml")]
[RibbonTab("ID_Tab_MetroCIM", DisplayName = "MetroCIM")]
[Command("ID_Button_ExportGltf", DisplayName = "Export\nglTF", ToolTip = "Export glTF with on-screen colors", ExtendedToolTip = "Exports visible model geometry to glTF (.glb/.gltf).")]
[Command("ID_Button_ExportXkt", DisplayName = "Export\nXKT", ToolTip = "Export XKT with on-screen colors", ExtendedToolTip = "Exports visible model geometry to XKT via xeokit-convert.")]
[Command("ID_Button_ExportIfc", DisplayName = "Export\nIFC", ToolTip = "Export IFC with on-screen colors", ExtendedToolTip = "Exports visible model geometry to IFC4 with system/override colors.")]
public sealed class MetroCIMRibbon : CommandHandlerPlugin
{
    public override int ExecuteCommand(string name, params string[] parameters)
    {
        return name switch
        {
            "ID_Button_ExportGltf" => ExportCommands.ExportGltf(),
            "ID_Button_ExportXkt" => ExportCommands.ExportXkt(),
            "ID_Button_ExportIfc" => ExportCommands.ExportIfc(),
            _ => 0
        };
    }

    public override CommandState CanExecuteCommand(string commandId)
    {
        Document? document = NwApp.ActiveDocument;
        bool ready = document is not null && !document.IsClear;
        return new CommandState(ready);
    }
}
