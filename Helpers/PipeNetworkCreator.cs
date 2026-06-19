using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

namespace CivilClaudePlugin.Helpers
{
    public static class PipeNetworkCreator
    {
        public static string ListParts()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId plId in civilDoc.Styles.PartsListSet)
                    {
                        var partsList = tr.GetObject(plId, OpenMode.ForRead) as PartsList;
                        if (partsList == null) continue;

                        sb.AppendLine($"PARTS LIST: {partsList.Name}");

                        sb.AppendLine("  PIPE aileleri:");
                        foreach (ObjectId fid in partsList.GetPartFamilyIdsByDomain(DomainType.Pipe))
                        {
                            var fam = tr.GetObject(fid, OpenMode.ForRead) as PartFamily;
                            if (fam != null) sb.AppendLine($"    - {fam.Name}");
                        }

                        sb.AppendLine("  STRUCTURE aileleri:");
                        foreach (ObjectId fid in partsList.GetPartFamilyIdsByDomain(DomainType.Structure))
                        {
                            var fam = tr.GetObject(fid, OpenMode.ForRead) as PartFamily;
                            if (fam != null) sb.AppendLine($"    - {fam.Name}");
                        }
                        // Not: boyutlar aileye gore filtreli olarak ListSizes ile gosterilir
                        sb.AppendLine();
                    }

                    sb.AppendLine("PIPE STILLERI:");
                    foreach (ObjectId id in civilDoc.Styles.PipeStyles)
                    {
                        var st = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PipeStyle;
                        if (st != null) sb.AppendLine($"  - {st.Name}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("STRUCTURE STILLERI:");
                    foreach (ObjectId id in civilDoc.Styles.StructureStyles)
                    {
                        var st = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.StructureStyle;
                        if (st != null) sb.AppendLine($"  - {st.Name}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("YUZEYLER (reference surface):");
                    foreach (ObjectId id in civilDoc.GetSurfaceIds())
                    {
                        var surf = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                        if (surf != null) sb.AppendLine($"  - {surf.Name}");
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

        // Pipe network olusturma akisinda TEK bir secimin seceneklerini dondurur.
        // field: partsList | pipeFamily | structFamily | pipeStyle | structStyle | surface
        // partsList: pipeFamily/structFamily icin hangi parts list (bos = tum parts list'ler)
        public static string ListNetworkOptions(string field, string partsListName)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();
                string f = (field ?? "").Trim().ToLowerInvariant();
                var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (f == "partslist")
                    {
                        sb.AppendLine("PARTS LIST:");
                        foreach (ObjectId plId in civilDoc.Styles.PartsListSet)
                        {
                            var pl = tr.GetObject(plId, OpenMode.ForRead) as PartsList;
                            if (pl != null && seen.Add(pl.Name)) sb.AppendLine($"  - {pl.Name}");
                        }
                    }
                    else if (f == "pipefamily" || f == "structfamily")
                    {
                        var domain = (f == "pipefamily") ? DomainType.Pipe : DomainType.Structure;
                        sb.AppendLine((f == "pipefamily") ? "PIPE AILESI:" : "STRUCTURE AILESI:");
                        foreach (ObjectId plId in civilDoc.Styles.PartsListSet)
                        {
                            var pl = tr.GetObject(plId, OpenMode.ForRead) as PartsList;
                            if (pl == null) continue;
                            if (!string.IsNullOrWhiteSpace(partsListName)
                                && !pl.Name.Equals(partsListName, System.StringComparison.OrdinalIgnoreCase)) continue;
                            foreach (ObjectId fid in pl.GetPartFamilyIdsByDomain(domain))
                            {
                                var fam = tr.GetObject(fid, OpenMode.ForRead) as PartFamily;
                                if (fam != null && seen.Add(fam.Name)) sb.AppendLine($"  - {fam.Name}");
                            }
                        }
                    }
                    else if (f == "pipestyle")
                    {
                        sb.AppendLine("PIPE STILI:");
                        foreach (ObjectId id in civilDoc.Styles.PipeStyles)
                        {
                            var st = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PipeStyle;
                            if (st != null && seen.Add(st.Name)) sb.AppendLine($"  - {st.Name}");
                        }
                    }
                    else if (f == "structstyle")
                    {
                        sb.AppendLine("STRUCTURE STILI:");
                        foreach (ObjectId id in civilDoc.Styles.StructureStyles)
                        {
                            var st = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.StructureStyle;
                            if (st != null && seen.Add(st.Name)) sb.AppendLine($"  - {st.Name}");
                        }
                    }
                    else if (f == "surface")
                    {
                        sb.AppendLine("YUZEY:");
                        foreach (ObjectId id in civilDoc.GetSurfaceIds())
                        {
                            var surf = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                            if (surf != null && seen.Add(surf.Name)) sb.AppendLine($"  - {surf.Name}");
                        }
                    }
                    else
                    {
                        tr.Commit();
                        return "Bilinmeyen secim turu: " + field;
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

        // Sadece secilen pipe/structure ailelerine ait boyutlari listele
        // (bos birakilan aile turu atlanir)
        public static string ListSizes(string pipeFamilyName, string structureFamilyName)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();
                var pipeSeen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                var structSeen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId plId in civilDoc.Styles.PartsListSet)
                    {
                        var partsList = tr.GetObject(plId, OpenMode.ForRead) as PartsList;
                        if (partsList == null) continue;

                        if (!string.IsNullOrWhiteSpace(pipeFamilyName))
                        {
                            foreach (ObjectId fid in partsList.GetPartFamilyIdsByDomain(DomainType.Pipe))
                            {
                                var fam = tr.GetObject(fid, OpenMode.ForRead) as PartFamily;
                                if (fam == null) continue;
                                if (!fam.Name.Equals(pipeFamilyName, System.StringComparison.OrdinalIgnoreCase)) continue;
                                for (int s = 0; s < fam.PartSizeCount; s++)
                                {
                                    var ps = tr.GetObject(fam[s], OpenMode.ForRead) as PartSize;
                                    if (ps != null) pipeSeen.Add(ps.Name);
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(structureFamilyName))
                        {
                            foreach (ObjectId fid in partsList.GetPartFamilyIdsByDomain(DomainType.Structure))
                            {
                                var fam = tr.GetObject(fid, OpenMode.ForRead) as PartFamily;
                                if (fam == null) continue;
                                if (!fam.Name.Equals(structureFamilyName, System.StringComparison.OrdinalIgnoreCase)) continue;
                                for (int s = 0; s < fam.PartSizeCount; s++)
                                {
                                    var ps = tr.GetObject(fam[s], OpenMode.ForRead) as PartSize;
                                    if (ps != null) structSeen.Add(ps.Name);
                                }
                            }
                        }
                    }
                    tr.Commit();
                }

                if (!string.IsNullOrWhiteSpace(pipeFamilyName))
                {
                    sb.AppendLine($"PIPE boyutlari ({pipeFamilyName}):");
                    if (pipeSeen.Count == 0) sb.AppendLine("  (boyut bulunamadi)");
                    foreach (var name in pipeSeen) sb.AppendLine($"  - {name}");
                }

                if (!string.IsNullOrWhiteSpace(structureFamilyName))
                {
                    sb.AppendLine($"STRUCTURE boyutlari ({structureFamilyName}):");
                    if (structSeen.Count == 0) sb.AppendLine("  (boyut bulunamadi)");
                    foreach (var name in structSeen) sb.AppendLine($"  - {name}");
                }

                if (sb.Length == 0) return "Boyut listelemek icin pipe/structure ailesi belirtilmeli.";
                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // alignmentNames: virgulle ayrilmis alignment isimleri (bos = cizimdeki TUM alignmentlar)
        // surfaceName: referans yuzey (bos = yuzeysiz olustur; kot hesabi atlanir)
        public static string CreateFromAlignments(string partsListName, string pipeFamilyName, string structureFamilyName,
            string pipeStyleName, string structureStyleName, string surfaceName, string flowDirection,
            string pipeSizeName, string structSizeName, string alignmentNames)
        {
            try
            {
                var check = PreCheck.HasAlignment();
                if (check != null) return check;
                check = PreCheck.SurfaceExists(surfaceName);
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                int created = 0;
                var sb = new System.Text.StringBuilder();

                // Alignment isim filtresi (bos = tum alignmentlar)
                var nameSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(alignmentNames))
                    foreach (var n in alignmentNames.Split(','))
                    {
                        var t = n.Trim();
                        if (t.Length > 0) nameSet.Add(t);
                    }
                bool hasNameFilter = nameSet.Count > 0;

                var alignmentIds = new List<ObjectId>();
                ObjectId partsListId = ObjectId.Null;
                ObjectId pipeStyleId = ObjectId.Null;
                ObjectId structStyleId = ObjectId.Null;
                ObjectId surfaceId = ObjectId.Null;

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId plId in civilDoc.Styles.PartsListSet)
                    {
                        var pl = tr.GetObject(plId, OpenMode.ForRead) as PartsList;
                        if (pl != null && pl.Name.Equals(partsListName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            partsListId = plId;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(pipeStyleName))
                        foreach (ObjectId id in civilDoc.Styles.PipeStyles)
                        {
                            var st = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PipeStyle;
                            if (st != null && st.Name.Equals(pipeStyleName, System.StringComparison.OrdinalIgnoreCase)) { pipeStyleId = id; break; }
                        }

                    if (!string.IsNullOrEmpty(structureStyleName))
                        foreach (ObjectId id in civilDoc.Styles.StructureStyles)
                        {
                            var st = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.StructureStyle;
                            if (st != null && st.Name.Equals(structureStyleName, System.StringComparison.OrdinalIgnoreCase)) { structStyleId = id; break; }
                        }

                    if (!string.IsNullOrEmpty(surfaceName))
                        foreach (ObjectId id in civilDoc.GetSurfaceIds())
                        {
                            var surf = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                            if (surf != null && surf.Name.Equals(surfaceName, System.StringComparison.OrdinalIgnoreCase)) { surfaceId = id; break; }
                        }

                    // Alignment'lari topla (isim filtresi varsa yalnizca eslesenler)
                    foreach (ObjectId id in civilDoc.GetAlignmentIds())
                    {
                        if (hasNameFilter)
                        {
                            var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                            if (al == null || !nameSet.Contains(al.Name)) continue;
                        }
                        alignmentIds.Add(id);
                    }

                    tr.Commit();
                }

                if (partsListId == ObjectId.Null)
                    return $"Parts list '{partsListName}' bulunamadi.";
                if (hasNameFilter && alignmentIds.Count == 0)
                    return "Belirtilen isimde alignment bulunamadi. Isimleri kontrol edin.";

                // Yuzey opsiyonel: ad verilmediyse yuzeysiz devam edilir (kot hesabi atlanir).
                bool hasSurface = surfaceId != ObjectId.Null;

                using (doc.LockDocument())
                {
                    foreach (var alId in alignmentIds)
                    {
                        try
                        {
                            string alName;
                            var vertexPoints = new List<Point3d>();

                            using (var tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                var al = tr.GetObject(alId, OpenMode.ForRead) as Alignment;
                                alName = al.Name;

                                // GetPolyline() cizime gecici bir polyline ekler ve id'sini doner.
                                // Vertex'leri okuduktan sonra bu polyline'i silmeliyiz; yoksa pipe
                                // network olustuktan sonra cizimde artik polyline'lar kalir.
                                ObjectId polyId = al.GetPolyline();
                                var poly = tr.GetObject(polyId, OpenMode.ForWrite) as Polyline;
                                if (poly != null)
                                {
                                    int vn = poly.NumberOfVertices;
                                    for (int vi = 0; vi < vn; vi++)
                                    {
                                        var p = poly.GetPoint3dAt(vi);
                                        vertexPoints.Add(new Point3d(p.X, p.Y, 0));
                                    }
                                    poly.Erase();
                                }
                                tr.Commit();
                            }

                            if (vertexPoints.Count < 2)
                            {
                                sb.AppendLine($"  {alName}: yeterli vertex yok, atlandi.");
                                continue;
                            }

                            string netName = alName;
                            ObjectId netId = Network.Create(civilDoc, ref netName);

                            using (var tr = doc.Database.TransactionManager.StartTransaction())
                            {
                                var network = tr.GetObject(netId, OpenMode.ForWrite) as Network;
                                network.PartsListId = partsListId;
                                network.ReferenceAlignmentId = alId;
                                if (hasSurface)
                                    network.ReferenceSurfaceId = surfaceId;

                                var pl = tr.GetObject(partsListId, OpenMode.ForRead) as PartsList;
                                ObjectId pipeFamilyId = pl[pipeFamilyName];
                                ObjectId structFamilyId = pl[structureFamilyName];

                                var pipeFam = tr.GetObject(pipeFamilyId, OpenMode.ForRead) as PartFamily;
                                var structFam = tr.GetObject(structFamilyId, OpenMode.ForRead) as PartFamily;

                                // Boyut sec (isimle eslesen; yoksa ilk boyut)
                                ObjectId pipeSizeId = FindSizeByName(tr, pipeFam, pipeSizeName);
                                ObjectId structSizeId = FindSizeByName(tr, structFam, structSizeName);

                                // Yuzey varsa her vertex'te kotunu oku; yoksa kot hesabi atlanir.
                                var surfElevs = new List<double>();
                                if (hasSurface)
                                {
                                    var surface = tr.GetObject(surfaceId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                                    foreach (var pt in vertexPoints)
                                    {
                                        double z = 0;
                                        try { z = surface.FindElevationAtXY(pt.X, pt.Y); } catch { z = 0; }
                                        surfElevs.Add(z);
                                    }
                                }

                                // Structure'lari ekle
                                var structIds = new List<ObjectId>();
                                foreach (var pt in vertexPoints)
                                {
                                    ObjectId sid = ObjectId.Null;
                                    network.AddStructure(structFamilyId, structSizeId, pt, 0, ref sid, false);
                                    if (structStyleId != ObjectId.Null && sid != ObjectId.Null)
                                    {
                                        var stx = tr.GetObject(sid, OpenMode.ForWrite) as Structure;
                                        if (stx != null) stx.StyleId = structStyleId;
                                    }
                                    structIds.Add(sid);
                                }

                                // Pipe'lari ekle ve bagla
                                var pipeIds = new List<ObjectId>();
                                for (int vi = 0; vi < vertexPoints.Count - 1; vi++)
                                {
                                    ObjectId pipeId = ObjectId.Null;
                                    var seg = new LineSegment3d(vertexPoints[vi], vertexPoints[vi + 1]);
                                    network.AddLinePipe(pipeFamilyId, pipeSizeId, seg, ref pipeId, false);

                                    if (pipeId != ObjectId.Null)
                                    {
                                        var pipe = tr.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
                                        if (pipe != null)
                                        {
                                            if (pipeStyleId != ObjectId.Null)
                                                pipe.StyleId = pipeStyleId;

                                            try
                                            {
                                                if (structIds[vi] != ObjectId.Null)
                                                    pipe.ConnectToStructure(ConnectorPositionType.Start, structIds[vi], true);
                                                if (structIds[vi + 1] != ObjectId.Null)
                                                    pipe.ConnectToStructure(ConnectorPositionType.End, structIds[vi + 1], true);
                                            }
                                            catch { }
                                        }
                                        pipeIds.Add(pipeId);
                                    }
                                }

                                tr.Commit();

                                // Kotlari yuzeye gore ayri transaction'da hesapla (yuzey yoksa atla)
                                if (hasSurface)
                                    SetPipeElevations(doc, pipeIds, surfElevs, flowDirection);
                            }

                            created++;
                            sb.AppendLine($"  {alName}: olusturuldu ({vertexPoints.Count} structure).");
                        }
                        catch (System.Exception exAl)
                        {
                            sb.AppendLine($"  Hata: {exAl.Message}");
                        }
                    }
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine(hasSurface
                    ? $"{created} pipe network olusturuldu (kotlar yuzeye/akis yonune gore ayarlandi)."
                    : $"{created} pipe network olusturuldu (yuzey secilmedi; kotlar varsayilan).");
                result.AppendLine();
                result.Append(sb.ToString());
                return result.ToString();
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Pipe kotlarini her borunun KENDI arazisine gore sabit cover ile ayarla.
        // Her boru iki ucunda yuzey - cover - cap; segment ici min slope; akis yonunde structure'da geri akis engellenir.
        // flowDirection: "start" (alignment basi->sonu), "end" (sonu->basi), "terrain" (yuksekten alcaga)
        private static void SetPipeElevations(Document doc, List<ObjectId> pipeIds, List<double> surfElevs, string flowDirection)
        {
            if (pipeIds.Count == 0) return;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                int vCount = surfElevs.Count;

                // Akis yonu: reversed=true ise akis sondan basa
                bool reversed = false;
                if (flowDirection == "end")
                    reversed = true;
                else if (flowDirection == "terrain")
                {
                    if (surfElevs[0] < surfElevs[vCount - 1])
                        reversed = true;
                }
                // "start" veya bos => reversed=false

                // Pipe parametreleri
                var minCover = new List<double>();
                var minSlope = new List<double>();
                var outerD = new List<double>();
                var innerR = new List<double>();
                var length = new List<double>();
                for (int i = 0; i < pipeIds.Count; i++)
                {
                    var pipe = tr.GetObject(pipeIds[i], OpenMode.ForRead) as Pipe;
                    minCover.Add(ReadRuleValue(tr, pipe, "Minimum Cover", 1.0));
                    minSlope.Add(ReadRuleValue(tr, pipe, "Minimum Slope", 0.5));
                    outerD.Add(pipe.OuterDiameterOrWidth);
                    innerR.Add(pipe.InnerDiameterOrWidth / 2.0);
                    length.Add(pipe.Length2DCenterToCenter);
                }

                // Her vertex icin invert: her boru kendi iki ucunu yuzeye gore cover'lar.
                // Bir vertex birden fazla boruya komsu olabilir; en dusuk talebi aliriz (guvenli taraf).
                var invAt = new double[vCount];
                var hasInv = new bool[vCount];

                for (int p = 0; p < pipeIds.Count; p++)
                {
                    int vA = p;
                    int vB = p + 1;

                    double invA = surfElevs[vA] - minCover[p] - outerD[p];
                    double invB = surfElevs[vB] - minCover[p] - outerD[p];

                    if (!hasInv[vA] || invA < invAt[vA]) { invAt[vA] = invA; hasInv[vA] = true; }
                    if (!hasInv[vB] || invB < invAt[vB]) { invAt[vB] = invB; hasInv[vB] = true; }
                }

                // Akis yonunde sirayla: her segmentte min slope sagla ve geri akisi engelle.
                System.Func<int, int> V = (k) => reversed ? (vCount - 1 - k) : k;
                System.Func<int, int> P = (k) => reversed ? (pipeIds.Count - 1 - k) : k;

                for (int k = 0; k < pipeIds.Count; k++)
                {
                    int vUp = V(k);
                    int vDn = V(k + 1);
                    int p = P(k);

                    double drop = length[p] * (minSlope[p] / 100.0);
                    double maxAllowedDn = invAt[vUp] - drop;
                    if (invAt[vDn] > maxAllowedDn)
                        invAt[vDn] = maxAllowedDn;
                }

                // Pipe'lara uygula (Start=vertex p, End=vertex p+1)
                for (int p = 0; p < pipeIds.Count; p++)
                {
                    var pipe = tr.GetObject(pipeIds[p], OpenMode.ForWrite) as Pipe;
                    if (pipe == null) continue;

                    double invStartVertex = invAt[p];
                    double invEndVertex = invAt[p + 1];

                    var sp = pipe.StartPoint;
                    var ep = pipe.EndPoint;
                    pipe.StartPoint = new Point3d(sp.X, sp.Y, invStartVertex + innerR[p]);
                    pipe.EndPoint = new Point3d(ep.X, ep.Y, invEndVertex + innerR[p]);
                }

                tr.Commit();
            }
        }

        // Aile icinde isimle eslesen boyutu bul; bulunmazsa ilk boyut
        private static ObjectId FindSizeByName(Transaction tr, PartFamily fam, string sizeName)
        {
            if (fam == null) return ObjectId.Null;
            if (fam.PartSizeCount == 0) return ObjectId.Null;

            if (!string.IsNullOrWhiteSpace(sizeName))
            {
                for (int s = 0; s < fam.PartSizeCount; s++)
                {
                    var ps = tr.GetObject(fam[s], OpenMode.ForRead) as PartSize;
                    if (ps != null && ps.Name.Equals(sizeName, System.StringComparison.OrdinalIgnoreCase))
                        return fam[s];
                }
                // Tam eslesme yoksa, iceren ilk boyutu dene
                for (int s = 0; s < fam.PartSizeCount; s++)
                {
                    var ps = tr.GetObject(fam[s], OpenMode.ForRead) as PartSize;
                    if (ps != null && ps.Name.IndexOf(sizeName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return fam[s];
                }
            }
            return fam[0];
        }

        // Pipe'in rule set'inden parametre degeri oku (DisplayName ile)
        private static double ReadRuleValue(Transaction tr, Pipe pipe, string paramName, double defaultValue)
        {
            try
            {
                ObjectId ruleSetId = pipe.RuleSetStyleId;
                if (ruleSetId == ObjectId.Null) return defaultValue;

                var ruleSet = tr.GetObject(ruleSetId, OpenMode.ForRead) as PipeRuleSetStyle;
                if (ruleSet == null) return defaultValue;

                foreach (ObjectId rid in ruleSet.GetRuleIds())
                {
                    var rule = tr.GetObject(rid, OpenMode.ForRead) as NetworkRule;
                    if (rule == null) continue;
                    foreach (var pdbl in rule.ParamsDouble)
                    {
                        if (pdbl.DisplayName != null &&
                            pdbl.DisplayName.IndexOf(paramName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return pdbl.Value;
                        }
                    }
                }
            }
            catch { }
            return defaultValue;
        }
    }
}