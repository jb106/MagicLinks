using System;

namespace MagicLinks.Network
{
    /// <summary>
    /// Receives the networked links declared by the generated script (Assets/Resources/MagicLinks/Network).
    /// The generated script lives in the game's assembly, so it registers itself here at startup.
    /// </summary>
    public static class MagicLinksNetworkRegistry
    {
        internal static uint SchemaHash { get; private set; }
        internal static Action<MagicLinksNetworkSession> Registration { get; private set; }
        internal static string[] GenerationErrors { get; private set; } = Array.Empty<string>();

        public static bool HasNetworkedLinks => Registration != null || GenerationErrors.Length > 0;

        public static void Install(uint schemaHash, Action<MagicLinksNetworkSession> registration, string[] generationErrors)
        {
            SchemaHash = schemaHash;
            Registration = registration;
            GenerationErrors = generationErrors ?? Array.Empty<string>();
        }
    }
}
