using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CivilClaudePlugin.UI;

namespace CivilClaudePlugin.Commands
{
    public class OpenClaudePanelCommand
    {
        private static PaletteSet _paletteSet;

        [CommandMethod("CLAUDEPANEL")]
        public void OpenPanel()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("Claude AI")
                {
                    Style = PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.Snappable,
                    MinimumSize = new System.Drawing.Size(350, 500)
                };

                var panel = new ClaudePanel();
                _paletteSet.AddVisual("Claude AI", panel);
            }

            _paletteSet.Visible = true;
        }
    }
}