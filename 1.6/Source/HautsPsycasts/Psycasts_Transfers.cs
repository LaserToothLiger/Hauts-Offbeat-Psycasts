using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsPsycasts
{
    /*Despite having similar names, icons, and premises, the "___ Transfer" psycasts don't actually share a common imperative chassis.
     * Energy Transfer
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
    /*Skill Transfer has no unique, XML-modifiable parameters for its base function (at least currently).
     * languageLearningPower: the base amount of progress you get towards learning the skill donor's language. (this is a Cybranian - Rim Langauges thing, and only works if the caster is of your faction)*/
    public class CompProperties_AbilityTransferSkills : CompProperties_EffectWithDest
    {
        public float languageLearningPower = 0.05f;
    }
    public class CompAbilityEffect_TransferSkills : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityTransferSkills Props
        {
            get
            {
                return (CompProperties_AbilityTransferSkills)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing && dest.HasThing)
            {
                base.Apply(target, dest);
                if (target.Thing is Pawn pawn && dest.Thing is Pawn pawn2 && pawn.skills != null && pawn2.skills != null)
                {
                    Dictionary<SkillDef, int> skillDiffs = new Dictionary<SkillDef, int>();
                    foreach (SkillRecord s in pawn.skills.skills)
                    {
                        if (!s.TotallyDisabled)
                        {
                            SkillRecord s2 = pawn2.skills.GetSkill(s.def);
                            if (s2 != null && !s2.TotallyDisabled)
                            {
                                skillDiffs.Add(s.def, s.GetLevel(false) - s2.GetLevel(false));
                            }
                        }
                    }
                    if (skillDiffs.Count > 0)
                    {
                        int highestSkillDiff = skillDiffs.Values.Max();
                        SkillDef toTransfer = skillDiffs.Keys.Where((SkillDef sd) => skillDiffs.TryGetValue(sd) >= highestSkillDiff).RandomElement();
                        if (toTransfer != null)
                        {
                            SkillRecord pawnSkill = pawn.skills.GetSkill(toTransfer);
                            SkillRecord pawnSkill2 = pawn2.skills.GetSkill(toTransfer);
                            pawnSkill.Learn(-pawnSkill.xpSinceLastLevel - (Rand.Value * SkillRecord.XpRequiredToLevelUpFrom(pawnSkill.GetLevel(false) - 1)), true, true);
                            pawnSkill2.Learn(pawnSkill2.XpRequiredForLevelUp, true, true);
                        }
                    }
                    ModCompatibilityUtility.LearnLanguage(pawn2, pawn, this.Props.languageLearningPower);
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
            return target != this.selectedTarget && this.HasAnyHigherSkills(target, this.selectedTarget) && base.ValidateTarget(target, showMessages);
        }
        public override bool CanHitTarget(LocalTargetInfo target)
        {
            return base.CanHitTarget(target) && this.HasAnySkillsCheck(target);
        }
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            AcceptanceReport acceptanceReport = this.HasAnySkills(target);
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
        private AcceptanceReport HasAnySkills(LocalTargetInfo target)
        {
            if (!this.HasAnySkillsCheck(target))
            {
                return "HVP_NoTransferrableSkills".Translate();
            }
            return true;
        }
        private AcceptanceReport HasAnyHigherSkills(LocalTargetInfo target, LocalTargetInfo selectedTarget)
        {
            if (!this.HasAnySkillsCheck(this.selectedTarget) || !this.HasAnySkillsCheck(target))
            {
                return false;
            }
            bool donorHasAnyHigherSkill = false;
            foreach (SkillRecord sr in target.Pawn.skills.skills)
            {
                if (!sr.TotallyDisabled && !this.selectedTarget.Pawn.skills.GetSkill(sr.def).TotallyDisabled && sr.Level > this.selectedTarget.Pawn.skills.GetSkill(sr.def).Level)
                {
                    donorHasAnyHigherSkill = true;
                    break;
                }
            }
            if (donorHasAnyHigherSkill)
            {
                return true;
            }
            return "HVP_NoLowerSkills".Translate();
        }
        private bool HasAnySkillsCheck(LocalTargetInfo target)
        {
            if (target.Thing != null && target.Thing is Pawn p && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon && p.skills != null && p.skills.skills.ContainsAny((SkillRecord sr) => !sr.TotallyDisabled))
            {
                return true;
            }
            return false;
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (target.Thing != null && target.Thing is Pawn pawn)
            {
                return this.HasAnySkills(target).Reason;
            }
            return base.ExtraLabelMouseAttachment(target);
        }
    }
}
