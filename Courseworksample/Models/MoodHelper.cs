namespace Courseworksample.Models;

public static class MoodHelper
{
    public static readonly Dictionary<string, List<string>> MoodsByCategory = new()
    {
        { "Positive", new List<string> { "Happy", "Excited", "Relaxed", "Grateful", "Confident" } },
        { "Neutral", new List<string> { "Calm", "Thoughtful", "Curious", "Nostalgic", "Bored" } },
        { "Negative", new List<string> { "Sad", "Angry", "Stressed", "Lonely", "Anxious" } }
    };

    public static List<string> AllMoods =>
        MoodsByCategory.Values.SelectMany(m => m).OrderBy(m => m).ToList();

    public static string GetCategory(string mood)
    {
        foreach (var kvp in MoodsByCategory)
        {
            if (kvp.Value.Any(m => m.Equals(mood, StringComparison.OrdinalIgnoreCase)))
                return kvp.Key;
        }
        return "Neutral";
    }
}