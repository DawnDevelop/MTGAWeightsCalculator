using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using MTGAWeightsCalculator.Models;

namespace MTGAWeightsCalculator.Services;


public partial class MTGDeckParser(HttpClient httpClient)
{

    public async Task<OutputCards?> ParseDeck(string cards)
    {
        var inputCards = ParseDeckFromInput(cards);

        var weightedCards = await GetMainDeckCardWeights();

        if (weightedCards == null)
            return null;

        var totalWeight = 0;
        var commander = inputCards.Where(x => x.IsCommander).SingleOrDefault();

        if(commander != null)
        {
            totalWeight += await GetCommanderWeight(commander.Name);
            inputCards.Remove(commander);
        }
        
        var outputCards = new List<OutputCard>();
        foreach (var inputCard in inputCards)
        {
            if (weightedCards.TryGetValue(inputCard.Name, out var weightedCard))
            {
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, weightedCard.Weight));
                totalWeight += weightedCard.Weight * inputCard.Quantity;
            }
        }
        
        return new OutputCards(totalWeight, outputCards);
    }

    public async Task<Dictionary<string, WeightedCard>> GetMainDeckCardWeights()
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

    public async Task<int> GetSingleCardWeight(string cardName)
    {
        var weights = await GetMainDeckCardWeights();

        if (weights.TryGetValue(cardName, out var card))
            return card.Weight;
        else 
            return 0;
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

        if(matches.Count == 0)
        {
            regex = MTGACompiledRegex();
            matches = regex.Matches(input);
        }

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

    [GeneratedRegex(@"Deck\s+(\d+)\s+([^0-9(]+)(?:\([^)]+\))?")]
    private static partial Regex MTGACompiledRegex();


}
