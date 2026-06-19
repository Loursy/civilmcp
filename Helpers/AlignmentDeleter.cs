using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    public static class AlignmentDeleter
    {
        // Cizimde secili olan alignment'lari sil
        public static string DeleteSelected()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var ed = doc.Editor;
                var sel = ed.SelectImplied();
                if (sel.Status != PromptStatus.OK || sel.Value.Count == 0)
                    return "Once cizimde alignment secmelisiniz.";

                int deleted = 0, failed = 0;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject so in sel.Value)
                    {
                        var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                        if (!(obj is Alignment)) continue;
                        if (TryErase(tr, so.ObjectId)) deleted++; else failed++;
                    }
                    tr.Commit();
                }

                if (deleted == 0 && failed == 0)
                    return "Secimde alignment bulunamadi.";
                return Report(deleted, failed);
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Ozelliklere gore alignment sil.
        // names: virgulle ayrilmis tam isimler (bos = filtre yok)
        // nameContains: isim icinde gecen metin (bos = filtre yok)
        // layer: alignment'in katmani (bos = filtre yok)
        // style: alignment stili (bos = filtre yok)
        // descriptionContains: aciklama icinde gecen metin (bos = filtre yok)
        // minLength/maxLength: uzunluk araligi (double.NaN = filtre yok)
        // deleteAll: true ise TUM alignment'lari sil (diger filtreler yok sayilir)
        // Birden fazla filtre verilirse hepsini birden saglayanlar silinir (VE mantigi).
        public static string DeleteByFilter(string names, string nameContains, string layer, string style,
            string descriptionContains, double minLength, double maxLength, bool deleteAll)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;

                var nameSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(names))
                {
                    foreach (var n in names.Split(','))
                    {
                        var t = n.Trim();
                        if (t.Length > 0) nameSet.Add(t);
                    }
                }

                bool hasNameFilter = nameSet.Count > 0;
                bool hasContains = !string.IsNullOrWhiteSpace(nameContains);
                bool hasLayer = !string.IsNullOrWhiteSpace(layer);
                bool hasStyle = !string.IsNullOrWhiteSpace(style);
                bool hasDesc = !string.IsNullOrWhiteSpace(descriptionContains);
                bool hasMinLen = !double.IsNaN(minLength);
                bool hasMaxLen = !double.IsNaN(maxLength);

                if (!deleteAll && !hasNameFilter && !hasContains && !hasLayer && !hasStyle
                    && !hasDesc && !hasMinLen && !hasMaxLen)
                    return "Silmek icin en az bir olcut belirtin (isim, katman, stil, aciklama, uzunluk) ya da tumunu silmek istedigini soyle.";

                int deleted = 0, failed = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var toErase = new List<ObjectId>();

                    foreach (ObjectId id in civilDoc.GetAlignmentIds())
                    {
                        var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (al == null) continue;

                        if (!deleteAll)
                        {
                            if (hasNameFilter && !nameSet.Contains(al.Name)) continue;
                            if (hasContains && al.Name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                            if (hasLayer && !al.Layer.Equals(layer, System.StringComparison.OrdinalIgnoreCase)) continue;
                            if (hasStyle && !GetStyleName(tr, al).Equals(style, System.StringComparison.OrdinalIgnoreCase)) continue;
                            if (hasDesc && (al.Description ?? string.Empty).IndexOf(descriptionContains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                            if (hasMinLen && al.Length < minLength) continue;
                            if (hasMaxLen && al.Length > maxLength) continue;
                        }

                        toErase.Add(id);
                    }

                    foreach (var id in toErase)
                    {
                        if (TryErase(tr, id)) deleted++; else failed++;
                    }

                    tr.Commit();
                }

                if (deleted == 0 && failed == 0)
                    return "Olcutlere uyan alignment bulunamadi.";
                return Report(deleted, failed);
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        private static bool TryErase(Transaction tr, ObjectId id)
        {
            try
            {
                var al = tr.GetObject(id, OpenMode.ForWrite) as Alignment;
                if (al == null || al.IsErased) return false;
                al.Erase();
                return true;
            }
            catch
            {
                // Profil/yuzey gibi bagimliliklar varsa silinemeyebilir
                return false;
            }
        }

        private static string GetStyleName(Transaction tr, Alignment al)
        {
            try
            {
                var style = tr.GetObject(al.StyleId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.AlignmentStyle;
                return style != null ? style.Name : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Report(int deleted, int failed)
        {
            if (failed > 0)
                return $"{deleted} alignment silindi, {failed} tanesi silinemedi (bagimli nesneleri olabilir).";
            return $"{deleted} alignment silindi.";
        }
    }
}
