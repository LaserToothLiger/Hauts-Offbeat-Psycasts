using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    //allows armor piercing bolts (from Thunderbolt and buffed Flashstorm) to have good vfx that last for more than one frame
    [StaticConstructorOnStartup]
    public class WeatherEvent_ArmourPiercingBolt : WeatherEvent_LightningFlash
    {
        public WeatherEvent_ArmourPiercingBolt(Map map): base(map)
        {
        }
        public WeatherEvent_ArmourPiercingBolt(Map map, IntVec3 forcedStrikeLoc): base(map)
        {
            this.strikeLoc = forcedStrikeLoc;
        }
        public override void FireEvent()
        {
            WeatherEvent_LightningStrike.DoStrike(this.strikeLoc, this.map, ref this.boltMesh);
        }
        public static void DoStrike(IntVec3 strikeLoc, Map map, ref Mesh boltMesh)
        {
            SoundDefOf.Thunder_OffMap.PlayOneShotOnCamera(map);
            if (!strikeLoc.IsValid)
            {
                strikeLoc = CellFinderLoose.RandomCellWith((IntVec3 sq) => sq.Standable(map) && !map.roofGrid.Roofed(sq), map, 1000);
            }
            boltMesh = LightningBoltMeshPool.RandomBoltMesh;
            if (!strikeLoc.Fogged(map))
            {
                Vector3 vector = strikeLoc.ToVector3Shifted();
                for (int i = 0; i < 4; i++)
                {
                    FleckMaker.ThrowSmoke(vector, map, 1.5f);
                    FleckMaker.ThrowMicroSparks(vector, map);
                    FleckMaker.ThrowLightningGlow(vector, map, 1.5f);
                }
            }
            SoundInfo soundInfo = SoundInfo.InMap(new TargetInfo(strikeLoc, map, false), MaintenanceType.None);
            SoundDefOf.Thunder_OnMap.PlayOneShot(soundInfo);
        }
        public override void WeatherEventDraw()
        {
            Graphics.DrawMesh(this.boltMesh, this.strikeLoc.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather), Quaternion.identity, FadedMaterialPool.FadedVersionOf(WeatherEvent_ArmourPiercingBolt.LightningMat, this.LightningBrightness), 0);
        }
        private IntVec3 strikeLoc = IntVec3.Invalid;
        private Mesh boltMesh;
        private static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt", -1);
    }
    //the skipgates that intermittently appear around skipped turrets and thermal pinholes
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
                    }
                    else
                    {
                        this.mote.Maintain();
                    }
                }
            }
            else if (this.mote != null)
            {
                this.mote.Destroy();
            }
        }
        public Mote mote;
    }
    //specifically for the thermal pinhole's overlaid skipgate mote
    public class MoteAttached_Skipgate : MoteAttached
    {
        protected override void TimeInterval(float deltaTime)
        {
            base.TimeInterval(deltaTime);
            this.exactRotation += this.rotationRate * deltaTime;
        }
    }
}
