namespace SportLinea.Models;

public static class SportTypes
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByCategory =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [SportCategories.Professional] = new[]
            {
                "Футбол", "Хоккей", "Теннис", "Баскетбол", "ММА"
            },
            [SportCategories.Amateur] = new[]
            {
                "Футбол", "Баскетбол", "Волейбол"
            },
            [SportCategories.Esports] = new[]
            {
                "CS2", "Dota 2", "League of Legends", "Valorant"
            }
        };

    public static IReadOnlyList<string> AllCategories { get; } =
        new[] { SportCategories.Esports, SportCategories.Professional, SportCategories.Amateur };

    public static IReadOnlyList<string> GetForCategory(string category) =>
        ByCategory.TryGetValue(category, out var sports) ? sports : Array.Empty<string>();

    public static bool IsValid(string category, string sportType) =>
        GetForCategory(category).Contains(sportType);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetByCategory() => ByCategory;
}
