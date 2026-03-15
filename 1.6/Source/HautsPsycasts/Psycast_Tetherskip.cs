using HautsFramework;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HautsPsycasts
{
    //derivative of GiveHediffPaired that obeys the size restriction that non-modded Skip psycasts have
    public class CompProperties_AbilityTetherSkip : CompProperties_AbilityGiveHediffPaired
    {
        public float maxBodySize;
    }
    public class CompAbilityEffect_TetherSkip : CompAbilityEffect_GiveHediffPaired
    {
        public new CompProperties_AbilityTetherSkip Props
        {
            get
            {
                return (CompProperties_AbilityTetherSkip)this.props;
            }
        }
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            AcceptanceReport acceptanceReport = this.CanSkipTarget(target);
            if (!acceptanceReport)
            {
                if (showMessages && !acceptanceReport.Reason.NullOrEmpty() && target.Thing is Pawn pawn)
                {
                    Messages.Message("CannotSkipTarget".Translate(pawn.Named("PAWN")) + ": " + acceptanceReport.Reason, pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, showMessages);
        }
        private AcceptanceReport CanSkipTarget(LocalTargetInfo target)
        {
            Pawn pawn;
            if ((pawn = target.Thing as Pawn) != null)
            {
                if (pawn.BodySize > this.Props.maxBodySize)
                {
                    return "CannotSkipTargetTooLarge".Translate();
                }
                if (pawn.kindDef.skipResistant)
                {
                    return "CannotSkipTargetPsychicResistant".Translate();
                }
            }
            return true;
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            return this.CanSkipTarget(target).Reason;
        }
    }
    /*tetherAnchor: when the hediff is gained, create this object at own position and Link to it
     * stunTicks: when the hediff is removed for any reason, the pawn not only teleports to the anchor's location, but is also stunned for a random value in this duration
     * other fields are S/VFX stuff*/
    public class HediffCompProperties_TetherVictim : HediffCompProperties_Link
    {
        public HediffCompProperties_TetherVictim()
        {
            this.compClass = typeof(HediffComp_TetherVictim);
        }
        public ThingDef tetherAnchor;
        public FleckDef fleckEntry;
        public SoundDef soundEntry;
        public FleckDef fleckExit1;
        public FleckDef fleckExit2;
        public SoundDef soundExit;
        public IntRange stunTicks;
    }
    public class HediffComp_TetherVictim : HediffComp_Link
    {
        public new HediffCompProperties_TetherVictim Props
        {
            get
            {
                return (HediffCompProperties_TetherVictim)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Thing anchor = GenSpawn.Spawn(this.Props.tetherAnchor, this.Pawn.PositionHeld, this.Pawn.Map, WipeMode.Vanish);
            this.other = anchor;
            this.drawConnection = true;
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (this.other != null)
            {
                HVPUtility.TetherSkipBack(this.Pawn, this.other, this.Props.fleckEntry, this.Props.soundEntry, this.Props.fleckExit1, this.Props.fleckExit2, this.Props.soundExit, this.Props.stunTicks, true);
            }
        }
    }
    /*the pawn that casts Tetherskip has the ability to remove any hediff they've applied with the ability. This is done by pairing those hediffs to this hediff, which is also created at time of cast,
     * and then providing a gizmo per such hediff that triggers its removal from the pawn it's on.
     * buttonTooltipFantasy is, as with any other "somethingFantasy", alternative text that displays when using RPG Flavour Adventure Pack.
     * aiCanAutoRecall allows NPC casters to utilize this hediff's hediff-removal powers. See AIShouldRecall(). You can patch AIShouldRecallOtherQualification(Hediff) to add more restrictions on when they do this.*/
    public class HediffCompProperties_LinkRevoker : HediffCompProperties_PairedHediff
    {
        public HediffCompProperties_LinkRevoker()
        {
            this.compClass = typeof(HediffComp_LinkRevoker);
        }
        public string icon;
        [MustTranslate]
        public string buttonLabel;
        [MustTranslate]
        public string buttonTooltip;
        [MustTranslate]
        public string buttonTooltipFantasy;
        public bool aiCanAutoRecall = false;
    }
    public class HediffComp_LinkRevoker : HediffComp_PairedHediff
    {
        public new HediffCompProperties_LinkRevoker Props
        {
            get
            {
                return (HediffCompProperties_LinkRevoker)this.props;
            }
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.icon, true);
            this.buttonLabel = this.Props.buttonLabel.Translate();
            this.buttonTooltip = (ModCompatibilityUtility.IsHighFantasy() ? this.Props.buttonTooltipFantasy : this.Props.buttonTooltip).Translate();
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(15, delta) && !this.Pawn.IsColonistPlayerControlled && this.hediffs != null)
            {
                for (int i = this.hediffs.Count - 1; i >= 0; i--)
                {
                    if (this.AIShouldRecall(this.hediffs[i]))
                    {
                        this.Recall(this.hediffs[i]);
                        break;
                    }
                }
            }
        }
        public bool AIShouldRecall(Hediff h)
        {
            if (((this.Props.aiCanAutoRecall && this.Pawn.HostileTo(h.pawn)) || this.AIShouldRecallOtherQualification(h)) && this.hediffs != null && this.hediffs.Contains(h) && h.pawn.Spawned)
            {
                HediffComp_TetherVictim hctv = h.TryGetComp<HediffComp_TetherVictim>();
                if (hctv != null && hctv.other != null && hctv.other.Position.IsValid)
                {
                    if (h.pawn.Position.DistanceTo(hctv.other.Position) >= 4f * h.pawn.GetStatValue(StatDefOf.MoveSpeed) || (h.pawn.Position.DistanceTo(hctv.other.Position) >= 2f * h.pawn.GetStatValue(StatDefOf.MoveSpeed) && !h.pawn.pather.Moving && CoverUtility.TotalSurroundingCoverScore(h.pawn.Position, h.pawn.Map) > 0f))
                    {
                        return true;
                    }
                    float meleeJumpCheck = 0f;
                    foreach (Pawn p in GenRadial.RadialDistinctThingsAround(hctv.other.Position, h.pawn.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (p.HostileTo(h.pawn))
                        {
                            meleeJumpCheck += p.GetStatValue(StatDefOf.MeleeDPS);
                            if (1.4f * meleeJumpCheck >= h.pawn.GetStatValue(StatDefOf.MeleeDPS))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        public bool AIShouldRecallOtherQualification(Hediff h)
        {
            return false;
        }
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (this.uiIcon == null)
            {
                this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.icon, true);
            }
            if (this.hediffs != null && HVPUtility.ShouldShowExtraPsycastGizmo(this.Pawn))
            {
                foreach (Hediff h in this.hediffs)
                {
                    if (h.pawn != null && h.pawn.Spawned)
                    {
                        Command_Action cmdRecall = new Command_Action
                        {
                            defaultLabel = this.buttonLabel.Formatted(h.pawn.Named("PAWN")).AdjustedFor(h.pawn, "PAWN", true).Resolve(),
                            defaultDesc = this.buttonTooltip.Formatted(h.pawn.Named("PAWN")).AdjustedFor(h.pawn, "PAWN", true).Resolve(),
                            icon = this.uiIcon,
                            action = delegate ()
                            {
                                this.Recall(h);
                            }
                        };
                        yield return cmdRecall;
                    }
                }
            }
            yield break;
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<string>(ref this.buttonLabel, "buttonLabel", this.Props.buttonLabel.Translate(), false);
            Scribe_Values.Look<string>(ref this.buttonTooltip, "buttonTooltip", this.Props.buttonTooltip.Translate(), false);
        }
        private void Recall(Hediff h)
        {
            if (this.Pawn.Spawned)
            {
                GenClamor.DoClamor(this.Pawn, this.Pawn.Position, 10f, ClamorDefOf.Ability);
            }
            h.pawn.health.RemoveHediff(h);
        }
        Texture2D uiIcon;
        string buttonLabel;
        string buttonTooltip;
    }
}
