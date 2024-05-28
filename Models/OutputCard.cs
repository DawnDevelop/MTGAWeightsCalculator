namespace MTGAWeightsCalculator.Models;

public record OutputCards(int TotalWeight, List<OutputCard> Cards);

public record OutputCard(int Quantity, string CardName, int Weight, int SingleWeight);
