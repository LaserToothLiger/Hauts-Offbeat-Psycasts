using RimWorld;
using Verse;

namespace HautsPsycasts
{
    //Thunderbolt does not call an actual lightning strike, since WeatherEvent_LightningStrike.DoStrike has a fixed damage and armor pen. Instead, it calls ArmourPiercingBolt, which lets those values be specified
    public class CompProperties_AbilityLightningStrike : CompProperties_AbilityEffect
    {
        public int damage;
        public float armorPenetration;
    }
    public class CompAbilityEffect_LightningStrike : CompAbilityEffect
    {
        public new CompProperties_AbilityLightningStrike Props
        {
            get
            {
                return (CompProperties_AbilityLightningStrike)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            HVPUtility.ArmourPiercingBolt(target.Cell, this.parent.pawn.Map, this.parent.pawn, this.Props.damage, this.Props.armorPenetration);
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (this.parent.pawn.Map.roofGrid.Roofed(target.Cell))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_RoofedLightning".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return true;
        }
    }
}
