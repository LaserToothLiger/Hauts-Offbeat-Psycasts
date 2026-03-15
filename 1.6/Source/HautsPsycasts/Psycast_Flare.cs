using RimWorld;

namespace HautsPsycasts
{
    //can't cast Flare if you already have Flare
    public class CompAbilityEffect_Unleash : CompAbilityEffect_GiveHediff
    {
        public override bool CanCast
        {
            get
            {
                return !this.parent.pawn.health.hediffSet.HasHediff(this.Props.hediffDef);
            }
        }
    }
}
