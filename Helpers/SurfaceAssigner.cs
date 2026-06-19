using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class SurfaceAssigner
    {
        // surfaceName: atanacak yuzey adi
        // networkName: bos = tum network'ler, dolu = sadece o network
        // Hem network seviyesinde hem her pipe/structure'a atar
        public static string AssignSurface(string surfaceName, string networkName)
        {
            try
            {
                var check = PreCheck.HasPipeNetwork();
                if (check != null) return check;
                check = PreCheck.SurfaceExists(surfaceName);
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;

                // Yuzeyi bul
                ObjectId surfaceId = ObjectId.Null;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civilDoc.GetSurfaceIds())
                    {
                        var surf = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                        if (surf != null && surf.Name.Equals(surfaceName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            surfaceId = id;
                            break;
                        }
                    }
                    tr.Commit();
                }

                if (surfaceId == ObjectId.Null)
                    return $"Yuzey '{surfaceName}' bulunamadi.";

                int netCount = 0, pipeCount = 0, structCount = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId netId in civilDoc.GetPipeNetworkIds())
                    {
                        var network = tr.GetObject(netId, OpenMode.ForWrite) as Network;
                        if (network == null) continue;

                        // Network ismi filtresi
                        if (!string.IsNullOrEmpty(networkName) &&
                            !network.Name.Equals(networkName, System.StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Network seviyesi
                        network.ReferenceSurfaceId = surfaceId;
                        netCount++;

                        // Her structure
                        foreach (ObjectId sid in network.GetStructureIds())
                        {
                            var st = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                            if (st != null)
                            {
                                st.RefSurfaceId = surfaceId;
                                structCount++;
                            }
                        }

                        // Her pipe
                        foreach (ObjectId pid in network.GetPipeIds())
                        {
                            var pipe = tr.GetObject(pid, OpenMode.ForWrite) as Pipe;
                            if (pipe != null)
                            {
                                pipe.RefSurfaceId = surfaceId;
                                pipeCount++;
                            }
                        }
                    }
                    tr.Commit();
                }

                if (netCount == 0)
                    return string.IsNullOrEmpty(networkName)
                        ? "Cizimde pipe network yok."
                        : $"Network '{networkName}' bulunamadi.";

                return $"Yuzey atandi: {netCount} network, {structCount} structure, {pipeCount} pipe.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }
    }
}