using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class RuleApplier
    {
        // target: "pipe", "structure" veya "all"
        public static string ApplyRules(string target)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                int pipeCount = 0, structCount = 0;

                bool doPipe = target == "pipe" || target == "all";
                bool doStruct = target == "structure" || target == "all";

                using (doc.LockDocument())
                {
                    // 1. ASAMA: Once structure'lara uygula ve commit et
                    if (doStruct)
                    {
                        using (var tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                            {
                                var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                                if (network == null) continue;

                                foreach (ObjectId sid in network.GetStructureIds())
                                {
                                    var st = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                                    if (st != null)
                                    {
                                        st.ApplyRules();
                                        structCount++;
                                    }
                                }
                            }
                            tr.Commit();
                        }
                    }

                    // 2. ASAMA: Structure'lar guncel kotta iken pipe'lara uygula
                    if (doPipe)
                    {
                        using (var tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                            {
                                var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                                if (network == null) continue;

                                foreach (ObjectId pid in network.GetPipeIds())
                                {
                                    var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                    if (pipe != null)
                                    {
                                        pipe.ApplyRules();
                                        pipeCount++;
                                    }
                                }
                            }
                            tr.Commit();
                        }
                    }
                }

                return $"Kurallar uygulandi: {structCount} structure, {pipeCount} pipe.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }
    }
}