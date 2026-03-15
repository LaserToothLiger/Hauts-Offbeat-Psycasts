using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace HautsPsycasts
{
    /*Evict's psyfocus cost scales with the nature of the target, between 2.5% and 60%.
     * killChance: Evict usually just despawns the target instantly, but if this chance is rolled then the target is Destroyed and then Killed
     * maxBodySize: if positive, prohibits the use of Evict on pawns whose body size exceeds this value. You could turn it on if you wanted Evict to be worse, I suppose. Why
     * respectsSkipResistance: a few pawn kinds are "skip-resistant" e.g. the Biotech apocriton, and can't normally be targeted by Skip or the like. Evict will only fail on skip-resistant pawns if you turn this on
     * immunePawnKinds: you can make certain pawns unable to be Evicted. I use this for Cooler Psycasts' superboss since it would otherwise be a disappointing and cheesy solution
     * psyfocusCostPerVictimScalar: psyfocus cost for given victim is determined by the evaluation of its body size (if pawn) or square footprint (if building) on this curve.*/
    public class CompProperties_AbilityEvict : CompProperties_AbilityEffect
    {
        public override bool OverridesPsyfocusCost
        {
            get
            {
                return true;
            }
        }
        public CompProperties_AbilityEvict()
        {
            this.compClass = typeof(CompAbilityEffect_Evict);
        }
        public override FloatRange PsyfocusCostRange
        {
            get
            {
                return new FloatRange(0.025f, 0.6f);
            }
        }
        public override string PsyfocusCostExplanation
        {
            get
            {
                string costString = ModCompatibilityUtility.IsHighFantasy() ? "HVP_EvictionSkipCostsF" : "HVP_EvictionSkipCosts";
                StringBuilder stringBuilder = new StringBuilder(costString.Translate() + ":");
                stringBuilder.AppendLine();
                foreach (CurvePoint point in this.psyfocusCostPerVictimSize.Points)
                {
                    stringBuilder.AppendLine("  - " + point.x + ": " + point.y.ToStringPercent());
                }
                return stringBuilder.ToString();
            }
        }
        public float killChance;
        public FleckDef fleck1;
        public FleckDef fleck2;
        public float fleckScale;
        public float maxBodySize;
        public bool respectsSkipResistance;
        public List<PawnKindDef> immunePawnKinds;
        public SimpleCurve psyfocusCostPerVictimSize;
    }
    public class CompAbilityEffect_Evict : CompAbilityEffect
    {
        public new CompProperties_AbilityEvict Props
        {
            get
            {
                return (CompProperties_AbilityEvict)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null)
            {
                float size = 1.1f * Math.Max(0.9f, (float)Math.Sqrt(target.Thing is Pawn pawn ? pawn.BodySize : target.Thing.def.Size.x * target.Thing.def.Size.z));
                if (this.Props.fleck1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(target.Cell.ToVector3Shifted(), target.Thing.Map, this.Props.fleck1, this.Props.fleckScale * size);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    target.Thing.Map.flecks.CreateFleck(dataStatic);
                    if (this.Props.fleck2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(target.Cell.ToVector3Shifted(), target.Thing.Map, this.Props.fleck2, this.Props.fleckScale * size);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        target.Thing.Map.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (target.Thing is Pawn p)
                {
                    if (!Rand.Chance(this.Props.killChance))
                    {
                        Faction faction = p.Faction ?? null;
                        p.ExitMap(false, p.def.defaultPlacingRot);
                        /*p.DeSpawn(DestroyMode.Vanish);
                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Decide);*/
                        if (p.Faction != faction)
                        {
                            p.SetFaction(faction);
                        }
                    }
                    else
                    {
                        p.Destroy();
                        p.Kill(null);
                    }
                }
                else
                {
                    target.Thing.Destroy();
                }
            }
        }
        public override float PsyfocusCostForTarget(LocalTargetInfo target)
        {
            if (target.Thing != null)
            {
                if (target.Thing is Pawn p)
                {
                    if (!this.Props.immunePawnKinds.NullOrEmpty() && this.Props.immunePawnKinds.Contains(p.kindDef))
                    {
                        return 6f;
                    }
                    return this.Props.psyfocusCostPerVictimSize.Evaluate(p.BodySize);
                }
                else if (target.Thing is Building b)
                {
                    return this.Props.psyfocusCostPerVictimSize.Evaluate(b.def.Size.x * b.def.Size.z);
                }
            }
            return this.parent.def.PsyfocusCost;
        }
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            if (target.Thing != null)
            {
                if (target.Thing is Pawn p)
                {
                    AcceptanceReport acceptanceReport = this.CanSkipTarget(target);
                    if (!acceptanceReport)
                    {
                        if (showMessages && !acceptanceReport.Reason.NullOrEmpty())
                        {
                            Messages.Message("CannotSkipTarget".Translate(p.Named("PAWN")) + ": " + acceptanceReport.Reason, p, MessageTypeDefOf.RejectInput, false);
                        }
                        return false;
                    }
                }
                else
                {
                    if (showMessages && !target.Thing.def.useHitPoints)
                    {
                        Messages.Message("HVP_MustBeVincible".Translate(), target.Thing, MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                }
                float num = this.PsyfocusCostForTarget(target);
                if (num > this.parent.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                {
                    if (showMessages)
                    {
                        string notEnoughPsyfocus = ModCompatibilityUtility.IsHighFantasy() ? "HVP_CommandPsycastNotEnoughPsyfocusForSizeF" : "HVP_CommandPsycastNotEnoughPsyfocusForSize";
                        Messages.Message("HVP_CommandPsycastNotEnoughPsyfocusForSize".Translate(num.ToStringPercent(), this.parent.pawn.psychicEntropy.CurrentPsyfocus.ToStringPercent("0.#"), this.parent.def.label.Named("PSYCASTNAME"), this.parent.pawn.Named("CASTERNAME")), this.parent.pawn, MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            return base.Valid(target, showMessages);
        }
        public override bool HideTargetPawnTooltip
        {
            get
            {
                return true;
            }
        }
        private AcceptanceReport CanSkipTarget(LocalTargetInfo target)
        {
            Pawn pawn;
            if ((pawn = target.Thing as Pawn) != null)
            {
                if (this.Props.maxBodySize > 0f && pawn.BodySize > this.Props.maxBodySize)
                {
                    return "CannotSkipTargetTooLarge".Translate();
                }
                if (this.Props.respectsSkipResistance && pawn.kindDef.skipResistant)
                {
                    return "CannotSkipTargetPsychicResistant".Translate();
                }
            }
            return true;
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (target.Thing != null && this.Valid(target, false))
            {
                return "AbilityPsyfocusCost".Translate() + ": " + this.PsyfocusCostForTarget(target).ToStringPercent("0.#");
            }
            return this.CanSkipTarget(target).Reason;
        }
    }
}
