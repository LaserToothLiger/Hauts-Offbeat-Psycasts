using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using VEF.AnimalBehaviours;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    /*Energy Transfer
     * affectedMeters: removes from any of these needs the first target has, and adds to any of these needs the second target has. If a party has multiple such needs, the amount removed/added from each is reduced commensurately.
     * baseFractionTransferred: takes [this percentage*first target's psysens] of the current level of a need.
     * The amount that the second target's needs gain is based on the amount the first target lost, multiplied by the first target's body size.*/
    public class CompProperties_AbilityTransferEnergy : CompProperties_EffectWithDest
    {
        public List<NeedDef> affectedMeters;
        public float baseFractionTransferred;
    }
    public class CompAbilityEffect_TransferEnergy : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityTransferEnergy Props
        {
            get
            {
                return (CompProperties_AbilityTransferEnergy)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing && dest.HasThing)
            {
                base.Apply(target, dest);
                if (target.Thing is Pawn pawn && dest.Thing is Pawn pawn2)
                {
                    float toTransfer = 0f;
                    List<Need> toTakeFrom = new List<Need>();
                    List<Need> toGoTo = new List<Need>();
                    foreach (NeedDef n in this.Props.affectedMeters)
                    {
                        Need need = pawn.needs.TryGetNeed(n);
                        if (need != null)
                        {
                            toTransfer += need.CurLevelPercentage;
                            toTakeFrom.Add(need);
                        }
                        Need need2 = pawn2.needs.TryGetNeed(n);
                        if (need2 != null)
                        {
                            toGoTo.Add(need2);
                        }
                    }
                    if (toTransfer > 0f && !toGoTo.NullOrEmpty())
                    {
                        toTransfer *= this.Props.baseFractionTransferred * Math.Min(1f, pawn.GetStatValue(StatDefOf.PsychicSensitivity));
                        foreach (Need need in toTakeFrom)
                        {
                            need.CurLevelPercentage -= toTransfer / toTakeFrom.Count;
                        }
                        toTransfer *= pawn.BodySize / pawn2.BodySize;
                        toTransfer /= toGoTo.Count;
                        foreach (Need need2 in toGoTo)
                        {
                            need2.CurLevelPercentage += toTransfer;
                        }
                    }
                }
            }
        }
        public override TargetingParameters targetParams
        {
            get
            {
                return new TargetingParameters
                {
                    canTargetSelf = true,
                    canTargetBuildings = false,
                    canTargetAnimals = false,
                    canTargetMechs = true,
                    canTargetLocations = false
                };
            }
        }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return target != this.selectedTarget && this.HasAnyNeedCheck(this.selectedTarget) && this.HasAnyNeedCheck(target) && base.ValidateTarget(target, showMessages);
        }
        public override bool CanHitTarget(LocalTargetInfo target)
        {
            return base.CanHitTarget(target) && this.HasAnyNeedCheck(target);
        }
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            AcceptanceReport acceptanceReport = this.HasAnyNeed(target);
            if (!acceptanceReport)
            {
                if (showMessages && !acceptanceReport.Reason.NullOrEmpty() && target.Thing is Pawn pawn)
                {
                    Messages.Message("HVP_CannotTransfer".Translate(pawn.Named("PAWN")) + ": " + acceptanceReport.Reason, pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, showMessages);
        }
        private AcceptanceReport HasAnyNeed(LocalTargetInfo target)
        {
            if (!this.HasAnyNeedCheck(target))
            {
                return "HVP_NoTransferrableEnergy".Translate();
            }
            return true;
        }
        private bool HasAnyNeedCheck(LocalTargetInfo target)
        {
            if (target.Thing != null && target.Thing is Pawn p && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon)
            {
                bool anyNeed = false;
                foreach (NeedDef n in this.Props.affectedMeters)
                {
                    Need need = p.needs.TryGetNeed(n);
                    if (need != null)
                    {
                        anyNeed = true;
                        break;
                    }
                }
                if (!anyNeed)
                {
                    return false;
                }
                return true;
            }
            return false;
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (target.Thing != null && target.Thing is Pawn pawn)
            {
                return this.HasAnyNeed(target).Reason;
            }
            return base.ExtraLabelMouseAttachment(target);
        }
    }
    //sinkhole traps do not work on VEF floating pawns. They otherwise inflict a stun when sprung; duration scales inversely with the victim's body size
    public class Building_TrapStunner : Building_Trap
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                SoundDefOf.TrapArm.PlayOneShot(new TargetInfo(base.Position, map, false));
            }
        }
        protected override void SpringSub(Pawn p)
        {
            if (base.Spawned)
            {
                SoundDefOf.TrapSpring.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
            }
            if (p == null)
            {
                return;
            }
            if (!StaticCollectionsClass.floating_animals.Contains(p))
            {
                int stunTime = (6 * Building_TrapStunner.DamageRandomFactorRange.RandomInRange / p.BodySize).SecondsToTicks();
                p.stances.stunner.StunFor(stunTime, this, false, true, false);
            }
        }
        private static readonly FloatRange DamageRandomFactorRange = new FloatRange(0.9f, 1.1f);
    }
    //Word of Warning's buff is a DamageNegation comp that only works if the pawn isn't downed or sleeping. Regardless of whether that condition is met, the cost is paid on taking damage; as set up in XML, this causes its removal
    public class HediffCompProperties_Forewarned : HediffCompProperties_DamageNegation
    {
        public HediffCompProperties_Forewarned()
        {
            this.compClass = typeof(HediffComp_Forewarned);
        }
        public bool mustBeConscious;
    }
    public class HediffComp_Forewarned : HediffComp_DamageNegation
    {
        public new HediffCompProperties_Forewarned Props
        {
            get
            {
                return (HediffCompProperties_Forewarned)this.props;
            }
        }
        public override bool ShouldDoModificationInner(DamageInfo dinfo)
        {
            if (this.Props.mustBeConscious && (this.Pawn.Downed || !this.Pawn.Awake() || this.Pawn.Suspended))
            {
                return false;
            }
            return base.ShouldDoModificationInner(dinfo);
        }
    }
}
