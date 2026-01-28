using SQLite;

namespace Courseworksample.Models;

public class JournalEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // One entry per day
    [Indexed(Unique = true)]
    public DateTime EntryDate { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    [MaxLength(50)]
    public string PrimaryMood { get; set; } = "";

    [MaxLength(50)]
    public string? SecondaryMood1 { get; set; }

    [MaxLength(50)]
    public string? SecondaryMood2 { get; set; }

    [MaxLength(80)]
    public string Category { get; set; } = "General";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int WordCount { get; set; }
}
 