using CsvHelper.Configuration.Attributes;

namespace MTGAWeightsCalculator.Models;

public class WeightedCard
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Expansion { get; set; }

    [Name("color_identity")]
    public string? ColorIdentity { get; init; }

    [Name("weight_hb_99")]
    public int WeightHistoricMainDeck { get; init; }


    [Name("weight_hb_cmd")] 
    public int WeightHistoricCommander { get; init; }

    [Name("weight_sb_99")] 
    public int WeightStandardMainDeck { get; init; }

    [Name("weight_sb_cmd")] 
    public int WeightStandardCommander { get; init; }
}
