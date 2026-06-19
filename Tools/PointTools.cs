using System.Collections.Generic;
using CivilClaudePlugin.Helpers;

namespace CivilClaudePlugin.Tools
{
    // COGO point (Civil 3D nokta) islemleri: nokta gruplarini ve nokta ozelliklerini
    // listele, noktalari ozelliklerine gore tekli veya toplu sec.
    public static class PointTools
    {
        public static List<ClaudeTool> Build() => new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "list_point_groups",
                Description = "Cizimdeki nokta gruplarini (ve her birindeki nokta sayisini) listeler. " +
                              "Kullanici bir gruba gore secim isterse, dogru grup adindan emin olmak icin bunu cagir.",
                InputSchema = new { type = "object", properties = new { } },
                Display = ToolResultDisplay.Options,
                Handler = _ => PointSelector.ListPointGroups()
            },

            new ClaudeTool
            {
                Name = "list_point_properties",
                Description = "COGO point ozelliklerini listeler (numara, ad, ham/tam aciklama, kot, " +
                              "northing, easting, katman, stil). selectedOnly=true ise yalnizca cizimde " +
                              "su an secili noktalar gosterilir.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        selectedOnly = new { type = "boolean", description = "true = yalnizca secili noktalar" }
                    }
                },
                Display = ToolResultDisplay.Info,
                Handler = i => PointSelector.ListProperties(ToolInput.Bool(i, "selectedOnly"))
            },

            new ClaudeTool
            {
                Name = "select_points",
                Description = "COGO point'leri ozelliklerine gore cizimde secer (tekli veya toplu). " +
                              "Verilen TUM kriterleri AYNI ANDA saglayan noktalar secilir (VE mantigi); " +
                              "ornegin 'PNT katmanindaki kotu 100'den buyuk noktalar'. Filtre olmayan alanlari " +
                              "bos birak (sayisal aralik kullanmiyorsan min/max gonderme). Tek nokta icin " +
                              "pointNumbers'a tek numara ver (orn. '5'); belirli kume/aralik icin '1,2,5-10' formati. " +
                              "all yalnizca kullanici acikca TUM noktalari secmek istediginde true olur.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pointNumbers = new { type = "string", description = "Tam/aralik nokta numaralari, orn. '5' veya '1,2,5-10'" },
                        minNumber = new { type = "number", description = "Minimum nokta numarasi" },
                        maxNumber = new { type = "number", description = "Maksimum nokta numarasi" },
                        nameContains = new { type = "string", description = "Nokta adinda gecen metin" },
                        rawDescContains = new { type = "string", description = "Ham aciklamada (raw description) gecen metin" },
                        fullDescContains = new { type = "string", description = "Tam aciklamada (full description) gecen metin" },
                        pointGroup = new { type = "string", description = "Yalnizca bu nokta grubundaki noktalar" },
                        layer = new { type = "string", description = "Noktanin katmani" },
                        style = new { type = "string", description = "Nokta stili adi" },
                        minElevation = new { type = "number", description = "Minimum kot" },
                        maxElevation = new { type = "number", description = "Maksimum kot" },
                        minNorthing = new { type = "number", description = "Minimum northing (Y)" },
                        maxNorthing = new { type = "number", description = "Maksimum northing (Y)" },
                        minEasting = new { type = "number", description = "Minimum easting (X)" },
                        maxEasting = new { type = "number", description = "Maksimum easting (X)" },
                        all = new { type = "boolean", description = "true ise TUM noktalar secilir" }
                    }
                },
                Handler = i => PointSelector.FilterAndSelect(
                    ToolInput.Str(i, "pointNumbers"),
                    ToolInput.Num(i, "minNumber", double.NaN), ToolInput.Num(i, "maxNumber", double.NaN),
                    ToolInput.Str(i, "nameContains"), ToolInput.Str(i, "rawDescContains"), ToolInput.Str(i, "fullDescContains"),
                    ToolInput.Str(i, "pointGroup"), ToolInput.Str(i, "layer"), ToolInput.Str(i, "style"),
                    ToolInput.Num(i, "minElevation", double.NaN), ToolInput.Num(i, "maxElevation", double.NaN),
                    ToolInput.Num(i, "minNorthing", double.NaN), ToolInput.Num(i, "maxNorthing", double.NaN),
                    ToolInput.Num(i, "minEasting", double.NaN), ToolInput.Num(i, "maxEasting", double.NaN),
                    ToolInput.Bool(i, "all"))
            },

            new ClaudeTool
            {
                Name = "set_points_elevation_from_surface",
                Description = "COGO point'leri, X,Y konumlarinin denk geldigi yuzeydeki kota gore kotlandirir " +
                              "(yuzeyden okunan kotu noktanin yeni kotu yapar). selectedOnly=true ise yalnizca cizimde " +
                              "su an secili noktalar; false ise TUM noktalar islenir. Kullanici 'secili noktalar' " +
                              "diyorsa veya az once secim yaptiysa selectedOnly=true gonder; 'tum noktalar' diyorsa false. " +
                              "surface DOLU ise yalnizca o yuzey kullanilir. surface BOS birakilirsa her nokta, icinde " +
                              "kaldigi yuzeyden otomatik kotlandirilir; yani noktalar farkli yuzeylere dusuyorsa her biri " +
                              "dogru yuzeyinden kotlanir (kullanici belirli bir yuzey soylemediyse surface'i bos birak). " +
                              "Hicbir yuzeyin sinirlari icinde kalmayan noktalar atlanir ve numaralari raporlanir.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        surface = new { type = "string", description = "Kullanilacak yuzey adi (bos = tek yuzeyi otomatik kullan)" },
                        selectedOnly = new { type = "boolean", description = "true = yalnizca secili noktalar; false = tum noktalar" }
                    }
                },
                Handler = i => PointSelector.SetElevationFromSurface(
                    ToolInput.Str(i, "surface"), ToolInput.Bool(i, "selectedOnly"))
            }
        };
    }
}
