using System.Collections.Generic;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.Tools
{
    // Pipe network (boru agi) olusturma/silme ve baglanti/kural/yuzey islemleri.
    public static class PipeNetworkTools
    {
        public static List<ClaudeTool> Build() => new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "list_pipe_network_options",
                Description = "Pipe network olustururken bir ayar icin secenekleri listeler. " +
                              "field su degerlerden biri olmali: partsList, pipeFamily, structFamily, " +
                              "pipeStyle, structStyle, surface. pipeFamily/structFamily icin partsList gerekir.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        field = new { type = "string", description = "partsList | pipeFamily | structFamily | pipeStyle | structStyle | surface" },
                        partsList = new { type = "string", description = "Secilen parts list (pipeFamily/structFamily icin)" }
                    },
                    required = new[] { "field" }
                },
                Display = ToolResultDisplay.Options,
                Handler = i => PipeNetworkCreator.ListNetworkOptions(ToolInput.Str(i, "field"), ToolInput.Str(i, "partsList"))
            },

            new ClaudeTool
            {
                Name = "list_pipe_sizes",
                Description = "Secilen pipe ve/veya structure ailesine ait mevcut boyutlari listeler. " +
                              "Pipe size icin pipeFamily, structure size icin structureFamily ver.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pipeFamily = new { type = "string", description = "Secilen pipe ailesi" },
                        structureFamily = new { type = "string", description = "Secilen structure ailesi" }
                    }
                },
                Display = ToolResultDisplay.Options,
                Handler = i => PipeNetworkCreator.ListSizes(ToolInput.Str(i, "pipeFamily"), ToolInput.Str(i, "structureFamily"))
            },

            new ClaudeTool
            {
                Name = "create_pipe_network",
                Description = "Alignmentlardan pipe network olusturur. Tum ayarlar list_pipe_network_options " +
                              "ve list_pipe_sizes ile tek tek toplandiktan SONRA cagir. " +
                              "alignments: kullanici BELIRLI alignment(lar) soyledIyse onlarin isimlerini virgulle yaz - " +
                              "yalnizca onlardan network olusturulur; kullanici alignment belirtmediyse bos birak (TUM alignmentlar). " +
                              "surface OPSIYONELDIR: kullanici yuzey istemiyorsa bos birak (network yuzeysiz olusur, kot hesabi atlanir). " +
                              "Boyut: kullanici pipe size / structure size SECTIYSE pipeSize/structureSize'a list_pipe_sizes'daki TAM adi yaz; " +
                              "secim yapmadiysa bos birak (ilk mevcut boyut). flowDirection varsayilan 'start'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partsList = new { type = "string" },
                        pipeFamily = new { type = "string" },
                        structureFamily = new { type = "string" },
                        pipeSize = new { type = "string", description = "Bos = ilk mevcut boyut" },
                        structureSize = new { type = "string", description = "Bos = ilk mevcut boyut" },
                        pipeStyle = new { type = "string" },
                        structureStyle = new { type = "string" },
                        surface = new { type = "string", description = "Referans yuzey. OPSIYONEL - bos = yuzeysiz olustur" },
                        alignments = new { type = "string", description = "Virgulle ayrilmis alignment isimleri; bos = cizimdeki TUM alignmentlar" },
                        flowDirection = new { type = "string", description = "start | end | terrain (varsayilan start)" }
                    },
                    required = new[] { "partsList", "pipeFamily", "structureFamily" }
                },
                Handler = i => PipeNetworkCreator.CreateFromAlignments(
                    ToolInput.Str(i, "partsList"), ToolInput.Str(i, "pipeFamily"), ToolInput.Str(i, "structureFamily"),
                    ToolInput.Str(i, "pipeStyle"), ToolInput.Str(i, "structureStyle"), ToolInput.Str(i, "surface"),
                    ToolInput.Str(i, "flowDirection", "start"),
                    ToolInput.Str(i, "pipeSize"), ToolInput.Str(i, "structureSize"),
                    ToolInput.Str(i, "alignments"))
            },

            new ClaudeTool
            {
                Name = "delete_pipe_network_by_name",
                Description = "Verilen network ICINDEKI tum parcalari (boru/structure) siler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { networkName = new { type = "string" } },
                    required = new[] { "networkName" }
                },
                Handler = i => PipeNetworkDeleter.DeleteByNetworkName(ToolInput.Str(i, "networkName"))
            },

            new ClaudeTool
            {
                Name = "delete_selected_parts",
                Description = "Cizimde secili olan boru/structure parcalarini siler.",
                InputSchema = new { type = "object", properties = new { } },
                Handler = _ => PipeNetworkDeleter.DeleteSelected()
            },

            new ClaudeTool
            {
                Name = "delete_parts_by_area",
                Description = "Belirtilen dikdortgen alandaki parcalari siler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        minX = new { type = "number" },
                        minY = new { type = "number" },
                        maxX = new { type = "number" },
                        maxY = new { type = "number" }
                    },
                    required = new[] { "minX", "minY", "maxX", "maxY" }
                },
                Handler = i => PipeNetworkDeleter.DeleteByArea(
                    ToolInput.Num(i, "minX"), ToolInput.Num(i, "minY"), ToolInput.Num(i, "maxX"), ToolInput.Num(i, "maxY"))
            },

            new ClaudeTool
            {
                Name = "delete_pipes_by_diameter",
                Description = "Cap araligindaki (mm) borulari siler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        minDia = new { type = "number", description = "Minimum cap (mm)" },
                        maxDia = new { type = "number", description = "Maksimum cap (mm)" }
                    },
                    required = new[] { "minDia", "maxDia" }
                },
                Handler = i => PipeNetworkDeleter.DeleteByPipeDiameter(ToolInput.Num(i, "minDia"), ToolInput.Num(i, "maxDia"))
            },

            new ClaudeTool
            {
                Name = "delete_duplicate_structures",
                Description = "Ayni noktadaki (tolerans icindeki) duplicate structure'lari siler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { tolerance = new { type = "number", description = "Varsayilan 0.5" } }
                },
                Handler = i => PipeNetworkDeleter.DeleteDuplicateStructures(ToolInput.Num(i, "tolerance", 0.5))
            },

            new ClaudeTool
            {
                Name = "delete_networks",
                Description = "Tum pipe NETWORK'lerini siler. networks = virgulle ayrilmis network isimleri; " +
                              "bos birakilirsa TUM pipe networkler silinir.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { networks = new { type = "string", description = "Virgulle ayrilmis isimler; bos = tumu" } }
                },
                Handler = i => PipeNetworkDeleter.DeleteNetworks(ToolInput.Str(i, "networks"))
            },

            new ClaudeTool
            {
                Name = "connect_pipes",
                Description = "Bagli olmayan borulari yakindaki structure'lara baglar.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { tolerance = new { type = "number", description = "Varsayilan 0.5" } }
                },
                Handler = i => PipeConnector.ConnectUnconnectedPipes(ToolInput.Num(i, "tolerance", 0.5))
            },

            new ClaudeTool
            {
                Name = "apply_rules",
                Description = "Boru/structure'lara kurallari uygular. target: pipe | structure | all.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "pipe | structure | all" } }
                },
                Handler = i => RuleApplier.ApplyRules(ToolInput.Str(i, "target", "all"))
            },

            new ClaudeTool
            {
                Name = "assign_surface",
                Description = "Pipe network/parcalara referans yuzey atar. network bos = tum networkler, " +
                              "ya da belirli bir network adi.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        surface = new { type = "string", description = "Yuzey adi" },
                        network = new { type = "string", description = "Bos = tum networkler" }
                    },
                    required = new[] { "surface" }
                },
                Handler = i => SurfaceAssigner.AssignSurface(ToolInput.Str(i, "surface"), ToolInput.Str(i, "network"))
            }
        };
    }
}
