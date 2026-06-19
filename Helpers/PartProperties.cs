using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class PartProperties
    {
        // ---- 1. ALANDAN ALANA KOPYALA ----
        // partType: pipe/structure/all, network: bos=hepsi
        // fromField/toField: "name" veya "description"
        public static string CopyField(string partType, string network, string fromField, string toField)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";
                int count = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var net = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (net == null) continue;
                        if (!string.IsNullOrEmpty(network) && !net.Name.Equals(network, System.StringComparison.OrdinalIgnoreCase)) continue;

                        if (doStruct)
                            foreach (ObjectId sid in net.GetStructureIds())
                            {
                                var st = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                                if (st == null) continue;
                                string val = ReadField(st.Name, st.Description, fromField);
                                WriteField(st, null, toField, val);
                                count++;
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe == null) continue;
                                string val = ReadField(pipe.Name, pipe.Description, fromField);
                                WriteField(null, pipe, toField, val);
                                count++;
                            }
                    }
                    tr.Commit();
                }

                return $"{count} parcada '{fromField}' -> '{toField}' kopyalandi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- 2. PROPERTIES LISTELE ----
        // selectedOnly: true ise sadece cizimde secili parcalar; false ise network/tum
        public static string ListProperties(string partType, string network, bool selectedOnly)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                var ed = doc.Editor;
                var sb = new System.Text.StringBuilder();

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";

                // Secili parca id'leri (selectedOnly icin)
                var selSet = new HashSet<ObjectId>();
                if (selectedOnly)
                {
                    var sel = ed.SelectImplied();
                    if (sel.Status == PromptStatus.OK && sel.Value != null)
                        foreach (SelectedObject so in sel.Value)
                            selSet.Add(so.ObjectId);
                    if (selSet.Count == 0)
                        return "Once cizimde pipe/structure secmelisiniz.";
                }

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var net = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (net == null) continue;
                        if (!selectedOnly && !string.IsNullOrEmpty(network) &&
                            !net.Name.Equals(network, System.StringComparison.OrdinalIgnoreCase)) continue;

                        if (doStruct)
                            foreach (ObjectId sid in net.GetStructureIds())
                            {
                                if (selectedOnly && !selSet.Contains(sid)) continue;
                                var st = tr.GetObject(sid, OpenMode.ForRead) as Structure;
                                if (st == null) continue;
                                sb.AppendLine($"STRUCTURE: {st.Name}");
                                sb.AppendLine($"  Aciklama: {st.Description}");
                                sb.AppendLine($"  Network: {net.Name}");
                                sb.AppendLine($"  Rim kotu: {st.RimElevation:F3}");
                                sb.AppendLine($"  Sump kotu: {st.SumpElevation:F3}");
                                sb.AppendLine($"  Ic olcu (mm): {st.InnerDiameterOrWidth * 1000:F0}");
                                sb.AppendLine();
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                if (selectedOnly && !selSet.Contains(pid)) continue;
                                var pipe = tr.GetObject(pid, OpenMode.ForRead) as Pipe;
                                if (pipe == null) continue;
                                sb.AppendLine($"PIPE: {pipe.Name}");
                                sb.AppendLine($"  Aciklama: {pipe.Description}");
                                sb.AppendLine($"  Network: {net.Name}");
                                sb.AppendLine($"  Ic cap (mm): {pipe.InnerDiameterOrWidth * 1000:F0}");
                                sb.AppendLine($"  Uzunluk: {pipe.Length2DCenterToCenter:F2}");
                                sb.AppendLine($"  Start invert: {pipe.StartPoint.Z - pipe.InnerDiameterOrWidth / 2.0:F3}");
                                sb.AppendLine($"  End invert: {pipe.EndPoint.Z - pipe.InnerDiameterOrWidth / 2.0:F3}");
                                sb.AppendLine();
                            }
                    }
                    tr.Commit();
                }

                var txt = sb.ToString();
                return string.IsNullOrWhiteSpace(txt) ? "Gosterilecek parca bulunamadi." : txt;
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- 3. FILTRELE + SEC ----
        // property: "diameter"(mm) | "length" | "rim" | "sump" | "invert"
        // op: "gt" | "lt" | "ge" | "le" | "eq" | "between"
        // value1 (ve between icin value2)
        public static string FilterAndSelect(string partType, string network, string property, string op, double value1, double value2)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                var ed = doc.Editor;

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";

                var matched = new List<ObjectId>();

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var net = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (net == null) continue;
                        if (!string.IsNullOrEmpty(network) && !net.Name.Equals(network, System.StringComparison.OrdinalIgnoreCase)) continue;

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForRead) as Pipe;
                                if (pipe == null) continue;
                                double v = PipeValue(pipe, property);
                                if (!double.IsNaN(v) && Compare(v, op, value1, value2))
                                    matched.Add(pid);
                            }

                        if (doStruct)
                            foreach (ObjectId sid in net.GetStructureIds())
                            {
                                var st = tr.GetObject(sid, OpenMode.ForRead) as Structure;
                                if (st == null) continue;
                                double v = StructValue(st, property);
                                if (!double.IsNaN(v) && Compare(v, op, value1, value2))
                                    matched.Add(sid);
                            }
                    }
                    tr.Commit();
                }

                if (matched.Count == 0)
                    return "Kosula uyan parca bulunamadi.";

                ed.SetImpliedSelection(matched.ToArray());
                return $"{matched.Count} parca secildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- yardimcilar ----
        private static string ReadField(string name, string desc, string field)
        {
            if (field == "name") return name ?? "";
            if (field == "description") return desc ?? "";
            return "";
        }

        private static void WriteField(Structure st, Pipe pipe, string field, string val)
        {
            if (st != null)
            {
                if (field == "name") { try { st.Name = val; } catch { } }
                else if (field == "description") st.Description = val;
            }
            if (pipe != null)
            {
                if (field == "name") { try { pipe.Name = val; } catch { } }
                else if (field == "description") pipe.Description = val;
            }
        }

        private static double PipeValue(Pipe pipe, string property)
        {
            switch (property)
            {
                case "diameter": return pipe.InnerDiameterOrWidth * 1000.0;
                case "length": return pipe.Length2DCenterToCenter;
                case "invert": return pipe.StartPoint.Z - pipe.InnerDiameterOrWidth / 2.0;
                default: return double.NaN;
            }
        }

        private static double StructValue(Structure st, string property)
        {
            switch (property)
            {
                case "diameter": return st.InnerDiameterOrWidth * 1000.0;
                case "rim": return st.RimElevation;
                case "sump": return st.SumpElevation;
                default: return double.NaN;
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