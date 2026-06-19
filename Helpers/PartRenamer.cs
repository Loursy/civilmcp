using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class PartRenamer
    {
        // partType: "pipe", "structure", "all"
        // network: bos = tum networkler, dolu = sadece o network

        public static string SequentialRename(string partType, string network, string prefix, int startNum)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;
                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";
                int num = startNum;
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
                                if (st != null) { st.Name = $"{prefix}{num}"; num++; count++; }
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe != null) { pipe.Name = $"{prefix}{num}"; num++; count++; }
                            }
                    }
                    tr.Commit();
                }
                return $"{count} parca yeniden adlandirildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        public static string AddPrefixSuffix(string partType, string network, string prefix, string suffix)
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
                                if (st != null) { st.Name = $"{prefix}{st.Name}{suffix}"; count++; }
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe != null) { pipe.Name = $"{prefix}{pipe.Name}{suffix}"; count++; }
                            }
                    }
                    tr.Commit();
                }
                return $"{count} parca yeniden adlandirildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // Bul-degistir. field: "name" veya "description"
        public static string FindReplace(string partType, string network, string find, string replace, string field)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;
                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";
                bool onDesc = field == "description";
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
                                string cur = onDesc ? (st.Description ?? "") : st.Name;
                                if (cur.Contains(find))
                                {
                                    string nv = cur.Replace(find, replace);
                                    if (onDesc) st.Description = nv; else { try { st.Name = nv; } catch { } }
                                    count++;
                                }
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe == null) continue;
                                string cur = onDesc ? (pipe.Description ?? "") : pipe.Name;
                                if (cur.Contains(find))
                                {
                                    string nv = cur.Replace(find, replace);
                                    if (onDesc) pipe.Description = nv; else { try { pipe.Name = nv; } catch { } }
                                    count++;
                                }
                            }
                    }
                    tr.Commit();
                }
                return $"{count} parcada degisiklik yapildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        public static string SetDescription(string partType, string network, string description)
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
                                if (st != null) { st.Description = description; count++; }
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe != null) { pipe.Description = description; count++; }
                            }
                    }
                    tr.Commit();
                }
                return $"{count} parcaya description atandi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // target: "name" veya "description"; template'de {size} -> boyut(mm)
        public static string SetBySize(string partType, string network, string target, string template)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;
                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;

                bool doPipe = partType == "pipe" || partType == "all";
                bool doStruct = partType == "structure" || partType == "all";
                bool setName = target == "name";
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
                                string size = $"{st.InnerDiameterOrWidth * 1000:F0}";
                                string val = template.Replace("{size}", size);
                                if (setName) st.Name = val; else st.Description = val;
                                count++;
                            }

                        if (doPipe)
                            foreach (ObjectId pid in net.GetPipeIds())
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe == null) continue;
                                string size = $"{pipe.InnerDiameterOrWidth * 1000:F0}";
                                string val = template.Replace("{size}", size);
                                if (setName) pipe.Name = val; else pipe.Description = val;
                                count++;
                            }
                    }
                    tr.Commit();
                }
                return $"{count} parca guncellendi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // Her parcaya network adini ver, ayni network'te birden fazla varsa .1 .2 ekle
        public static string NameByNetwork(string partType)
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
                        string baseName = net.Name;

                        if (doStruct)
                        {
                            var sIds = net.GetStructureIds();
                            int idx = 1;
                            bool multiple = sIds.Count > 1;
                            foreach (ObjectId sid in sIds)
                            {
                                var st = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                                if (st != null)
                                {
                                    st.Name = multiple ? $"{baseName}.{idx}" : baseName;
                                    idx++;
                                    count++;
                                }
                            }
                        }

                        if (doPipe)
                        {
                            var pIds = net.GetPipeIds();
                            int idx = 1;
                            bool multiple = pIds.Count > 1;
                            foreach (ObjectId pid in pIds)
                            {
                                var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                                if (pipe != null)
                                {
                                    pipe.Name = multiple ? $"{baseName}.{idx}" : baseName;
                                    idx++;
                                    count++;
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
                return $"{count} parca network adina gore adlandirildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }
    }
}