using HautsFramework;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using VEF.AnimalBehaviours;
using Verse;

namespace HautsPsycasts
{
    //every time a carezone applies its buff to pawns, it also scans for all filth in its radius that isn't on the exemptFilthTypes blacklist, and cleans it up a little
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
    //Prevents Tremorzone from targeting a vacuum cell (e.g. space cells in Odyssey) as that would make no goddamn sense
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
    /*staggerFor: pawns hit by a tremorzone's pulse are "staggered by damage" for a random amount of ticks in this range. Stunnable buildings hit by the pulse are stunned for a random amount of ticks in this range
     * staggerPower: ewisott
     * fleckOnActivation, effecter: since there's a tactically relevant delay between tremorzone pulses, it's good to have a visual indication of when they happen. This is that indication. Love me some lensing effects*/
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
            foreach (IntVec3 intVec in GenRadial.RadialCellsAround(this.parent.Position, this.Props.range, true))
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
                    stunComp.StunHandler.StunFor(this.Props.staggerFor.RandomInRange, this.parent, false);
                }
            }
        }
        public override void AffectPawn(Pawn pawn)
        {
            base.AffectPawn(pawn);
            if (pawn.stances != null && !StaticCollectionsClass.floating_animals.Contains(pawn) && !pawn.Flying)
            {
                pawn.stances.stagger.StaggerFor(this.Props.staggerFor.RandomInRange, this.Props.staggerPower);
            }
        }
        private Effecter effecter;
    }
}
