using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using MTGAWeightsCalculator.Models;

namespace MTGAWeightsCalculator.Services;


public partial class MTGDeckParser(HttpClient httpClient)
{

    public async Task<OutputWeight> ParseDeck(string cards)
    {
        var inputCards = ParseDeckFromMtga(cards);

        var weightedCards = await GetCardWeights();

        if (weightedCards == null)
            return new OutputWeight(0);

        var totalWeight = 0;
        foreach (var inputCard in inputCards)
        {
            if (weightedCards.TryGetValue(inputCard.Name, out var weightedCard))
            {
                totalWeight += int.Parse(weightedCard.Weight) * inputCard.Quantity;
            }
        }

        return new OutputWeight(totalWeight);
    }

    public async Task<Dictionary<string, WeightedCard>> GetCardWeights()
    {
        var weightedCards = new Dictionary<string, WeightedCard>();

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };

        var csvContent = await httpClient.GetStreamAsync("csv/weights.csv");

        using (var reader = new StreamReader(csvContent))
        using (var csv = new CsvReader(reader, csvConfig))
        {
            var records = csv.GetRecordsAsync<WeightedCard>();

            await foreach (var card in records)
            {
                weightedCards[card.Name] = card;
            }
        }

        return weightedCards;
    }

    public static List<InputCard> ParseDeckFromMtga(string input)
    {
        List<InputCard> cards = [];
        var regex = CompiledRegex();
        var matches = regex.Matches(input);

        foreach (var match in matches.Where(match => match.Groups.Count == 3))
        {
            var quantity = int.Parse(match.Groups[1].Value);
            var name = match.Groups[2].Value.Trim();
            cards.Add(new InputCard(name, quantity));
        }

        return cards;
    }

    [GeneratedRegex(@"(\d+) ([^0-9]+)")]
    private static partial Regex CompiledRegex();
}
