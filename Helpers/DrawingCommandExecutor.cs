using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;

namespace CivilClaudePlugin.Helpers
{
    public static class DrawingCommandExecutor
    {
        public static string SelectByFilter(string objectType, string colorName, string layerName)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var ed = doc.Editor;
                var db = doc.Database;

                int targetColor = string.IsNullOrEmpty(colorName) ? -1 : GetColorIndex(colorName);

                // Önce obje tipi ve katman filtresiyle seçim yap
                var filters = new List<TypedValue>();

                string dxfType = null;
                if (!string.IsNullOrEmpty(objectType))
                    dxfType = GetDxfType(objectType);
                if (dxfType != null)
                    filters.Add(new TypedValue((int)DxfCode.Start, dxfType));

                if (!string.IsNullOrEmpty(layerName))
                    filters.Add(new TypedValue((int)DxfCode.LayerName, layerName));

                SelectionFilter filter = filters.Count > 0
                    ? new SelectionFilter(filters.ToArray())
                    : null;

                PromptSelectionResult result = filter != null
                    ? ed.SelectAll(filter)
                    : ed.SelectAll();

                if (result.Status != PromptStatus.OK || result.Value.Count == 0)
                    return "Hiç obje bulunamadı.";

                // Renk filtresi yoksa direkt seç
                if (targetColor < 0)
                {
                    ed.SetImpliedSelection(result.Value);
                    return $"{result.Value.Count} obje seçildi.";
                }

                // Renk filtresi varsa ByLayer dahil kontrol et
                var matchingIds = new List<Autodesk.AutoCAD.DatabaseServices.ObjectId>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    foreach (SelectedObject selObj in result.Value)
                    {
                        var entity = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        int entityColor;
                        if (entity.ColorIndex == 256) // ByLayer
                        {
                            // Katmanın rengine bak
                            var layer = tr.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
                            entityColor = layer.Color.ColorIndex;
                        }
                        else
                        {
                            entityColor = entity.ColorIndex;
                        }

                        if (entityColor == targetColor)
                            matchingIds.Add(selObj.ObjectId);
                    }
                    tr.Commit();
                }

                if (matchingIds.Count == 0)
                    return $"'{colorName}' renginde obje bulunamadı.";

                ed.SetImpliedSelection(matchingIds.ToArray());
                return $"{matchingIds.Count} obje seçildi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        private static string GetDxfType(string objectType)
        {
            switch (objectType.ToLower().Trim())
            {
                case "polyline":
                case "lwpolyline":
                case "pline":
                    return "LWPOLYLINE";
                case "line":
                    return "LINE";
                case "arc":
                    return "ARC";
                case "circle":
                    return "CIRCLE";
                case "text":
                    return "TEXT";
                case "mtext":
                    return "MTEXT";
                case "block":
                case "insert":
                    return "INSERT";
                case "hatch":
                    return "HATCH";
                default:
                    return null;
            }
        }

        private static int GetColorIndex(string colorName)
        {
            switch (colorName.ToLower().Trim())
            {
                case "kırmızı":
                case "kirmizi":
                case "red":
                    return 1;
                case "sarı":
                case "sari":
                case "yellow":
                    return 2;
                case "yeşil":
                case "yesil":
                case "green":
                    return 3;
                case "cyan":
                    return 4;
                case "mavi":
                case "blue":
                    return 5;
                case "magenta":
                case "pembe":
                    return 6;
                case "beyaz":
                case "white":
                    return 7;
                default:
                    return -1;
            }
        }
    }
}