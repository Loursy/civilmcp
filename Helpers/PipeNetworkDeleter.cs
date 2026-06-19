using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class PipeNetworkDeleter
    {
        // Belirli network'un tum pipe ve structure'larini sil
        public static string DeleteByNetworkName(string networkName)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                int deleted = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (network == null) continue;
                        if (!network.Name.Equals(networkName, System.StringComparison.OrdinalIgnoreCase)) continue;

                        deleted += EraseParts(tr, network.GetPipeIds());
                        deleted += EraseParts(tr, network.GetStructureIds());
                    }
                    tr.Commit();
                }

                return $"{deleted} parca silindi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Cizimde secili pipe/structure'lari sil
        public static string DeleteSelected()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var ed = doc.Editor;
                var sel = ed.SelectImplied();
                if (sel.Status != PromptStatus.OK || sel.Value.Count == 0)
                    return "Once cizimde pipe/structure secmelisiniz.";

                int deleted = 0;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject so in sel.Value)
                    {
                        var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                        if (obj is Pipe || obj is Structure)
                        {
                            obj.Erase();
                            deleted++;
                        }
                    }
                    tr.Commit();
                }

                return $"{deleted} parca silindi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Belirli dikdortgen alandaki pipe/structure'lari sil
        public static string DeleteByArea(double minX, double minY, double maxX, double maxY)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                int deleted = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (network == null) continue;

                        // Structure'lar
                        foreach (ObjectId sid in network.GetStructureIds())
                        {
                            var st = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                            if (st == null) continue;
                            var p = st.Position;
                            if (p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY)
                            {
                                st.Erase();
                                deleted++;
                            }
                        }

                        // Pipe'lar (orta noktasina gore)
                        foreach (ObjectId pid in network.GetPipeIds())
                        {
                            var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                            if (pipe == null) continue;
                            var mid = new Point3d(
                                (pipe.StartPoint.X + pipe.EndPoint.X) / 2,
                                (pipe.StartPoint.Y + pipe.EndPoint.Y) / 2, 0);
                            if (mid.X >= minX && mid.X <= maxX && mid.Y >= minY && mid.Y <= maxY)
                            {
                                pipe.Erase();
                                deleted++;
                            }
                        }
                    }
                    tr.Commit();
                }

                return $"{deleted} parca silindi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Belirli cap araligindaki pipe'lari sil (mm cinsinden iç çap)
        public static string DeleteByPipeDiameter(double minDia, double maxDia)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                int deleted = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (network == null) continue;

                        foreach (ObjectId pid in network.GetPipeIds())
                        {
                            var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                            if (pipe == null) continue;
                            double dia = pipe.InnerDiameterOrWidth;
                            if (dia >= minDia && dia <= maxDia)
                            {
                                pipe.Erase();
                                deleted++;
                            }
                        }
                    }
                    tr.Commit();
                }

                return $"{deleted} pipe silindi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Ayni konumda birden fazla structure varsa o noktadaki TUM structure'lari sil
        // (tolerans icinde). Tek basina duran structure'lara dokunulmaz.
        public static string DeleteDuplicateStructures(double tolerance)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                int deleted = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    // 1. Asama: tum structure'lari konuma gore kumele
                    var clusters = new List<List<ObjectId>>();
                    var clusterPoints = new List<Point3d>();

                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (network == null) continue;

                        foreach (ObjectId sid in network.GetStructureIds())
                        {
                            var st = tr.GetObject(sid, OpenMode.ForRead) as Structure;
                            if (st == null) continue;
                            var p = st.Position;

                            int clusterIndex = -1;
                            for (int i = 0; i < clusterPoints.Count; i++)
                            {
                                double dx = clusterPoints[i].X - p.X;
                                double dy = clusterPoints[i].Y - p.Y;
                                double dist = System.Math.Sqrt(dx * dx + dy * dy);
                                if (dist <= tolerance)
                                {
                                    clusterIndex = i;
                                    break;
                                }
                            }

                            if (clusterIndex >= 0)
                            {
                                clusters[clusterIndex].Add(sid);
                            }
                            else
                            {
                                clusters.Add(new List<ObjectId> { sid });
                                clusterPoints.Add(p);
                            }
                        }
                    }

                    // 2. Asama: icinde birden fazla structure olan kumelerin tamamini sil
                    foreach (var cluster in clusters)
                    {
                        if (cluster.Count < 2) continue;
                        foreach (var sid in cluster)
                        {
                            var st = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                            if (st != null && !st.IsErased)
                            {
                                st.Erase();
                                deleted++;
                            }
                        }
                    }
                    tr.Commit();
                }

                return $"{deleted} cakisan structure silindi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Belirli network'leri komple sil (parcalar + network kaydi)
        // networkNames: virgulle ayrilmis network isimleri, bos = hepsi
        public static string DeleteNetworks(string networkNames)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;

                var targetNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                bool deleteAll = string.IsNullOrWhiteSpace(networkNames);
                if (!deleteAll)
                {
                    foreach (var n in networkNames.Split(','))
                    {
                        var trimmed = n.Trim();
                        if (trimmed.Length > 0) targetNames.Add(trimmed);
                    }
                }

                int netDeleted = 0, partsDeleted = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var toErase = new List<ObjectId>();

                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (network == null) continue;
                        if (!deleteAll && !targetNames.Contains(network.Name)) continue;

                        // Once parcalari sil
                        partsDeleted += EraseParts(tr, network.GetPipeIds());
                        partsDeleted += EraseParts(tr, network.GetStructureIds());
                        toErase.Add(netId);
                    }

                    // Sonra network kayitlarini sil
                    foreach (var netId in toErase)
                    {
                        var network = tr.GetObject(netId, OpenMode.ForWrite) as Network;
                        if (network != null && !network.IsErased)
                        {
                            network.Erase();
                            netDeleted++;
                        }
                    }

                    tr.Commit();
                }

                if (netDeleted == 0)
                    return deleteAll ? "Cizimde pipe network yok." : "Belirtilen network bulunamadi.";

                return $"{netDeleted} network silindi ({partsDeleted} parca).";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        private static int EraseParts(Transaction tr, ObjectIdCollection ids)
        {
            int count = 0;
            foreach (ObjectId id in ids)
            {
                var obj = tr.GetObject(id, OpenMode.ForWrite);
                if (obj != null && !obj.IsErased)
                {
                    obj.Erase();
                    count++;
                }
            }
            return count;
        }
    }
}