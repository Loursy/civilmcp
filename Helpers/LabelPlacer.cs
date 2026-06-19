using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class LabelPlacer
    {
        // Mevcut label stillerini listele.
        // partType: "pipe" = sadece pipe, "structure" = sadece structure, "all"/bos = ikisi
        public static string ListLabelStyles(string partType = "all")
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;
                var doc = Application.DocumentManager.MdiActiveDocument;

                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();

                string pt = string.IsNullOrWhiteSpace(partType) ? "all" : partType.Trim().ToLowerInvariant();
                bool wantPipe = pt == "pipe" || pt == "all";
                bool wantStruct = pt == "structure" || pt == "all";

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (wantPipe)
                    {
                        sb.AppendLine("PIPE LABEL STILLERI:");
                        foreach (ObjectId id in civilDoc.Styles.LabelStyles.PipeLabelStyles.PlanProfileLabelStyles)
                        {
                            var ls = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.LabelStyle;
                            if (ls != null) sb.AppendLine($"  - {ls.Name}");
                        }
                    }

                    if (wantStruct)
                    {
                        if (wantPipe) sb.AppendLine();
                        sb.AppendLine("STRUCTURE LABEL STILLERI:");
                        foreach (ObjectId id in civilDoc.Styles.LabelStyles.StructureLabelStyles.LabelStyles)
                        {
                            var ls = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.LabelStyle;
                            if (ls != null) sb.AppendLine($"  - {ls.Name}");
                        }
                    }

                    tr.Commit();
                }

                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // partType: "pipe", "structure", "all"
        // network: bos = tum networkler
        // pipeStyleName/structStyleName: kullanilacak LABEL stilleri
        // pipeObjStyleFilter/structObjStyleFilter: sadece bu NESNE stiline sahip parcalara etiket (bos = filtre yok)
        public static string AddLabels(string partType, string network, string pipeStyleName, string structStyleName,
            string pipeObjStyleFilter, string structObjStyleFilter)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;
                var doc = Application.DocumentManager.MdiActiveDocument;

                var civilDoc = CivilApplication.ActiveDocument;

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";

                ObjectId pipeStyleId = ObjectId.Null;
                ObjectId structStyleId = ObjectId.Null;

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (doPipe && !string.IsNullOrEmpty(pipeStyleName))
                        foreach (ObjectId id in civilDoc.Styles.LabelStyles.PipeLabelStyles.PlanProfileLabelStyles)
                        {
                            var ls = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.LabelStyle;
                            if (ls != null && ls.Name.Equals(pipeStyleName, System.StringComparison.OrdinalIgnoreCase)) { pipeStyleId = id; break; }
                        }

                    if (doStruct && !string.IsNullOrEmpty(structStyleName))
                        foreach (ObjectId id in civilDoc.Styles.LabelStyles.StructureLabelStyles.LabelStyles)
                        {
                            var ls = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.LabelStyle;
                            if (ls != null && ls.Name.Equals(structStyleName, System.StringComparison.OrdinalIgnoreCase)) { structStyleId = id; break; }
                        }

                    tr.Commit();
                }

                int pipeCount = 0, structCount = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var net = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (net == null) continue;
                        if (!string.IsNullOrEmpty(network) && !net.Name.Equals(network, System.StringComparison.OrdinalIgnoreCase)) continue;

                        if (doPipe && pipeStyleId != ObjectId.Null)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                // Nesne stili filtresi
                                if (!string.IsNullOrEmpty(pipeObjStyleFilter))
                                {
                                    var pp = tr.GetObject(pid, OpenMode.ForRead) as Pipe;
                                    if (pp == null || !GetStyleName(tr, pp.StyleId).Equals(pipeObjStyleFilter, System.StringComparison.OrdinalIgnoreCase))
                                        continue;
                                }
                                PipeLabel.Create(pid, 0.5, pipeStyleId);
                                pipeCount++;
                            }

                        if (doStruct && structStyleId != ObjectId.Null)
                            foreach (ObjectId sid in net.GetStructureIds())
                            {
                                var stObj = tr.GetObject(sid, OpenMode.ForRead) as Structure;
                                if (stObj == null) continue;

                                // Nesne stili filtresi
                                if (!string.IsNullOrEmpty(structObjStyleFilter))
                                {
                                    if (!GetStyleName(tr, stObj.StyleId).Equals(structObjStyleFilter, System.StringComparison.OrdinalIgnoreCase))
                                        continue;
                                }
                                StructureLabel.Create(sid, structStyleId, stObj.Position);
                                structCount++;
                            }
                    }
                    tr.Commit();
                }
                return $"Etiket eklendi: {structCount} structure, {pipeCount} pipe.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Bir style ObjectId'sinden stil adini al
        private static string GetStyleName(Transaction tr, ObjectId styleId)
        {
            if (styleId == ObjectId.Null) return "";
            try
            {
                var obj = tr.GetObject(styleId, OpenMode.ForRead);
                if (obj is Autodesk.Civil.DatabaseServices.Styles.PipeStyle ps) return ps.Name;
                if (obj is Autodesk.Civil.DatabaseServices.Styles.StructureStyle ss) return ss.Name;
            }
            catch { }
            return "";
        }
    }
}