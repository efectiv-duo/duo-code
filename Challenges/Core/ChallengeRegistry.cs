using System.Reflection;

namespace duo_code.Challenges.Core;

public static class ChallengeRegistry
{
    private static readonly Dictionary<int, IChallenge> _challenges = new();
    private static bool _initialized = false;
    
    public static void Initialize()
    {
        if (_initialized) return;
        
        // Find all types that implement IChallenge
        var challengeTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IChallenge).IsAssignableFrom(t))
            .ToList();
        
        Console.WriteLine($"Found {challengeTypes.Count} challenge types");
        
        foreach (var type in challengeTypes)
        {
            try
            {
                var challenge = Activator.CreateInstance(type) as IChallenge;
                if (challenge != null)
                {
                    _challenges[challenge.Id] = challenge;
                    Console.WriteLine($"Registered challenge {challenge.Id}: {challenge.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create challenge {type.Name}: {ex.Message}");
            }
        }
        
        _initialized = true;
    }
    
    public static IChallenge? GetChallenge(int id)
    {
        Initialize();
        return _challenges.TryGetValue(id, out var challenge) ? challenge : null;
    }
    
    public static IEnumerable<IChallenge> GetAllChallenges()
    {
        Initialize();
        return _challenges.Values.OrderBy(c => c.Id);
    }
}