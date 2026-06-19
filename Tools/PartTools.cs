using System.Collections.Generic;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.Tools
{
    // Boru/structure parcalarinin isim/aciklama/ozellik islemleri.
    // partType: pipe | structure | all. network: bos = tum networkler veya bir ad.
    public static class PartTools
    {
        public static List<ClaudeTool> Build() => new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "part_sequential_rename",
                Description = "Parcalari sirali yeniden adlandirir (prefix + artan sayi).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string", description = "Bos = tum networkler" },
                        prefix = new { type = "string" },
                        start = new { type = "integer", description = "Baslangic numarasi (varsayilan 1)" }
                    }
                },
                Handler = i => PartRenamer.SequentialRename(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "prefix"), ToolInput.Int(i, "start", 1))
            },

            new ClaudeTool
            {
                Name = "part_add_affix",
                Description = "Parca isimlerine onek/sonek ekler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        prefix = new { type = "string" },
                        suffix = new { type = "string" }
                    }
                },
                Handler = i => PartRenamer.AddPrefixSuffix(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "prefix"), ToolInput.Str(i, "suffix"))
            },

            new ClaudeTool
            {
                Name = "part_find_replace",
                Description = "Parca isim veya aciklamalarinda bul-degistir yapar. field: name | description.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        find = new { type = "string" },
                        replace = new { type = "string" },
                        field = new { type = "string", description = "name | description" }
                    }
                },
                Handler = i => PartRenamer.FindReplace(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "find"), ToolInput.Str(i, "replace"), ToolInput.Str(i, "field", "name"))
            },

            new ClaudeTool
            {
                Name = "part_set_description",
                Description = "Parcalarin aciklamasini sabit bir metne ayarlar.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        description = new { type = "string" }
                    }
                },
                Handler = i => PartRenamer.SetDescription(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"), ToolInput.Str(i, "description"))
            },

            new ClaudeTool
            {
                Name = "part_set_by_size",
                Description = "Parcanin isim veya aciklamasini boyuta gore sablonla ayarlar. " +
                              "target: name | description. template'te {size} yer tutucusu cap/genislik ile degisir.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        target = new { type = "string", description = "name | description" },
                        template = new { type = "string", description = "Orn. 'DN{size} pipe'" }
                    }
                },
                Handler = i => PartRenamer.SetBySize(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "target", "description"), ToolInput.Str(i, "template", "{size}"))
            },

            new ClaudeTool
            {
                Name = "part_name_by_network",
                Description = "Parcalari ait olduklari network adiyla adlandirir (NetworkAdi.1, NetworkAdi.2 ...).",
                InputSchema = new
                {
                    type = "object",
                    properties = new { partType = new { type = "string", description = "pipe | structure | all" } }
                },
                Handler = i => PartRenamer.NameByNetwork(ToolInput.Str(i, "partType", "all"))
            },

            new ClaudeTool
            {
                Name = "part_copy_field",
                Description = "Bir alani digerine kopyalar (orn. isimleri aciklamaya). from/to: name | description.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        from = new { type = "string", description = "name | description" },
                        to = new { type = "string", description = "name | description" }
                    }
                },
                Handler = i => PartProperties.CopyField(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "from", "name"), ToolInput.Str(i, "to", "description"))
            },

            new ClaudeTool
            {
                Name = "part_list_properties",
                Description = "Parcalarin ozelliklerini listeler/okur. selectedOnly=true ise yalnizca " +
                              "cizimde secili parcalar.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        selectedOnly = new { type = "boolean" }
                    }
                },
                Display = ToolResultDisplay.Info,
                Handler = i => PartProperties.ListProperties(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"), ToolInput.Bool(i, "selectedOnly"))
            },

            new ClaudeTool
            {
                Name = "part_filter_select",
                Description = "Bir ozellige gore parcalari filtreleyip secer. property: diameter(mm) | length | " +
                              "rim | sump | invert. op: gt | lt | ge | le | eq | between. value2 yalnizca 'between' icin.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all" },
                        network = new { type = "string" },
                        property = new { type = "string", description = "diameter | length | rim | sump | invert" },
                        op = new { type = "string", description = "gt | lt | ge | le | eq | between" },
                        value1 = new { type = "number" },
                        value2 = new { type = "number", description = "Yalnizca 'between' icin" }
                    },
                    required = new[] { "property", "op", "value1" }
                },
                Handler = i => PartProperties.FilterAndSelect(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "property", "diameter"), ToolInput.Str(i, "op", "gt"),
                    ToolInput.Num(i, "value1"), ToolInput.Num(i, "value2"))
            }
        };
    }
}
