using HautsFramework;
using RimWorld;
using Verse;

namespace HautsPsycasts
{
    /*damageToStolenThing: deal this much Deterioration damage to the stolen item
     * goodwillDamageIfCaughtStealing: if you steal from a member of an ultratech (or archotech) faction, or if you steal apparel or equipment, you lose this much goodwill with the victim's faction
     * alertRaise: regardless of how it's used, the target gains this much Alert Level (a stat which makes it harder for your pawns to pickpocket them, see the Framework's pilfering system)*/
    public class CompProperties_AbilitySkipTheft : CompProperties_AbilityEffect
    {
        public IntRange damageToStolenThing;
        public int goodwillDamageIfCaughtStealing;
        public float alertRaise;
    }
    public class CompAbilityEffect_SkipTheft : CompAbilityEffect
    {
        public new CompProperties_AbilitySkipTheft Props
        {
            get
            {
                return (CompProperties_AbilitySkipTheft)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null && target.Thing is Pawn p)
            {
                if (p.HostileTo(this.parent.pawn))
                {
                    if (p.equipment != null && p.equipment.HasAnything())
                    {
                        this.StealEquipment(p);
                    }
                    else if (p.apparel != null && p.apparel.WornApparelCount > 0)
                    {
                        this.StealApparel(p);
                    }
                    else
                    {
                        this.StealFromInventory(p);
                    }
                }
                else
                {
                    if (p.inventory != null && p.inventory.innerContainer.Count > 0)
                    {
                        this.StealFromInventory(p);
                    }
                    else if (p.apparel != null && p.apparel.WornApparelCount > 0)
                    {
                        this.StealApparel(p);
                    }
                    else
                    {
                        this.StealEquipment(p);
                    }
                }
                PilferingSystemUtility.IncreaseAlertLevel(p, this.Props.alertRaise);
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Thing != null && target.Thing is Pawn p)
            {
                if ((p.inventory != null && p.inventory.innerContainer.Count > 0) || ((!p.kindDef.destroyGearOnDrop || p.kindDef.canStrip) && ((p.apparel != null && p.apparel.WornApparelCount > 0) || (p.equipment != null && p.equipment.HasAnything()))))
                {
                    return true;
                }
            }
            if (throwMessages)
            {
                Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_TargetLacksStealables".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
            }
            return false;
        }
        protected void StealFromInventory(Pawn p)
        {
            Thing toSteal = p.inventory.innerContainer.RandomElement();
            if (toSteal.stackCount > toSteal.def.stackLimit)
            {
                p.inventory.TryAddAndUnforbid(toSteal.SplitOff(toSteal.stackCount - toSteal.def.stackLimit));
            }
            p.inventory.RemoveCount(toSteal.def, toSteal.stackCount, false);
            this.parent.pawn.inventory.TryAddAndUnforbid(toSteal);
            if (toSteal.def.useHitPoints)
            {
                toSteal.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, this.Props.damageToStolenThing.RandomInRange, 0f, -1f, this.parent.pawn));
            }
            if (p.Faction != null && (p.Faction.def.techLevel == TechLevel.Ultra || p.Faction.def.techLevel == TechLevel.Archotech))
            {
                this.CaughtStealing(p);
            }
        }
        protected void StealApparel(Pawn p)
        {
            Apparel toSteal = p.apparel.WornApparel.RandomElement();
            p.apparel.Unlock(toSteal);
            p.apparel.Remove(toSteal);
            this.parent.pawn.inventory.TryAddAndUnforbid(toSteal);
            if (toSteal.def.useHitPoints)
            {
                toSteal.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, this.Props.damageToStolenThing.RandomInRange, 0f, -1f, this.parent.pawn));
            }
            this.CaughtStealing(p);
        }
        protected void StealEquipment(Pawn p)
        {
            ThingWithComps toSteal = p.equipment.Primary;
            p.equipment.Remove(toSteal);
            this.parent.pawn.inventory.TryAddAndUnforbid(toSteal);
            if (toSteal.def.useHitPoints)
            {
                toSteal.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, this.Props.damageToStolenThing.RandomInRange, 0f, -1f, this.parent.pawn));
            }
            this.CaughtStealing(p);
        }
        protected void CaughtStealing(Pawn p)
        {
            if (!p.IsSlaveOfColony)
            {
                Faction homeFaction = p.HomeFaction;
                if (this.Props.goodwillDamageIfCaughtStealing != 0 && this.parent.pawn.Faction == Faction.OfPlayer && homeFaction != null && !homeFaction.HostileTo(this.parent.pawn.Faction) && (this.Props.applyGoodwillImpactToLodgers || !p.IsQuestLodger()) && !p.IsQuestHelper())
                {
                    Faction.OfPlayer.TryAffectGoodwillWith(homeFaction, this.Props.goodwillDamageIfCaughtStealing, true, true, HistoryEventDefOf.UsedHarmfulAbility, null);
                }
            }
        }
    }
}
