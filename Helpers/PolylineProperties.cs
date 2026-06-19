using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CivilClaudePlugin.Helpers
{
    public static class PolylineProperties
    {
        // Renk index -> Turkce isim
        private static string ColorName(int ci)
        {
            switch (ci)
            {
                case 1: return "kırmızı";
                case 2: return "sarı";
                case 3: return "yeşil";
                case 4: return "cyan";
                case 5: return "mavi";
                case 6: return "magenta";
                case 7: return "beyaz";
                case 256: return "ByLayer";
                default: return $"renk {ci}";
            }
        }

        // ---- LISTELE ----
        // selectedOnly: true ise sadece secili; layer filtresi (bos=tum)
        public static string ListProperties(bool selectedOnly, string layerFilter)
        {
            try
            {
                var check = PreCheck.ActiveDoc();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var sb = new System.Text.StringBuilder();
                int count = 0;

                var selSet = new HashSet<ObjectId>();
                if (selectedOnly)
                {
                    var sel = ed.SelectImplied();
                    if (sel.Status == PromptStatus.OK && sel.Value != null)
                        foreach (SelectedObject so in sel.Value)
                            selSet.Add(so.ObjectId);
                    if (selSet.Count == 0)
                        return "Once cizimde polyline/line secmelisiniz.";
                }

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                    foreach (ObjectId id in ms)
                    {
                        if (selectedOnly && !selSet.Contains(id)) continue;
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        if (ent is Polyline pl)
                        {
                            if (!string.IsNullOrEmpty(layerFilter) && !pl.Layer.Equals(layerFilter, System.StringComparison.OrdinalIgnoreCase)) continue;
                            sb.AppendLine($"POLYLINE:");
                            sb.AppendLine($"  Katman: {pl.Layer}");
                            sb.AppendLine($"  Renk: {ColorName(pl.ColorIndex)}");
                            sb.AppendLine($"  Uzunluk: {pl.Length:F2}");
                            sb.AppendLine($"  Alan: {(pl.Closed ? pl.Area.ToString("F2") : "(acik)")}");
                            sb.AppendLine($"  Vertex sayisi: {pl.NumberOfVertices}");
                            sb.AppendLine($"  Kapali mi: {(pl.Closed ? "evet" : "hayir")}");
                            sb.AppendLine();
                            count++;
                        }
                        else if (ent is Line ln)
                        {
                            if (!string.IsNullOrEmpty(layerFilter) && !ln.Layer.Equals(layerFilter, System.StringComparison.OrdinalIgnoreCase)) continue;
                            sb.AppendLine($"LINE:");
                            sb.AppendLine($"  Katman: {ln.Layer}");
                            sb.AppendLine($"  Renk: {ColorName(ln.ColorIndex)}");
                            sb.AppendLine($"  Uzunluk: {ln.Length:F2}");
                            sb.AppendLine();
                            count++;
                        }
                    }
                    tr.Commit();
                }

                if (count == 0) return "Gosterilecek polyline/line bulunamadi.";
                return $"{count} obje:\n\n" + sb.ToString();
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- FILTRELE + SEC ----
        // property: "length" | "area" | "vertices"
        // op: gt/lt/ge/le/eq/between ; layer filtresi (bos=tum)
        public static string FilterAndSelect(string property, string op, double value1, double value2, string layerFilter)
        {
            try
            {
                var check = PreCheck.ActiveDoc();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var matched = new List<ObjectId>();

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                    foreach (ObjectId id in ms)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        double v = double.NaN;
                        string entLayer = null;

                        if (ent is Polyline pl)
                        {
                            entLayer = pl.Layer;
                            switch (property)
                            {
                                case "length": v = pl.Length; break;
                                case "area": v = pl.Closed ? pl.Area : double.NaN; break;
                                case "vertices": v = pl.NumberOfVertices; break;
                            }
                        }
                        else if (ent is Line ln)
                        {
                            entLayer = ln.Layer;
                            if (property == "length") v = ln.Length;
                        }
                        else continue;

                        if (!string.IsNullOrEmpty(layerFilter) && (entLayer == null || !entLayer.Equals(layerFilter, System.StringComparison.OrdinalIgnoreCase))) continue;
                        if (double.IsNaN(v)) continue;
                        if (Compare(v, op, value1, value2)) matched.Add(id);
                    }
                    tr.Commit();
                }

                if (matched.Count == 0) return "Kosula uyan obje bulunamadi.";

                ed.SetImpliedSelection(matched.ToArray());
                return $"{matched.Count} obje secildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- DUZENLE (renk/katman degistir) ----
        // Filtre: mevcut renk (fromColor, bos=hepsi) ve/veya mevcut katman (fromLayer, bos=hepsi)
        // Degisiklik: newColor (bos=degistirme), newLayer (bos=degistirme)
        public static string ModifyPolylines(string fromColor, string fromLayer, string newColor, string newLayer)
        {
            try
            {
                var check = PreCheck.ActiveDoc();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                int count = 0;

                int fromCi = ColorIndexFromName(fromColor);   // -1 = filtre yok
                int newCi = ColorIndexFromName(newColor);     // -1 = degistirme

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                    // Yeni katman varsa mevcut mu kontrol et
                    if (!string.IsNullOrEmpty(newLayer))
                    {
                        var lt = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                        if (!lt.Has(newLayer))
                            return $"'{newLayer}' adında bir katman yok. Önce katmanı oluşturun.";
                    }

                    foreach (ObjectId id in ms)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (!(ent is Polyline) && !(ent is Line)) continue;

                        // Filtre: renk
                        if (fromCi != -1)
                        {
                            int entCi = ent.ColorIndex;
                            if (entCi == 256) // ByLayer: katman rengini al
                            {
                                var lyr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                if (lyr != null) entCi = lyr.Color.ColorIndex;
                            }
                            if (entCi != fromCi) continue;
                        }

                        // Filtre: katman
                        if (!string.IsNullOrEmpty(fromLayer) && !ent.Layer.Equals(fromLayer, System.StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Degisiklik uygula
                        var entW = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (newCi != -1)
                            entW.ColorIndex = newCi;
                        if (!string.IsNullOrEmpty(newLayer))
                            entW.Layer = newLayer;
                        count++;
                    }
                    tr.Commit();
                }

                return $"{count} obje guncellendi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // Renk adi -> index; bos/taninmaz = -1
        private static int ColorIndexFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            switch (name.Trim().ToLower())
            {
                case "kırmızı": case "kirmizi": case "red": return 1;
                case "sarı": case "sari": case "yellow": return 2;
                case "yeşil": case "yesil": case "green": return 3;
                case "cyan": return 4;
                case "mavi": case "blue": return 5;
                case "magenta": return 6;
                case "beyaz": case "white": return 7;
                default: return -1;
            }
        }
        private static bool Compare(double v, string op, double a, double b)
        {
            switch (op)
            {
                case "gt": return v > a;
                case "lt": return v < a;
                case "ge": return v >= a;
                case "le": return v <= a;
                case "eq": return System.Math.Abs(v - a) < 0.001;
                case "between": return v >= System.Math.Min(a, b) && v <= System.Math.Max(a, b);
                default: return false;
            }
        }
    }
}