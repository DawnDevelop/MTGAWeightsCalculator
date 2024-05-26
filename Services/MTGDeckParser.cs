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
        var inputCards = ParseDeckFromInput(cards);

        var weightedCards = await GetCardWeights();

        if (weightedCards == null)
            return new OutputWeight(0);

        var totalWeight = 0;
        var commander = inputCards.Where(x => x.IsCommander).SingleOrDefault();

        if(commander != null)
        {
            totalWeight += await GetCommanderWeight(commander.Name);
            inputCards.Remove(commander);
        }
            
        foreach (var inputCard in inputCards)
        {
            if (weightedCards.TryGetValue(inputCard.Name, out var weightedCard))
            {
                totalWeight += weightedCard.Weight * inputCard.Quantity;
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

        var csvContent = await httpClient.GetStringAsync("csv/WeightsMainDeck.csv");

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, csvConfig);
        
        var records = csv.GetRecordsAsync<WeightedCard>();

        await foreach (var card in records)
        {
            weightedCards[card.Name] = card;
        }
        
        return weightedCards;
    }

    public async Task<int> GetCommanderWeight(string cardName)
    {

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };

        var csvContent = await httpClient.GetStringAsync("csv/WeightsCommander.csv");
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, csvConfig);
        var records = csv.GetRecords<WeightedCard>();

        foreach (var card in records)
        {
            if (card.Name == cardName)
                return card.Weight;
        }

        return 0;
    }

    public List<InputCard> ParseDeckFromInput(string input)
    {
        var cards = new List<InputCard>();
        var regex = CompiledRegex();
        var matches = regex.Matches(input);

        var isCommanderSection = false;

        foreach (Match match in matches)
        {
            var line = match.Groups[0].Value.Trim();

            if (line.StartsWith("Commander"))
            {
                isCommanderSection = true;
                continue; 
            }

            var quantity = 1; // Default quantity if not specified
            var parts = line.Split(' ', 2); // Split quantity and name
            if (parts.Length == 2 && int.TryParse(parts[0], out int qty))
            {
                quantity = qty;
                line = parts[1]; // Update line to exclude quantity
            }

            if (isCommanderSection)
            {
                if (line.Contains("Deck"))
                    line = line.Replace("Deck", "").Trim(' ');

                cards.Add(new InputCard(line, quantity, true)); // Mark as commander card
                isCommanderSection = false;
                continue;
            }

            cards.Add(new InputCard(line, quantity));
        }

        return cards;
    }

    [GeneratedRegex(@"(\d* ?[^0-9]+)")]
    private static partial Regex CompiledRegex();

}
