using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class PipeConnector
    {
        // Bagli olmayan pipe uclarini, tolerans icindeki structure'lara baglar
        public static string ConnectUnconnectedPipes(double tolerance)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                int connected = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
                        if (network == null) continue;

                        // Bu network'teki tum structure'larin konumlarini topla
                        var structs = new List<KeyValuePair<ObjectId, Point3d>>();
                        foreach (ObjectId sid in network.GetStructureIds())
                        {
                            var st = tr.GetObject(sid, OpenMode.ForRead) as Structure;
                            if (st != null) structs.Add(new KeyValuePair<ObjectId, Point3d>(sid, st.Position));
                        }

                        // Her pipe'in bagli olmayan uclarini kontrol et
                        foreach (ObjectId pid in network.GetPipeIds())
                        {
                            var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                            if (pipe == null) continue;

                            // Start ucu bagli degilse
                            if (pipe.StartStructureId == ObjectId.Null)
                            {
                                var sid = FindNearestStructure(structs, pipe.StartPoint, tolerance);
                                if (sid != ObjectId.Null)
                                {
                                    pipe.ConnectToStructure(ConnectorPositionType.Start, sid, true);
                                    connected++;
                                }
                            }

                            // End ucu bagli degilse
                            if (pipe.EndStructureId == ObjectId.Null)
                            {
                                var sid = FindNearestStructure(structs, pipe.EndPoint, tolerance);
                                if (sid != ObjectId.Null)
                                {
                                    pipe.ConnectToStructure(ConnectorPositionType.End, sid, true);
                                    connected++;
                                }
                            }
                        }
                    }
                    tr.Commit();
                }

                return $"{connected} pipe ucu structure'a baglandi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        private static ObjectId FindNearestStructure(List<KeyValuePair<ObjectId, Point3d>> structs, Point3d pt, double tolerance)
        {
            ObjectId best = ObjectId.Null;
            double bestDist = tolerance;

            foreach (var kvp in structs)
            {
                double dx = kvp.Value.X - pt.X;
                double dy = kvp.Value.Y - pt.Y;
                double dist = System.Math.Sqrt(dx * dx + dy * dy);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = kvp.Key;
                }
            }

            return best;
        }
    }
}