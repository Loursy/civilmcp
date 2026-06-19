using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;

namespace CivilClaudePlugin.Helpers
{
    // Islem oncesi ortak on kontroller. Sorun varsa kullaniciya anlasilir Turkce mesaj dondurur.
    // Sorun yoksa null dondurur (= devam edebilirsin).
    public static class PreCheck
    {
        // Aktif çizim var mı?
        public static string ActiveDoc()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return "Açık bir çizim yok. Lütfen önce bir çizim açın.";
            return null;
        }

        // Civil 3D dökümanı erişilebilir mi?
        public static string CivilDoc()
        {
            var baseCheck = ActiveDoc();
            if (baseCheck != null) return baseCheck;

            try
            {
                var civilDoc = CivilApplication.ActiveDocument;
                if (civilDoc == null)
                    return "Civil 3D dökümanına erişilemedi. Çizimin Civil 3D'de açık olduğundan emin olun.";
            }
            catch
            {
                return "Civil 3D dökümanına erişilemedi. Çizimin Civil 3D'de açık olduğundan emin olun.";
            }
            return null;
        }

        // Çizimde en az bir pipe network var mı?
        public static string HasPipeNetwork()
        {
            var baseCheck = CivilDoc();
            if (baseCheck != null) return baseCheck;

            var civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc.GetPipeNetworkIds().Count == 0)
                return "Çizimde hiç pipe network yok. Önce bir pipe network oluşturun.";
            return null;
        }

        // Çizimde en az bir COGO point (nokta) var mı?
        public static string HasCogoPoints()
        {
            var baseCheck = CivilDoc();
            if (baseCheck != null) return baseCheck;

            var civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc.CogoPoints.Count == 0)
                return "Çizimde hiç nokta (COGO point) yok.";
            return null;
        }

        // Çizimde en az bir alignment var mı?
        public static string HasAlignment()
        {
            var baseCheck = CivilDoc();
            if (baseCheck != null) return baseCheck;

            var civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc.GetAlignmentIds().Count == 0)
                return "Çizimde hiç alignment yok. Önce alignment oluşturun.";
            return null;
        }

        // Çizimde en az bir yüzey var mı?
        public static string HasSurface()
        {
            var baseCheck = CivilDoc();
            if (baseCheck != null) return baseCheck;

            var civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc.GetSurfaceIds().Count == 0)
                return "Çizimde hiç yüzey (surface) yok. Önce bir yüzey ekleyin.";
            return null;
        }

        // Belirli isimde bir yüzey var mı? (bos isim = kontrol etme)
        public static string SurfaceExists(string surfaceName)
        {
            if (string.IsNullOrWhiteSpace(surfaceName)) return null;
            var baseCheck = HasSurface();
            if (baseCheck != null) return baseCheck;

            var doc = Application.DocumentManager.MdiActiveDocument;
            var civilDoc = CivilApplication.ActiveDocument;
            bool found = false;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civilDoc.GetSurfaceIds())
                {
                    var surf = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                    if (surf != null && surf.Name.Equals(surfaceName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                tr.Commit();
            }
            if (!found)
                return $"'{surfaceName}' adında bir yüzey bulunamadı. Yüzey adını kontrol edin.";
            return null;
        }
    }
}