using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace CivilClaudePlugin.Helpers
{
    // COGO point (Civil 3D nokta) listeleme ve filtreye gore (tekli/toplu) secme.
    // FilterAndSelect tum verilen kriterleri AYNI ANDA saglayan noktalari secer (VE mantigi):
    // tek bir nokta numarasi vererek tekli, aralik/filtre vererek toplu secim yapilir.
    public static class PointSelector
    {
        // ---- NOKTA GRUPLARINI LISTELE ----
        public static string ListPointGroups()
        {
            try
            {
                var check = PreCheck.CivilDoc();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                var sb = new System.Text.StringBuilder();
                int count = 0;

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civilDoc.PointGroups)
                    {
                        var pg = tr.GetObject(id, OpenMode.ForRead) as PointGroup;
                        if (pg == null) continue;
                        int n = 0;
                        try { n = pg.GetPointNumbers().Length; } catch { }
                        sb.AppendLine($"{pg.Name} ({n} nokta)");
                        count++;
                    }
                    tr.Commit();
                }

                if (count == 0) return "Cizimde nokta grubu yok.";
                return $"{count} nokta grubu:\n\n" + sb.ToString();
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- NOKTA OZELLIKLERINI LISTELE ----
        // selectedOnly: true ise yalnizca cizimde secili noktalar.
        public static string ListProperties(bool selectedOnly)
        {
            try
            {
                var check = PreCheck.HasCogoPoints();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                var ed = doc.Editor;
                var sb = new System.Text.StringBuilder();
                int count = 0;
                const int maxShown = 300;

                var selSet = new HashSet<ObjectId>();
                if (selectedOnly)
                {
                    var sel = ed.SelectImplied();
                    if (sel.Status == PromptStatus.OK && sel.Value != null)
                        foreach (SelectedObject so in sel.Value)
                            selSet.Add(so.ObjectId);
                    if (selSet.Count == 0)
                        return "Once cizimde nokta (COGO point) secmelisiniz.";
                }

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civilDoc.CogoPoints)
                    {
                        if (selectedOnly && !selSet.Contains(id)) continue;
                        var pt = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
                        if (pt == null) continue;

                        count++;
                        if (count <= maxShown)
                        {
                            sb.AppendLine($"NOKTA {pt.PointNumber}:");
                            if (!string.IsNullOrEmpty(pt.PointName)) sb.AppendLine($"  Ad: {pt.PointName}");
                            sb.AppendLine($"  Ham aciklama: {pt.RawDescription}");
                            sb.AppendLine($"  Tam aciklama: {pt.FullDescription}");
                            sb.AppendLine($"  Kot: {pt.Elevation:F3}");
                            sb.AppendLine($"  Northing: {pt.Northing:F3}");
                            sb.AppendLine($"  Easting: {pt.Easting:F3}");
                            sb.AppendLine($"  Katman: {pt.Layer}");
                            sb.AppendLine($"  Stil: {StyleName(pt, tr)}");
                            sb.AppendLine();
                        }
                    }
                    tr.Commit();
                }

                if (count == 0) return "Gosterilecek nokta bulunamadi.";
                if (count > maxShown)
                    sb.AppendLine($"... ve {count - maxShown} nokta daha (ilk {maxShown} gosterildi).");
                return $"{count} nokta:\n\n" + sb.ToString();
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- FILTRELE + SEC ----
        // Tum dolu kriterler AYNI ANDA saglanmali (VE). Bos/NaN kriterler dikkate alinmaz.
        // pointNumbers: "1,2,5-10" gibi tam/aralik listesi (tekli veya belirli kume secimi).
        // all: true ise (ve baska kriter yoksa) tum noktalar secilir.
        public static string FilterAndSelect(
            string pointNumbers, double minNumber, double maxNumber,
            string nameContains, string rawDescContains, string fullDescContains,
            string pointGroup, string layer, string style,
            double minElevation, double maxElevation,
            double minNorthing, double maxNorthing,
            double minEasting, double maxEasting,
            bool all)
        {
            try
            {
                var check = PreCheck.HasCogoPoints();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                var ed = doc.Editor;

                var numberSet = ParseNumberList(pointNumbers); // null = filtre yok

                bool hasFilter =
                    numberSet != null ||
                    !double.IsNaN(minNumber) || !double.IsNaN(maxNumber) ||
                    !string.IsNullOrEmpty(nameContains) ||
                    !string.IsNullOrEmpty(rawDescContains) ||
                    !string.IsNullOrEmpty(fullDescContains) ||
                    !string.IsNullOrEmpty(pointGroup) ||
                    !string.IsNullOrEmpty(layer) ||
                    !string.IsNullOrEmpty(style) ||
                    !double.IsNaN(minElevation) || !double.IsNaN(maxElevation) ||
                    !double.IsNaN(minNorthing) || !double.IsNaN(maxNorthing) ||
                    !double.IsNaN(minEasting) || !double.IsNaN(maxEasting);

                if (!hasFilter && !all)
                    return "Secim icin en az bir kriter verin veya tum noktalari secmek istiyorsaniz acikca belirtin.";

                var matched = new List<ObjectId>();

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    // Nokta grubu filtresi: grubun nokta numaralarini al
                    HashSet<uint> groupSet = null;
                    if (!string.IsNullOrEmpty(pointGroup))
                    {
                        groupSet = PointGroupNumbers(civilDoc, tr, pointGroup);
                        if (groupSet == null)
                            return $"'{pointGroup}' adinda bir nokta grubu bulunamadi.";
                    }

                    foreach (ObjectId id in civilDoc.CogoPoints)
                    {
                        var pt = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
                        if (pt == null) continue;

                        if (numberSet != null && !numberSet.Contains(pt.PointNumber)) continue;
                        if (!double.IsNaN(minNumber) && pt.PointNumber < minNumber) continue;
                        if (!double.IsNaN(maxNumber) && pt.PointNumber > maxNumber) continue;
                        if (groupSet != null && !groupSet.Contains(pt.PointNumber)) continue;

                        if (!Contains(pt.PointName, nameContains)) continue;
                        if (!Contains(pt.RawDescription, rawDescContains)) continue;
                        if (!Contains(pt.FullDescription, fullDescContains)) continue;

                        if (!string.IsNullOrEmpty(layer) &&
                            !pt.Layer.Equals(layer, System.StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrEmpty(style) &&
                            !StyleName(pt, tr).Equals(style, System.StringComparison.OrdinalIgnoreCase)) continue;

                        if (!double.IsNaN(minElevation) && pt.Elevation < minElevation) continue;
                        if (!double.IsNaN(maxElevation) && pt.Elevation > maxElevation) continue;
                        if (!double.IsNaN(minNorthing) && pt.Northing < minNorthing) continue;
                        if (!double.IsNaN(maxNorthing) && pt.Northing > maxNorthing) continue;
                        if (!double.IsNaN(minEasting) && pt.Easting < minEasting) continue;
                        if (!double.IsNaN(maxEasting) && pt.Easting > maxEasting) continue;

                        matched.Add(id);
                    }
                    tr.Commit();
                }

                if (matched.Count == 0) return "Kosula uyan nokta bulunamadi.";

                ed.SetImpliedSelection(matched.ToArray());
                return matched.Count == 1 ? "1 nokta secildi." : $"{matched.Count} nokta secildi.";
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // ---- YUZEYDEN KOTLANDIR ----
        // Her noktanin X,Y konumundaki yuzey kotunu okuyup noktanin kotu yapar.
        // surfaceName DOLU ise yalnizca o yuzey kullanilir. surfaceName BOS ise her nokta icin
        // yuzeyler sirayla denenir ve noktanin icinde kaldigi ILK yuzeyden kotlandirilir
        // (boylece farkli yuzeylere dusen noktalar dogru yuzeyden kotlanir; tek yuzey de bunun ozel halidir).
        // selectedOnly=true ise yalnizca secili noktalar islenir.
        // Hicbir yuzeyin (veya secilen yuzeyin) sinirlari icinde kalmayan noktalar atlanir ve
        // numaralari listelenir.
        public static string SetElevationFromSurface(string surfaceName, bool selectedOnly)
        {
            try
            {
                var check = PreCheck.HasCogoPoints();
                if (check != null) return check;
                check = PreCheck.HasSurface();
                if (check != null) return check;

                var doc = Application.DocumentManager.MdiActiveDocument;
                var civilDoc = CivilApplication.ActiveDocument;
                var ed = doc.Editor;

                var selSet = new HashSet<ObjectId>();
                if (selectedOnly)
                {
                    var sel = ed.SelectImplied();
                    if (sel.Status == PromptStatus.OK && sel.Value != null)
                        foreach (SelectedObject so in sel.Value)
                            selSet.Add(so.ObjectId);
                    if (selSet.Count == 0)
                        return "Once cizimde nokta (COGO point) secmelisiniz.";
                }

                int updated = 0;
                var failed = new List<uint>();                  // hicbir yuzeye dusmeyen noktalar
                var perSurface = new Dictionary<string, int>(); // hangi yuzeyden kac nokta

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    // Kullanilacak yuzeyleri belirle
                    var surfaces = new List<Autodesk.Civil.DatabaseServices.Surface>();
                    var names = new List<string>();
                    foreach (ObjectId id in civilDoc.GetSurfaceIds())
                    {
                        var s = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                        if (s == null) continue;
                        names.Add(s.Name);
                        bool match = string.IsNullOrEmpty(surfaceName) ||
                                     s.Name.Equals(surfaceName, System.StringComparison.OrdinalIgnoreCase);
                        if (match) surfaces.Add(s);
                    }

                    if (!string.IsNullOrEmpty(surfaceName) && surfaces.Count == 0)
                    {
                        tr.Commit();
                        return $"'{surfaceName}' adinda bir yuzey bulunamadi. Mevcut yuzeyler: " + string.Join(", ", names);
                    }

                    foreach (ObjectId id in civilDoc.CogoPoints)
                    {
                        if (selectedOnly && !selSet.Contains(id)) continue;
                        var pt = tr.GetObject(id, OpenMode.ForWrite) as CogoPoint;
                        if (pt == null) continue;

                        // Noktanin icinde kaldigi ilk yuzeyden kotu al
                        bool done = false;
                        foreach (var surf in surfaces)
                        {
                            double z;
                            try { z = surf.FindElevationAtXY(pt.Easting, pt.Northing); }
                            catch { continue; } // bu yuzeyin sinirlari disinda, digerini dene

                            pt.Elevation = z;
                            updated++;
                            perSurface[surf.Name] = perSurface.TryGetValue(surf.Name, out var c) ? c + 1 : 1;
                            done = true;
                            break;
                        }
                        if (!done) failed.Add(pt.PointNumber);
                    }
                    tr.Commit();
                }

                if (updated == 0 && failed.Count == 0) return "Islenecek nokta bulunamadi.";

                var msg = new System.Text.StringBuilder();
                msg.Append($"{updated} nokta yuzeyden kotlandirildi.");

                // Birden fazla yuzey kullanildiysa dagilimi goster
                if (perSurface.Count > 1)
                {
                    var parts = new List<string>();
                    foreach (var kv in perSurface) parts.Add($"{kv.Key}: {kv.Value}");
                    msg.Append(" (" + string.Join(", ", parts) + ")");
                }

                if (failed.Count > 0)
                {
                    msg.AppendLine();
                    string scope = string.IsNullOrEmpty(surfaceName) ? "hicbir yuzeyin sinirlari icinde degil"
                                                                     : $"'{surfaceName}' yuzeyinin sinirlari disinda";
                    msg.Append($"Kotlandirilamayan {failed.Count} nokta ({scope}): {FormatNumberList(failed)}");
                }

                return msg.ToString();
            }
            catch (System.Exception ex) { return $"Hata: {ex.Message}"; }
        }

        // Nokta numaralarini virgulle ayrilmis liste yapar; cok uzunsa ilk 100'u gosterip kalanı ozetler.
        private static string FormatNumberList(List<uint> numbers)
        {
            const int cap = 100;
            if (numbers.Count <= cap) return string.Join(", ", numbers);
            var shown = numbers.GetRange(0, cap);
            return string.Join(", ", shown) + $", ... (+{numbers.Count - cap} nokta daha)";
        }

        // ---- yardimcilar ----

        // Verilen alt metni iceriyor mu (buyuk/kucuk harf duyarsiz). Filtre bossa daima true.
        private static bool Contains(string value, string sub)
        {
            if (string.IsNullOrEmpty(sub)) return true;
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf(sub, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // "1,2,5-10" -> {1,2,5,6,7,8,9,10}. Bos/gecersiz = null (filtre yok).
        private static HashSet<uint> ParseNumberList(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var set = new HashSet<uint>();
            foreach (var rawPart in text.Split(','))
            {
                var part = rawPart.Trim();
                if (part.Length == 0) continue;
                int dash = part.IndexOf('-');
                if (dash > 0)
                {
                    var a = part.Substring(0, dash).Trim();
                    var b = part.Substring(dash + 1).Trim();
                    if (uint.TryParse(a, out var lo) && uint.TryParse(b, out var hi))
                    {
                        if (lo > hi) { var t = lo; lo = hi; hi = t; }
                        for (uint n = lo; n <= hi; n++) set.Add(n);
                    }
                }
                else if (uint.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var single))
                {
                    set.Add(single);
                }
            }
            return set.Count == 0 ? null : set;
        }

        // Nokta grubundaki nokta numaralari; grup bulunamazsa null.
        private static HashSet<uint> PointGroupNumbers(CivilDocument civilDoc, Transaction tr, string groupName)
        {
            foreach (ObjectId id in civilDoc.PointGroups)
            {
                var pg = tr.GetObject(id, OpenMode.ForRead) as PointGroup;
                if (pg == null) continue;
                if (!pg.Name.Equals(groupName, System.StringComparison.OrdinalIgnoreCase)) continue;
                var set = new HashSet<uint>();
                try { foreach (var n in pg.GetPointNumbers()) set.Add(n); } catch { }
                return set;
            }
            return null;
        }

        // Noktanin nokta stili adi; cozulemezse bos string.
        private static string StyleName(CogoPoint pt, Transaction tr)
        {
            try
            {
                if (pt.StyleId == ObjectId.Null) return "";
                var style = tr.GetObject(pt.StyleId, OpenMode.ForRead)
                    as Autodesk.Civil.DatabaseServices.Styles.PointStyle;
                return style != null ? style.Name : "";
            }
            catch { return ""; }
        }
    }
}
