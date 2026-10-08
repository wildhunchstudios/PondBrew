using System;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        // Foundation only: action observations and player-facing entry/help are TUT-04/05.
        // Keep the tutorial in every full-slot snapshot meanwhile, including staff writes.
        public ShopTutorial Tutorial { get; private set; } = new ShopTutorial();

        void RestoreTutorial(TutorialSave save)
        {
            if (!ShopTutorial.TryRestore(save, Model, Cycle.DayNumber, out var tutorial))
                throw new InvalidOperationException("Cannot restore tutorial data from the validated slot.");
            Tutorial = tutorial;
        }
    }
}
