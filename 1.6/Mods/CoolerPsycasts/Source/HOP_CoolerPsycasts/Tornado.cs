using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace HOP_CoolerPsycasts
{
    /*while very akin to a genuine tornado, the thing created by Tornado Link has various unique behaviors.
     * It has a fixed ticksLeftToDisappear of 3 hours.
     * While linked, doesn't randomly move and its ticksLeftToDisappear running out doesn't result in its expiry. Therefore it must store whether it is linked.
     * It can have a "targetCell" IntVec3 assigned to it, which it moves to while linked*/
    [StaticConstructorOnStartup]
    public class LinkedTornado : ThingWithComps
    {
        private float FadeInOutFactor
        {
            get
            {
                float num = Mathf.Clamp01((float)(Find.TickManager.TicksGame - this.spawnTick) / 120f);
                float num2 = ((this.leftFadeOutTicks < 0) ? 1f : Mathf.Min((float)this.leftFadeOutTicks / 120f, 1f));
                return Mathf.Min(num, num2);
            }
        }
        public override Vector2 DrawSize
        {
            get
            {
                return new Vector2(45f, 100f);
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<Vector2>(ref this.realPosition, "realPosition", default(Vector2), false);
            Scribe_Values.Look<float>(ref this.direction, "direction", 0f, false);
            Scribe_Values.Look<int>(ref this.spawnTick, "spawnTick", 0, false);
            Scribe_Values.Look<int>(ref this.leftFadeOutTicks, "leftFadeOutTicks", 0, false);
            Scribe_Values.Look<int>(ref this.ticksLeftToDisappear, "ticksLeftToDisappear", 7500, false);
            Scribe_Values.Look<float>(ref this.wanderSpeed, "wanderSpeed", 0.028333334f, false);
            Scribe_Values.Look<bool>(ref this.linked, "linked", false, false);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                Vector3 vector = base.Position.ToVector3Shifted();
                this.realPosition = new Vector2(vector.x, vector.z);
                this.direction = Rand.Range(0f, 360f);
                this.spawnTick = Find.TickManager.TicksGame;
                this.leftFadeOutTicks = -1;
                this.ticksLeftToDisappear = 7500;
                this.linked = false;
                this.canExpire = true;
            }
            this.CreateSustainer();
        }
        protected override void Tick()
        {
            if (!base.Spawned)
            {
                return;
            }
            if (this.sustainer == null)
            {
                Log.Error("Tornado sustainer is null.");
                this.CreateSustainer();
            }
            Sustainer sustainer = this.sustainer;
            if (sustainer != null)
            {
                sustainer.Maintain();
            }
            this.UpdateSustainerVolume();
            base.GetComp<CompWindSource>().wind = 5f * this.FadeInOutFactor;
            if (this.leftFadeOutTicks == 0)
            {
                this.Destroy(DestroyMode.Vanish);
                return;
            }
            if (this.leftFadeOutTicks >= 0)
            {
                this.leftFadeOutTicks--;
                return;
            }
            else
            {
                if (!this.linked)
                {
                    this.canExpire = true;
                    if (LinkedTornado.directionNoise == null)
                    {
                        LinkedTornado.directionNoise = new Perlin(0.0020000000949949026, 2.0, 0.5, 4, 1948573612, QualityMode.Medium);
                    }
                    this.direction += (float)LinkedTornado.directionNoise.GetValue((double)Find.TickManager.TicksAbs, (double)((float)(this.thingIDNumber % 500) * 1000f), 0.0) * 0.87f;
                    this.realPosition = this.realPosition.Moved(this.direction, this.wanderSpeed);
                }
                else if (this.targetCell.IsValid && this.targetCell.InBounds(this.Map) && this.targetCell.DistanceTo(base.Position) > 2f)
                {
                    this.direction = this.realPosition.AngleTo(this.targetCell.ToVector2());
                    this.realPosition = this.realPosition.Moved(this.direction, Math.Min(this.wanderSpeed, this.Position.DistanceTo(this.targetCell)));
                }
                IntVec3 intVec = new Vector3(this.realPosition.x, 0f, this.realPosition.y).ToIntVec3();
                if (intVec.InBounds(base.Map))
                {
                    base.Position = intVec;
                    if (this.IsHashIntervalTick(15))
                    {
                        this.DamageCloseThings();
                    }
                    if (Rand.MTBEventOccurs(15f, 1f, 1f))
                    {
                        this.DamageFarThings();
                    }
                    if (this.IsHashIntervalTick(20))
                    {
                        this.DestroyRoofs();
                    }
                    if (this.ticksLeftToDisappear > 0)
                    {
                        this.ticksLeftToDisappear--;
                    }
                    if (this.ticksLeftToDisappear == 0 && this.canExpire)
                    {
                        this.leftFadeOutTicks = 120;
                        Messages.Message("MessageTornadoDissipated".Translate(), new TargetInfo(base.Position, base.Map, false), MessageTypeDefOf.PositiveEvent, true);
                    }
                    if (this.IsHashIntervalTick(4) && !this.CellImmuneToDamage(base.Position))
                    {
                        float num = Rand.Range(0.6f, 1f);
                        FleckMaker.ThrowTornadoDustPuff(new Vector3(this.realPosition.x, 0f, this.realPosition.y)
                        {
                            y = AltitudeLayer.MoteOverhead.AltitudeFor()
                        } + Vector3Utility.RandomHorizontalOffset(1.5f), base.Map, Rand.Range(1.5f, 3f), new Color(num, num, num));
                        return;
                    }
                }
                else
                {
                    this.leftFadeOutTicks = 120;
                    Messages.Message("MessageTornadoLeftMap".Translate(), new TargetInfo(base.Position, base.Map, false), MessageTypeDefOf.PositiveEvent, true);
                }
            }
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Rand.PushState();
            Rand.Seed = this.thingIDNumber;
            for (int i = 0; i < 180; i++)
            {
                this.DrawTornadoPart(LinkedTornado.PartsDistanceFromCenter.RandomInRange, Rand.Range(0f, 360f), Rand.Range(0.9f, 1.1f), Rand.Range(0.52f, 0.88f));
            }
            Rand.PopState();
        }
        private void DrawTornadoPart(float distanceFromCenter, float initialAngle, float speedMultiplier, float colorMultiplier)
        {
            int ticksGame = Find.TickManager.TicksGame;
            float num = 1f / distanceFromCenter;
            float num2 = 25f * speedMultiplier * num;
            float num3 = (initialAngle + (float)ticksGame * num2) % 360f;
            Vector2 vector = this.realPosition.Moved(num3, this.AdjustedDistanceFromCenter(distanceFromCenter));
            vector.y += distanceFromCenter * 4f;
            vector.y += LinkedTornado.ZOffsetBias;
            Vector3 vector2 = new Vector3(vector.x, AltitudeLayer.Weather.AltitudeFor() + 0.03658537f * Rand.Range(0f, 1f), vector.y);
            float num4 = distanceFromCenter * 3f;
            float num5 = 1f;
            if (num3 > 270f)
            {
                num5 = GenMath.LerpDouble(270f, 360f, 0f, 1f, num3);
            }
            else if (num3 > 180f)
            {
                num5 = GenMath.LerpDouble(180f, 270f, 1f, 0f, num3);
            }
            float num6 = Mathf.Min(distanceFromCenter / (LinkedTornado.PartsDistanceFromCenter.max + 2f), 1f);
            float num7 = Mathf.InverseLerp(0.18f, 0.4f, num6);
            Vector3 vector3 = new Vector3(Mathf.Sin((float)ticksGame / 1000f + (float)(this.thingIDNumber * 10)) * 2f, 0f, 0f);
            Vector3 vector4 = vector2 + vector3 * num7;
            float num8 = Mathf.Max(1f - num6, 0f) * num5 * this.FadeInOutFactor;
            Color color = new Color(colorMultiplier, colorMultiplier, colorMultiplier, num8);
            LinkedTornado.matPropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Matrix4x4 matrix4x = Matrix4x4.TRS(vector4, Quaternion.Euler(0f, num3, 0f), new Vector3(num4, 1f, num4));
            Graphics.DrawMesh(MeshPool.plane10, matrix4x, LinkedTornado.TornadoMaterial, 0, null, 0, LinkedTornado.matPropertyBlock);
        }
        private float AdjustedDistanceFromCenter(float distanceFromCenter)
        {
            float num = Mathf.Min(distanceFromCenter / 8f, 1f);
            num *= num;
            return distanceFromCenter * num;
        }
        private void UpdateSustainerVolume()
        {
            this.sustainer.info.volumeFactor = this.FadeInOutFactor;
        }
        private void CreateSustainer()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                SoundDef tornado = SoundDefOf.Tornado;
                this.sustainer = tornado.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                this.UpdateSustainerVolume();
            });
        }
        private void DamageCloseThings()
        {
            int num = GenRadial.NumCellsInRadius(4.2f);
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = base.Position + GenRadial.RadialPattern[i];
                if (intVec.InBounds(base.Map) && !this.CellImmuneToDamage(intVec))
                {
                    Pawn firstPawn = intVec.GetFirstPawn(base.Map);
                    if (firstPawn == null || !firstPawn.Downed || !Rand.Bool)
                    {
                        float num2 = GenMath.LerpDouble(0f, 4.2f, 1f, 0.2f, intVec.DistanceTo(base.Position));
                        this.DoDamage(intVec, num2);
                    }
                }
            }
        }
        private void DamageFarThings()
        {
            IntVec3 intVec = (from x in GenRadial.RadialCellsAround(base.Position, 10f, true)
                              where x.InBounds(base.Map)
                              select x).RandomElement<IntVec3>();
            if (this.CellImmuneToDamage(intVec))
            {
                return;
            }
            this.DoDamage(intVec, 0.5f);
        }
        private void DestroyRoofs()
        {
            this.removedRoofsTmp.Clear();
            foreach (IntVec3 intVec in from x in GenRadial.RadialCellsAround(base.Position, 4.2f, true)
                                       where x.InBounds(base.Map)
                                       select x)
            {
                if (!this.CellImmuneToDamage(intVec) && intVec.Roofed(base.Map))
                {
                    RoofDef roof = intVec.GetRoof(base.Map);
                    if (!roof.isThickRoof && !roof.isNatural)
                    {
                        RoofCollapserImmediate.DropRoofInCells(intVec, base.Map, null);
                        this.removedRoofsTmp.Add(intVec);
                    }
                }
            }
            if (this.removedRoofsTmp.Count > 0)
            {
                RoofCollapseCellsFinder.CheckCollapseFlyingRoofs(this.removedRoofsTmp, base.Map, true, false);
            }
        }
        private bool CellImmuneToDamage(IntVec3 c)
        {
            if (c.Roofed(base.Map) && c.GetRoof(base.Map).isThickRoof)
            {
                return true;
            }
            Building edifice = c.GetEdifice(base.Map);
            return edifice != null && edifice.def.category == ThingCategory.Building && (edifice.def.building.isNaturalRock || (edifice.def == ThingDefOf.Wall && edifice.Faction == null));
        }
        private void DoDamage(IntVec3 c, float damageFactor)
        {
            LinkedTornado.tmpThings.Clear();
            LinkedTornado.tmpThings.AddRange(c.GetThingList(base.Map));
            Vector3 vector = c.ToVector3Shifted();
            Vector2 vector2 = new Vector2(vector.x, vector.z);
            float num = -this.realPosition.AngleTo(vector2) + 180f;
            for (int i = 0; i < LinkedTornado.tmpThings.Count; i++)
            {
                BattleLogEntry_DamageTaken battleLogEntry_DamageTaken = null;
                switch (LinkedTornado.tmpThings[i].def.category)
                {
                    case ThingCategory.Pawn:
                        {
                            Pawn pawn = (Pawn)LinkedTornado.tmpThings[i];
                            battleLogEntry_DamageTaken = new BattleLogEntry_DamageTaken(pawn, RulePackDefOf.DamageEvent_Tornado, null);
                            Find.BattleLog.Add(battleLogEntry_DamageTaken);
                            if (pawn.RaceProps.baseHealthScale < 1f)
                            {
                                damageFactor *= pawn.RaceProps.baseHealthScale;
                            }
                            if (pawn.RaceProps.Animal)
                            {
                                damageFactor *= 0.75f;
                            }
                            if (pawn.Downed)
                            {
                                damageFactor *= 0.2f;
                            }
                            break;
                        }
                    case ThingCategory.Item:
                        damageFactor *= 0.68f;
                        break;
                    case ThingCategory.Building:
                        damageFactor *= 0.8f;
                        break;
                    case ThingCategory.Plant:
                        damageFactor *= 1.7f;
                        break;
                }
                int num2 = Mathf.Max(GenMath.RoundRandom(30f * damageFactor), 1);
                LinkedTornado.tmpThings[i].TakeDamage(new DamageInfo(DamageDefOf.TornadoScratch, (float)num2, 0f, num, this, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false)).AssociateWithLog(battleLogEntry_DamageTaken);
            }
            LinkedTornado.tmpThings.Clear();
        }
        public Vector2 realPosition;
        public float direction;
        public int spawnTick;
        public int leftFadeOutTicks = -1;
        public int ticksLeftToDisappear = -1;
        public bool canExpire = true;
        public float wanderSpeed = 0.028333334f;
        public bool linked = false;
        public IntVec3 targetCell;
        private Sustainer sustainer;
        private static MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
        public static ModuleBase directionNoise;
        private static readonly Material TornadoMaterial = MaterialPool.MatFrom("Things/Ethereal/Tornado", ShaderDatabase.Transparent, MapMaterialRenderQueues.Tornado);
        private static readonly FloatRange PartsDistanceFromCenter = new FloatRange(1f, 10f);
        private static readonly float ZOffsetBias = -4f * LinkedTornado.PartsDistanceFromCenter.min;
        public List<IntVec3> removedRoofsTmp = new List<IntVec3>();
        public static List<Thing> tmpThings = new List<Thing>();
    }
}
