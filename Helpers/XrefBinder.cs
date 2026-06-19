using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace CivilClaudePlugin.Helpers
{
    public static class XrefBinder
    {
        public static string BindAllXrefs()
        {
            var sb = new System.Text.StringBuilder();
            int totalDocs = 0;
            int totalBound = 0;

            try
            {
                var docManager = Application.DocumentManager;

                // Önce tüm dokümanları bir listeye al (foreach sırasında aktif değiştirmek sorun çıkarır)
                var docs = new List<Document>();
                foreach (Document d in docManager)
                    docs.Add(d);

                foreach (Document doc in docs)
                {
                    totalDocs++;

                    // Bu çizimi aktif yap
                    docManager.MdiActiveDocument = doc;

                    int foundInDoc = 0;
                    int boundInDoc = 0;
                    var db = doc.Database;

                    using (doc.LockDocument())
                    {
                        var idsToBind = new ObjectIdCollection();

                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var xrefGraph = db.GetHostDwgXrefGraph(false);

                            for (int i = 0; i < xrefGraph.NumNodes; i++)
                            {
                                var node = xrefGraph.GetXrefNode(i) as XrefGraphNode;
                                if (node == null) continue;

                                if (node.XrefStatus == XrefStatus.Resolved && node.BlockTableRecordId != ObjectId.Null)
                                {
                                    var btr = tr.GetObject(node.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
                                    if (btr != null && btr.IsFromExternalReference && !btr.IsFromOverlayReference)
                                    {
                                        foundInDoc++;
                                        idsToBind.Add(node.BlockTableRecordId);
                                    }
                                }
                            }
                            tr.Commit();
                        }

                        if (idsToBind.Count > 0)
                        {
                            try
                            {
                                db.BindXrefs(idsToBind, true);
                                boundInDoc = idsToBind.Count;
                                totalBound += boundInDoc;
                            }
                            catch (System.Exception bindEx)
                            {
                                sb.AppendLine($"  {GetDocName(doc)}: {foundInDoc} bulundu, BIND HATASI - {bindEx.Message}");
                                continue;
                            }
                        }
                    }

                    sb.AppendLine($"  {GetDocName(doc)}: {foundInDoc} bulundu, {boundInDoc} bind edildi.");
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"Toplam {totalDocs} çizim işlendi, {totalBound} xref bind edildi.");
                result.AppendLine();
                result.Append(sb.ToString());
                return result.ToString();
            }
            catch (System.Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        private static string GetDocName(Document doc)
        {
            try
            {
                return System.IO.Path.GetFileName(doc.Name);
            }
            catch
            {
                return doc.Name;
            }
        }
    }
}