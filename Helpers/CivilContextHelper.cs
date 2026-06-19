using Autodesk.AutoCAD.ApplicationServices;

namespace CivilClaudePlugin.Helpers
{
    public static class CivilContextHelper
    {
        public static string GetDrawingContext()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Aktif drawing: {doc.Name}");
                sb.AppendLine($"Drawing path: {doc.Database.Filename}");
                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                return $"Drawing bilgisi alınamadı: {ex.Message}";
            }
        }
    }
}