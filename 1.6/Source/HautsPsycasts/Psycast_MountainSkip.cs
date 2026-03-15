using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HautsPsycasts
{
    /*Mountain Skip can't be used if the current weather is in the disallowedWeathers blacklist.
     * This is because the most consistent signifier that you're in an underground map, which we don't want this psycast to work in for narrative reasons, is the Underground weather type.*/
    public class CompProperties_AbilityRoofSkip : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityRoofSkip()
        {
            this.compClass = typeof(CompAbilityEffect_RoofSkip);
        }
        public List<WeatherDef> disallowedWeathers;
    }
    public class CompAbilityEffect_RoofSkip : CompAbilityEffect
    {
        public new CompProperties_AbilityRoofSkip Props
        {
            get
            {
                return (CompProperties_AbilityRoofSkip)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            RoofDef roof = target.Cell.GetRoof(this.parent.pawn.Map);
            if (roof != null)
            {
                if (roof.canCollapse)
                {
                    if (roof == RoofDefOf.RoofRockThin)
                    {
                        this.parent.pawn.Map.roofGrid.SetRoof(target.Cell, RoofDefOf.RoofRockThick);
                    }
                    else
                    {
                        this.parent.pawn.Map.roofGrid.SetRoof(target.Cell, RoofDefOf.RoofRockThin);
                    }
                }
            }
            else if (RoofCollapseUtility.WithinRangeOfRoofHolder(target.Cell, this.parent.pawn.Map, false) && RoofCollapseUtility.ConnectedToRoofHolder(target.Cell, this.parent.pawn.Map, true))
            {
                this.parent.pawn.Map.roofGrid.SetRoof(target.Cell, RoofDefOf.RoofRockThin);
            }
            FleckMaker.ThrowDustPuff(target.Cell, this.parent.pawn.Map, 2f);
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            WeatherDef weather = this.parent.pawn.Map.weatherManager.curWeather;
            if (weather != null && this.Props.disallowedWeathers.Contains(weather))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_YoureUnderground".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            RoofDef roof = target.Cell.GetRoof(this.parent.pawn.Map);
            if (roof != null)
            {
                if (!roof.canCollapse)
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_InvincibleRoof".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            else if (!RoofCollapseUtility.WithinRangeOfRoofHolder(target.Cell, this.parent.pawn.Map, false) || !RoofCollapseUtility.ConnectedToRoofHolder(target.Cell, this.parent.pawn.Map, true))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_WhereYouGonnaPutThatRoofEh".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }
}
