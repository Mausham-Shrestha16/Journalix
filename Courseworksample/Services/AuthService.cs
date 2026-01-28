using System.Security.Cryptography;
using System.Text;
using Courseworksample.Data;
using Courseworksample.Models;
using SQLite;

namespace Courseworksample.Services;

/// <summary>
/// Service for handling user authentication
/// </summary>
public class AuthService
{
    private readonly AppDatabase _db;
    private User? _currentUser;

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public event Action? OnAuthStateChanged;

    public AuthService(AppDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Hash password using SHA256
    /// </summary>
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    public async Task<(bool Success, string Message)> RegisterAsync(string username, string password, string email, string fullName)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username is required");

            if (username.Length < 3)
                return (false, "Username must be at least 3 characters");

            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password is required");

            if (password.Length < 6)
                return (false, "Password must be at least 6 characters");

            // Check if username already exists
            var conn = await _db.GetConnectionAsync();
            var existing = await conn.Table<User>()
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (existing != null)
                return (false, "Username already exists");

            // Create new user
            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Email = email,
                FullName = fullName,
                CreatedAt = DateTime.Now
            };

            await conn.InsertAsync(user);

            return (true, "Registration successful! Please login.");
        }
        catch (Exception ex)
        {
            return (false, $"Registration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Login user
    /// </summary>
    public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Username and password are required");

            var conn = await _db.GetConnectionAsync();
            var user = await conn.Table<User>()
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user == null)
                return (false, "Invalid username or password");

            var passwordHash = HashPassword(password);
            if (user.PasswordHash != passwordHash)
                return (false, "Invalid username or password");

            // Update last login
            user.LastLoginAt = DateTime.Now;
            await conn.UpdateAsync(user);

            // Set current user
            _currentUser = user;
            OnAuthStateChanged?.Invoke();

            return (true, "Login successful!");
        }
        catch (Exception ex)
        {
            return (false, $"Login failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Logout current user
    /// </summary>
    public void Logout()
    {
        _currentUser = null;
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Check if any user exists in the system
    /// </summary>
    public async Task<bool> HasUsersAsync()
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            var count = await conn.Table<User>().CountAsync();
            return count > 0;
        }
        catch
        {
            return false;
        }
    }
}