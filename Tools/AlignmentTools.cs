using System.Collections.Generic;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.Tools
{
    // Alignment (guzergah) islemleri. Her tool mevcut static helper'i sarar;
    // tanim + handler yan yana durur (tek dogruluk kaynagi).
    public static class AlignmentTools
    {
        public static List<ClaudeTool> Build() => new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "list_alignments",
                Description = "Cizimdeki mevcut alignment isimlerini listeler. Kullanici hangi " +
                              "alignmentlardan bahsettiginden emin olmadiginda, ozellikle toplu " +
                              "silme/yeniden adlandirma oncesi bunu cagir.",
                InputSchema = new { type = "object", properties = new { } },
                Display = ToolResultDisplay.Options,
                Handler = _ => AlignmentRenamer.ListAlignments()
            },

            new ClaudeTool
            {
                Name = "rename_alignment",
                Description = "Tek bir alignment'i yeniden adlandirir.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        oldName = new { type = "string", description = "Mevcut (tam) alignment adi" },
                        newName = new { type = "string", description = "Yeni alignment adi" }
                    },
                    required = new[] { "oldName", "newName" }
                },
                Handler = i => AlignmentRenamer.RenameOne(ToolInput.Str(i, "oldName"), ToolInput.Str(i, "newName"))
            },

            new ClaudeTool
            {
                Name = "add_alignment_affix",
                Description = "Tum alignment isimlerine onek ve/veya sonek ekler. Kullanilmayan alani bos birak.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        prefix = new { type = "string", description = "Basa eklenecek metin (bos olabilir)" },
                        suffix = new { type = "string", description = "Sona eklenecek metin (bos olabilir)" }
                    }
                },
                Handler = i => AlignmentRenamer.AddPrefixSuffix(ToolInput.Str(i, "prefix"), ToolInput.Str(i, "suffix"))
            },

            new ClaudeTool
            {
                Name = "find_replace_alignment",
                Description = "Tum alignment isimlerinde bul-degistir yapar.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        find = new { type = "string", description = "Aranacak metin" },
                        replace = new { type = "string", description = "Yerine yazilacak metin (bos olabilir)" }
                    },
                    required = new[] { "find" }
                },
                Handler = i => AlignmentRenamer.FindReplace(ToolInput.Str(i, "find"), ToolInput.Str(i, "replace"))
            },

            new ClaudeTool
            {
                Name = "delete_selected_alignments",
                Description = "Cizimde SU AN secili olan alignmentlari siler.",
                InputSchema = new { type = "object", properties = new { } },
                Handler = _ => AlignmentDeleter.DeleteSelected()
            },

            new ClaudeTool
            {
                Name = "delete_alignments",
                Description = "Alignmentlari isme/katmana/stile/aciklamaya/uzunluga gore veya tumunu siler. " +
                              "Verilen tum filtreleri AYNI ANDA saglayanlar silinir (VE mantigi); ornegin " +
                              "'X katmanindaki 100 metreden uzun alignmentlar'. Filtre olmayan alanlari bos birak " +
                              "(uzunluk filtresi yoksa minLength/maxLength gonderme). 'all' yalnizca kullanici acikca " +
                              "TUM alignmentlari silmek istediginde true olur.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        names = new { type = "string", description = "Virgulle ayrilmis TAM alignment isimleri" },
                        nameContains = new { type = "string", description = "Isim icinde gecen alt metin" },
                        layer = new { type = "string", description = "Alignment'in katmani" },
                        style = new { type = "string", description = "Alignment stili" },
                        descriptionContains = new { type = "string", description = "Aciklama icinde gecen metin" },
                        minLength = new { type = "number", description = "Minimum alignment uzunlugu (m)" },
                        maxLength = new { type = "number", description = "Maksimum alignment uzunlugu (m)" },
                        all = new { type = "boolean", description = "true ise TUM alignmentlar silinir" }
                    }
                },
                Handler = i => AlignmentDeleter.DeleteByFilter(
                    ToolInput.Str(i, "names"), ToolInput.Str(i, "nameContains"),
                    ToolInput.Str(i, "layer"), ToolInput.Str(i, "style"),
                    ToolInput.Str(i, "descriptionContains"),
                    ToolInput.Num(i, "minLength", double.NaN), ToolInput.Num(i, "maxLength", double.NaN),
                    ToolInput.Bool(i, "all"))
            },

            new ClaudeTool
            {
                Name = "list_alignment_styles",
                Description = "Alignment olusturmadan ONCE mevcut alignment stillerini, label set'leri " +
                              "ve katmanlari listeler. create_alignments cagirmadan once bunu cagir ve " +
                              "kullaniciya hangi stil/label set/katman ve ana guzergah istedigini sor.",
                InputSchema = new { type = "object", properties = new { } },
                Display = ToolResultDisplay.Options,
                Handler = _ => AlignmentCreator.ListStylesAndLayers()
            },

            new ClaudeTool
            {
                Name = "create_alignments",
                Description = "Polyline'lardan alignment olusturur. ONCE list_alignment_styles ile " +
                              "secenekleri al ve kullaniciya stil/label set/hedef katman, ANA GUZERGAH'i " +
                              "(olusturulan alignmentlar bu guzergah boyunca chainage'a gore numaralanir) ve " +
                              "isimlendirmenin HANGI NUMARADAN baslayacagini (startNumber) sor. " +
                              "source='selection' secili nesneler icin, source='filter' renk/katmana gore filtre icin.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        source = new { type = "string", description = "'selection' veya 'filter'" },
                        color = new { type = "string", description = "filter icin renk (orn. kırmızı)" },
                        layer = new { type = "string", description = "filter icin kaynak katman" },
                        style = new { type = "string", description = "Secilen alignment stili" },
                        labelSet = new { type = "string", description = "Secilen label set" },
                        targetLayer = new { type = "string", description = "Secilen hedef katman" },
                        mainAlignment = new { type = "string", description = "Ana guzergah (numaralandirma referansi)" },
                        startNumber = new { type = "integer", description = "Isimlendirmenin baslayacagi numara (kullaniciya sor; belirtilmezse 1)" }
                    },
                    required = new[] { "source" }
                },
                Handler = i =>
                {
                    var source = ToolInput.Str(i, "source", "filter");
                    var startNumber = ToolInput.Int(i, "startNumber", 1);
                    if (source == "selection")
                        return AlignmentCreator.CreateFromSelection(
                            ToolInput.Str(i, "style"), ToolInput.Str(i, "labelSet"),
                            ToolInput.Str(i, "targetLayer"), ToolInput.Str(i, "mainAlignment"), startNumber);
                    return AlignmentCreator.CreateFromFilter(
                        ToolInput.Str(i, "color"), ToolInput.Str(i, "layer"), ToolInput.Str(i, "style"),
                        ToolInput.Str(i, "labelSet"), ToolInput.Str(i, "targetLayer"), ToolInput.Str(i, "mainAlignment"), startNumber);
                }
            }
        };
    }
}
