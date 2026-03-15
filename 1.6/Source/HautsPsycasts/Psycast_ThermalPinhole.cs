using HautsFramework;
using System;
using Verse;

namespace HautsPsycasts
{
    /*Thermal Pinhole was also supposed to extinguish wicks on lit explosives, but for some reason I walked this back. Currently doesn't do that. Instead...
     * heatPerSecond: pushes positive or negative heat of up to this amount every 60 ticks. The directionality is whichever would bring the current local temperature into...
     * desiredTemperatureRange: ...this range. If already in this range, don't push heat
     * fireExtinguishment: how much extinguishing damage is dealt to each BadAttachable (fire, VGE astrofire, VQE Cryptoforge cryptofreeze...) that is found within fireRadius cells
     * effecterDef: nifty little visual effect to distinguish thermal pinholes from regular solar pinholes (although a slightly different color and the skipgate overlay help with that too)*/
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
            if (this.parent.IsHashIntervalTick(60))
            {
                this.PushHeat(1f);
            }
        }
        public void PushHeat(float magnitude = 1f)
        {
            if (this.ShouldPushHeatNow)
            {
                float ambientTemperature = this.parent.AmbientTemperature;
                if (ambientTemperature < this.Props.desiredTemperatureRange.min)
                {
                    GenTemperature.PushHeat(this.parent.PositionHeld, this.parent.MapHeld, magnitude * Math.Min(this.Props.heatPerSecond, this.Props.desiredTemperatureRange.min - ambientTemperature));
                }
                else if (ambientTemperature > this.Props.desiredTemperatureRange.max)
                {
                    GenTemperature.PushHeat(this.parent.PositionHeld, this.parent.MapHeld, magnitude * Math.Min(-this.Props.heatPerSecond, this.Props.desiredTemperatureRange.max - ambientTemperature));
                }
            }
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(this.parent.Position, this.parent.Map, this.Props.fireRadius, true))
            {
                BadAttachable ba = thing.def.GetModExtension<BadAttachable>();
                if (ba != null && ba.extinguishingDamageDef != null)
                {
                    thing.TakeDamage(new DamageInfo(ba.extinguishingDamageDef, this.Props.fireExtinguishment));
                }
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
}
