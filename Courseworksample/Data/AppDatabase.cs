using SQLite;
using Courseworksample.Models;

namespace Courseworksample.Data;

public class AppDatabase
{
    private readonly string _dbPath;
    private SQLiteAsyncConnection? _conn;

    public AppDatabase()
    {
        // Slides-style: AppDataDirectory + .db3
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "coursework.db3");
    }

    public async Task<SQLiteAsyncConnection> GetAsync()
    {
        if (_conn is not null) return _conn;

        _conn = new SQLiteAsyncConnection(_dbPath);

        // Slides-style: CreateTableAsync<T>()
        await _conn.CreateTableAsync<JournalEntry>();
        await _conn.CreateTableAsync<Tag>();
        await _conn.CreateTableAsync<EntryTag>();
        await _conn.CreateTableAsync<User>();  // Added User table

        return _conn;
    }

    /// <summary>
    /// Helper method to get connection (for AuthService and other services)
    /// </summary>
    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        return await GetAsync();
    }
}