using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace HOP_CoolerPsycasts
{
    /*Psyfocus Transfer
     * hediffToRecipient: grants this hediff to the second target
     * maxPsyfocusStolen: ewisott
     * maxPsyfocusReceived: this or the total amount of psyfocus stolen, whichever is lower, is the base amount of severity the granted hediff should have (or gain, if it already exists)...
     * severityPerStolenPsyfocus: ...which gets multiplied by this value to determine the final severity gain*/
    public class CompProperties_AbilityTransferPsyfocus : CompProperties_EffectWithDest
    {
        public HediffDef hediffToRecipient;
        public float maxPsyfocusStolen;
        public float maxPsyfocusReceived;
        public float severityPerStolenPsyfocus;
    }
    public class CompAbilityEffect_TransferPsyfocus : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityTransferPsyfocus Props
        {
            get
            {
                return (CompProperties_AbilityTransferPsyfocus)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing && dest.HasThing)
            {
                base.Apply(target, dest);
                if (target.Thing is Pawn pawn && dest.Thing is Pawn pawn2 && pawn.psychicEntropy != null && pawn2.psychicEntropy != null)
                {
                    float psyfocusDrain = Math.Min(this.Props.maxPsyfocusStolen, pawn.psychicEntropy.CurrentPsyfocus);
                    float psyfocusGain = Math.Min(this.Props.maxPsyfocusReceived, psyfocusDrain);
                    pawn.psychicEntropy.OffsetPsyfocusDirectly(-psyfocusDrain);
                    float buffSeverity = psyfocusGain * this.Props.severityPerStolenPsyfocus;
                    Hediff extantHediff = pawn2.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffToRecipient);
                    if (extantHediff != null)
                    {
                        extantHediff.Severity += buffSeverity;
                    } else {
                        Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffToRecipient, pawn2, null);
                        pawn2.health.AddHediff(hediff);
                        hediff.Severity = buffSeverity;
                    }
                }
            }
        }
        public override TargetingParameters targetParams
        {
            get
            {
                return new TargetingParameters
                {
                    canTargetSelf = true,
                    canTargetBuildings = false,
                    canTargetAnimals = true,
                    canTargetMechs = true,
                    canTargetLocations = false
                };
            }
        }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return target != this.selectedTarget && this.BothPartiesHavePsyfocus(target, this.selectedTarget) && base.ValidateTarget(target, showMessages);
        }
        public override bool CanHitTarget(LocalTargetInfo target)
        {
            return base.CanHitTarget(target) && this.HasPsyfocusBarCheck(target) && target != this.parent.pawn;
        }
        public override bool Valid(LocalTargetInfo target, bool showMessages = true)
        {
            AcceptanceReport acceptanceReport = this.HasPsyfocusBar(target);
            if (!acceptanceReport)
            {
                if (showMessages && !acceptanceReport.Reason.NullOrEmpty() && target.Thing is Pawn pawn)
                {
                    Messages.Message("HVP_CannotTransfer".Translate(pawn.Named("PAWN")) + ": " + acceptanceReport.Reason, pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, showMessages);
        }
        private AcceptanceReport HasPsyfocusBar(LocalTargetInfo target)
        {
            if (!this.HasPsyfocusBarCheck(target))
            {
                return !ModCompatibilityUtility.IsHighFantasy() ? "HVP_NotAPsycaster".Translate() : "HVP_NotAPsycasterF".Translate();
            }
            return true;
        }
        private AcceptanceReport BothPartiesHavePsyfocus(LocalTargetInfo target, LocalTargetInfo selectedTarget)
        {
            if (!this.HasPsyfocusBarCheck(this.selectedTarget) || !this.HasPsyfocusBarCheck(target))
            {
                return !ModCompatibilityUtility.IsHighFantasy() ? "HVP_NotAPsycaster".Translate() : "HVP_NotAPsycasterF".Translate();
            }
            return true;
        }
        private bool HasPsyfocusBarCheck(LocalTargetInfo target)
        {
            if (target.Thing != null && target.Thing is Pawn p && p.HasPsylink && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon)
            {
                return true;
            }
            return false;
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (target.Thing != null && target.Thing is Pawn pawn)
            {
                return this.HasPsyfocusBar(target).Reason;
            }
            return base.ExtraLabelMouseAttachment(target);
        }
    }
    /*Squadron Call has the same targeting restrictions as Drop Pod Call
     * pawnKinds: pawns created by this psycast are of a random kind from this list
     * giveTraitToPassengers: all story tracker-having pawns created by this ability gain this trait
     * SimpleCurve and scalar are as any other Call cast*/
    public class CompProperties_AbilityDropSquad : CompProperties_AbilityEffect
    {
        public List<PawnKindDef> pawnKinds;
        public TraitDef giveTraitToPassengers;
        public SimpleCurve podPerStatCurve;
        public StatDef statScalar;
    }
    public class CompAbilityEffect_DropSquad : CompAbilityEffect
    {
        public new CompProperties_AbilityDropSquad Props
        {
            get
            {
                return (CompProperties_AbilityDropSquad)this.props;
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
            Faction f = this.parent.pawn.Faction;
            List<Pawn> pawns = new List<Pawn>();
            List<Pawn> pawns2 = new List<Pawn>();
            for (int i = 0; i < ets; i++)
            {
                PawnKindDef pkd = this.Props.pawnKinds.RandomElement();
                ActiveTransporterInfo activeTransporterInfo = new ActiveTransporterInfo();
                bool leaveSlag = false;
                Pawn pawnToDrop = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pkd, null, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 1f, 1f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, ModsConfig.IdeologyActive ? this.parent.pawn.Ideo : null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                if (pawnToDrop.story != null)
                {
                    pawnToDrop.story.traits.GainTrait(new Trait(this.Props.giveTraitToPassengers, 0, true));
                }
                if (pawnToDrop.apparel != null)
                {
                    pawnToDrop.apparel.LockAll();
                }
                pawnToDrop.SetFaction(f);
                activeTransporterInfo.innerContainer.TryAdd(pawnToDrop, true);
                activeTransporterInfo.openDelay = 110;
                activeTransporterInfo.leaveSlag = leaveSlag;
                DropPodUtility.MakeDropPodAt(target.Cell, this.parent.pawn.Map, activeTransporterInfo, null);
                if (pawnToDrop.playerSettings != null)
                {
                    pawnToDrop.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
                    pawnToDrop.playerSettings.medCare = MedicalCareCategory.NoMeds;
                }
                if (Rand.Chance(0.5f))
                {
                    pawns.Add(pawnToDrop);
                }
                else
                {
                    pawns2.Add(pawnToDrop);
                }
            }
            if (f != null && f != Faction.OfPlayer)
            {
                if (pawns.Count > 0)
                {
                    LordMaker.MakeNewLord(f, new LordJob_EscortPawn(this.parent.pawn), this.parent.pawn.Map, pawns);
                }
                if (pawns2.Count > 0)
                {
                    LordMaker.MakeNewLord(f, new LordJob_DefendPoint(this.parent.pawn.Position, 15f, 40f, false, false), this.parent.pawn.Map, pawns2);
                }
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
    //Tornado Link. You create the thingDef, you create a linkHediff on the caster and link it to the created thing. allowOnBuildings is just like AbilitySpawn
    public class CompProperties_AbilitySpawnLinkedObject : CompProperties_AbilityEffectWithDuration
    {
        public CompProperties_AbilitySpawnLinkedObject()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnLinkedObject);
        }
        public ThingDef thingDef;
        public bool allowOnBuildings = true;
        public HediffDef linkHediff;
    }
    public class CompAbilityEffect_SpawnLinkedObject : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilitySpawnLinkedObject Props
        {
            get
            {
                return (CompProperties_AbilitySpawnLinkedObject)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Hediff existingHediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.linkHediff);
            if (existingHediff != null)
            {
                HediffComp_LinkTornado hclt = existingHediff.TryGetComp<HediffComp_LinkTornado>();
                if (hclt != null)
                {
                    foreach (Thing t in hclt.others)
                    {
                        if (t is LinkedTornado lt)
                        {
                            lt.targetCell = target.Cell;
                            lt.linked = true;
                            lt.canExpire = false;
                        }
                    }
                }
            }
            else
            {
                LinkedTornado lt = (LinkedTornado)ThingMaker.MakeThing(this.Props.thingDef, null);
                lt.targetCell = target.Cell;
                GenSpawn.Spawn(lt, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                lt.ticksLeftToDisappear = (int)(this.GetDurationSeconds(null) * 60);
                lt.linked = true;
                lt.canExpire = false;
                Hediff hediff = HediffMaker.MakeHediff(this.Props.linkHediff, this.parent.pawn, null);
                HediffComp_Link hcl = hediff.TryGetComp<HediffComp_Link>();
                if (hcl != null)
                {
                    hcl.other = lt;
                    hcl.drawConnection = true;
                }
                HediffComp_MultiLink hcml = hediff.TryGetComp<HediffComp_MultiLink>();
                if (hcml != null)
                {
                    if (hcml.others == null)
                    {
                        hcml.others = new List<Thing>();
                    }
                    hcml.others.Add(lt);
                    if (hcml.motes == null)
                    {
                        hcml.motes = new List<MoteDualAttached>();
                    }
                    hcml.motes.Add(null);
                    hcml.drawConnection = true;
                }
                this.parent.pawn.health.AddHediff(hediff);
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
            return true;
        }
    }
    /*Voidquake
     * incidentDef: creates this incident on cast
     * mentalState: inflicts this state on non-Anomaly organic pawns
     * hediffMechs|Entities: inflicts this hediff on non-Anomaly non-flesh pawns|Anomaly pawns*/
    public class CompProperties_AbilityVoidquake : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityVoidquake()
        {
            this.compClass = typeof(CompAbilityEffect_Voidquake);
        }
        public IncidentDef incidentDef;
        public MentalStateDef mentalState;
        public HediffDef hediffMechs;
        public HediffDef hediffEntities;
    }
    public class CompAbilityEffect_Voidquake : CompAbilityEffect
    {
        public new CompProperties_AbilityVoidquake Props
        {
            get
            {
                return (CompProperties_AbilityVoidquake)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            foreach (Pawn pawn in this.parent.pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (this.CanApplyEffects(pawn) && !pawn.Fogged() && pawn.Position.DistanceTo(this.parent.pawn.Position) > this.parent.def.EffectRadius)
                {
                    if (ModsConfig.AnomalyActive && (pawn.RaceProps.IsAnomalyEntity || pawn.IsMutant))
                    {
                        pawn.health.AddHediff(this.Props.hediffEntities, null);
                    }
                    else if (pawn.RaceProps.IsFlesh)
                    {
                        CompAbilityEffect_GiveMentalState.TryGiveMentalState(this.Props.mentalState, pawn, this.parent.def, StatDefOf.PsychicSensitivity, this.parent.pawn, false);
                        RestUtility.WakeUp(pawn, true);
                    }
                    else
                    {
                        pawn.health.AddHediff(this.Props.hediffMechs, null);
                    }
                }
            }
            if (this.Props.incidentDef != null)
            {
                IIncidentTarget iit = this.parent.pawn.MapHeld ?? Find.Maps.RandomElement();
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(this.Props.incidentDef.category, iit);
                parms.forced = true;
                if (this.Props.incidentDef.pointsScaleable)
                {
                    parms = Find.Storyteller.storytellerComps.First((StorytellerComp x) => x is StorytellerComp_OnOffCycle || x is StorytellerComp_RandomMain).GenerateParms(this.Props.incidentDef.category, parms.target);
                }
                this.Props.incidentDef.Worker.TryExecute(parms);
            }
            base.Apply(target, dest);
        }
        private bool CanApplyEffects(Pawn p)
        {
            return !p.kindDef.isBoss && !p.Dead && !p.Suspended && (p.IsMutant || p.GetStatValue(StatDefOf.PsychicSensitivity, true, -1) > float.Epsilon);
        }
    }
}
