using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    /*substitute the regular Flashstorm ability comp for this in xpath. Presuming you turned on the relevant mod setting, the fields are used to inform the properties of the created GameCondition_BetterFlashstorm
     * Otherwise, it just does what the normal Flashstorm comp does (jack shit).
     * randomStrikePeriodicity: lower values = more regular lightning strikes. Narrower range = less variation in time between regular lightning strikes
     * BetterFlashstorms also create powerful lightning strikes that operate on a separate timer, targetedStrikePeriodicity. They do the specified amount of damage with the specified amount of AP.
     *   These strikes have a 10% chance to target an in-radius damageable Thing hostile to the caster. (BetterFlashstorm stores the caster when created by this ability comp)
     * initialStrikeDelay: how long do you have to wait after the flashstorm is created until it drops its first lightning bolt.*/
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
            }
            else
            {
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
                    HVPUtility.ArmourPiercingBolt(intVec, base.SingleMap, this.caster ?? this.conditionCauser, (int)this.apBoltDamage, this.apBoltAP);
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
        public IntRange ticksBetweenAPBolts = new IntRange(492, 492);
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
}
