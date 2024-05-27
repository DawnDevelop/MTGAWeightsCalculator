using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using MTGAWeightsCalculator.Models;

namespace MTGAWeightsCalculator.Services;

public partial class MTGDeckParser(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private Dictionary<string, WeightedCard>? _mainDeckWeightsCache;
    private Dictionary<string, int>? _commanderWeightsCache;
    private static readonly char[] separator = ['\r', '\n'];

    public async Task<OutputCards?> ParseDeck(string cards)
    {
        if (_mainDeckWeightsCache == null || _commanderWeightsCache == null)
            await InitializeAsync();

        var inputCards = ParseDeckFromInput(cards);

        var totalWeight = 0;
        var outputCards = new List<OutputCard>();

        foreach (var inputCard in inputCards)
        {
            if (inputCard.IsCommander)
            {
                var commanderWeight = await GetCommanderWeightAsync(inputCard.Name);
                totalWeight += commanderWeight;
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, commanderWeight));
            }
            else if (_mainDeckWeightsCache!.TryGetValue(inputCard.Name, out var weightedCard))
            {
                var cardWeight = weightedCard.Weight * inputCard.Quantity;
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, cardWeight));
                totalWeight += cardWeight;
            }
        }

        return new OutputCards(totalWeight, outputCards);
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(InitializeMainDeckWeightsCache(), InitializeCommanderWeightsCache());
    }

    public async Task InitializeMainDeckWeightsCache()
    {
        var csvContent = await _httpClient.GetStringAsync("csv/WeightsMainDeck.csv");
        _mainDeckWeightsCache = ParseCsv<string, WeightedCard>(csvContent, dis => dis.Name, card => card.Name);
    }

    public async Task InitializeCommanderWeightsCache()
    {
        var csvContent = await _httpClient.GetStringAsync("csv/WeightsCommander.csv");
        _commanderWeightsCache = ParseCsv<string, WeightedCard>(csvContent, dis => dis.Name, card => card.Name)
            .ToDictionary(card => card.Key, card => card.Value.Weight);
    }

    public async Task<int> GetCommanderWeightAsync(string cardName)
    {
        if (_commanderWeightsCache == null)
            await InitializeCommanderWeightsCache();

        return _commanderWeightsCache!.TryGetValue(cardName, out var weight) ? weight : 0;
    }
    public async Task<int> GetSingleCardWeightAsync(string cardName)
    {
        if(_mainDeckWeightsCache == null)
            await InitializeMainDeckWeightsCache();

        return _mainDeckWeightsCache!.TryGetValue(cardName, out var card) ? card.Weight : 0;
    }

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

    private static Dictionary<TKey, TValue> ParseCsv<TKey, TValue>(string csvContent, Func<TValue, TKey> distinctBy, Func<TValue, TKey> keySelector)
        where TKey : notnull
        where TValue : class
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, config);

        var records = csv.GetRecords<TValue>();
        var dictionary = records.DistinctBy(distinctBy).ToDictionary(keySelector);

        return dictionary;
    }

    [GeneratedRegex(@"\(.*?\).*")]
    private static partial Regex CompiledRegex();
}
