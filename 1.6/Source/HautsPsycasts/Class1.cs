using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using HautsFramework;
using VEF;
using Verse.Sound;
using VEF.AnimalBehaviours;
using RimWorld.QuestGen;
using RimWorld.Planet;
using System.Reflection;
using static UnityEngine.GraphicsBuffer;
using Verse.AI.Group;
using Verse.Noise;
using Verse.AI;
using VEF.Abilities;
using static System.Collections.Specialized.BitVector32;

namespace HautsPsycasts
{
    [StaticConstructorOnStartup]
    public class HautsPsycasts
    {
            private static readonly Type patchType = typeof(HautsPsycasts);
        static HautsPsycasts()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautspsycasts");
            harmony.Patch(AccessTools.Method(typeof(Caravan), nameof(Caravan.GetGizmos)),
                          postfix: new HarmonyMethod(patchType, nameof(HVPGetGizmosPostfix)));
            harmony.Patch(AccessTools.Method(typeof(ThingSetMaker), nameof(ThingSetMaker.Generate), new[] { typeof(ThingSetMakerParams) }),
                          postfix: new HarmonyMethod(patchType, nameof(HVP_ThingSetMaker_GeneratePostfix)));
            harmony.Patch(AccessTools.Method(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel)),
                          postfix: new HarmonyMethod(patchType, nameof(HVP_TryGiveAbilityOfLevelPostfix)));
            Log.Message("HVP_Initialize".Translate().CapitalizeFirst());
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static IEnumerable<Gizmo> HVPGetGizmosPostfix(IEnumerable<Gizmo> __result, Caravan __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }
            foreach (Pawn p in __instance.PawnsListForReading)
            {
                p.health.hediffSet.TryGetHediff(HVPDefOf.HVP_Hammerspace, out Hediff hediff);
                if (hediff != null)
                {
                    HediffComp_Hammerspace hchs = hediff.TryGetComp<HediffComp_Hammerspace>();
                    if (hchs != null)
                    {
                        foreach (Gizmo giz2 in hchs.CompGetGizmos())
                        {
                            yield return giz2;
                        }
                    }
                }
            }
        }
        public static void HVP_ThingSetMaker_GeneratePostfix(ref List<Thing> __result, ThingSetMakerParams parms)
        {
            if (parms.traderDef != null && parms.traderDef.stockGenerators != null)
            {
                int numPsytrainers = 0;
                List<ThingDef> generatedDefs = new List<ThingDef>();
                foreach (Thing t in __result)
                {
                    if (t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.NeurotrainersPsycast))
                    {
                        numPsytrainers++;
                        generatedDefs.Add(t.def);
                    }
                }
                numPsytrainers *= (int)HVP_Mod.settings.psytrainersForSaleMultiplier - 1;
                if (numPsytrainers > 0)
                {
                    IEnumerable<ThingDef> descendantThingDefs = ThingCategoryDefOf.NeurotrainersPsycast.DescendantThingDefs;
                    while (numPsytrainers > 0)
                    {
                        ThingDef chosenThingDef;
                        if (!descendantThingDefs.Where((ThingDef t) => t.tradeability.TraderCanSell() && !generatedDefs.Contains(t)).TryRandomElement(out chosenThingDef))
                        {
                            break;
                        }
                        Thing toAdd = StockGeneratorUtility.TryMakeForStock(chosenThingDef, 1, parms.makingFaction).FirstOrDefault();
                        if (toAdd != null)
                        {
                            __result.Add(toAdd);
                            generatedDefs.Add(toAdd.def);
                        }
                        numPsytrainers--;
                    }
                }
            }
        }
        public static void HVP_TryGiveAbilityOfLevelPostfix(Hediff_Psylink __instance, int abilityLevel)
        {
            int psycastsToAward = (int)HVP_Mod.settings.psycastsLearnedPerLevel - 1;
            if (psycastsToAward > 0 && __instance.pawn.abilities != null)
            {
                List<RimWorld.AbilityDef> psycastsOfLevel = new List<RimWorld.AbilityDef>();
                foreach (RimWorld.AbilityDef a in DefDatabase<RimWorld.AbilityDef>.AllDefs)
                {
                    if (a.IsPsycast && a.level == abilityLevel && __instance.pawn.abilities.GetAbility(a) == null)
                    {
                        psycastsOfLevel.Add(a);
                    }
                }
                while (psycastsToAward > 0 && psycastsOfLevel.Count > 0)
                {
                    RimWorld.AbilityDef abilityDef = psycastsOfLevel.RandomElement();
                    __instance.pawn.abilities.GainAbility(abilityDef);
                    psycastsOfLevel.Remove(abilityDef);
                    psycastsToAward--;
                }
            }
        }
    }

    [DefOf]
    public static class HVPDefOf
    {
        static HVPDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVPDefOf));
        }
        public static GameConditionDef HVP_BetterFlashstorm;
        public static HediffDef HVP_Hammerspace;
        public static QuestScriptDef HVP_DowsingQuest;
        public static QuestScriptDef HVP_DowsingQuest2;
    }
    //level 1
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
            return this.HasAnyNeedCheck(this.selectedTarget) && this.HasAnyNeedCheck(target) && base.ValidateTarget(target, showMessages);
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
    //level 2
    public class CompProperties_AuraFilthCleaner : CompProperties_AuraEmitterHediff
    {
        public CompProperties_AuraFilthCleaner()
        {
            this.compClass = typeof(CompAuraFilthCleaner);
        }
        public List<ThingDef> exemptFilthTypes;
    }
    public class CompAuraFilthCleaner : CompAuraEmitterHediff
    {
        public new CompProperties_AuraFilthCleaner Props
        {
            get
            {
                return (CompProperties_AuraFilthCleaner)this.props;
            }
        }
        public override void DoOnTrigger()
        {
            base.DoOnTrigger();
            foreach (Filth filth in GenRadial.RadialDistinctThingsAround(this.parent.Position, this.parent.Map, this.Props.range, true).OfType<Filth>().Distinct<Filth>())
            {
                if (!this.Props.exemptFilthTypes.Contains(filth.def))
                {
                    filth.ThinFilth();
                }
            }
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
            } else {
                base.TransformThis();
            }
        }
    }
    public class Hediff_BeingParalyzed : HediffWithComps
    {
        public override void PostTick()
        {
            base.PostTick();
            this.Severity += 1.2f*this.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
        }
    }
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
            } else {
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
    public class CompProperties_AbilityTransferSkills : CompProperties_EffectWithDest
    {
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
                                skillDiffs.Add(s.def,s.GetLevel(false)-s2.GetLevel(false));
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
                            pawnSkill.Learn(-pawnSkill.xpSinceLastLevel-(Rand.Value*SkillRecord.XpRequiredToLevelUpFrom(pawnSkill.GetLevel(false)-1)), true,true);
                            pawnSkill2.Learn(pawnSkill2.XpRequiredForLevelUp, true,true);
                        }
                    }
                    HautsUtility.LearnLanguage(pawn2,pawn,0.05f);
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
            return this.HasAnyHigherSkills(target,this.selectedTarget) && base.ValidateTarget(target, showMessages);
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
                HVPUtility.TetherSkipBack(this.Pawn, this.other,this.Props.fleckEntry, this.Props.soundEntry, this.Props.fleckExit1, this.Props.fleckExit2, this.Props.soundExit,this.Props.stunTicks,true);
            }
        }
    }
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
            this.buttonTooltip = (HautsUtility.IsHighFantasy() ? this.Props.buttonTooltipFantasy :this.Props.buttonTooltip).Translate();
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(15, delta) && this.hediffs != null)
            {
                for (int i = this.hediffs.Count - 1; i >= 0; i--)
                {
                    if (this.AIShouldRecall(this.hediffs[i]))
                    {
                        this.Recall(this.hediffs[i]);
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
    //level 3
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
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + (HautsUtility.IsHighFantasy() ? "HVP_NotAPsycasterF".Translate() : "HVP_NotAPsycaster".Translate()), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
    }
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
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            foreach (Thing t in this.others)
            {
                if (t is Pawn p && p.psychicEntropy != null)
                {
                    p.psychicEntropy.TryAddEntropy(this.Props.victimEntropyPerSecond / 60f, null, true, true);
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
    public class CompProperties_AbilityReplicate : CompProperties_AbilityEffect
    {
        public List<ThingCategoryDef> allowedItemCategories;
        public List<FleckDef> fleckDefs;
        public int preCastTicks;
        public float scale = 1f;
        public int marketValueLimitItem;
        public int marketValueLimitStack;
    }
    public class CompAbilityEffect_Replicate : CompAbilityEffect
    {
        public new CompProperties_AbilityReplicate Props
        {
            get
            {
                return (CompProperties_AbilityReplicate)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null)
            {
                Thing thing = ThingMaker.MakeThing(target.Thing.def, target.Thing.Stuff ?? null);
                thing.stackCount = Math.Max(1, Math.Min(target.Thing.stackCount, (int)(this.Props.marketValueLimitStack / target.Thing.MarketValue)));
                GenSpawn.Spawn(thing, this.parent.pawn.Position, this.parent.pawn.Map, WipeMode.Vanish);
            }
            if (this.Props.preCastTicks <= 0)
            {
                SoundDef sound = this.Props.sound;
                if (sound != null)
                {
                    sound.PlayOneShot(new TargetInfo(this.parent.pawn.Position, this.parent.pawn.Map, false));
                }
                this.SpawnAllFlecks(this.parent.pawn);
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            bool fitsInCategories = false;
            if (target.Thing != null)
            {
                if (!target.Thing.def.thingCategories.NullOrEmpty() && target.Thing.def.thingCategories.ContainsAny((ThingCategoryDef tcd) => this.Props.allowedItemCategories.Contains(tcd) || tcd.Parents.ToList().ContainsAny((ThingCategoryDef tcd2) => this.Props.allowedItemCategories.Contains(tcd2))))
                {
                    fitsInCategories = true;
                }
                if (target.Thing.MarketValue > this.Props.marketValueLimitItem)
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_ItemTooExpensive".Translate(this.Props.marketValueLimitItem), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            if (!fitsInCategories)
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_WrongItemType".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }
        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            if (this.Props.preCastTicks > 0)
            {
                yield return new PreCastAction
                {
                    action = delegate (LocalTargetInfo t, LocalTargetInfo d)
                    {
                        this.SpawnAllFlecks(this.parent.pawn);
                        SoundDef sound = this.Props.sound;
                        if (sound == null)
                        {
                            return;
                        }
                        sound.PlayOneShot(new TargetInfo(t.Cell, this.parent.pawn.Map, false));
                    },
                    ticksAwayFromCast = this.Props.preCastTicks
                };
            }
            yield break;
        }
        private void SpawnAllFlecks(LocalTargetInfo target)
        {
            if (!this.Props.fleckDefs.NullOrEmpty<FleckDef>())
            {
                for (int i = 0; i < this.Props.fleckDefs.Count; i++)
                {
                    this.SpawnFleck(target, this.Props.fleckDefs[i]);
                }
            }
        }
        private void SpawnFleck(LocalTargetInfo target, FleckDef def)
        {
            if (target.HasThing && target.Thing.SpawnedOrAnyParentSpawned)
            {
                FleckMaker.AttachedOverlay(target.Thing, def, Vector3.zero, this.Props.scale, -1f);
                return;
            }
            FleckMaker.Static(target.Cell, this.parent.pawn.Map, def, this.Props.scale);
        }
    }
    public class CompAbilityEffect_SpawnTZ : CompAbilityEffect_Spawn
    {
        public new CompProperties_AbilitySpawn Props
        {
            get
            {
                return (CompProperties_AbilitySpawn)this.props;
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell.IsValid)
            {
                TerrainDef td = target.Cell.GetTerrain(this.parent.pawn.Map);
                if (td != null && td.exposesToVacuum)
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_InSpaceNoOneCanHearYouQuake".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
    }
    public class CompProperties_AuraStaggerer : CompProperties_AuraEmitter
    {
        public CompProperties_AuraStaggerer()
        {
            this.compClass = typeof(CompAuraStaggerer);
        }
        public IntRange staggerFor;
        public float staggerPower;
        public FleckDef fleckOnActivation;
        public EffecterDef effecter;
    }
    public class CompAuraStaggerer : CompAuraEmitter
    {
        public new CompProperties_AuraStaggerer Props
        {
            get
            {
                return (CompProperties_AuraStaggerer)this.props;
            }
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EffecterDef groundSpawnerSustainedEffecter = this.Props.effecter;
            this.effecter = ((groundSpawnerSustainedEffecter != null) ? groundSpawnerSustainedEffecter.Spawn(this.parent, this.parent.Map, 1f) : null);
        }
        public override void CompTick()
        {
            base.CompTick();
            Effecter effecter = this.effecter;
            if (effecter != null)
            {
                effecter.EffectTick(this.parent, this.parent);
            }
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (this.effecter != null)
            {
                this.effecter.ForceEnd();
            }
        }
        public override void DoOnTrigger()
        {
            base.DoOnTrigger();
            if (this.Props.fleckOnActivation != null)
            {
                FleckMaker.Static(this.parent.Position, this.parent.MapHeld, this.Props.fleckOnActivation, this.Props.range / 4f);
            }
            int maxDust = 10;
            foreach (IntVec3 intVec in GenRadial.RadialCellsAround(this.parent.Position,this.Props.range,true))
            {
                if (maxDust > 0 && Rand.Chance(0.4f))
                {
                    FleckMaker.ThrowDustPuffThick(intVec.ToVector3Shifted(), this.parent.Map, Rand.Range(1f, 3f), CompAbilityEffect_Wallraise.DustColor);
                    maxDust--;
                }
            }
            foreach (Building building in GenRadial.RadialDistinctThingsAround(this.parent.Position, this.parent.Map, this.Props.range, true).OfType<Building>().Distinct<Building>())
            {
                CompStunnable stunComp = building.GetComp<CompStunnable>();
                if (stunComp != null)
                {
                    stunComp.StunHandler.StunFor(this.Props.staggerFor.RandomInRange,this.parent,false);
                }
            }
        }
        public override void AffectPawn(Pawn pawn)
        {
            base.AffectPawn(pawn);
            if (pawn.stances != null && !StaticCollectionsClass.floating_animals.Contains(pawn) && !pawn.Flying)
            {
                pawn.stances.stagger.StaggerFor(this.Props.staggerFor.RandomInRange,this.Props.staggerPower);
            }
        }
        private Effecter effecter;
    }
    public class CompAbilityEffect_VaultSkip : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilityGiveHediff Props
        {
            get
            {
                return (CompProperties_AbilityGiveHediff)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            List<Thing> thingsToTake = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.parent.def.EffectRadius*this.parent.pawn.GetStatValue(HautsDefOf.Hauts_SkipcastRangeFactor), true))
            {
                if (t.def.EverHaulable)
                {
                    t.TryMakeMinified();
                    thingsToTake.Add(t);
                }
            }
            if (this.Props.ignoreSelf && target.Pawn == this.parent.pawn)
            {
                return;
            }
            if (!this.Props.onlyApplyToSelf && this.Props.applyToTarget)
            {
                this.ApplyInner(target.Pawn, this.parent.pawn, thingsToTake);
            }
            if (this.Props.applyToSelf || this.Props.onlyApplyToSelf)
            {
                this.ApplyInner(this.parent.pawn, target.Pawn,thingsToTake);
            }
        }
        protected void ApplyInner(Pawn target, Pawn other, List<Thing> thingsToTake)
        {
            if (target != null)
            {
                if (this.TryResist(target))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "Resisted".Translate(), -1f);
                    return;
                }
                Hediff firstHediffOfDef = target.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef, false);
                if (firstHediffOfDef != null)
                {
                    this.FillStorage(firstHediffOfDef,thingsToTake);
                    return;
                }
                Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, target, this.Props.onlyBrain ? target.health.hediffSet.GetBrain() : null);
                HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
                if (hediffComp_Disappears != null)
                {
                    hediffComp_Disappears.ticksToDisappear = base.GetDurationSeconds(target).SecondsToTicks();
                }
                if (this.Props.severity >= 0f)
                {
                    hediff.Severity = this.Props.severity;
                }
                HediffComp_Link hediffComp_Link = hediff.TryGetComp<HediffComp_Link>();
                if (hediffComp_Link != null)
                {
                    hediffComp_Link.other = other;
                    hediffComp_Link.drawConnection = (target == this.parent.pawn);
                }
                target.health.AddHediff(hediff, null, null, null);
                this.FillStorage(hediff, thingsToTake);
            }
        }
        private void FillStorage(Hediff hediff, List<Thing> thingsToTake)
        {
            HediffComp_Hammerspace hchs = hediff.TryGetComp<HediffComp_Hammerspace>();
            if (hchs != null)
            {
                hchs.radius = this.parent.def.EffectRadius;
                if (hchs.innerContainer == null)
                {
                    hchs.innerContainer = new ThingOwner<Thing>(hediff.pawn, false, LookMode.Deep);
                }
                foreach (Thing t in thingsToTake)
                {
                    if (t.holdingOwner != null)
                    {
                        t.holdingOwner.Remove(t);
                    }
                    hchs.innerContainer.TryAdd(t,t.stackCount);
                }
            }
        }
        protected virtual bool TryResist(Pawn pawn)
        {
            return false;
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return this.parent.pawn.Faction != Faction.OfPlayer && target.Pawn != null;
        }
    }
    public class HediffCompProperties_Hammerspace : HediffCompProperties
    {
        public HediffCompProperties_Hammerspace()
        {
            this.compClass = typeof(HediffComp_Hammerspace);
        }
        public string dumpIcon;
        public float eachItemLostOnDeathChance;
        public float lostChanceIfNonPlayer;
        public float severityPerThing;
        public int displayLimit = 10;
        public FleckDef fleckDump1;
        public FleckDef fleckDump2;
        public SoundDef soundOnDump;
    }
    public class HediffComp_Hammerspace : HediffComp, IThingHolder
    {
        public HediffCompProperties_Hammerspace Props
        {
            get
            {
                return (HediffCompProperties_Hammerspace)this.props;
            }
        }
        public IThingHolder ParentHolder
        {
            get
            {
                return this.Pawn;
            }
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }
        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }
        public override string CompTipStringExtra { 
            get {
                string storedStrings = "HVP_StoringTheFollowing".Translate();
                if (this.innerContainer != null)
                {
                    int displayLimit = this.Props.displayLimit;
                    foreach (Thing t in this.innerContainer)
                    {
                        storedStrings += "\n" + t.Label;
                        displayLimit--;
                        if (displayLimit <= 0)
                        {
                            break;
                        }
                    }
                    if (this.innerContainer.Count > this.Props.displayLimit)
                    {
                        storedStrings += "\n" + "HVP_AndYetMoreStored".Translate(this.innerContainer.Count - this.Props.displayLimit);
                    }
                }
                return base.CompTipStringExtra + storedStrings;
            } 
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.dumpIcon, true);
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.innerContainer = new ThingOwner<Thing>(this);
        }
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (this.uiIcon == null)
            {
                this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.dumpIcon, true);
            }
            if (this.innerContainer != null && HVPUtility.ShouldShowExtraPsycastGizmo(this.Pawn))
            {
                Command_Action cmdRecall = new Command_Action
                {
                    defaultLabel = "HVP_StorageDumpLabel".Translate(),
                    defaultDesc = "HVP_StorageDumpText".Translate(),
                    icon = this.uiIcon,
                    action = delegate ()
                    {
                        this.StorageDump(0f);
                    }
                };
                yield return cmdRecall;
            }
            yield break;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.innerContainer != null)
            {
                this.parent.Severity = this.innerContainer.Count * this.Props.severityPerThing;
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (this.innerContainer.Count > 0)
            {
                this.StorageDump(this.Pawn.IsColonist?this.Props.eachItemLostOnDeathChance: this.Props.lostChanceIfNonPlayer, this.Pawn.Dead);
            }
        }
        private void StorageDump(float chanceToLose, bool sendMessage = false)
        {
            bool lostAnItem = false;
            if (this.Pawn.SpawnedOrAnyParentSpawned)
            {
                for (int i = this.innerContainer.Count - 1; i >= 0; i--)
                {
                    if (!Rand.Chance(chanceToLose) || !this.innerContainer[i].def.destroyable)
                    {
                        this.innerContainer.TryDrop(this.innerContainer[i], this.Pawn.PositionHeld, this.Pawn.MapHeld, ThingPlaceMode.Near, out Thing thing);
                    } else {
                        this.innerContainer[i].Destroy();
                        lostAnItem = true;
                    }
                }
            } else if (this.Pawn.IsCaravanMember()) {
                for (int i = this.innerContainer.Count - 1; i >= 0; i--)
                {
                    if (!Rand.Chance(chanceToLose) || !this.innerContainer[i].def.destroyable)
                    {
                        this.innerContainer.TryTransferToContainer(this.innerContainer[i], this.Pawn.inventory.innerContainer, this.innerContainer[i].stackCount, out Thing thing);
                    } else {
                        this.innerContainer[i].Destroy();
                        lostAnItem = true;
                    }
                }
            }
            if (PawnUtility.ShouldSendNotificationAbout(this.Pawn) && sendMessage)
            {
                string deathNotification = "HVP_VaultDeath".Translate().Formatted(this.Pawn.Named("PAWN")).AdjustedFor(this.Pawn, "PAWN", true).Resolve();
                if (lostAnItem)
                {
                    deathNotification += " " + "HVP_VaultDeathNoWarranty".Translate();
                }
                Messages.Message(deathNotification, this.Pawn, MessageTypeDefOf.NeutralEvent, false);
            }
            if (this.Pawn.SpawnedOrAnyParentSpawned)
            {
                if (this.Props.fleckDump1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(this.Pawn.PositionHeld.ToVector3Shifted(), this.Pawn.MapHeld, this.Props.fleckDump1, this.radius);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    this.Pawn.MapHeld.flecks.CreateFleck(dataStatic);
                    if (this.Props.fleckDump2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(this.Pawn.PositionHeld.ToVector3Shifted(), this.Pawn.MapHeld, this.Props.fleckDump2, this.radius);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        this.Pawn.MapHeld.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (this.Props.soundOnDump != null)
                {
                    this.Props.soundOnDump.PlayOneShot(new TargetInfo(this.Pawn.PositionHeld, this.Pawn.MapHeld, false));
                }
            }
            this.Pawn.health.RemoveHediff(this.parent);
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Deep.Look<ThingOwner<Thing>>(ref this.innerContainer, "innerContainer", new object[] { this });
            Scribe_Values.Look<float>(ref this.radius, "radius", 1f, false);
        }
        public ThingOwner<Thing> innerContainer;
        public Texture2D uiIcon;
        public float radius = 1f;
    }
    //level 4
    public class CompProperties_AbilityDropPod : CompProperties_AbilityEffect
    {
        public Dictionary<string, int> weightings;
        public HediffDef giveHediffToPassengers;
        public SimpleCurve podPerStatCurve;
        public StatDef statScalar;
    }
    public class CompAbilityEffect_DropPod : CompAbilityEffect
    {
        public new CompProperties_AbilityDropPod Props
        {
            get
            {
                return (CompProperties_AbilityDropPod)this.props;
            }
        }
        public int PodsToSpawn
        {
            get
            {
                return (int)Math.Round(Mathf.Max(this.Props.podPerStatCurve.Evaluate(this.parent.pawn.GetStatValue(this.Props.statScalar)), 1));
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            int ets = this.PodsToSpawn;
            for (int i = 0; i < ets; i++)
            {
                string outcome = this.Props.weightings.RandomElementByWeight((KeyValuePair<string, int> x) => x.Value).Key;
                ActiveTransporterInfo activeTransporterInfo = new ActiveTransporterInfo();
                bool leaveSlag = false;
                if (outcome == "resources")
                {
                    ThingDef thingDef = ThingSetMaker_ResourcePod.RandomPodContentsDef(false);
                    ThingDef stuff = GenStuff.RandomStuffByCommonalityFor(thingDef, TechLevel.Undefined);
                    Thing thing = ThingMaker.MakeThing(thingDef, stuff);
                    float valueCap = Rand.Range(150f, 600f);
                    int stackCount = Rand.Range(20, 40);
                    if (stackCount > thing.def.stackLimit)
                    {
                        stackCount = thing.def.stackLimit;
                    }
                    float bmv = thing.GetStatValue(StatDefOf.MarketValue, true, -1);
                    if ((float)stackCount * bmv > valueCap)
                    {
                        stackCount = Math.Max(1, Mathf.FloorToInt(valueCap / bmv));
                    }
                    thing.stackCount = stackCount;
                    activeTransporterInfo.innerContainer.TryAdd(thing, true);
                } else if (outcome == "slag") {
                    leaveSlag = true;
                } else if (outcome == "animal") {
                    List<PawnKindDef> possiblePawns = DefDatabase<PawnKindDef>.AllDefsListForReading.FindAll((PawnKindDef pkd) => pkd.RaceProps.Animal && pkd.RaceProps.IsFlesh && !pkd.RaceProps.Dryad);
                    Pawn pawnToDrop = PawnGenerator.GeneratePawn(new PawnGenerationRequest(possiblePawns.RandomElementByWeight((PawnKindDef pkd) => Math.Max(0.001f, 1f / Math.Max(1f, pkd.race.BaseMarketValue))), null, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                    pawnToDrop.health.AddHediff(this.Props.giveHediffToPassengers, null, null, null);
                    activeTransporterInfo.innerContainer.TryAdd(pawnToDrop, true);
                } else {
                    if (i == 0 && PawnUtility.ShouldSendNotificationAbout(this.parent.pawn))
                    {
                        Messages.Message("HVP_DropPodDefault".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.NeutralEvent, false);
                    }
                    FilthMaker.TryMakeFilth(target.Cell, this.parent.pawn.Map, ThingDefOf.Filth_Ash, 2, FilthSourceFlags.None, true);
                }
                activeTransporterInfo.openDelay = 110;
                activeTransporterInfo.leaveSlag = leaveSlag;
                DropPodUtility.MakeDropPodAt(target.Cell, this.parent.pawn.Map, activeTransporterInfo, null);
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell.Roofed(this.parent.pawn.Map) || target.Cell.Fogged(this.parent.pawn.Map) || !target.Cell.Standable(this.parent.pawn.Map))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_NotGoodDropSpot".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return true;
        }
    }
    public class CompAbilityEffect_Unleash : CompAbilityEffect_GiveHediff
    {
        public override bool CanCast
        {
            get
            {
                return !this.parent.pawn.health.hediffSet.HasHediff(this.Props.hediffDef);
            }
        }
    }
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
    public class CompProperties_AbilitySkipTheft : CompProperties_AbilityEffect
    {
        public IntRange damageToStolenThing;
        public int goodwillDamageIfCaughtStealing;
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
                    } else if (p.apparel != null && p.apparel.WornApparelCount > 0) {
                        this.StealApparel(p);
                    } else {
                        this.StealFromInventory(p);
                    }
                } else {
                    if (p.inventory != null && p.inventory.innerContainer.Count > 0)
                    {
                        this.StealFromInventory(p);
                    } else if (p.apparel != null && p.apparel.WornApparelCount > 0) {
                        this.StealApparel(p);
                    } else {
                        this.StealEquipment(p);
                    }
                }
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
    //level 5
    public class CompProperties_AbilityFleckAndFlux : CompProperties_AbilityFleckOnTarget
    {
        public CompProperties_AbilityFleckAndFlux()
        {
            this.compClass = typeof(CompAbilityEffect_FleckAndFlux);
        }
        public bool replaceExisting;
        public HediffDef hediffDef;
        public float severity = -1f;
    }
    public class CompAbilityEffect_FleckAndFlux : CompAbilityEffect_FleckOnTarget
    {
        public new CompProperties_AbilityFleckAndFlux Props
        {
            get
            {
                return (CompProperties_AbilityFleckAndFlux)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(target.Cell, this.parent.pawn.Map, this.parent.def.EffectRadius, true))
            {
                if (t is Pawn p)
                {
                    this.ApplyHediff(p);
                }
            }
            GenExplosion.DoExplosion(target.Cell, this.parent.pawn.Map, this.parent.def.EffectRadius, DamageDefOf.EMP, this.parent.pawn);
        }
        public void ApplyHediff(Pawn target)
        {
            if (this.Props.replaceExisting)
            {
                Hediff firstHediffOfDef = target.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef, false);
                if (firstHediffOfDef != null)
                {
                    target.health.RemoveHediff(firstHediffOfDef);
                }
            }
            Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, target);
            if (this.Props.severity >= 0f)
            {
                hediff.Severity = this.Props.severity;
            }
            target.health.AddHediff(hediff, null, null, null);
            if (this.Props.goodwillImpact != 0 && this.parent.pawn.Faction == Faction.OfPlayer && target.HomeFaction != null && !target.HomeFaction.HostileTo(this.parent.pawn.Faction) && (this.Props.applyGoodwillImpactToLodgers || !target.IsQuestLodger()) && !target.IsQuestHelper())
            {
                Faction.OfPlayer.TryAffectGoodwillWith(target.HomeFaction, this.Props.goodwillImpact, true, true, HistoryEventDefOf.UsedHarmfulAbility, null);
            }
        }
    }
    [StaticConstructorOnStartup]
    public class Meteoroid : Skyfaller
    {
        protected override void Impact()
        {
            if (HautsUtility.CanBeHitByAirToSurface(base.Position, base.Map, false))
            {
                if (this.chunk == null)
                {
                    this.chunk = ThingDefOf.ChunkSlagSteel;
                }
                float damageMulti = HVPUtility.ChunkMeteorDamageMulti(this.chunk);
                int totalDamage = GenMath.RoundRandom((float)this.def.skyfaller.explosionDamage.defaultDamage * this.def.skyfaller.explosionDamageFactor * damageMulti);
                GenExplosion.DoExplosion(base.Position, base.Map, this.def.skyfaller.explosionRadius, this.def.skyfaller.explosionDamage, this.caster ?? null, totalDamage, -1f, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, (!this.def.skyfaller.damageSpawnedThings) ? this.innerContainer.ToList<Thing>() : null, null, true, 1f, 0f, true, null, 1f, null, preExplosionSpawnSingleThingDef: ThingDefOf.Filth_BlastMark);
            }
            CellRect cellRect = this.OccupiedRect();
            for (int i = 0; i < cellRect.Area * this.def.skyfaller.motesPerCell; i++)
            {
                FleckMaker.ThrowDustPuff(cellRect.RandomVector3, base.Map, 2f);
            }
            if (this.def.skyfaller.MakesShrapnel)
            {
                SkyfallerShrapnelUtility.MakeShrapnel(base.Position, base.Map, this.shrapnelDirection, this.def.skyfaller.shrapnelDistanceFactor, this.def.skyfaller.metalShrapnelCountRange.RandomInRange, this.def.skyfaller.rubbleShrapnelCountRange.RandomInRange, true);
            }
            if (this.def.skyfaller.cameraShake > 0f && base.Map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(this.def.skyfaller.cameraShake);
            }
            if (this.def.skyfaller.impactSound != null)
            {
                this.def.skyfaller.impactSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map, false), MaintenanceType.None));
            }
            this.Destroy();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look<ThingDef>(ref this.chunk, "chunk");
            Scribe_References.Look<Pawn>(ref this.caster, "caster", false);
        }
        public ThingDef chunk;
        public Pawn caster;
    }
    public class CompProperties_AbilityMSkip : CompProperties_EffectWithDest
    {
        public ThingDef skyfaller;
    }
    public class CompAbilityEffect_MSkip : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityMSkip Props
        {
            get
            {
                return (CompProperties_AbilityMSkip)this.props;
            }
        }
        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            yield return new PreCastAction
            {
                action = delegate (LocalTargetInfo t, LocalTargetInfo d)
                {
                    if (!this.parent.def.HasAreaOfEffect)
                    {
                        Pawn pawn = t.Pawn;
                        if (pawn != null)
                        {
                            FleckCreationData dataAttachedOverlay = FleckMaker.GetDataAttachedOverlay(pawn, FleckDefOf.PsycastSkipFlashEntry, new Vector3(-0.5f, 0f, -0.5f), 1f, -1f);
                            dataAttachedOverlay.link.detachAfterTicks = 5;
                            pawn.Map.flecks.CreateFleck(dataAttachedOverlay);
                        }
                        else
                        {
                            FleckMaker.Static(t.CenterVector3, this.parent.pawn.Map, FleckDefOf.PsycastSkipFlashEntry, 1f);
                        }
                        FleckMaker.Static(d.Cell, this.parent.pawn.Map, FleckDefOf.PsycastSkipInnerExit, 1f);
                    }
                    if (this.Props.destination != AbilityEffectDestination.RandomInRange)
                    {
                        FleckMaker.Static(d.Cell, this.parent.pawn.Map, FleckDefOf.PsycastSkipOuterRingExit, 1f);
                    }
                    if (!this.parent.def.HasAreaOfEffect)
                    {
                        SoundDefOf.Psycast_Skip_Entry.PlayOneShot(new TargetInfo(t.Cell, this.parent.pawn.Map, false));
                        SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(d.Cell, this.parent.pawn.Map, false));
                    }
                },
                ticksAwayFromCast = 5
            };
            yield break;
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing)
            {
                base.Apply(target, dest);
                LocalTargetInfo destination = base.GetDestination(dest.IsValid ? dest : target);
                if (destination.IsValid && (destination.Cell.GetRoof(this.parent.pawn.Map) == null || !destination.Cell.GetRoof(this.parent.pawn.Map).isThickRoof))
                {
                    Pawn pawn = this.parent.pawn;
                    if (!this.parent.def.HasAreaOfEffect)
                    {
                        this.parent.AddEffecterToMaintain(EffecterDefOf.Skip_Entry.Spawn(target.Thing, pawn.Map, 1f), target.Thing.Position, 60, null);
                    }
                    else
                    {
                        this.parent.AddEffecterToMaintain(EffecterDefOf.Skip_EntryNoDelay.Spawn(target.Thing, pawn.Map, 1f), target.Thing.Position, 60, null);
                    }
                    if (this.Props.destination == AbilityEffectDestination.Selected)
                    {
                        this.parent.AddEffecterToMaintain(EffecterDefOf.Skip_Exit.Spawn(destination.Cell, pawn.Map, 1f), destination.Cell, 60, null);
                    }
                    else
                    {
                        this.parent.AddEffecterToMaintain(EffecterDefOf.Skip_ExitNoDelay.Spawn(destination.Cell, pawn.Map, 1f), destination.Cell, 60, null);
                    }
                    Meteoroid meteoroid = (Meteoroid)GenSpawn.Spawn(SkyfallerMaker.MakeSkyfaller(this.Props.skyfaller), destination.Cell, pawn.Map, WipeMode.Vanish);
                    meteoroid.caster = pawn;
                    meteoroid.chunk = target.Thing.def;
                    target.Thing.Destroy();
                }
            }
        }
        public override bool CanHitTarget(LocalTargetInfo target)
        {
            return base.CanPlaceSelectedTargetAt(target) && base.CanHitTarget(target);
        }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (target.Cell.GetRoof(this.parent.pawn.Map) != null && target.Cell.GetRoof(this.parent.pawn.Map).isThickRoof)
            {
                if (showMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_MeteorMountain".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.HasThing)
            {
                if (!target.Thing.HasThingCategory(ThingCategoryDefOf.Chunks) && !target.Thing.HasThingCategory(ThingCategoryDefOf.StoneChunks))
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_NotAChunk".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            return base.Valid(target, throwMessages);
        }
    }
    public class CompProperties_AbilityReave : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public FloatRange damageToPawns;
        public FloatRange damageToBuildings;
        public FleckDef fleck1;
        public FleckDef fleck2;
        public float fleckScale;
    }
    public class CompAbilityEffect_Reave : CompAbilityEffect
    {
        public new CompProperties_AbilityReave Props
        {
            get
            {
                return (CompProperties_AbilityReave)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null)
            {
                if (this.Props.fleck1 != null)
                {
                    Vector3 vfxOffset = new Vector3((Rand.Value - 0.5f), (Rand.Value - 0.5f), (Rand.Value - 0.5f));
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(target.Cell.ToVector3Shifted() + vfxOffset, target.Thing.Map, this.Props.fleck1, this.Props.fleckScale);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    target.Thing.Map.flecks.CreateFleck(dataStatic);
                    if (this.Props.fleck2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(target.Cell.ToVector3Shifted() + vfxOffset, target.Thing.Map, this.Props.fleck2, this.Props.fleckScale);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        target.Thing.Map.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (target.Thing is Pawn p)
                {
                    p.TakeDamage(new DamageInfo(this.Props.damageDef, this.Props.damageToPawns.RandomInRange, 99999f, -1f, this.parent.pawn));
                }
                else
                {
                    target.Thing.TakeDamage(new DamageInfo(this.Props.damageDef, this.Props.damageToBuildings.RandomInRange, 99999f, -1f, this.parent.pawn));
                }
            }
        }
    }
    public class CompProperties_ThermoPusher : CompProperties
    {
        public CompProperties_ThermoPusher()
        {
            this.compClass = typeof(CompThermoPusher);
        }
        public float heatPerSecond;
        public FloatRange desiredTemperatureRange;
        public float fireExtinguishment;
        public float fireRadius;
        public EffecterDef effecterDef;
    }
    public class CompThermoPusher : ThingComp
    {
        public CompProperties_ThermoPusher Props
        {
            get
            {
                return (CompProperties_ThermoPusher)this.props;
            }
        }
        public virtual bool ShouldPushHeatNow
        {
            get
            {
                if (!this.parent.SpawnedOrAnyParentSpawned)
                {
                    return false;
                }
                CompProperties_ThermoPusher props = this.Props;
                float ambientTemperature = this.parent.AmbientTemperature;
                return this.enabled && (ambientTemperature < props.desiredTemperatureRange.min || ambientTemperature > props.desiredTemperatureRange.max);
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            if (this.effecter == null)
            {
                this.effecter = this.Props.effecterDef.Spawn();
                this.effecter.Trigger(this.parent, this.parent);
            }
            if (this.effecter != null)
            {
                this.effecter.EffectTick(this.parent, this.parent);
            }
            if (this.parent.IsHashIntervalTick(60) && this.ShouldPushHeatNow)
            {
                this.PushHeat(1f);
            }
        }
        public void PushHeat(float magnitude = 1f)
        {
            float ambientTemperature = this.parent.AmbientTemperature;
            if (ambientTemperature < this.Props.desiredTemperatureRange.min)
            {
                GenTemperature.PushHeat(this.parent.PositionHeld, this.parent.MapHeld, magnitude*Math.Min(this.Props.heatPerSecond, this.Props.desiredTemperatureRange.min - ambientTemperature));
            } else if (ambientTemperature > this.Props.desiredTemperatureRange.max) {
                GenTemperature.PushHeat(this.parent.PositionHeld, this.parent.MapHeld, magnitude * Math.Min(-this.Props.heatPerSecond, this.Props.desiredTemperatureRange.max - ambientTemperature));
            }
            foreach (Fire fire in GenRadial.RadialDistinctThingsAround(this.parent.Position, this.parent.Map, this.Props.fireRadius, true).OfType<Fire>().Distinct<Fire>())
            {
                fire.TakeDamage(new DamageInfo(DamageDefOf.Extinguish,this.Props.fireExtinguishment));
            }
        }
        public override void CompTickRare()
        {
            base.CompTickRare();
            if (this.ShouldPushHeatNow)
            {
                this.PushHeat(4.1666665f);
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<bool>(ref this.enabled, "enabled", true, false);
        }
        public bool enabled = true;
        public Effecter effecter;
    }
    public class CompAbilityEffect_Intervention : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilityGiveHediff Props
        {
            get
            {
                return (CompProperties_AbilityGiveHediff)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (this.Props.ignoreSelf && target.Pawn == this.parent.pawn)
            {
                return;
            }
            if (!this.Props.onlyApplyToSelf && this.Props.applyToTarget)
            {
                this.ApplyInner(target.Pawn, this.parent.pawn);
            }
            if (this.Props.applyToSelf || this.Props.onlyApplyToSelf)
            {
                this.ApplyInner(this.parent.pawn, target.Pawn);
            }
        }
        protected void ApplyInner(Pawn target, Pawn other)
        {
            if (target != null)
            {
                if (this.TryResist(target))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "Resisted".Translate(), -1f);
                    return;
                }
                if (this.Props.replaceExisting)
                {
                    Hediff firstHediffOfDef = target.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef, false);
                    if (firstHediffOfDef != null)
                    {
                        target.health.RemoveHediff(firstHediffOfDef);
                    }
                }
                Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, target, this.Props.onlyBrain ? target.health.hediffSet.GetBrain() : null);
                HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
                if (hediffComp_Disappears != null)
                {
                    hediffComp_Disappears.ticksToDisappear = base.GetDurationSeconds(target).SecondsToTicks();
                }
                if (this.Props.severity >= 0f)
                {
                    hediff.Severity = this.Props.severity;
                }
                HediffComp_Intervention hci = hediff.TryGetComp<HediffComp_Intervention>();
                if (hci != null)
                {
                    hci.faction = other.Faction ?? null;
                }
                target.health.AddHediff(hediff, null, null, null);
            }
        }
        protected virtual bool TryResist(Pawn pawn)
        {
            return false;
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return this.parent.pawn.Faction != Faction.OfPlayer && target.Pawn != null;
        }
    }
    public class HediffCompProperties_Intervention : HediffCompProperties
    {
        public HediffCompProperties_Intervention()
        {
            this.compClass = typeof(HediffComp_Intervention);
        }
        public HediffDef hediffOnDowned;
        public FleckDef fleckEntry;
        public SoundDef soundEntry;
        public FleckDef fleckExit1;
        public FleckDef fleckExit2;
        public SoundDef soundExit;
        public IntRange stunTicks;
    }
    public class HediffComp_Intervention : HediffComp
    {
        public HediffCompProperties_Intervention Props
        {
            get
            {
                return (HediffCompProperties_Intervention)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.Downed)
            {
                this.GoToBedOuter();
            }
        }
        public override void Notify_PawnKilled()
        {
            this.GoToBedOuter();
            base.Notify_PawnKilled();
        }
        private void GoToBedOuter()
        {
            if (this.faction == null)
            {
                this.faction = this.Pawn.Faction;
            }
            Faction pFaction = this.Pawn.Faction;
            Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffOnDowned, this.Pawn);
            this.Pawn.health.AddHediff(hediff, null);
            bool anyBedFound = false;
            if (this.Pawn.SpawnedOrAnyParentSpawned)
            {
                this.GoToBed(this.Pawn.MapHeld, out anyBedFound);
            }
            if (!anyBedFound)
            {
                if (pFaction == this.faction || this.faction.RelationKindWith(pFaction) == FactionRelationKind.Hostile)
                {
                    foreach (Map m in Find.Maps)
                    {
                        if (m.ParentFaction != null && m.ParentFaction == this.faction)
                        {
                            this.GoToBed(m, out anyBedFound);
                        }
                        if (anyBedFound)
                        {
                            break;
                        }
                    }
                    if (!anyBedFound)
                    {
                        foreach (Settlement sett in Find.WorldObjects.Settlements)
                        {
                            if (sett.Faction != null && sett.Faction == this.faction && sett.Map == null)
                            {
                                PawnUtility.ForceEjectFromContainer(this.Pawn);
                                this.Pawn.GetCaravan()?.RemovePawn(this.Pawn);
                                if (this.Pawn.Faction != null && faction.HostileTo(this.Pawn.Faction) && faction != Faction.OfPlayer)
                                {
                                    faction.kidnapped.Kidnap(this.Pawn, null);
                                    Messages.Message("HVP_PsychicKidnapping".Translate(this.faction.Name, this.Pawn.Name.ToStringShort), MessageTypeDefOf.NegativeEvent, false);
                                } else {
                                    if (this.Pawn.Spawned)
                                    {
                                        this.Pawn.ExitMap(false, this.Pawn.def.defaultPlacingRot);
                                    }
                                    if (!this.Pawn.IsWorldPawn())
                                    {
                                        Find.WorldPawns.PassToWorld(this.Pawn, PawnDiscardDecideMode.Decide);
                                    }
                                }
                                anyBedFound = true;
                            }
                        }
                    }
                }
            }
            if (this.Pawn.Faction != pFaction)
            {
                this.Pawn.SetFaction(pFaction);
            }
            this.Pawn.health.RemoveHediff(this.parent);
        }
        private void GoToBed(Map map, out bool anyBed)
        {
            anyBed = false;
            //grab all beds
            List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.Bed);
            if (list.Count > 0)
            {
                /*verify they are all beds because ThingRequestGroups are witchcraft to me. they've been accurate THUS FAR but who knows w mods.
                 also so I don't have to keep casting every Thing in the list as a Bed every time*/
                List<Building_Bed> beds = new List<Building_Bed>();
                foreach (Thing t in list)
                {
                    if (t is Building_Bed b)
                    {
                        beds.Add(b);
                    }
                }
                if (beds.Count > 0)
                {
                    //remove beds w current occupants
                    for (int i = beds.Count - 1; i >= 0; i--)
                    {
                        if (beds[i].CurOccupants.Count() > 0)
                        {
                            beds.RemoveAt(i);
                        }
                    }
                    if (beds.Count > 0)
                    {
                        //remove beds based on whether pawn is animal, baby, or grown human
                        if (this.Pawn.RaceProps.Animal)
                        {
                            for (int i = beds.Count - 1; i >= 0; i--)
                            {
                                if (beds[i].def.building.bed_humanlike)
                                {
                                    beds.RemoveAt(i);
                                }
                            }
                        } else if (this.Pawn.RaceProps.Humanlike && this.Pawn.DevelopmentalStage <= DevelopmentalStage.Baby) {
                            for (int i = beds.Count - 1; i >= 0; i--)
                            {
                                if (!beds[i].ForHumanBabies)
                                {
                                    beds.RemoveAt(i);
                                }
                            }
                        } else {
                            for (int i = beds.Count - 1; i >= 0; i--)
                            {
                                if (!beds[i].def.building.bed_humanlike || beds[i].ForHumanBabies)
                                {
                                    beds.RemoveAt(i);
                                }
                            }
                        }
                        if (beds.Count > 0)
                        {
                            /*effect is dependent on relation to caster: same/allied/neutral/null-faction should go to free-people beds, hostile-faction to prison
                             animal beds can't be prisoner or slave beds, so animals default to free-people*/
                            if (ModsConfig.IdeologyActive && this.Pawn.IsSlave && this.Pawn.SlaveFaction == this.faction)
                            {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (!beds[i].ForSlaves)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            } else if (this.Pawn.RaceProps.Animal) {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (beds[i].ForPrisoners || beds[i].ForSlaves)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            } else if (this.Pawn.IsPrisoner && this.Pawn.guest.HostFaction == this.faction) {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (!beds[i].ForPrisoners)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            } else if (this.Pawn.Faction != null) {
                                if (this.Pawn.Faction.AllyOrNeutralTo(this.faction))
                                {
                                    for (int i = beds.Count - 1; i >= 0; i--)
                                    {
                                        if (beds[i].ForPrisoners || beds[i].ForSlaves)
                                        {
                                            beds.RemoveAt(i);
                                        }
                                    }
                                } else if (map.ParentFaction != null && (map.ParentFaction == this.faction || map.ParentFaction.AllyOrNeutralTo(this.faction))) {
                                    for (int i = beds.Count - 1; i >= 0; i--)
                                    {
                                        if (!beds[i].ForPrisoners)
                                        {
                                            beds.RemoveAt(i);
                                        }
                                    }
                                } else {
                                    beds.Clear();
                                }
                            } else {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (!beds[i].ForPrisoners)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            }
                            if (beds.Count > 0)
                            {
                                //preferentially we will only use beds actually owned by the caster's faction
                                if (this.faction != null)
                                {
                                    bool anyAlliedBeds = false;
                                    foreach (Building_Bed b in beds)
                                    {
                                        if (b.Faction != null && b.Faction == this.faction)
                                        {
                                            anyAlliedBeds = true;
                                            break;
                                        }
                                    }
                                    if (anyAlliedBeds)
                                    {
                                        for (int i = beds.Count - 1; i >= 0; i--)
                                        {
                                            if (beds[i].Faction == null || beds[i].Faction != this.faction)
                                            {
                                                beds.RemoveAt(i);
                                            }
                                        }
                                    }
                                }
                                if (beds.Count > 0)
                                {
                                    //now we prioritize medical beds
                                    List<Building_Bed> medicalBeds = new List<Building_Bed>();
                                    foreach (Building_Bed b in beds)
                                    {
                                        if (b.Medical)
                                        {
                                            medicalBeds.Add(b);
                                        }
                                    }
                                    if (medicalBeds.Count > 0)
                                    {
                                        this.GoToBedInner(medicalBeds);
                                    } else {
                                        this.GoToBedInner(beds);
                                    }
                                    anyBed = true;
                                } else {
                                    anyBed = false;
                                }
                            }
                        }
                    }
                }
            }
        }
        private void GoToBedInner(List<Building_Bed> beds)
        {
            beds.SortBy((Building_Bed m) => this.Pawn.Position.DistanceToSquared(m.Position));
            if (!this.Pawn.Destroyed)
            {
                PawnUtility.ForceEjectFromContainer(this.Pawn);
                HVPUtility.TetherSkipBack(this.Pawn, beds[0], this.Props.fleckEntry, this.Props.soundEntry, this.Props.fleckExit1, this.Props.fleckExit2, this.Props.soundExit, this.Props.stunTicks, false);
                if (RestUtility.CanUseBedNow(beds[0], this.Pawn, false, false, null) && beds[0].AnyUnoccupiedSleepingSlot && (beds[0].Medical || beds[0].AnyUnownedSleepingSlot || beds[0].IsOwner(this.Pawn)))
                {
                    this.Pawn.jobs.Notify_TuckedIntoBed(beds[0]);
                    this.Pawn.mindState.Notify_TuckedIntoBed();
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look<Faction>(ref this.faction, "faction", false);
        }
        public Faction faction;
    }
    public class CompProperties_AbilitySterilize : CompProperties_AbilitySpawn
    {
        public CompProperties_AbilitySterilize()
        {
            this.compClass = typeof(CompAbilityEffect_Sterilize);
        }
        public List<HediffDef> unaffectedAddictionsOrDiseases;
        public List<HediffDef> otherAffectedHediffs;
        public HediffDef addsHediff;
    }
    public class CompAbilityEffect_Sterilize : CompAbilityEffect
    {
        public new CompProperties_AbilitySterilize Props
        {
            get
            {
                return (CompProperties_AbilitySterilize)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null && target.Thing is Pawn p)
            {
                List<Hediff> curableHediffs = HVPUtility.SterilizableHediffs(this.Props,p);
                if (curableHediffs.Count > 0)
                {
                    Hediff h = curableHediffs.RandomElement();
                    BodyPartRecord bpr = h.Part ?? null;
                    if (PawnUtility.ShouldSendNotificationAbout(this.parent.pawn))
                    {
                        Messages.Message("HVP_RemovedHediff".Translate(h.LabelBase,p.Name.ToStringShort), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.PositiveEvent, false);
                    }
                    p.health.RemoveHediff(h);
                    if (this.Props.addsHediff != null)
                    {
                        p.health.AddHediff(this.Props.addsHediff,bpr ?? null);
                    }
                }
            }
        }
    }
    //level 6
    public class CompProperties_AbilityArrangeFortune : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityArrangeFortune()
        {
            this.compClass = typeof(CompAbilityEffect_ArrangeFortune);
        }
        public IntRange delayTicks;
        public List<IncidentDef> excludedGoodEvents = new List<IncidentDef>();
        public SimpleCurve eventPerStatCurve;
        public StatDef statScalar;
    }
    public class CompAbilityEffect_ArrangeFortune : CompAbilityEffect
    {
        public new CompProperties_AbilityArrangeFortune Props
        {
            get
            {
                return (CompProperties_AbilityArrangeFortune)this.props;
            }
        }
        public int EventsToSpawn
        {
            get
            {
                return (int)Math.Round(Mathf.Max(this.Props.eventPerStatCurve.Evaluate(this.parent.pawn.GetStatValue(this.Props.statScalar)), 1));
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            int ets = this.EventsToSpawn;
            for (int i = 0; i < ets; i++)
            {
                IncidentParms incidentParms = new IncidentParms
                {
                    target = this.parent.pawn.MapHeld ?? Find.AnyPlayerHomeMap,
                    forced = true,
                    points = StorytellerUtility.DefaultThreatPointsNow(this.parent.pawn.MapHeld ?? Find.AnyPlayerHomeMap),
                };
                List<IncidentDef> incidents = HautsUtility.goodEventPool.Where((IncidentDef id) => !this.Props.excludedGoodEvents.Contains(id) && id.Worker.CanFireNow(incidentParms)).ToList();
                Find.Storyteller.incidentQueue.Add(incidents.RandomElement(), Find.TickManager.TicksGame + this.Props.delayTicks.RandomInRange, incidentParms, 60000);
            }
        }
    }
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
                string costString = HautsUtility.IsHighFantasy() ? "HVP_EvictionSkipCostsF" : "HVP_EvictionSkipCosts";
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
                float size = 1.1f*Math.Max(0.9f,(float)Math.Sqrt(target.Thing is Pawn pawn ? pawn.BodySize : target.Thing.def.Size.x*target.Thing.def.Size.z));
                if (this.Props.fleck1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(target.Cell.ToVector3Shifted(), target.Thing.Map, this.Props.fleck1, this.Props.fleckScale*size);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    target.Thing.Map.flecks.CreateFleck(dataStatic);
                    if (this.Props.fleck2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(target.Cell.ToVector3Shifted(), target.Thing.Map, this.Props.fleck2, this.Props.fleckScale*size);
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
                    } else {
                        p.Destroy();
                        p.Kill(null);
                    }
                } else {
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
                    return this.Props.psyfocusCostPerVictimSize.Evaluate(p.BodySize);
                } else if (target.Thing is Building b) {
                    return this.Props.psyfocusCostPerVictimSize.Evaluate(b.def.Size.x*b.def.Size.z);
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
                } else {
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
                        string notEnoughPsyfocus = HautsUtility.IsHighFantasy() ? "HVP_CommandPsycastNotEnoughPsyfocusForSizeF" : "HVP_CommandPsycastNotEnoughPsyfocusForSize";
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
    public class CompProperties_AbilitySpawnInfestation : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySpawnInfestation()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnInfestation);
        }
        public int maxHivesPerMap;
        public SimpleCurve hivePerStatCurve;
        public StatDef statScalar;
    }
    public class CompAbilityEffect_SpawnInfestation : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnInfestation Props
        {
            get
            {
                return (CompProperties_AbilitySpawnInfestation)this.props;
            }
        }
        public int HivesToSpawn
        {
            get
            {
                return Math.Max((int)Math.Ceiling(Mathf.Max(this.Props.hivePerStatCurve.Evaluate(this.parent.pawn.GetStatValue(this.Props.statScalar)), 1)), HiveUtility.TotalSpawnedHivesCount(this.parent.pawn.Map) - this.Props.maxHivesPerMap);
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            InfestationUtility.SpawnTunnels(this.HivesToSpawn, this.parent.pawn.Map, true, true, null, target.Cell, null);
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (HiveUtility.TotalSpawnedHivesCount(this.parent.pawn.Map) >= this.Props.maxHivesPerMap)
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_TooManyHivesOnMap".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            if (!target.Cell.Walkable(this.parent.pawn.Map) || this.CellHasBlockingThings(target.Cell, this.parent.pawn.Map))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_HiveBlocked".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            Region region = target.Cell.GetRegion(this.parent.pawn.Map, RegionType.Set_Passable);
            if (region == null)
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_HiveBlocked".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            if (target.Cell.GetTemperature(this.parent.pawn.Map) < -17f)
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_HiveTooCold".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return true;
        }
        private bool CellHasBlockingThings(IntVec3 cell, Map map)
        {
            List<Thing> thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (thingList[i] is Pawn || thingList[i] is Hive || thingList[i] is TunnelHiveSpawner)
                {
                    return true;
                }
                if (thingList[i].def.category == ThingCategory.Building && thingList[i].def.passability == Traversability.Impassable && GenSpawn.SpawningWipes(ThingDefOf.Hive, thingList[i].def))
                {
                    return true;
                }
            }
            return false;
        }
    }
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
            this.buttonTooltip = (HautsUtility.IsHighFantasy() ? this.Props.buttonTooltipFantasy : this.Props.buttonTooltip).Translate();
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
                        } else if (h is Hediff_MissingPart && (h.Part.parent == null || (!this.Pawn.health.hediffSet.PartIsMissing(h.Part.parent) && !this.Pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(h.Part.parent)))) {
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
                    } else if (missingParts.Count > 0) {
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
    public class CompAbilityEffect_Melding : CompAbilityEffect_GiveHediffPaired
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.Thing != null && target.Thing is Pawn p && !p.IsPrisoner && !p.IsSlave)
            {
                if (p.Faction != null)
                {
                    if (p.HomeFaction != Faction.OfPlayer)
                    {
                        p.SetFaction(p.HomeFaction);
                    }
                }
                QuestUtility.SendQuestTargetSignals(p.questTags, "Arrested", p.Named("SUBJECT"));
                if (p.Faction != null)
                {
                    QuestUtility.SendQuestTargetSignals(p.Faction.questTags, "FactionMemberArrested", p.Faction.Named("FACTION"));
                }
            }
            base.Apply(target, dest);
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return this.Valid(target, false);
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return base.Valid(target, throwMessages) && target.Thing != null && target.Thing is Pawn p && p.DevelopmentalStage.Adult();
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
                    canTargetMechs = false
                };
            }
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (this.selectedTarget.IsValid)
            {
                return "HVP_MeldInto".Translate();
            }
            return "HVP_Meld".Translate();
        }
    }
    //pertinent to psycasts at multiple levels
    public class CompProperties_AbilitySpawnAlliedBuilding : CompProperties_AbilitySpawn
    {
        public CompProperties_AbilitySpawnAlliedBuilding()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnAlliedBuilding);
        }
        public bool isTrap;
    }
    public class CompAbilityEffect_SpawnAlliedBuilding : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnAlliedBuilding Props
        {
            get
            {
                return (CompProperties_AbilitySpawnAlliedBuilding)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Thing thingToSpawn = ThingMaker.MakeThing(this.Props.thingDef);
            thingToSpawn.SetFaction(this.parent.pawn.Faction ?? null);
            GenSpawn.Spawn(thingToSpawn, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
            if (this.Props.sendSkipSignal)
            {
                CompAbilityEffect_Teleport.SendSkipUsedSignal(target, this.parent.pawn);
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell.Filled(this.parent.pawn.Map) || (!this.Props.allowOnBuildings && target.Cell.GetEdifice(this.parent.pawn.Map) != null))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "AbilityOccupiedCells".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            if (this.Props.isTrap)
            {
                foreach (IntVec3 c in GenAdj.OccupiedRect(target.Cell, this.Props.thingDef.defaultPlacingRot, this.Props.thingDef.Size).ExpandedBy(1))
                {
                    List<Thing> list = this.parent.pawn.Map.thingGrid.ThingsListAt(c);
                    for (int i = 0; i < list.Count; i++)
                    {
                        Thing thing2 = list[i];
                        if ((thing2.def.category == ThingCategory.Building && thing2.def.building.isTrap) || ((thing2.def.IsBlueprint || thing2.def.IsFrame) && thing2.def.entityDefToBuild is ThingDef && ((ThingDef)thing2.def.entityDefToBuild).building.isTrap))
                        {
                            if (throwMessages)
                            {
                                Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "CannotPlaceAdjacentTrap".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                            }
                            return false;
                        }
                    }
                }
            }
            TerrainAffordanceDef terrainAffordanceNeed = this.Props.thingDef.GetTerrainAffordanceNeed(this.Props.thingDef.defaultStuff);
            if (terrainAffordanceNeed != null)
            {
                CellRect cellRect = GenAdj.OccupiedRect(target.Cell, this.Props.thingDef.defaultPlacingRot, this.Props.thingDef.Size);
                cellRect.ClipInsideMap(this.parent.pawn.Map);
                foreach (IntVec3 c2 in cellRect)
                {
                    if (!this.parent.pawn.Map.terrainGrid.TerrainAt(c2).affordances.Contains(terrainAffordanceNeed))
                    {
                        if (throwMessages)
                        {
                            Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "TerrainCannotSupport".Translate(this.Props.thingDef), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                        }
                        return false;
                    }
                    List<Thing> thingList = c2.GetThingList(this.parent.pawn.Map);
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        if (thingList[i].def.entityDefToBuild is TerrainDef terrainDef && !terrainDef.affordances.Contains(terrainAffordanceNeed))
                        {
                            if (throwMessages)
                            {
                                Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "TerrainCannotSupport".Translate(this.Props.thingDef), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                            }
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
    public class HediffCompProperties_LinkBuildEntropy : HediffCompProperties_MultiLink
    {
        public HediffCompProperties_LinkBuildEntropy()
        {
            this.compClass = typeof(HediffComp_LinkBuildEntropy);
        }
        public float casterEntropyGainPerSecond;
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
            //this.ticksSpentDowned = 0;
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
        private void RemoveOthers()
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
            if (this.Pawn.psychicEntropy != null)
            {
                this.Pawn.psychicEntropy.TryAddEntropy(this.Props.casterEntropyGainPerSecond * this.parent.Severity / 60f, null, true, true);
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
    public class CompProperties_SkipBackAfterDelay : CompProperties_DestroyAfterDelay
    {
        public CompProperties_SkipBackAfterDelay()
        {
            this.compClass = typeof(CompSkipBackAfterDelay);
        }
        public FleckDef fleck1;
        public FleckDef fleck2;
        public float fleckScale;
        public SoundDef sound;
    }
    public class CompSkipBackAfterDelay : CompDestroyAfterDelay
    {
        public new CompProperties_SkipBackAfterDelay Props
        {
            get
            {
                return (CompProperties_SkipBackAfterDelay)this.props;
            }
        }
        public override void CompTick()
        {
            if (this.TicksLeft <= 0 && !this.parent.Destroyed)
            {
                CompExplosive cexp = this.parent.TryGetComp<CompExplosive>();
                if (cexp != null && cexp.wickStarted)
                {
                    return;
                }
                if (this.Props.fleck1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(this.parent.Position.ToVector3Shifted(), this.parent.Map, this.Props.fleck1, this.Props.fleckScale);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    this.parent.Map.flecks.CreateFleck(dataStatic);
                    if (this.Props.fleck2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(this.parent.Position.ToVector3Shifted(), this.parent.Map, this.Props.fleck2, this.Props.fleckScale);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        this.parent.Map.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (this.Props.sound != null)
                {
                    this.Props.sound.PlayOneShot(new TargetInfo(this.parent.Position, this.parent.Map, false));
                }
                this.parent.Destroy(this.Props.destroyMode);
            }
        }
    }
    public class CompProperties_SkipgateOverlay : CompProperties
    {
        public CompProperties_SkipgateOverlay()
        {
            this.compClass = typeof(CompSkipgateOverlay);
        }
        public float scale;
        public ThingDef mote;
        public int flashFrequency;
        public float flashHeightOffset;
        public float rotationRate;
    }
    public class CompSkipgateOverlay : ThingComp
    {
        public CompProperties_SkipgateOverlay Props
        {
            get
            {
                return (CompProperties_SkipgateOverlay)this.props;
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            if (this.parent.Spawned)
            {
                if (this.Props.flashFrequency > 0 && this.parent.IsHashIntervalTick(this.Props.flashFrequency))
                {
                    FleckMaker.Static(this.parent.PositionHeld.ToVector3Shifted() + new Vector3(0f, this.Props.flashHeightOffset, 0f), this.parent.MapHeld, FleckDefOf.PsycastSkipOuterRingExit, this.Props.scale);
                }
                if (this.Props.mote != null)
                {
                    if (this.mote == null)
                    {
                        this.mote = MoteMaker.MakeAttachedOverlay(this.parent, this.Props.mote, Vector3.zero, this.Props.scale, -1f);
                        this.mote.rotationRate = this.Props.rotationRate;
                    } else {
                        this.mote.Maintain();
                    }
                }
            } else if (this.mote != null) {
                this.mote.Destroy();
            }
        }
        public Mote mote;
    }
    public class MoteAttached_Skipgate : MoteAttached
    {
        protected override void TimeInterval(float deltaTime)
        {
            base.TimeInterval(deltaTime);
            this.exactRotation += this.rotationRate * deltaTime;
        }
    }
    //flashstorm buff
    public class CompProperties_AbilityBetterFlashstorm : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityBetterFlashstorm()
        {
            this.compClass = typeof(CompAbilityEffect_BetterFlashstorm);
        }
        public IntRange randomStrikePeriodicity;
        public IntRange targetedStrikePeriodicity;
        public float damage;
        public float armorPenetration;
        public IntRange initialStrikeDelay;
    }
    public class CompAbilityEffect_BetterFlashstorm : CompAbilityEffect
    {
        public new CompProperties_AbilityBetterFlashstorm Props
        {
            get
            {
                return (CompProperties_AbilityBetterFlashstorm)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Map map = this.parent.pawn.Map;
            if (HVP_Mod.settings.buffFlashstorm)
            {
                Thing conditionCauser = GenSpawn.Spawn(ThingDefOf.Flashstorm, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                GameCondition_BetterFlashstorm gameCondition_Flashstorm = (GameCondition_BetterFlashstorm)GameConditionMaker.MakeCondition(HVPDefOf.HVP_BetterFlashstorm, -1);
                gameCondition_Flashstorm.centerLocation = target.Cell.ToIntVec2;
                gameCondition_Flashstorm.ticksBetweenStrikes = this.Props.randomStrikePeriodicity;
                gameCondition_Flashstorm.ticksBetweenAPBolts = this.Props.targetedStrikePeriodicity;
                gameCondition_Flashstorm.apBoltDamage = this.Props.damage;
                gameCondition_Flashstorm.apBoltAP = this.Props.armorPenetration;
                gameCondition_Flashstorm.areaRadiusOverride = new IntRange(Mathf.RoundToInt(this.parent.def.EffectRadius), Mathf.RoundToInt(this.parent.def.EffectRadius));
                gameCondition_Flashstorm.Duration = Mathf.RoundToInt((float)this.parent.def.EffectDuration(this.parent.pawn).SecondsToTicks());
                gameCondition_Flashstorm.suppressEndMessage = true;
                gameCondition_Flashstorm.initialStrikeDelay = this.Props.initialStrikeDelay;
                gameCondition_Flashstorm.conditionCauser = conditionCauser;
                gameCondition_Flashstorm.ambientSound = true;
                gameCondition_Flashstorm.caster = this.parent.pawn;
                map.gameConditionManager.RegisterCondition(gameCondition_Flashstorm);
                this.ApplyGoodwillImpact(target, gameCondition_Flashstorm.AreaRadius);
            } else {
                Thing conditionCauser = GenSpawn.Spawn(ThingDefOf.Flashstorm, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                GameCondition_Flashstorm gameCondition_Flashstorm = (GameCondition_Flashstorm)GameConditionMaker.MakeCondition(GameConditionDefOf.Flashstorm, -1);
                gameCondition_Flashstorm.centerLocation = target.Cell.ToIntVec2;
                gameCondition_Flashstorm.areaRadiusOverride = new IntRange(Mathf.RoundToInt(this.parent.def.EffectRadius), Mathf.RoundToInt(this.parent.def.EffectRadius));
                gameCondition_Flashstorm.Duration = Mathf.RoundToInt((float)this.parent.def.EffectDuration(this.parent.pawn).SecondsToTicks());
                gameCondition_Flashstorm.suppressEndMessage = true;
                gameCondition_Flashstorm.initialStrikeDelay = new IntRange(60, 180);
                gameCondition_Flashstorm.conditionCauser = conditionCauser;
                gameCondition_Flashstorm.ambientSound = true;
                map.gameConditionManager.RegisterCondition(gameCondition_Flashstorm);
                this.ApplyGoodwillImpact(target, gameCondition_Flashstorm.AreaRadius);
            }
        }
        private void ApplyGoodwillImpact(LocalTargetInfo target, int radius)
        {
            if (this.parent.pawn.Faction != Faction.OfPlayer)
            {
                return;
            }
            this.affectedFactionCache.Clear();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(target.Cell, this.parent.pawn.Map, (float)radius, true))
            {
                Pawn p;
                if ((p = (thing as Pawn)) != null && thing.Faction != null && thing.Faction != this.parent.pawn.Faction && !thing.Faction.HostileTo(this.parent.pawn.Faction) && !this.affectedFactionCache.Contains(thing.Faction) && (base.Props.applyGoodwillImpactToLodgers || !p.IsQuestLodger()))
                {
                    this.affectedFactionCache.Add(thing.Faction);
                    Faction.OfPlayer.TryAffectGoodwillWith(thing.Faction, base.Props.goodwillImpact, true, true, HistoryEventDefOf.UsedHarmfulAbility, null);
                }
            }
            this.affectedFactionCache.Clear();
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell.Roofed(this.parent.pawn.Map))
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "AbilityRoofed".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return true;
        }
        private HashSet<Faction> affectedFactionCache = new HashSet<Faction>();
    }
    public class GameCondition_BetterFlashstorm : GameCondition
    {
        public int AreaRadius
        {
            get
            {
                return this.areaRadius;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<IntVec2>(ref this.centerLocation, "centerLocation", default(IntVec2), false);
            Scribe_Values.Look<int>(ref this.areaRadius, "areaRadius", 0, false);
            Scribe_Values.Look<IntRange>(ref this.areaRadiusOverride, "areaRadiusOverride", default(IntRange), false);
            Scribe_Values.Look<IntRange>(ref this.ticksBetweenStrikes, "ticksBetweenStrikes", default(IntRange), false);
            Scribe_Values.Look<IntRange>(ref this.ticksBetweenAPBolts, "ticksBetweenAPBolts", default(IntRange), false);
            Scribe_Values.Look<float>(ref this.apBoltDamage, "apBoltDamage", 10f, false);
            Scribe_Values.Look<float>(ref this.apBoltAP, "apBoltAP", 0f, false);
            Scribe_Values.Look<int>(ref this.nextLightningTicks, "nextLightningTicks", 0, false);
            Scribe_Values.Look<int>(ref this.nextAPBoltTicks, "nextAPBoltTicks", 0, false);
            Scribe_Values.Look<IntRange>(ref this.initialStrikeDelay, "initialStrikeDelay", default(IntRange), false);
            Scribe_Values.Look<bool>(ref this.ambientSound, "ambientSound", false, false);
            Scribe_Values.Look<bool>(ref this.avoidConditionCauser, "avoidConditionCauser", false, false);
            Scribe_References.Look<Pawn>(ref this.caster, "caster", false);
        }
        public override void Init()
        {
            base.Init();
            this.areaRadius = ((this.areaRadiusOverride == IntRange.Zero) ? GameCondition_BetterFlashstorm.AreaRadiusRange.RandomInRange : this.areaRadiusOverride.RandomInRange);
            this.nextLightningTicks = Find.TickManager.TicksGame + this.initialStrikeDelay.RandomInRange;
            this.nextAPBoltTicks = Find.TickManager.TicksGame + this.ticksBetweenAPBolts.RandomInRange;
            if (this.centerLocation.IsInvalid)
            {
                this.FindGoodCenterLocation();
            }
        }
        public override void GameConditionTick()
        {
            if (Find.TickManager.TicksGame > this.nextLightningTicks)
            {
                Vector2 vector = Rand.UnitVector2 * Rand.Range(0f, (float)this.areaRadius);
                IntVec3 intVec = new IntVec3((int)Math.Round((double)vector.x) + this.centerLocation.x, 0, (int)Math.Round((double)vector.y) + this.centerLocation.z);
                if (this.IsGoodLocationForStrike(intVec))
                {
                    base.SingleMap.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(base.SingleMap, intVec));
                    this.nextLightningTicks = Find.TickManager.TicksGame + this.ticksBetweenStrikes.RandomInRange;
                }
            }
            if (Find.TickManager.TicksGame > this.nextAPBoltTicks)
            {
                Vector2 vector = Rand.UnitVector2 * Rand.Range(0f, (float)this.areaRadius);
                IntVec3 intVec = new IntVec3((int)Math.Round((double)vector.x) + this.centerLocation.x, 0, (int)Math.Round((double)vector.y) + this.centerLocation.z);
                if (this.caster != null && this.caster.Faction != null)
                {
                    foreach (Thing t in GenRadial.RadialDistinctThingsAround(this.conditionCauser.Position, base.SingleMap, this.areaRadius, true))
                    {
                        if (t.HostileTo(this.caster) && t.def.useHitPoints && Rand.Chance(0.1f) && this.IsGoodLocationForStrike(t.PositionHeld))
                        {
                            intVec = t.PositionHeld;
                        }
                    }
                }
                if (this.IsGoodLocationForStrike(intVec))
                {
                    HVPUtility.ArmourPiercingBolt(intVec,base.SingleMap,this.caster??this.conditionCauser,(int)this.apBoltDamage,this.apBoltAP);
                    this.nextAPBoltTicks = Find.TickManager.TicksGame + this.ticksBetweenAPBolts.RandomInRange;
                }
            }
            if (this.ambientSound)
            {
                if (this.soundSustainer == null || this.soundSustainer.Ended)
                {
                    this.soundSustainer = SoundDefOf.FlashstormAmbience.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(this.centerLocation.ToIntVec3, base.SingleMap, false), MaintenanceType.PerTick));
                    return;
                }
                this.soundSustainer.Maintain();
            }
        }
        public override void End()
        {
            base.SingleMap.weatherDecider.DisableRainFor(30000);
            base.End();
        }
        private void FindGoodCenterLocation()
        {
            if (base.SingleMap.Size.x <= 16 || base.SingleMap.Size.z <= 16)
            {
                throw new Exception("Map too small for flashstorm.");
            }
            for (int i = 0; i < 10; i++)
            {
                this.centerLocation = new IntVec2(Rand.Range(8, base.SingleMap.Size.x - 8), Rand.Range(8, base.SingleMap.Size.z - 8));
                if (this.IsGoodCenterLocation(this.centerLocation))
                {
                    break;
                }
            }
        }
        private bool IsGoodLocationForStrike(IntVec3 loc)
        {
            return loc.InBounds(base.SingleMap) && !loc.Roofed(base.SingleMap) && loc.Standable(base.SingleMap) && (!this.avoidConditionCauser || this.conditionCauser == null || !this.conditionCauser.OccupiedRect().ExpandedBy(2).Contains(loc));
        }
        private bool IsGoodCenterLocation(IntVec2 loc)
        {
            int num = 0;
            int num2 = (int)(3.1415927f * (float)this.areaRadius * (float)this.areaRadius / 2f);
            foreach (IntVec3 loc2 in this.GetPotentiallyAffectedCells(loc))
            {
                if (this.IsGoodLocationForStrike(loc2))
                {
                    num++;
                }
                if (num >= num2)
                {
                    break;
                }
            }
            return num >= num2;
        }
        private IEnumerable<IntVec3> GetPotentiallyAffectedCells(IntVec2 center)
        {
            int num;
            for (int x = center.x - this.areaRadius; x <= center.x + this.areaRadius; x = num)
            {
                for (int z = center.z - this.areaRadius; z <= center.z + this.areaRadius; z = num)
                {
                    if ((center.x - x) * (center.x - x) + (center.z - z) * (center.z - z) <= this.areaRadius * this.areaRadius)
                    {
                        yield return new IntVec3(x, 0, z);
                    }
                    num = z + 1;
                }
                num = x + 1;
            }
            yield break;
        }
        public static IntRange AreaRadiusRange = new IntRange(45, 60);
        public IntRange ticksBetweenStrikes = new IntRange(160, 400);
        public IntRange ticksBetweenAPBolts = new IntRange(492,492);
        public float apBoltDamage = 10;
        public float apBoltAP = 0f;
        private const int RainDisableTicksAfterConditionEnds = 30000;
        private const int AvoidConditionCauserExpandRect = 2;
        public IntVec2 centerLocation = IntVec2.Invalid;
        public IntRange areaRadiusOverride = IntRange.Zero;
        public IntRange initialStrikeDelay = IntRange.Zero;
        public bool ambientSound;
        private int areaRadius;
        private int nextLightningTicks;
        private int nextAPBoltTicks;
        private Sustainer soundSustainer;
        public bool avoidConditionCauser;
        public Pawn caster;
    }
    //utility methods
    public class HVPUtility
    {
        //also doubles as INCENDIARY BOLT
        public static void ArmourPiercingBolt(IntVec3 strikeLoc, Map map, Thing caster, int damage, float armorPenetration)
        {
            SoundDefOf.Thunder_OffMap.PlayOneShotOnCamera(map);
            if (!strikeLoc.IsValid)
            {
                strikeLoc = CellFinderLoose.RandomCellWith((IntVec3 sq) => sq.Standable(map) && !map.roofGrid.Roofed(sq), map, 1000);
            }
            Mesh boltMesh = LightningBoltMeshPool.RandomBoltMesh;
            if (!strikeLoc.Fogged(map))
            {
                GenExplosion.DoExplosion(strikeLoc, map, 1.9f, DamageDefOf.Flame, caster, damage, armorPenetration, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, null, null, true, 1f, 0f, true, null, 1f, null, null);
                Vector3 loc = strikeLoc.ToVector3Shifted();
                for (int i = 0; i < 4; i++)
                {
                    FleckMaker.ThrowSmoke(loc, map, 1.5f);
                    FleckMaker.ThrowMicroSparks(loc, map);
                    FleckMaker.ThrowLightningGlow(loc, map, 1.5f);
                }
            }
            SoundInfo info = SoundInfo.InMap(new TargetInfo(strikeLoc, map, false), MaintenanceType.None);
            SoundDefOf.Thunder_OnMap.PlayOneShot(info);
            Graphics.DrawMesh(boltMesh, strikeLoc.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather), Quaternion.identity, FadedMaterialPool.FadedVersionOf(MatLoader.LoadMat("Weather/LightningBolt", -1), 1f), 0);
        }
        public static void TetherSpawnPawn(Pawn p, Thing anchor)
        {
            GenSpawn.Spawn(p, anchor.Position, anchor.Map, Rot4.South, WipeMode.Vanish, false, false);
        }
        public static void TetherSkipBack(Pawn p, Thing anchor, FleckDef fleckEntry, SoundDef soundEntry, FleckDef fleckExit1, FleckDef fleckExit2, SoundDef soundExit, IntRange stunTicks, bool destroy = false, bool evenIfDespawned = true)
        {
            if (p != null && !p.Dead && !p.Destroyed && anchor!= null && anchor.Spawned)
            {
                Faction pFaction = p.Faction ?? null;
                if (!p.Spawned)
                {
                    if (!evenIfDespawned)
                    {
                        return;
                    } else {
                        Caravan c = p.GetCaravan();
                        if (c != null)
                        {
                            bool shouldArriveAtAnchor = true;
                            foreach (Pawn caravaneer in c.pawns)
                            {
                                if (caravaneer != p && caravaneer.RaceProps.intelligence == Intelligence.Humanlike && caravaneer.Faction == c.Faction && !caravaneer.Downed && !caravaneer.InMentalState)
                                {
                                    shouldArriveAtAnchor = false;
                                    break;
                                }
                            }
                            if (shouldArriveAtAnchor)
                            {
                                List<Pawn> pawnsToSpawn = new List<Pawn>();
                                foreach (Pawn caravaneer in c.pawns)
                                {
                                    pawnsToSpawn.Add(caravaneer);
                                }
                                foreach (Pawn p2 in pawnsToSpawn)
                                {
                                    HVPUtility.TetherSpawnPawn(p2, anchor);
                                }
                                c.RemoveAllPawns();
                                if (c.pawns.Count == 0 && !c.Destroyed)
                                {
                                    c.Destroy();
                                }
                            } else {
                                HVPUtility.TetherSpawnPawn(p, anchor);
                                c.RemovePawn(p);
                                if (c.pawns.Count == 0 && !c.Destroyed)
                                {
                                    c.Destroy();
                                }
                            }
                        } else {
                            HVPUtility.TetherSpawnPawn(p, anchor);
                        }
                    }
                }
                if (fleckEntry != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(p.Position.ToVector3Shifted(), p.Map, fleckEntry, 1f);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    p.Map.flecks.CreateFleck(dataStatic);
                }
                if (soundEntry != null)
                {
                    soundEntry.PlayOneShot(new TargetInfo(p.Position, p.Map, false));
                }
                if (p.Map == null)
                {
                    HVPUtility.TetherSpawnPawn(p, anchor);
                } else if (p.Map != anchor.Map) {
                    p.ExitMap(false,p.Rotation);
                    HVPUtility.TetherSpawnPawn(p, anchor);
                }
                p.Position = anchor.Position;
                if (fleckExit1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(anchor.Position.ToVector3Shifted(), anchor.Map, fleckExit1, 1f);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    anchor.Map.flecks.CreateFleck(dataStatic);
                    if (fleckExit2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(anchor.Position.ToVector3Shifted(), anchor.Map, fleckExit2, 1f);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        anchor.Map.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (soundExit != null)
                {
                    soundExit.PlayOneShot(new TargetInfo(p.Position, p.Map, false));
                }
                if ((p.Faction == Faction.OfPlayer || p.IsPlayerControlled) && p.Position.Fogged(p.Map))
                {
                    FloodFillerFog.FloodUnfog(p.Position, p.Map);
                }
                p.stances.stunner.StunFor(stunTicks.RandomInRange, null, false, false, false);
                p.Notify_Teleported(true, true);
                if (p.Faction != pFaction)
                {
                    p.SetFaction(pFaction);
                }
            }
            if (destroy && !anchor.Destroyed)
            {
                anchor.Destroy();
            }
        }
        public static bool ShouldShowExtraPsycastGizmo(Pawn p)
        {
            return (p.IsPlayerControlled || DebugSettings.ShowDevGizmos) && p.Awake() && !p.DeadOrDowned && !p.Suspended && !p.InMentalState && p.HasPsylink && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon;
        }
        public static float ChunkMeteorDamageMulti(ThingDef td)
        {
            float damageMulti = 1f;
            if (td.butcherProducts != null && td.butcherProducts.Count > 0)
            {
                ThingDef product = td.butcherProducts.RandomElement().thingDef;
                if (product != null)
                {
                    damageMulti = HVPUtility.CMDMInner(product);
                }
            } else if (td.smeltProducts != null && td.smeltProducts.Count > 0) {
                ThingDef product = td.smeltProducts.RandomElement().thingDef;
                if (product != null)
                {
                    damageMulti = HVPUtility.CMDMInner(product);
                }
            }
            return damageMulti;
        }
        public static float CMDMInner(ThingDef td)
        {
            StuffProperties sprops = td.stuffProps;
            if (sprops != null)
            {
                if (sprops.statFactors != null)
                {
                    return Math.Max(1f,sprops.statFactors.GetStatFactorFromList(StatDefOf.MaxHitPoints));
                }
            }
            return 1f;
        }
        public static List<Hediff> SterilizableHediffs(CompProperties_AbilitySterilize cpas, Pawn p)
        {
            List<Hediff> curableHediffs = new List<Hediff>();
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (cpas.otherAffectedHediffs.Contains(h.def) || ((h.def.makesSickThought || h is Hediff_Addiction) && !cpas.unaffectedAddictionsOrDiseases.Contains(h.def)))
                {
                    curableHediffs.Add(h);
                }
            }
            return curableHediffs;
        }
    }
    public class HVP_Settings : ModSettings
    {
        public float psytrainersForSaleMultiplier = 1f;
        public float psycastsLearnedPerLevel = 1f;
        public bool buffFlashstorm = true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref psytrainersForSaleMultiplier, "psytrainersForSaleMultiplier", 1f);
            Scribe_Values.Look(ref psycastsLearnedPerLevel, "psycastsLearnedPerLevel", 1f);
            Scribe_Values.Look(ref buffFlashstorm, "buffFlashstorm", true);
            base.ExposeData();
        }
    }
    public class HVP_Mod : Mod
    {
        public HVP_Mod(ModContentPack content) : base(content)
        {
            HVP_Mod.settings = GetSettings<HVP_Settings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            //number of psytrainers for sale multiplier
            float x = inRect.xMin, y = inRect.yMin + 25, halfWidth = inRect.width * 0.5f;
            displayPsytrainerSaleMultiplier = ((int)settings.psytrainersForSaleMultiplier).ToString();
            float origR = settings.psytrainersForSaleMultiplier;
            Rect psytrainerSaleRect = new Rect(x + 10, y, halfWidth - 15, 32);
            settings.psytrainersForSaleMultiplier = Widgets.HorizontalSlider(psytrainerSaleRect, settings.psytrainersForSaleMultiplier, 1f, 3f, true, "HVP_SettingPSM".Translate(), "1x", "3x", 1f);
            TooltipHandler.TipRegion(psytrainerSaleRect.LeftPart(1f), "HVP_TooltipPsytrainerSaleMulti".Translate());
            if (origR != settings.psytrainersForSaleMultiplier)
            {
                displayPsytrainerSaleMultiplier= ((int)settings.psytrainersForSaleMultiplier).ToString() + "x";
            }
            y += 32;
            string origStringR = displayPsytrainerSaleMultiplier;
            displayPsytrainerSaleMultiplier = Widgets.TextField(new Rect(x + 10, y, 50, 32), displayPsytrainerSaleMultiplier);
            if (!displayPsytrainerSaleMultiplier.Equals(origStringR))
            {
                this.ParseInput(displayPsytrainerSaleMultiplier, settings.psytrainersForSaleMultiplier, out settings.psytrainersForSaleMultiplier);
            }
            y -= 32;
            //number of psycasts learned per level
            displayPsycastsLearnedPerLevel = ((int)settings.psycastsLearnedPerLevel).ToString();
            float origL = settings.psycastsLearnedPerLevel;
            Rect psycastsLearnedRect = new Rect(x + 5 + halfWidth, y, halfWidth - 15, 32);
            settings.psycastsLearnedPerLevel = Widgets.HorizontalSlider(psycastsLearnedRect, settings.psycastsLearnedPerLevel, 1f, 4f, true, "HVT_SettingPLPL".Translate(), "1", "4", 1f);
            TooltipHandler.TipRegion(psycastsLearnedRect.LeftPart(1f), "HVP_TooltipSettingPLPL".Translate());
            if (origL != settings.psycastsLearnedPerLevel)
            {
                displayPsycastsLearnedPerLevel = ((int)settings.psycastsLearnedPerLevel).ToString();
            }
            y += 32;
            string origStringL = displayPsycastsLearnedPerLevel;
            displayPsycastsLearnedPerLevel = Widgets.TextField(new Rect(x + 5 + halfWidth, y, 50, 32), displayPsycastsLearnedPerLevel);
            if (!displayPsycastsLearnedPerLevel.Equals(origStringL))
            {
                this.ParseInput(displayPsycastsLearnedPerLevel, settings.psycastsLearnedPerLevel, out settings.psycastsLearnedPerLevel);
            }
            //buff flashstorm? YES DEFINITELY ABSOLUTELY
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("HVP_SettingBuffFlashstorm".Translate(), ref settings.buffFlashstorm, "HVP_TooltipBuffFlashstorm".Translate());
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
        private void ParseInput(string buffer, float origValue, out float newValue)
        {
            if (!float.TryParse(buffer, out newValue))
                newValue = origValue;
            if (newValue < 0)
                newValue = origValue;
        }
        public override string SettingsCategory()
        {
            return "Hauts' Offbeat Psycasts";
        }
        public static HVP_Settings settings;
        public string displayPsytrainerSaleMultiplier;
        public string displayPsycastsLearnedPerLevel;
    }
}
