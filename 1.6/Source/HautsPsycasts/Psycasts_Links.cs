using HautsFramework;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HautsPsycasts
{
    /*Shared property of the hidden hediffs given to Link casters.
     * casterEntropyGainPerSecond: increase neural heat by [this amount * this hediff's current severity] per 60 ticks
     * entropyPeriodicity: how often that entropy is gained. It normally iterates every tick because only doing it every few ticks results in awkward-looking visible jitters on the pawn's neural heat bar,
     *   but certain other mods may necessitate adding the entropy in less frequent chunks. ISEKAI RPG LEVELING (the name is in capslock) grants xp per neural heat gain (scaling w/ the amount, but min 3 per gain),
     *   which means if not for the xpath ISEKAI RPG LEVELING patch that makes the entropyPeriodicity of all Link casts less frequent, you'd get insane xp from just maintaining a single Link.
     * severityPerLink: multiple Links of the same type with the same caster are all handled in one hidden hediff. To scale up the neural heat gain linearly with each Link of the same type, each Link should contribute
     *   an identical amount of added severity.
     * casterMustBePsycastCapable: Links automatically get removed if this hediff's pawn isn't a psycaster or has 0% psysens.
     * casterMustBeConscious: auto remove if this pawn isn't conscious
     * ticksToNextDamage: every this many ticks, triggers DoPeriodicDamage(). By default, this only (if countsAsAttack is true) notifies all Linked pawns they just took damage, which enables animals to flee or go manhunter
     *   when linked to. You can patch it to do other stuff, as Psyphon Link does.
     * icon/buttonLabel/buttonTooltip: for the gizmo that lets you remove this hediff and all its links.
     */
    public class HediffCompProperties_LinkBuildEntropy : HediffCompProperties_MultiLink
    {
        public HediffCompProperties_LinkBuildEntropy()
        {
            this.compClass = typeof(HediffComp_LinkBuildEntropy);
        }
        public float casterEntropyGainPerSecond;
        public int entropyPeriodicity = 1;
        public float severityPerLink;
        public bool casterMustBePsycastCapable = true;
        public bool casterMustBeConscious = true;
        public int ticksToNextDamage;
        public bool countsAsAttack = true;
        public string icon;
        public string buttonLabel;
        public string buttonTooltip;
    }
    public class HediffComp_LinkBuildEntropy : HediffComp_MultiLink
    {
        public new HediffCompProperties_LinkBuildEntropy Props
        {
            get
            {
                return (HediffCompProperties_LinkBuildEntropy)this.props;
            }
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.icon, true);
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.drawConnection = true;
            this.ticksToNextDamage = this.Props.ticksToNextDamage;
        }
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (this.uiIcon == null)
            {
                this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.icon, true);
            }
            if (this.others != null && this.others.Count > 0 && HVPUtility.ShouldShowExtraPsycastGizmo(this.Pawn))
            {
                Command_Action cmdRecall = new Command_Action
                {
                    defaultLabel = this.Props.buttonLabel.Translate(),
                    defaultDesc = this.Props.buttonTooltip.Translate(),
                    icon = this.uiIcon,
                    action = delegate ()
                    {
                        this.RemoveOthers();
                    }
                };
                yield return cmdRecall;
            }
            yield break;
        }
        protected virtual void RemoveOthers()
        {
            this.others.Clear();
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            this.parent.Severity = this.others != null ? this.others.Count : this.parent.Severity;
            if (!this.Pawn.Spawned || (this.Props.casterMustBePsycastCapable && (this.Pawn.psychicEntropy == null || !this.Pawn.psychicEntropy.IsPsychicallySensitive)) || (this.Props.casterMustBeConscious && (this.Pawn.DeadOrDowned || this.Pawn.Suspended || !this.Pawn.Awake())))
            {
                this.Pawn.health.RemoveHediff(this.parent);
                return;
            }
            if (this.Pawn.psychicEntropy != null && this.Pawn.IsHashIntervalTick(this.Props.entropyPeriodicity))
            {
                this.Pawn.psychicEntropy.TryAddEntropy(this.Props.casterEntropyGainPerSecond * this.parent.Severity / (60f / (float)this.Props.entropyPeriodicity), null, true, true);
                if (this.Pawn.psychicEntropy.limitEntropyAmount && this.Pawn.psychicEntropy.EntropyRelativeValue > 1f)
                {
                    this.Pawn.health.RemoveHediff(this.parent);
                    return;
                }
            }
            this.ticksToNextDamage--;
            if (this.ticksToNextDamage <= 0)
            {
                this.DoPeriodicDamage();
                this.ticksToNextDamage = this.Props.ticksToNextDamage;
            }
        }
        public virtual void DoPeriodicDamage()
        {
            for (int i = this.others.Count - 1; i >= 0; i--)
            {
                this.DoPeriodicDamageInner(this.others[i]);
            }
        }
        public virtual void DoPeriodicDamageInner(Thing t)
        {
            if (t is Pawn p && !p.Dead && this.Props.countsAsAttack)
            {
                p.mindState.Notify_DamageTaken(new DamageInfo(DamageDefOf.Stun, 0, 0f, -1, this.Pawn));
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<int>(ref this.ticksToNextDamage, "ticksToNextDamage", this.Props.ticksToNextDamage, false);
        }
        int ticksToNextDamage;
        Texture2D uiIcon;
    }
    /*Paralysis Link's initially inflicted condition gains severity over time due to this hediff class.
     * This hediff comp, being a ChangeIfSeverityVsHitPoints derivative, will turn into the paralysis condition on hitting enough severity...
     *   ...UNLESS the pawn is non-organic. Then, it instead stuns the pawn for mechStunTime*/
    public class Hediff_BeingParalyzed : HediffWithComps
    {
        public override void PostTick()
        {
            base.PostTick();
            this.Severity += 1.2f * this.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
        }
    }
    public class HediffCompProperties_Paralysis : HediffCompProperties_ChangeIfSeverityVsHitPoints
    {
        public HediffCompProperties_Paralysis()
        {
            this.compClass = typeof(HediffComp_Paralysis);
        }
        public IntRange mechStunTime;
    }
    public class HediffComp_Paralysis : HediffComp_ChangeIfSeverityVsHitPoints
    {
        public new HediffCompProperties_Paralysis Props
        {
            get
            {
                return (HediffCompProperties_Paralysis)this.props;
            }
        }
        public override bool ShouldTransform()
        {
            return base.ShouldTransform() || this.Pawn.Downed;
        }
        protected override void TransformThis()
        {
            if (this.Pawn.stances != null && !this.Pawn.RaceProps.IsFlesh)
            {
                this.Pawn.stances.stunner.StunFor(this.Props.mechStunTime.RandomInRange, null);
            }
            else
            {
                base.TransformThis();
            }
        }
    }
    //Psyphon Link can't target anyone without a psylink
    public class CompAbilityEffect_Psyphon : CompAbilityEffect_GiveHediffPaired
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.HasThing && target.Thing is Pawn p)
            {
                if (!p.HasPsylink)
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + (ModCompatibilityUtility.IsHighFantasy() ? "HVP_NotAPsycasterF".Translate() : "HVP_NotAPsycaster".Translate()), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
    }
    /*this is for the hediff on the caster, not the target
     * psyfocusPerInterval: for each affected pawn, gain this much psyfocus every time the effect is instantiated. Victim loses that much.
     * victimEntropyPerSecond: ewisott
     * intervalsTilUnwillingEnd: this hediff starts with a timer initially set to this value. Each hostile or non-same-faction pawn to which a psyphon has been attached decrements this timer whenever the effect is instantiated,
     *   and if it ends then everyone involved is stunned.*/
    public class HediffCompProperties_Psyphon : HediffCompProperties_LinkBuildEntropy
    {
        public HediffCompProperties_Psyphon()
        {
            this.compClass = typeof(HediffComp_Psyphon);
        }
        public float psyfocusPerInterval;
        public float victimEntropyPerSecond;
        public int intervalsTilUnwillingEnd;
        public IntRange stunTicksOnUnwillingEnd;
    }
    public class HediffComp_Psyphon : HediffComp_LinkBuildEntropy
    {
        public new HediffCompProperties_Psyphon Props
        {
            get
            {
                return (HediffCompProperties_Psyphon)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.intsTilUnwillingEnd = this.Props.intervalsTilUnwillingEnd;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.others != null && this.Pawn.IsHashIntervalTick(15, delta))
            {
                foreach (Thing t in this.others)
                {
                    if (t is Pawn p && p.psychicEntropy != null)
                    {
                        p.psychicEntropy.TryAddEntropy(this.Props.victimEntropyPerSecond / 4f, null, true, true);
                    }
                }
            }
        }
        public override void DoPeriodicDamageInner(Thing t)
        {
            base.DoPeriodicDamageInner(t);
            if (t is Pawn p)
            {
                if (this.Pawn.psychicEntropy == null || !p.HasPsylink || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || this.Pawn.psychicEntropy.CurrentPsyfocus >= 0.999f || (!this.Pawn.IsPlayerControlled && !this.Pawn.HostileTo(p) && p.psychicEntropy.EntropyRelativeValue > p.psychicEntropy.MaxEntropy))
                {
                    this.Pawn.health.RemoveHediff(this.parent);
                    return;
                }
                if (p.psychicEntropy != null)
                {
                    if (p.psychicEntropy.CurrentPsyfocus < 0.01f)
                    {
                        this.Pawn.health.RemoveHediff(this.parent);
                        return;
                    }
                    this.Pawn.psychicEntropy.OffsetPsyfocusDirectly(this.Props.psyfocusPerInterval);
                    p.psychicEntropy.OffsetPsyfocusDirectly(-this.Props.psyfocusPerInterval);
                }
                if (p.Faction == null || p.Faction != this.Pawn.Faction || p.HostileTo(this.Pawn))
                {
                    this.intsTilUnwillingEnd--;
                    if (this.intsTilUnwillingEnd < 0)
                    {
                        this.Pawn.health.RemoveHediff(this.parent);
                        if (PawnUtility.ShouldSendNotificationAbout(this.Pawn))
                        {
                            Messages.Message("HVP_PsyphonBroken".Translate(this.Pawn.Name.ToStringShort, p.Name.ToStringShort), this.Pawn, MessageTypeDefOf.NeutralEvent, false);
                        }
                        p.stances.stunner.StunFor(this.Props.stunTicksOnUnwillingEnd.RandomInRange, null, false, true, false);
                        this.Pawn.stances.stunner.StunFor(this.Props.stunTicksOnUnwillingEnd.RandomInRange, null, false, true, false);
                        return;
                    }
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<int>(ref this.intsTilUnwillingEnd, "intsTilUnwillingEnd", this.Props.intervalsTilUnwillingEnd, false);
        }
        public int intsTilUnwillingEnd;
    }
}
