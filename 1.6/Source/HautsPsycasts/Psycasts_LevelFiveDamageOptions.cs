using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    /*Flux Pulse
     * replaceExisting: replaces the hediff already found on the pawn with a new one, if any
     * hediffDef: the hediff to apply
     * severity: if non-negative, sets the created hediff's initial severity to this amount. Otherwise the hediff obviously just uses its initial severity*/
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
    /*Meteoroid Skip
     * Meteoroids are unique skyfallers. When created by the ability comp, the target chunk's ThingDef is stored as the Meteoroid's chunk, which is used to determine its explosion damage
     * The damage of a meteoroid is attributed to the caster, a reference to which is also stored in the Meteoroid.
     * skyfaller: the ThingDef created, which must be of the Meteoroid class*/
    [StaticConstructorOnStartup]
    public class Meteoroid : Skyfaller
    {
        protected override void Impact()
        {
            if (HautsMiscUtility.CanBeHitByAirToSurface(base.Position, base.Map, false))
            {
                if (this.chunk == null)
                {
                    this.chunk = ThingDefOf.ChunkSlagSteel;
                }
                float damageMulti = Meteoroid.ChunkMeteorDamageMulti(this.chunk);
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
        public static float ChunkMeteorDamageMulti(ThingDef td)
        {
            float damageMulti = 1f;
            if (td.butcherProducts != null && td.butcherProducts.Count > 0)
            {
                ThingDef product = td.butcherProducts.RandomElement().thingDef;
                if (product != null)
                {
                    damageMulti = Meteoroid.CMDMInner(product);
                }
            } else if (td.smeltProducts != null && td.smeltProducts.Count > 0) {
                ThingDef product = td.smeltProducts.RandomElement().thingDef;
                if (product != null)
                {
                    damageMulti = Meteoroid.CMDMInner(product);
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
                    return Math.Max(1f, sprops.statFactors.GetStatFactorFromList(StatDefOf.MaxHitPoints));
                }
            }
            return 1f;
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
    /*Reave
     * damageDef: ewisott
     * damageToPawns|Buildings: ewisott. Reave's stated damage multiplier against buildings is actually no such thing; the damage dealt to buildings is defined separately from its anti-pawn damage
     * fleck1|2|Scale: governs the little skipgate that plays on the pawn. Wow just like the icon!!1!*/
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
}
