using Courseworksample.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Courseworksample.Data;

public class JournalRepository
{
    private readonly AppDatabase _db;
    public JournalRepository(AppDatabase db) => _db = db;

    private static DateTime Normalize(DateTime d) => d.Date;

    private static int CountWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return s.Split(new[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    // ===================== ENTRIES =====================
    public async Task<JournalEntry?> GetByDateAsync(DateTime date)
    {
        var conn = await _db.GetAsync();
        var target = Normalize(date); // ✅ compute in C#, not inside SQL/LINQ

        return await conn.Table<JournalEntry>()
            .Where(e => e.EntryDate == target)
            .FirstOrDefaultAsync();
    }

    public async Task<JournalEntry> UpsertAsync(JournalEntry entry)
    {
        var conn = await _db.GetAsync();
        entry.EntryDate = Normalize(entry.EntryDate);
        entry.WordCount = CountWords(entry.Content);

        var existing = await GetByDateAsync(entry.EntryDate);

        if (existing == null)
        {
            entry.CreatedAt = DateTime.Now;
            entry.UpdatedAt = DateTime.Now;
            await conn.InsertAsync(entry);
        }
        else
        {
            entry.Id = existing.Id;
            entry.CreatedAt = existing.CreatedAt;
            entry.UpdatedAt = DateTime.Now;
            await conn.UpdateAsync(entry);
        }

        return entry;
    }

    public async Task<int> DeleteByDateAsync(DateTime date)
    {
        var conn = await _db.GetAsync();
        var entry = await GetByDateAsync(date);
        if (entry == null) return 0;

        var links = await conn.Table<EntryTag>()
            .Where(x => x.EntryId == entry.Id).ToListAsync();

        foreach (var l in links)
            await conn.DeleteAsync(l);

        return await conn.DeleteAsync(entry);
    }

    public async Task<List<JournalEntry>> GetAllNewestFirstAsync()
    {
        var conn = await _db.GetAsync();
        return await conn.Table<JournalEntry>()
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync();
    }

    public async Task<List<JournalEntry>> GetPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var all = await GetAllNewestFirstAsync();
        return all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }

    public async Task<List<JournalEntry>> SearchAsync(string query)
    {
        var q = (query ?? "").Trim().ToLower();
        if (q.Length == 0) return new();

        var all = await GetAllNewestFirstAsync();
        return all.Where(e =>
            (e.Title ?? "").ToLower().Contains(q) ||
            (e.Content ?? "").ToLower().Contains(q)).ToList();
    }

    // ===================== TAGS =====================
    public async Task<Tag> GetOrCreateTagAsync(string name)
    {
        var conn = await _db.GetAsync();
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) clean = "General";

        var existing = await conn.Table<Tag>()
            .Where(t => t.Name.ToLower() == clean.ToLower())
            .FirstOrDefaultAsync();

        if (existing != null) return existing;

        var tag = new Tag { Name = clean };
        await conn.InsertAsync(tag);
        return tag;
    }

    public async Task SetTagsAsync(int entryId, IEnumerable<string> tags)
    {
        var conn = await _db.GetAsync();

        var old = await conn.Table<EntryTag>()
            .Where(x => x.EntryId == entryId).ToListAsync();

        foreach (var o in old)
            await conn.DeleteAsync(o);

        foreach (var t in tags
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var tag = await GetOrCreateTagAsync(t);
            await conn.InsertAsync(new EntryTag
            {
                EntryId = entryId,
                TagId = tag.Id
            });
        }
    }

    public async Task<List<string>> GetTagsAsync(int entryId)
    {
        var conn = await _db.GetAsync();

        var links = await conn.Table<EntryTag>()
            .Where(x => x.EntryId == entryId).ToListAsync();

        if (links.Count == 0) return new();

        var tagIds = links.Select(l => l.TagId).ToHashSet();
        var tags = await conn.Table<Tag>().ToListAsync();

        return tags.Where(t => tagIds.Contains(t.Id))
            .Select(t => t.Name)
            .OrderBy(x => x)
            .ToList();
    }

    // ===================== ANALYTICS =====================
    public async Task<int> GetTotalEntriesAsync()
    {
        var conn = await _db.GetAsync();
        return await conn.Table<JournalEntry>().CountAsync();
    }

    public async Task<int> GetTotalWordsAsync()
    {
        var conn = await _db.GetAsync();
        var all = await conn.Table<JournalEntry>().ToListAsync();
        return all.Sum(e => e.WordCount);
    }

    public async Task<Dictionary<string, int>> GetMoodCountsAsync()
    {
        var conn = await _db.GetAsync();
        var all = await conn.Table<JournalEntry>().ToListAsync();

        return all
            .Select(e => (e.PrimaryMood ?? "").Trim())
            .Where(m => m.Length > 0)
            .GroupBy(m => m, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, int>> GetCategoryCountsAsync()
    {
        var conn = await _db.GetAsync();
        var all = await conn.Table<JournalEntry>().ToListAsync();

        return all
            .Select(e => (e.Category ?? "General").Trim())
            .Where(c => c.Length > 0)
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, int>> GetTagCountsAsync()
    {
        var conn = await _db.GetAsync();

        var tags = await conn.Table<Tag>().ToListAsync();
        var links = await conn.Table<EntryTag>().ToListAsync();

        var tagIdToName = tags.ToDictionary(t => t.Id, t => t.Name);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in links.GroupBy(l => l.TagId))
        {
            if (!tagIdToName.TryGetValue(g.Key, out var name)) continue;

            name = (name ?? "").Trim();
            if (name.Length == 0) continue;

            counts[name] = g.Count();
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, int>> GetEntriesPerMonthAsync()
    {
        var conn = await _db.GetAsync();
        var all = await conn.Table<JournalEntry>().ToListAsync();

        return all
            .GroupBy(e => e.EntryDate.ToString("yyyy-MM"))
            .OrderByDescending(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    // ===================== STREAKS =====================
    public async Task<(int CurrentStreak, int LongestStreak, int MissedDays)> GetStreaksAsync()
    {
        var conn = await _db.GetAsync();
        var entries = await conn.Table<JournalEntry>()
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync();

        if (entries.Count == 0)
            return (0, 0, 0);

        var dates = entries.Select(e => e.EntryDate.Date).OrderByDescending(d => d).ToList();

        // Calculate current streak
        int currentStreak = 0;
        var checkDate = DateTime.Today;

        foreach (var date in dates)
        {
            if (date == checkDate || date == checkDate.AddDays(-1))
            {
                currentStreak++;
                checkDate = date.AddDays(-1);
            }
            else if (date < checkDate)
            {
                break;
            }
        }

        // Calculate longest streak
        int longestStreak = 0;
        int tempStreak = 1;

        var sortedDates = dates.OrderBy(d => d).ToList();

        for (int i = 0; i < sortedDates.Count - 1; i++)
        {
            var diff = (sortedDates[i + 1] - sortedDates[i]).Days;

            if (diff == 1)
            {
                tempStreak++;
                longestStreak = Math.Max(longestStreak, tempStreak);
            }
            else
            {
                tempStreak = 1;
            }
        }

        longestStreak = Math.Max(longestStreak, tempStreak);
        longestStreak = Math.Max(longestStreak, currentStreak);

        // Calculate missed days (from first entry to today)
        if (dates.Count > 0)
        {
            var firstEntryDate = sortedDates.First();
            var totalDays = (DateTime.Today - firstEntryDate).Days + 1;
            var missedDays = totalDays - dates.Count;
            return (currentStreak, longestStreak, Math.Max(0, missedDays));
        }

        return (currentStreak, longestStreak, 0);
    }

    // ===================== FILTERING =====================
    public async Task<List<string>> GetAllCategoriesAsync()
    {
        var conn = await _db.GetAsync();
        return (await conn.Table<JournalEntry>().ToListAsync())
            .Select(e => e.Category ?? "General")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<List<string>> GetAllMoodsAsync()
    {
        var conn = await _db.GetAsync();
        return (await conn.Table<JournalEntry>().ToListAsync())
            .Select(e => e.PrimaryMood ?? "")
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        var conn = await _db.GetAsync();
        return (await conn.Table<Tag>().ToListAsync())
            .Select(t => t.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<List<JournalEntry>> FilterAsync(string? category, string? mood, string? tag)
    {
        var conn = await _db.GetAsync();
        var entries = await conn.Table<JournalEntry>().ToListAsync();

        if (!string.IsNullOrWhiteSpace(category))
            entries = entries.Where(e =>
                string.Equals(e.Category, category,
                    StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(mood))
            entries = entries.Where(e =>
                string.Equals(e.PrimaryMood, mood,
                    StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagObj = (await conn.Table<Tag>().ToListAsync())
                .FirstOrDefault(t =>
                    string.Equals(t.Name, tag,
                        StringComparison.OrdinalIgnoreCase));

            if (tagObj == null) return new();

            var entryIds = (await conn.Table<EntryTag>().ToListAsync())
                .Where(l => l.TagId == tagObj.Id)
                .Select(l => l.EntryId)
                .ToHashSet();

            entries = entries.Where(e => entryIds.Contains(e.Id)).ToList();
        }

        return entries.OrderByDescending(e => e.EntryDate).ToList();
    }

    // ===================== EXPORT =====================
    public async Task<string> ExportToCsvAsync()
    {
        var conn = await _db.GetAsync();
        var entries = await conn.Table<JournalEntry>()
            .OrderBy(e => e.EntryDate)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,Title,Content,PrimaryMood,Category,WordCount");

        foreach (var e in entries)
        {
            string Esc(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";

            sb.AppendLine(string.Join(",",
                e.EntryDate.ToString("yyyy-MM-dd"),
                Esc(e.Title),
                Esc(e.Content),
                Esc(e.PrimaryMood),
                Esc(e.Category),
                e.WordCount
            ));
        }

        return sb.ToString();
    }

    public async Task<string> ExportToJsonAsync()
    {
        var conn = await _db.GetAsync();
        var entries = await conn.Table<JournalEntry>()
            .OrderBy(e => e.EntryDate)
            .ToListAsync();

        return System.Text.Json.JsonSerializer.Serialize(
            entries,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        );
    }

    public async Task<byte[]> ExportToPdfBytesAsync()
    {
        var conn = await _db.GetAsync();
        var entries = await conn.Table<JournalEntry>()
            .OrderBy(e => e.EntryDate)
            .ToListAsync();

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);

                page.Header().Text("Journal Export").FontSize(18).SemiBold();

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(10);

                    foreach (var e in entries)
                    {
                        col.Item().Border(1).Padding(10).Column(c =>
                        {
                            c.Spacing(4);

                            c.Item().Text($"{e.EntryDate:yyyy-MM-dd} | {e.Title}")
                                .SemiBold();

                            c.Item().Text($"Mood: {e.PrimaryMood}   Category: {e.Category}   Words: {e.WordCount}")
                                .FontSize(10);

                            if (!string.IsNullOrWhiteSpace(e.Content))
                                c.Item().Text(e.Content).FontSize(11);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    // ===================== IMPORT =====================
    // Imports entries from JSON produced by ExportToJsonAsync().
    // Note: this restores entries only (tags are not in that JSON).
    public async Task<int> ImportFromJsonAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        List<JournalEntry>? items;
        try
        {
            items = System.Text.Json.JsonSerializer.Deserialize<List<JournalEntry>>(json);
        }
        catch
        {
            return 0;
        }

        if (items == null || items.Count == 0) return 0;

        int imported = 0;

        foreach (var e in items)
        {
            // normalize & recompute
            e.EntryDate = e.EntryDate.Date;
            e.WordCount = CountWords(e.Content);

            // upsert by date
            await UpsertAsync(e);
            imported++;
        }

        return imported;
    }
}
