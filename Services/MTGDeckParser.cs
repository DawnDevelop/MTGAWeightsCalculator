using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using MTGAWeightsCalculator.Models;

namespace MTGAWeightsCalculator.Services;

public partial class MTGDeckParser(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private Dictionary<string, WeightedCard>? _brawlWeightsCache;
    private static readonly char[] separator = ['\r', '\n'];

    /// <summary>
    /// Parses a deck from a string representation, calculates weights for each card, and returns an object representing the parsed deck with total weight and ordered card details.
    /// </summary>
    /// <param name="cards">A string representation of the deck to be parsed.</param>
    /// <param name="isHistoricBrawl">A boolean value indicating whether the deck is for Historic Brawl format.</param>
    /// <returns>An OutputCards object containing the total weight of the deck and a list of cards with their respective weights, or null if parsing fails.</returns>
    /// <example>
    /// var result = await ParseDeck("Card1,Card2", true);
    /// Console.WriteLine(result?.TotalWeight); // Outputs the total weight of the deck
    /// </example>
    public async Task<OutputCards?> ParseDeck(string cards, bool isHistoricBrawl)
    {
        if (_brawlWeightsCache == null)
            await InitializeBrawlWeightsCache();

        var inputCards = ParseDeckFromInput(cards);

        var totalWeight = 0;
        var outputCards = new List<OutputCard>();

        foreach (var inputCard in inputCards)
        {
            if (inputCard.IsCommander)
            {
                var commanderWeight = await GetCommanderWeightAsync(inputCard.Name, isHistoricBrawl);
                totalWeight += commanderWeight;
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, commanderWeight, commanderWeight));
            }
            else
            {
                var cardSingleWeight = await GetSingleCardWeightAsync(inputCard.Name, isHistoricBrawl);
                var cardWeight = cardSingleWeight * inputCard.Quantity;
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, cardWeight, cardSingleWeight));
                totalWeight += cardWeight;
            }
        }

        return new OutputCards(totalWeight, [.. outputCards.OrderByDescending(x => x.Weight)]);
    }

    public async Task InitializeBrawlWeightsCache()
    {
        var csvContent = await _httpClient.GetStringAsync("csv/BrawlWeights.csv");
        _brawlWeightsCache = ParseCsv<string, WeightedCard>(csvContent, dis => dis.Name, card => card.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetAllCardNames()
    {
        if (_brawlWeightsCache == null)
            await InitializeBrawlWeightsCache();

        return [.. _brawlWeightsCache!.Select(x => x.Value.Name).Order()];
    }

    public async Task<int> GetCommanderWeightAsync(string cardName, bool isHistoricBrawl = false)
    {
        if (_brawlWeightsCache == null)
            await InitializeBrawlWeightsCache();

        if (_brawlWeightsCache!.TryGetValue(cardName, out var card))
            return isHistoricBrawl ? card.WeightHistoricCommander : card.WeightStandardCommander;
        else
            return 0;
    }
    public async Task<int> GetSingleCardWeightAsync(string cardName, bool isHistoricBrawl = false)
    {
        if (_brawlWeightsCache == null)
            await InitializeBrawlWeightsCache();

        if (_brawlWeightsCache!.TryGetValue(cardName, out var card))
            return isHistoricBrawl ? card.WeightHistoricMainDeck : card.WeightStandardMainDeck;
        else
            return 0;
    }

    /// <summary>
    /// Parses a deck list from a given input string into a list of InputCard objects.
    /// The input string should contain card entries separated by lines, where each line
    /// specifies a card quantity and name, with optional "Commander" and "Sideboard" section headers.
    /// </summary>
    /// <param name="input">The input string containing the deck list information.</param>
    /// <returns>A list of InputCard objects, each representing a parsed card from the input.</returns>
    /// <example>
    /// string deckInput = "3 Mountain\n2 Swamp\nCommander\n1 Niv-Mizzet, Parun";
    /// List<InputCard> deck = MTGDeckParser.ParseDeckFromInput(deckInput);
    /// foreach(var card in deck)
    /// {
    ///     Console.WriteLine($"{card.Quantity} {card.Name} {(card.IsCommander ? "(Commander)" : "")}");
    /// }
    /// // Output:
    /// // 3 Mountain
    /// // 2 Swamp
    /// // 1 Niv-Mizzet, Parun (Commander)
    /// </example>
    public static List<InputCard> ParseDeckFromInput(string input)
    {
        var cards = new List<InputCard>();
        var isCommanderSection = false;
        var lines = input.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var newLine = RemoveExtraInfo(line);
            if (newLine.StartsWith("Deck") || newLine.StartsWith("Sideboard"))
                continue;

            if (newLine.StartsWith("Commander"))
            {
                isCommanderSection = true;
                continue;
            }

            var parts = newLine.Split(' ', 2);
            if (int.TryParse(parts[0], out int quantity))
            {
                var isCommander = isCommanderSection;
                cards.Add(new InputCard(parts[1], isCommander ? 1 : quantity, isCommander));
                isCommanderSection = false;
            }
        }

        return cards;
    }

    public static string RemoveExtraInfo(string input)
    {
        return CompiledRegex().Replace(input, "").Trim();
    }

    /// <summary>
    /// Parses CSV content and returns a dictionary with distinct records based on the specified keys and selectors.
    /// </summary>
    /// <typeparam name="TKey">The type of the key in the dictionary, which must be non-null.</typeparam>
    /// <typeparam name="TValue">The type of the records, which must be a class.</typeparam>
    /// <param name="csvContent">The CSV content to be parsed.</param>
    /// <param name="distinctBy">A function to determine distinct records from the parsed values.</param>
    /// <param name="keySelector">A function to extract keys from the values for the dictionary.</param>
    /// <param name="comparer">An optional equality comparer for the dictionary keys.</param>
    /// <returns>A dictionary where each key is derived from a value using the <paramref name="keySelector"/>, with values being the distinct objects from the CSV content.</returns>
    /// <example>
    /// var csvContent = "Id,Name\n1,Item1\n2,Item2\n1,Item1";
    /// var distinctBy = (Item x) => x.Id;
    /// var keySelector = (Item x) => x.Id;
    /// var dictionary = ParseCsv(csvContent, distinctBy, keySelector);
    /// Console.WriteLine(dictionary.Count); // Expected output: 2
    /// </example>
    private static Dictionary<TKey, TValue> ParseCsv<TKey, TValue>(string csvContent, Func<TValue, TKey> distinctBy, Func<TValue, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
        where TValue : class
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower(),
            
        };

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, config);

        var records = csv.GetRecords<TValue>();
        var dictionary = records.DistinctBy(distinctBy).ToDictionary(keySelector, comparer);

        return dictionary;
    }

    [GeneratedRegex(@"\(.*?\).*")]
    private static partial Regex CompiledRegex();
}
