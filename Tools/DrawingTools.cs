using System.Collections.Generic;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.Tools
{
    // Genel cizim islemleri: nesne secimi, xref bind, polyline/line ozellikleri.
    public static class DrawingTools
    {
        public static List<ClaudeTool> Build() => new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "select_objects",
                Description = "Cizimde nesneleri tur/renk/katmana gore secer (orn. 'kirmizi polyline'lari sec'). " +
                              "Filtre olmayan alanlari bos birak.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        objectType = new { type = "string", description = "polyline | line | arc | circle | text | mtext | block | hatch | bos=hepsi" },
                        color = new { type = "string", description = "kırmızı | sarı | yeşil | cyan | mavi | magenta | beyaz | bos=hepsi" },
                        layer = new { type = "string", description = "Katman adi veya bos=hepsi" }
                    }
                },
                Handler = i => DrawingCommandExecutor.SelectByFilter(
                    ToolInput.Str(i, "objectType"), ToolInput.Str(i, "color"), ToolInput.Str(i, "layer"))
            },

            new ClaudeTool
            {
                Name = "bind_xrefs",
                Description = "Tum acik cizimlerdeki tum xref'leri bind eder.",
                InputSchema = new { type = "object", properties = new { } },
                Handler = _ => XrefBinder.BindAllXrefs()
            },

            new ClaudeTool
            {
                Name = "polyline_list_properties",
                Description = "Polyline/line ozelliklerini listeler (pipe network parcalari DEGIL). " +
                              "selectedOnly=true ise yalnizca secili olanlar; layer bos = tum katmanlar.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        selectedOnly = new { type = "boolean" },
                        layer = new { type = "string", description = "Bos = tum katmanlar" }
                    }
                },
                Display = ToolResultDisplay.Info,
                Handler = i => PolylineProperties.ListProperties(ToolInput.Bool(i, "selectedOnly"), ToolInput.Str(i, "layer"))
            },

            new ClaudeTool
            {
                Name = "polyline_filter_select",
                Description = "Polyline'lari bir ozellige gore filtreleyip secer. property: length | area | vertices. " +
                              "op: gt | lt | ge | le | eq | between. value2 yalnizca 'between' icin. layer bos = tum.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        property = new { type = "string", description = "length | area | vertices" },
                        op = new { type = "string", description = "gt | lt | ge | le | eq | between" },
                        value1 = new { type = "number" },
                        value2 = new { type = "number", description = "Yalnizca 'between' icin" },
                        layer = new { type = "string", description = "Bos = tum katmanlar" }
                    },
                    required = new[] { "property", "op", "value1" }
                },
                Handler = i => PolylineProperties.FilterAndSelect(
                    ToolInput.Str(i, "property", "length"), ToolInput.Str(i, "op", "gt"),
                    ToolInput.Num(i, "value1"), ToolInput.Num(i, "value2"), ToolInput.Str(i, "layer"))
            },

            new ClaudeTool
            {
                Name = "polyline_modify",
                Description = "Polyline/line renk veya katmanini degistirir. fromColor/fromLayer filtredir " +
                              "(bos = filtre yok). newColor/newLayer degisikliktir (bos = degistirme). " +
                              "Renkler: kırmızı, sarı, yeşil, cyan, mavi, magenta, beyaz.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        fromColor = new { type = "string", description = "Filtre: renk (bos = yok)" },
                        fromLayer = new { type = "string", description = "Filtre: katman (bos = yok)" },
                        newColor = new { type = "string", description = "Yeni renk (bos = degistirme)" },
                        newLayer = new { type = "string", description = "Yeni katman (bos = degistirme)" }
                    }
                },
                Handler = i => PolylineProperties.ModifyPolylines(
                    ToolInput.Str(i, "fromColor"), ToolInput.Str(i, "fromLayer"),
                    ToolInput.Str(i, "newColor"), ToolInput.Str(i, "newLayer"))
            }
        };
    }
}
