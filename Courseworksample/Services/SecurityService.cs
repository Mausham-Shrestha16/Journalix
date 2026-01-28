using System.Security.Cryptography;
using System.Text;

namespace Courseworksample.Services;

public class SecurityService
{
    private const string PinKey = "cw_pin_hash_v1";

    public async Task<bool> HasPinAsync()
        => !string.IsNullOrWhiteSpace(await SecureStorage.GetAsync(PinKey));

    public async Task SetPinAsync(string pin)
        => await SecureStorage.SetAsync(PinKey, Hash(pin));

    public async Task<bool> VerifyPinAsync(string pin)
    {
        var saved = await SecureStorage.GetAsync(PinKey);
        if (string.IsNullOrWhiteSpace(saved)) return false;
        return saved == Hash(pin);
    }

    private static string Hash(string input)
    {
        input ??= "";
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }
}
