using System.Collections.Generic;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.Tools
{
    // Boru/structure etiketleme.
    public static class LabelTools
    {
        public static List<ClaudeTool> Build() => new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "list_label_styles",
                Description = "Etiket eklemeden ONCE mevcut label stillerini listeler. partType, " +
                              "etiketlenecek seye gore: 'pipe' sadece borular, 'structure' sadece " +
                              "structure'lar, 'all' ikisi. Bu cagridan SONRA yalnizca ilgili alana ait " +
                              "stili sor (pipe ise pipe label stili, structure ise structure label stili).",
                InputSchema = new
                {
                    type = "object",
                    properties = new { partType = new { type = "string", description = "pipe | structure | all" } }
                },
                Display = ToolResultDisplay.Options,
                Handler = i => LabelPlacer.ListLabelStyles(ToolInput.Str(i, "partType", "all"))
            },

            new ClaudeTool
            {
                Name = "add_labels",
                Description = "Borulara/structure'lara etiket ekler. partType, list_label_styles ile ayni olmali. " +
                              "pipeStyle/structStyle uygulanacak LABEL stilleridir. pipeObjStyle/structObjStyle " +
                              "OPSIYONEL filtredir - yalnizca NESNE stili eslesen parcalari etiketler (orn. yalnizca " +
                              "'CB Gully' nesne stilli structure'lar). Filtre yoksa bos birak.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partType = new { type = "string", description = "pipe | structure | all (list_label_styles ile ayni)" },
                        network = new { type = "string", description = "Bos = tum networkler" },
                        pipeStyle = new { type = "string", description = "Pipe label stili" },
                        structStyle = new { type = "string", description = "Structure label stili" },
                        pipeObjStyle = new { type = "string", description = "Opsiyonel nesne-stili filtresi (pipe)" },
                        structObjStyle = new { type = "string", description = "Opsiyonel nesne-stili filtresi (structure)" }
                    }
                },
                Handler = i => LabelPlacer.AddLabels(
                    ToolInput.Str(i, "partType", "all"), ToolInput.Str(i, "network"),
                    ToolInput.Str(i, "pipeStyle"), ToolInput.Str(i, "structStyle"),
                    ToolInput.Str(i, "pipeObjStyle"), ToolInput.Str(i, "structObjStyle"))
            }
        };
    }
}
