using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System.Collections.Generic;

namespace CivilClaudePlugin.Helpers
{
    public static class AlignmentRenamer
    {
        // Tek bir alignment'i yeniden adlandir
        public static string RenameOne(string oldName, string newName)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;

                if (NameExists(doc, civilDoc, newName))
                    return $"'{newName}' adi zaten kullaniliyor.";

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    bool found = false;
                    foreach (ObjectId id in civilDoc.GetAlignmentIds())
                    {
                        var al = tr.GetObject(id, OpenMode.ForWrite) as Alignment;
                        if (al != null && al.Name.Equals(oldName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            al.Name = newName;
                            found = true;
                            break;
                        }
                    }
                    tr.Commit();

                    return found
                        ? $"'{oldName}' -> '{newName}' olarak degistirildi."
                        : $"'{oldName}' adinda alignment bulunamadi.";
                }
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Tum alignment isimlerine onek/sonek ekle
        public static string AddPrefixSuffix(string prefix, string suffix)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                int count = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civilDoc.GetAlignmentIds())
                    {
                        var al = tr.GetObject(id, OpenMode.ForWrite) as Alignment;
                        if (al != null)
                        {
                            al.Name = $"{prefix}{al.Name}{suffix}";
                            count++;
                        }
                    }
                    tr.Commit();
                }

                return $"{count} alignment yeniden adlandirildi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // Tum alignment isimlerinde bul-degistir
        public static string FindReplace(string find, string replace)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                int count = 0;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civilDoc.GetAlignmentIds())
                    {
                        var al = tr.GetObject(id, OpenMode.ForWrite) as Alignment;
                        if (al != null && al.Name.Contains(find))
                        {
                            al.Name = al.Name.Replace(find, replace);
                            count++;
                        }
                    }
                    tr.Commit();
                }

                return $"{count} alignment isminde degisiklik yapildi.";
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        public static string ListAlignments()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Aktif drawing yok.";

                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("MEVCUT ALIGNMENTLAR:");

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civilDoc.GetAlignmentIds())
                    {
                        var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (al != null) sb.AppendLine($"  - {al.Name}");
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

        private static bool NameExists(Document doc, CivilDocument civilDoc, string name)
        {
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civilDoc.GetAlignmentIds())
                {
                    var al = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                    if (al != null && al.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        tr.Commit();
                        return true;
                    }
                }
                tr.Commit();
            }
            return false;
        }
    }
}