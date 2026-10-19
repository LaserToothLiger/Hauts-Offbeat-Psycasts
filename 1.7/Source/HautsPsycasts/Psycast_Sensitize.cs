using HautsFramework;
using Verse;

namespace HautsPsycasts
{
    //Sensitize-specific fields govern the psysens debuff inflicted on self. It's a PairedHediff to the psysens buff on the target, so removing one preemptively removes the other.
    public class CompProperties_AbilitySensitize : CompProperties_AbilityGiveHediffCasterStatScalingSeverity
    {
        public HediffDef hediffToSelf;
        public float severitySelf;
    }
    public class CompAbilityEffect_Sensitize : CompAbilityEffect_GiveHediffCasterStatScalingSeverity
    {
        public new CompProperties_AbilitySensitize Props
        {
            get
            {
                return (CompProperties_AbilitySensitize)this.props;
            }
        }
        protected override void RefreshMoreSevereHediff(Pawn target, Hediff firstHediffOfDef)
        {
            base.RefreshMoreSevereHediff(target, firstHediffOfDef);
            HediffComp_Disappears hcd = firstHediffOfDef.TryGetComp<HediffComp_Disappears>();
            if (hcd != null)
            {
                hcd.ticksToDisappear = base.GetDurationSeconds(target).SecondsToTicks();
                HediffComp_PairedHediff ph = firstHediffOfDef.TryGetComp<HediffComp_PairedHediff>();
                if (ph != null)
                {
                    ph.SynchronizePairedHediffDurations();
                }
                return;
            }
        }
        protected override void ModifyCreatedHediff(Pawn target, Hediff h)
        {
            base.ModifyCreatedHediff(target, h);
            Hediff hediffToSelf = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffToSelf);
            if (hediffToSelf == null)
            {
                hediffToSelf = HediffMaker.MakeHediff(this.Props.hediffToSelf, this.parent.pawn);
                hediffToSelf.Severity = this.Props.severitySelf;
                this.parent.pawn.health.AddHediff(hediffToSelf);
            }
            else
            {
                hediffToSelf.Severity += this.Props.severitySelf;
            }
            HediffComp_Disappears hediffComp_Disappears = hediffToSelf.TryGetComp<HediffComp_Disappears>();
            if (hediffComp_Disappears != null)
            {
                hediffComp_Disappears.ticksToDisappear = base.GetDurationSeconds(target).SecondsToTicks();
            }
            if (h is HediffWithComps)
            {
                HediffComp_PairedHediff ph = h.TryGetComp<HediffComp_PairedHediff>();
                if (ph != null)
                {
                    ph.hediffs.Add(hediffToSelf);
                    if (hediffToSelf is HediffWithComps)
                    {
                        HediffComp_PairedHediff ph2 = hediffToSelf.TryGetComp<HediffComp_PairedHediff>();
                        if (ph2 != null)
                        {
                            ph2.hediffs.Add(h);
                        }
                    }
                    ph.SynchronizePairedHediffDurations();
                }
            }
        }
    }
}
