using System.Text.Json;
using SportLinea.Models;

namespace SportLinea.Services;

public interface ILineSelectionService
{
    IReadOnlyList<int> GetDisplayIds(IReadOnlyList<int> availableIds);

    IReadOnlyList<int> Refresh(IReadOnlyList<int> availableIds);
}

public class LineSelectionService : ILineSelectionService
{
    private const string SessionKey = "PlayerLineEventIds";
    private const string AwaitingRefreshKey = "PlayerLineAwaitingRefresh";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LineSelectionService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyList<int> GetDisplayIds(IReadOnlyList<int> availableIds)
    {
        var session = GetSession();

        if (IsAwaitingRefresh(session))
            return Array.Empty<int>();

        var stored = ReadStoredIds(session);
        if (stored.Count == 0)
            return Array.Empty<int>();

        var visible = stored.Where(availableIds.Contains).ToList();
        if (visible.Count == 0)
        {
            SetAwaitingRefresh(session, true);
            return Array.Empty<int>();
        }

        return visible;
    }

    public IReadOnlyList<int> Refresh(IReadOnlyList<int> availableIds)
    {
        var session = GetSession();

        if (availableIds.Count == 0)
        {
            SetAwaitingRefresh(session, true);
            session.Remove(SessionKey);
            return Array.Empty<int>();
        }

        var selected = SelectRandom(availableIds);
        SaveStoredIds(session, selected);
        SetAwaitingRefresh(session, false);
        return selected;
    }

    private ISession GetSession() =>
        _httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("HTTP session is not available.");

    private static bool IsAwaitingRefresh(ISession session) =>
        session.GetString(AwaitingRefreshKey) == "1";

    private static void SetAwaitingRefresh(ISession session, bool value)
    {
        if (value)
            session.SetString(AwaitingRefreshKey, "1");
        else
            session.Remove(AwaitingRefreshKey);
    }

    private static List<int> ReadStoredIds(ISession session)
    {
        var json = session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json))
            return new List<int>();

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch (JsonException)
        {
            return new List<int>();
        }
    }

    private static List<int> SelectRandom(IReadOnlyList<int> availableIds)
    {
        var count = Math.Min(LineRules.PlayerLineDisplayCount, availableIds.Count);
        return availableIds.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
    }

    private static void SaveStoredIds(ISession session, IReadOnlyList<int> ids) =>
        session.SetString(SessionKey, JsonSerializer.Serialize(ids));
}
