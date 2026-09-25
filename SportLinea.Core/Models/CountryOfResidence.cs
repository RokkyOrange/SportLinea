namespace SportLinea.Models;

public enum CountryOfResidence
{
    USA,
    Russia
}

public static class CountryPolicy
{
    public static bool AllowsCrazyTime(CountryOfResidence country) =>
        country == CountryOfResidence.USA;

    public static string GetDisplayName(CountryOfResidence country) => country switch
    {
        CountryOfResidence.USA => "США",
        CountryOfResidence.Russia => "Россия",
        _ => country.ToString()
    };
}
