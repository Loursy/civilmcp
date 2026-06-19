using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System.Collections.Generic;
using System.Linq;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace CivilClaudePlugin.Helpers
{
    public static class AlignmentCreator
    {
        public static string ListStylesAndLayers()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("ALIGNMENT STILLERI:");
                foreach (ObjectId id in civilDoc.Styles.AlignmentStyles)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.AlignmentStyle;
                        if (style != null) sb.AppendLine($"  - {style.Name}");
                        tr.Commit();
                    }
                }

                sb.AppendLine();
                sb.AppendLine("LABEL SET STILLERI:");
                foreach (ObjectId id in civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.AlignmentLabelSetStyle;
                        if (style != null) sb.AppendLine($"  - {style.Name}");
                        tr.Commit();
                    }
                }

                sb.AppendLine();
                sb.AppendLine("KATMANLAR:");
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var lt = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (ObjectId id in lt)
                    {
                        var layer = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                        if (layer != null) sb.AppendLine($"  - {layer.Name}");
                    }
                    tr.Commit();
                }

                sb.AppendLine();
                sb.AppendLine("MEVCUT ALIGNMENTLAR (ana guzergah icin):");
                foreach (ObjectId id in civilDoc.GetAlignmentIds())
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (al != null) sb.AppendLine($"  - {al.Name}");
                        tr.Commit();
                    }
                }

                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        public static string CreateFromFilter(string colorName, string layerName, string styleName, string labelSetName, string targetLayer, string mainAlignmentName, int startNumber)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var ed = doc.Editor;
                var filters = new List<TypedValue>
                {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                };
                if (!string.IsNullOrEmpty(layerName))
                    filters.Add(new TypedValue((int)DxfCode.LayerName, layerName));

                var result = ed.SelectAll(new SelectionFilter(filters.ToArray()));
                if (result.Status != PromptStatus.OK || result.Value.Count == 0)
                    return "Hic polyline bulunamadi.";

                int targetColor = string.IsNullOrEmpty(colorName) ? -1 : GetColorIndex(colorName);
                var ids = new List<ObjectId>();

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject so in result.Value)
                    {
                        var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as AcEntity;
                        if (ent == null) continue;

                        if (targetColor >= 0)
                        {
                            int entColor = ent.ColorIndex == 256
                                ? ((LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead)).Color.ColorIndex
                                : ent.ColorIndex;
                            if (entColor != targetColor) continue;
                        }
                        ids.Add(so.ObjectId);
                    }
                    tr.Commit();
                }

                if (ids.Count == 0)
                    return "Filtreye uyan polyline bulunamadi.";

                return CreateAlignments(doc, ids, styleName, labelSetName, targetLayer, mainAlignmentName, startNumber);
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        public static string CreateFromSelection(string styleName, string labelSetName, string targetLayer, string mainAlignmentName, int startNumber)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var ed = doc.Editor;
                var selResult = ed.SelectImplied();

                if (selResult.Status != PromptStatus.OK || selResult.Value.Count == 0)
                    return "Once cizimde polyline secmelisiniz.";

                var ids = new List<ObjectId>();
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject so in selResult.Value)
                    {
                        var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                        if (ent is Polyline)
                            ids.Add(so.ObjectId);
                    }
                    tr.Commit();
                }

                if (ids.Count == 0)
                    return "Secimde polyline bulunamadi.";

                return CreateAlignments(doc, ids, styleName, labelSetName, targetLayer, mainAlignmentName, startNumber);
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        private static string CreateAlignments(Document doc, List<ObjectId> polylineIds, string styleName, string labelSetName, string targetLayer, string mainAlignmentName, int startNumber)
        {
            var civilDoc = CivilApplication.ActiveDocument;

            ObjectId styleId = GetAlignmentStyleByName(civilDoc, doc, styleName);
            ObjectId labelSetId = GetLabelSetByName(civilDoc, doc, labelSetName);
            ObjectId layerId = GetLayerByName(doc, targetLayer);

            var createdIds = new List<ObjectId>();

            using (doc.LockDocument())
            {
                int tmp = 0;
                foreach (var plineId in polylineIds)
                {
                    try
                    {
                        tmp++;
                        string tempName = $"__tmp_align_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}_{tmp}";

                        var plOpts = new PolylineOptions
                        {
                            PlineId = plineId,
                            AddCurvesBetweenTangents = false,
                            EraseExistingEntities = false
                        };

                        ObjectId newId = Alignment.Create(
                            civilDoc,
                            plOpts,
                            tempName,
                            ObjectId.Null,
                            layerId,
                            styleId,
                            labelSetId);

                        createdIds.Add(newId);
                    }
                    catch { }
                }
            }

            if (createdIds.Count == 0)
                return "Hic alignment olusturulamadi.";

            // Chainage'a gore sirala ve yeniden adlandir
            string renameResult = RenameByChainage(doc, civilDoc, createdIds, mainAlignmentName, startNumber);

            return $"{createdIds.Count} alignment olusturuldu. {renameResult}";
        }

        private static string RenameByChainage(Document doc, CivilDocument civilDoc, List<ObjectId> createdIds, string mainAlignmentName, int startNumber)
        {
            try
            {
                // Ana guzergahi bul
                ObjectId mainId = ObjectId.Null;
                foreach (ObjectId id in civilDoc.GetAlignmentIds())
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (al != null && al.Name.Equals(mainAlignmentName, System.StringComparison.OrdinalIgnoreCase))
                            mainId = id;
                        tr.Commit();
                    }
                    if (mainId != ObjectId.Null) break;
                }

                if (mainId == ObjectId.Null)
                    return $"(Ana guzergah '{mainAlignmentName}' bulunamadi, isimlendirme yapilamadi.)";

                // Her created alignment icin baslangic noktasinin ana guzergahtaki chainage'ini bul
                var stationList = new List<KeyValuePair<ObjectId, double>>();

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var mainAl = tr.GetObject(mainId, OpenMode.ForRead) as Alignment;

                    foreach (var cid in createdIds)
                    {
                        var al = tr.GetObject(cid, OpenMode.ForRead) as Alignment;
                        if (al == null) continue;

                        // Alignment baslangic noktasinin koordinatini al
                        double e = 0, n = 0;
                        al.PointLocation(al.StartingStation, 0, ref e, ref n);

                        // Ana guzergaha izdusur
                        double sta = 0, off = 0;
                        try
                        {
                            mainAl.StationOffset(e, n, ref sta, ref off);
                        }
                        catch
                        {
                            sta = double.MaxValue; // izdusurulemezse en sona
                        }

                        stationList.Add(new KeyValuePair<ObjectId, double>(cid, sta));
                    }
                    tr.Commit();
                }

                // Chainage'a gore sirala (kucukten buyuge)
                var sorted = stationList.OrderBy(x => x.Value).ToList();

                // Mevcut isimleri topla (cakisma kontrolu icin)
                var existingNames = new HashSet<string>();
                foreach (ObjectId id in civilDoc.GetAlignmentIds())
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (al != null && !createdIds.Contains(id))
                            existingNames.Add(al.Name);
                        tr.Commit();
                    }
                }

                // Sirayla yeniden adlandir
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    int num = startNumber > 0 ? startNumber : 1;
                    foreach (var kvp in sorted)
                    {
                        var al = tr.GetObject(kvp.Key, OpenMode.ForWrite) as Alignment;
                        if (al == null) continue;

                        string newName = $"{num}";
                        while (existingNames.Contains(newName))
                        {
                            num++;
                            newName = $"{num}";
                        }
                        al.Name = newName;
                        existingNames.Add(newName);
                        num++;
                    }
                    tr.Commit();
                }

                return "Chainage'a gore isimlendirildi.";
            }
            catch (System.Exception ex)
            {
                return $"(Isimlendirme hatasi: {ex.Message})";
            }
        }

        private static ObjectId GetAlignmentStyleByName(CivilDocument civilDoc, Document doc, string name)
        {
            try
            {
                foreach (ObjectId id in civilDoc.Styles.AlignmentStyles)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.AlignmentStyle;
                        tr.Commit();
                        if (style != null && (string.IsNullOrEmpty(name) || style.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                            return id;
                    }
                }
                if (civilDoc.Styles.AlignmentStyles.Count > 0)
                    return civilDoc.Styles.AlignmentStyles[0];
            }
            catch { }
            return ObjectId.Null;
        }

        private static ObjectId GetLabelSetByName(CivilDocument civilDoc, Document doc, string name)
        {
            try
            {
                foreach (ObjectId id in civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.AlignmentLabelSetStyle;
                        tr.Commit();
                        if (style != null && (string.IsNullOrEmpty(name) || style.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                            return id;
                    }
                }
                if (civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles.Count > 0)
                    return civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles[0];
            }
            catch { }
            return ObjectId.Null;
        }

        private static ObjectId GetLayerByName(Document doc, string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return doc.Database.LayerZero;

            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var lt = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt.Has(layerName))
                    {
                        var id = lt[layerName];
                        tr.Commit();
                        return id;
                    }

                    var ltWrite = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
                    var newLayer = new LayerTableRecord { Name = layerName };
                    var newId = ltWrite.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                    tr.Commit();
                    return newId;
                }
            }
            catch { }
            return doc.Database.LayerZero;
        }

        private static int GetColorIndex(string colorName)
        {
            switch (colorName.ToLower().Trim())
            {
                case "kırmızı": case "kirmizi": case "red": return 1;
                case "sarı": case "sari": case "yellow": return 2;
                case "yeşil": case "yesil": case "green": return 3;
                case "cyan": return 4;
                case "mavi": case "blue": return 5;
                case "magenta": case "pembe": return 6;
                case "beyaz": case "white": return 7;
                default: return -1;
            }
        }
    }
}