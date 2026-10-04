#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // Frozen game-v2 field shape used by the format4/6 regression fixtures.
        // Serializing the current SaveData under an old envelope forges a file that
        // the strict reader must reject as the live schema evolves.
        [Serializable] sealed class SmokeGameV2
        {
            public int version=2;
            public int currency;
            public bool upgradeOwned;
            public int availableBloom,availableFlow;
            public GrowerSave[] growers;
            public SmokeIncubatorV2 incubator;
            public TankSave[] tanks;
            public string[] discoveries;
            public PriceSave[] prices;
        }
        [Serializable] sealed class SmokeIncubatorV2
        {
            public IncubatorPhase phase;
            public int stagedBloom,stagedFlow;
            public string speciesId,specimenId;
            public float secondsRemaining;
        }
        string LegacySmokeGameV2Json(bool pretty=false)
        {
            var game=shop.Model.CaptureSave();
            Check(!game.sparkResearchUnlocked && !game.mistResearchUnlocked && game.availableSpark==0 && game.availableMist==0 &&
                game.incubator.stagedSpark==0 && game.incubator.stagedMist==0 &&
                Array.TrueForAll(game.discoveries,id=>id=="pondlet" || id=="bubblebelly" || id=="ribbonfin") &&
                shop.Model.TankIds.Length==2 && shop.Model.StorageIds.Length==0 && shop.Model.ConditionerIds.Length==0,
                "Historical game-v2 fixture contains only the original two-culture shop");
            return JsonUtility.ToJson(new SmokeGameV2
            {
                currency=game.currency,upgradeOwned=game.upgradeOwned,availableBloom=game.availableBloom,availableFlow=game.availableFlow,
                growers=Array.FindAll(game.growers,g=>g.cultureId=="bloom" || g.cultureId=="flow"),
                incubator=new SmokeIncubatorV2 { phase=game.incubator.phase,stagedBloom=game.incubator.stagedBloom,
                    stagedFlow=game.incubator.stagedFlow,speciesId=game.incubator.speciesId,specimenId=game.incubator.specimenId,secondsRemaining=game.incubator.secondsRemaining },
                tanks=game.tanks,discoveries=game.discoveries,prices=game.prices
            },pretty);
        }
    }
}
#endif
