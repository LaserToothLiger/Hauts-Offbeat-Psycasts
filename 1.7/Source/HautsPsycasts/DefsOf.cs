using RimWorld;
using Verse;

namespace HautsPsycasts
{
    [DefOf]
    public static class HVPDefOf
    {
        static HVPDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVPDefOf));
        }
        public static GameConditionDef HVP_BetterFlashstorm;
        public static HediffDef HVP_Hammerspace;
        public static HediffDef HVP_IShallLiveForever;
        public static TraitDef HVP_Squaddie;
    }
}
