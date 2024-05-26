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

        var outputCards = new List<OutputCard>();
        foreach (var inputCard in inputCards)
        {
            if(inputCard.IsCommander)
            {
                var commanderWeight = await GetCommanderWeight(inputCard.Name);
                totalWeight += commanderWeight;
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, commanderWeight));
            }
            else if (weightedCards.TryGetValue(inputCard.Name, out var weightedCard))
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
        var isCommanderSection = false;
        var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);


        foreach (var line in lines)
        {
            var newLine = RemoveExtraInfo(line);

            if (newLine.StartsWith("Deck"))
                continue;
            if (newLine.StartsWith("Sideboard"))
                continue;

            if (newLine.StartsWith("Commander"))
            {
                isCommanderSection = true;
                continue; 
            }

            var parts = newLine.Split(' ', 2);
            if (int.TryParse(parts[0], out int quantity))
            {
                if(isCommanderSection)
                {
                    cards.Add(new InputCard(parts[1], 1, true)); // Mark as commander card
                    isCommanderSection = false;
                    continue;
                }
                else
                {
                    cards.Add(new InputCard(parts[1], quantity));
                }
            }

        }

        return cards;
    }

    public static string RemoveExtraInfo(string input)
    {
        string pattern = @"\(.*?\).*";
        var replaced = Regex.Replace(input, pattern, "").Trim();
        return replaced;
    }
}
