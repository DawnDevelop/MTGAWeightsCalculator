namespace MTGAWeightsCalculator.Models;

public class InputCard
{
    public string Name { get; set; }
    public int Quantity { get; set; }

    public InputCard(string name, int quantity)
    {
        Name = name;
        Quantity = quantity;
    }
}

public class Deck
{
    public List<InputCard> MainDeck { get; set; } = [];
    public List<InputCard> Sideboard { get; set; } = [];
}