using HautsPsycasts;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HOP_CoolerPsycasts
{
    /*Since, as far as I can tell, VEF's HediffCompProperties_Targeting doesn't work in the Combat Extended system, Marking Pulse's debuff works different if you have CE.
     * The only unique code aspect of it is that the victim gets stunned on taking any damage, for a random duration within stunTicks.*/
    public class HediffCompProperties_IShallStrikeYouDumb : HediffCompProperties
    {
        public HediffCompProperties_IShallStrikeYouDumb()
        {
            this.compClass = typeof(HediffComp_IShallStrikeYouDumb);
        }
        public int stunTicks;
    }
    public class HediffComp_IShallStrikeYouDumb : HediffComp
    {
        public HediffCompProperties_IShallStrikeYouDumb Props
        {
            get
            {
                return (HediffCompProperties_IShallStrikeYouDumb)this.props;
            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (this.Pawn.stances != null && this.Pawn.stances.stunner != null)
            {
                this.Pawn.stances.stunner.StunFor(this.Props.stunTicks,dinfo.Instigator);
            }
        }
    }
    //Blessings from Word of Blessing cannot coexist on the same pawn, because they remove each other. They also fade if the victim is psychically deaf
    public class HediffBlessing : Hediff
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            List<Hediff> otherBlessings = new List<Hediff>();
            foreach (Hediff h in this.pawn.health.hediffSet.hediffs)
            {
                if (h != this && h is HediffBlessing)
                {
                    otherBlessings.Add(h);
                }
            }
            foreach (Hediff h in otherBlessings)
            {
                this.pawn.health.RemoveHediff(h);
            }
        }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (this.pawn.IsHashIntervalTick(250))
            {
                this.Severity = this.pawn.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon ? this.def.minSeverity : this.def.maxSeverity;
            }
        }
    }
    /*the hidden hediff on the caster of Tornado Link. It sets the tornado's speed to controlledSpeed while linked, and on unlinking it sets its speed to wanderSpeed.
     * secsForAiToLetLoose: if an NPC has a linked tornado, once this hediff's age exceeds this value, the hediff disappears, thus unleashing the tornado. */
    public class HediffCompProperties_LinkTornado : HediffCompProperties_LinkBuildEntropy
    {
        public HediffCompProperties_LinkTornado()
        {
            this.compClass = typeof(HediffComp_LinkTornado);
        }
        public float controlledSpeed;
        public float wanderSpeed;
        public int secsForAiToLetLoose;
    }
    public class HediffComp_LinkTornado : HediffComp_LinkBuildEntropy
    {
        public new HediffCompProperties_LinkTornado Props
        {
            get
            {
                return (HediffCompProperties_LinkTornado)this.props;
            }
        }
        public override void DoPeriodicDamageInner(Thing t)
        {
            if (t is LinkedTornado lt)
            {
                lt.linked = true;
                lt.wanderSpeed = this.Props.controlledSpeed;
                lt.canExpire = false;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.Faction != null && this.Pawn.HostileTo(Faction.OfPlayerSilentFail) && this.parent.ageTicks >= (this.Props.secsForAiToLetLoose*60))
            {
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
        protected override void RemoveOthers()
        {
            this.UnlinkTornados();
            base.RemoveOthers();
        }
        public override void CompPostPostRemoved()
        {
            if (!this.others.NullOrEmpty())
            {
                this.UnlinkTornados();
            }
            base.CompPostPostRemoved();
        }
        public override void DoToDistanceBrokenLink(Thing other)
        {
            this.UnlinkThisTornado(other);
        }
        public void UnlinkThisTornado(Thing t)
        {
            if (t is LinkedTornado lt)
            {
                lt.wanderSpeed = this.Props.wanderSpeed;
                lt.linked = false;
                lt.canExpire = true;
            }
        }
        public void UnlinkTornados()
        {
            foreach (Thing t in this.others)
            {
                this.UnlinkThisTornado(t);
            }
        }
    }
    /*voidquake inflicts a hediff on the caster.
     * mentalStateMTBdays: MTB this many days...
     * mentalStateRoster: do a random mental STATE from this list*/
    public class HediffCompProperties_VoidExposure : HediffCompProperties_SeverityPerDay
    {
        public HediffCompProperties_VoidExposure()
        {
            this.compClass = typeof(HediffComp_VoidExposure);
        }
        public float mentalStateMTBdays;
        public List<MentalStateDef> mentalStateRoster;
    }
    public class HediffComp_VoidExposure : HediffComp_SeverityPerDay
    {
        public HediffCompProperties_VoidExposure Props
        {
            get
            {
                return (HediffCompProperties_VoidExposure)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(1020) && this.Pawn.mindState != null && this.Pawn.mindState.mentalStateHandler != null && !this.Pawn.mindState.mentalStateHandler.InMentalState && Rand.MTBEventOccurs(this.Props.mentalStateMTBdays, 60000f, 1020f))
            {
                List<MentalStateDef> msds = this.Props.mentalStateRoster;
                while (msds.Count > 0)
                {
                    MentalStateDef msd = msds.RandomElement();
                    if (this.Pawn.mindState.mentalStateHandler.TryStartMentalState(msd))
                    {
                        break;
                    } else {
                        msds.Remove(msd);
                    }
                }
            }
        }
    }
}
