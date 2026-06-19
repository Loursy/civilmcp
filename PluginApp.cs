using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(CivilClaudePlugin.PluginApp))]
[assembly: CommandClass(typeof(CivilClaudePlugin.Commands.OpenClaudePanelCommand))]

namespace CivilClaudePlugin
{
    public class PluginApp : IExtensionApplication
    {
        public void Initialize()
        {
            Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
                "Civil3D Claude Plugin yüklendi. CLAUDEPANEL yazarak paneli açabilirsiniz.");
        }

        public void Terminate() { }
    }
}