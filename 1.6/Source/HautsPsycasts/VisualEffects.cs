using RimWorld;
using UnityEngine;
using Verse;

namespace HautsPsycasts
{
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
