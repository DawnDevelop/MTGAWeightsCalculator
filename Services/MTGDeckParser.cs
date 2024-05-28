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
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, commanderWeight));
            }
            else
            {
                var cardWeight = await GetSingleCardWeightAsync(inputCard.Name, isHistoricBrawl);
                outputCards.Add(new OutputCard(inputCard.Quantity, inputCard.Name, cardWeight));
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
