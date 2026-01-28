using SQLite;

namespace Courseworksample.Models;

public class Tag
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    [SQLite.MaxLength(50)]   // ✅ FIX: fully qualify
    public string Name { get; set; } = "";
}
