using RimWorld;
using System.Collections.Generic;
using VEF.AnimalBehaviours;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    /*derivative of AbilitySpawn that sets the spawned thing to the caster's faction, and requires the targeted cell's terrain to be capable of supporting the spawned thing.
     * Used by Sinkhole Skip (lvl 1) and Turret Skip (lvl 4).
     * isTrap: prevents targeting of a cell adjacent to any trap building or blueprint thereof
     * allowOnTrees: allows for the targeting of a cell that has a tree. Like any other plant, AbilitySpawn will destroy the tree if necessary to make room for its spawn*/
    public class CompProperties_AbilitySpawnAlliedBuilding : CompProperties_AbilitySpawn
    {
        public CompProperties_AbilitySpawnAlliedBuilding()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnAlliedBuilding);
        }
        public bool isTrap;
        public bool allowOnTrees = true;
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
            if (!this.Props.allowOnTrees)
            {
                foreach (Thing t in target.Cell.GetThingList(this.parent.pawn.Map))
                {
                    if (t.def.plant != null && t.def.plant.IsTree)
                    {
                        if (throwMessages)
                        {
                            Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "AbilityOccupiedCells".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                        }
                        return false;
                    }
                }
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
    //governs skipped turrets' timed life. The various unique fields here are S/VFX that play when the timed life is up
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
}
