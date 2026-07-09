namespace AutoCheck.Models;

/// <summary>Single source of truth for the difficulty-weighted attempt score.</summary>
public static class Scoring
{
    public static int Weighted(IReadOnlyCollection<(int Score, int Difficulty)> items)
    {
        if (items.Count == 0) return 0;
        double totalWeight = items.Sum(i => (double)i.Difficulty);
        if (totalWeight <= 0)   // difficulty not set — fall back to a plain average
            return (int)Math.Round(items.Average(i => (double)i.Score));
        return (int)Math.Round(items.Sum(i => i.Score * (double)i.Difficulty) / totalWeight);
    }
}
