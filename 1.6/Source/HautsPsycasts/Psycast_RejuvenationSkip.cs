using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HautsPsycasts
{
    /*doesn't just use Anomaly regeneration because that requires Anomaly, which I don't presume you necessarily have. Don't let any shill shame you for not having Anomaly, it's just mid
     * ticksToNextHeal: every this amount of ticks, do the heal (reduce the severity of a random Injury hediff, or if there are none, turn a random MissingPart into an Injury).
     * healPerSeverity: the severity removed from a random Injury hediff = [this amount * {this hediff's severity, rounded up to nearest whole number}]
     * hediffOnRemoval: when this hediff is removed (as it is on taking damage, see the Notify_PawnPostApplyDamage override; or upon expiring naturally; or upon pushing the gizmo) the pawn gains this hediff.
     * icon, various "button" fields: for the gizmo that lets you prematurely remove this hediff. The "fantasy" tooltip is what gets shown if you're using RPG Fantasy Flavour Pack
     * This hediff is hardcoded to remove itself if the pawn lacks any healable hediffs, isn't player controlled, and isn't in a mental state (essentially as if the pawn is making the decision to terminate it themselves).
     */
    public class HediffCompProperties_RJ : HediffCompProperties_SeverityPerDay
    {
        public HediffCompProperties_RJ()
        {
            this.compClass = typeof(HediffComp_RJ);
        }
        public int ticksToNextHeal;
        public float healPerSeverity;
        public string icon;
        [MustTranslate]
        public string buttonLabel;
        [MustTranslate]
        public string buttonTooltip;
        [MustTranslate]
        public string buttonTooltipFantasy;
        public HediffDef hediffOnRemoval;
    }
    public class HediffComp_RJ : HediffComp_SeverityPerDay
    {
        public HediffCompProperties_RJ Props
        {
            get
            {
                return (HediffCompProperties_RJ)this.props;
            }
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.icon, true);
            this.buttonLabel = this.Props.buttonLabel.Translate();
            this.buttonTooltip = (ModCompatibilityUtility.IsHighFantasy() ? this.Props.buttonTooltipFantasy : this.Props.buttonTooltip).Translate();
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            this.Pawn.health.RemoveHediff(this.parent);
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            this.Pawn.health.AddHediff(this.Props.hediffOnRemoval);
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (this.parent.ageTicks % this.Props.ticksToNextHeal == 0)
            {
                float bankedHealing = (float)Math.Ceiling(this.parent.Severity) * this.Props.healPerSeverity;
                while (bankedHealing > 0f)
                {
                    List<Hediff> injuries = new List<Hediff>();
                    List<Hediff> missingParts = new List<Hediff>();
                    foreach (Hediff h in this.Pawn.health.hediffSet.hediffs)
                    {
                        if (h is Hediff_Injury)
                        {
                            injuries.Add(h);
                        }
                        else if (h is Hediff_MissingPart && (h.Part.parent == null || (!this.Pawn.health.hediffSet.PartIsMissing(h.Part.parent) && !this.Pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(h.Part.parent))))
                        {
                            missingParts.Add(h);
                        }
                    }
                    if (!this.Pawn.IsPlayerControlled && !this.Pawn.InMentalState && injuries.Count == 0 && missingParts.Count == 0)
                    {
                        this.Pawn.health.RemoveHediff(this.parent);
                        return;
                    }
                    if (injuries.Count > 0)
                    {
                        Hediff h = injuries.RandomElement();
                        float toHeal = Math.Min(h.Severity, 1f);
                        h.Severity -= toHeal;
                    }
                    else if (missingParts.Count > 0)
                    {
                        Hediff h = missingParts.RandomElement();
                        BodyPartRecord part = h.Part;
                        this.Pawn.health.RemoveHediff(h);
                        Hediff hediff5 = this.Pawn.health.AddHediff(HediffDefOf.Misc, part, null, null);
                        float partHealth = this.Pawn.health.hediffSet.GetPartHealth(part);
                        hediff5.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);
                    }
                    bankedHealing -= 1f;
                }
            }
            base.CompPostTick(ref severityAdjustment);
        }
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (this.uiIcon == null)
            {
                this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.icon, true);
            }
            if (this.Pawn.IsPlayerControlled || DebugSettings.ShowDevGizmos)
            {
                Command_Action cmdRecall = new Command_Action
                {
                    defaultLabel = this.buttonLabel.Formatted().Resolve(),
                    defaultDesc = this.buttonTooltip.Formatted(this.Pawn.Named("PAWN")).AdjustedFor(this.Pawn, "PAWN", true).Resolve(),
                    icon = this.uiIcon,
                    action = delegate ()
                    {
                        this.Pawn.health.RemoveHediff(this.parent);
                    }
                };
                yield return cmdRecall;
            }
            yield break;
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<string>(ref this.buttonLabel, "buttonLabel", this.Props.buttonLabel.Translate(), false);
            Scribe_Values.Look<string>(ref this.buttonTooltip, "buttonTooltip", this.Props.buttonTooltip.Translate(), false);
        }
        Texture2D uiIcon;
        string buttonLabel;
        string buttonTooltip;
    }
}
