using System;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// The scene's start (the one object in Shop.unity): it hands over to the game's session (<see cref="GrowthSession"/>),
    /// which builds the shop, the player and the UI. Also reads the command line for the session and the validation routes.
    /// GR-17 removed the classic game it used to run.
    /// </summary>
    public sealed class ShopSession : MonoBehaviour
    {
        /// <summary>A command-line flag's value: the argument after it, "" when it has none, null when the flag isn't there.</summary>
        public static string Arg(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (args[i] == key) return i + 1 < args.Length ? args[i + 1] : "";
            return null;
        }

        void Awake()
        {
            gameObject.AddComponent<GrowthSession>();
            enabled = false;
            Destroy(this);
        }
    }
}
