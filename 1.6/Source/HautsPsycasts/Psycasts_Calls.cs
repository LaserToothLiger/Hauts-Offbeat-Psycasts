using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HautsPsycasts
{
    /*All call abilities have a SimpleCurve field. However much of statScalar the caster has is the x value (input), and the y value (evaluation result) is used to determine how many times the effect procs.
     * Drop Pod Call can't target any cell which is roofed or not standable. Each proc is a drop pod that lands near the target point.
     * giveHediffToPassengers: pawns spawned inside these drop pods start with this hediff (if specified)
     * weightings: keys are possible drop pod contents, values are relative weightings.
     *   -None: contains ash filth (currently unused)
     *   -Slag: contains a steel slag chunk
     *   -Resources: contains whatever one pod's worth of stuff would be from the normal Resource Pod event
     *   -Animal: contains a non-dryad animal (lower market value is likelier), subject to giveHediffToPasengers*/
    public enum DropPodCallOutcome : byte
    {
        None,
        Slag,
        Resources,
        Animal
    }
    public class CompProperties_AbilityDropPod : CompProperties_AbilityEffect
    {
        public Dictionary<DropPodCallOutcome, int> weightings;
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
                DropPodCallOutcome outcome = this.Props.weightings.RandomElementByWeight((KeyValuePair<DropPodCallOutcome, int> x) => x.Value).Key;
                ActiveTransporterInfo activeTransporterInfo = new ActiveTransporterInfo();
                bool leaveSlag = false;
                if (outcome == DropPodCallOutcome.Resources)
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
                }
                else if (outcome == DropPodCallOutcome.Slag)
                {
                    leaveSlag = true;
                }
                else if (outcome == DropPodCallOutcome.Animal)
                {
                    List<PawnKindDef> possiblePawns = DefDatabase<PawnKindDef>.AllDefsListForReading.FindAll((PawnKindDef pkd) => pkd.RaceProps.Animal && pkd.RaceProps.IsFlesh && !pkd.RaceProps.Dryad);
                    Pawn pawnToDrop = PawnGenerator.GeneratePawn(new PawnGenerationRequest(possiblePawns.RandomElementByWeight((PawnKindDef pkd) => Math.Max(0.001f, 1f / Math.Max(1f, pkd.race.BaseMarketValue))), null, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                    if (this.Props.giveHediffToPassengers != null)
                    {
                        pawnToDrop.health.AddHediff(this.Props.giveHediffToPassengers, null, null, null);
                    }
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
    /*Fortune Call with the SimpleCurve and statScalar.
     * delayTicks: created incidents are added to the storyteller's incident queue, with a delay = a random number of ticks within this range
     * excludedGoodEvents: Blacklist for events that are otherwise considered "good events" by the Framework. Specifically created to ensure you don't get Animal Self-Tamed over and over from a costly level 6 psycast.*/
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
        public int minEventImpact;
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
                GoodAndBadIncidentsUtility.MakeGoodEvent(this.parent.pawn, this.Props.delayTicks.RandomInRange, this.Props.excludedGoodEvents, minImpact: this.Props.minEventImpact);
            }
        }
    }
    /*Hive Call, same curve and stat dealio.
     * maxHivesPerMap: no matter how many hives the curve says this should make, it will only create enough to bring the number of hives on the map up to this amount. Hive Call can't be cast if there are at least this many hives on the map.*/
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
                return Math.Min((int)Math.Ceiling(Mathf.Max(this.Props.hivePerStatCurve.Evaluate(this.parent.pawn.GetStatValue(this.Props.statScalar)), 1)), this.Props.maxHivesPerMap - HiveUtility.TotalSpawnedHivesCount(this.parent.pawn.Map));
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            int hives = this.HivesToSpawn;
            if (hives > 0)
            {
                InfestationUtility.SpawnTunnels(this.HivesToSpawn, this.parent.pawn.Map, true, true, null, target.Cell, null);
            }
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
}
