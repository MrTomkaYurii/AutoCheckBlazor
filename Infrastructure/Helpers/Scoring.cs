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

    /// <summary>
    /// Per-task score from weighted acceptance criteria:
    /// score = Σ(weight of met requirements) / Σ(weight of all requirements) × 100.
    /// All-equal weights reduce to a plain met/total ratio.
    /// </summary>
    public static int FromRequirements(
        IReadOnlyCollection<double> metWeights,
        IReadOnlyCollection<double> unmetWeights)
    {
        double total = metWeights.Sum() + unmetWeights.Sum();
        if (total <= 0) return 0;
        return Math.Clamp((int)Math.Round(metWeights.Sum() / total * 100), 0, 100);
    }

    /// <summary>
    /// Final lab grade from the auto-check and defence scores: 40% auto + 60% defence,
    /// rounded and clamped to 0–100. Single source of truth for the formula shown in
    /// the grade dialog and used when seeding demo data.
    /// </summary>
    public static int Final(int auto, int defense) =>
        Math.Clamp((int)Math.Round(0.4 * auto + 0.6 * defense), 0, 100);
}
