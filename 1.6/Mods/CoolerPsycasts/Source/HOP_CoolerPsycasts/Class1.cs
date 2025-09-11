using HautsFramework;
using HautsPsycasts;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;

namespace HOP_CoolerPsycasts
{
    public class HOP_CoolerPsycasts
    {
    }
    public class CompProperties_AbilityDatashock : CompProperties_AbilityEffectWithDuration
    {
        public StatDef effectMultiplier;
        public float hackProgress;
        public MentalStateDef mentalState;
        public float researchProgress;
        public IntRange stunTicksVsNonDroneMechs;
    }
    public class CompAbilityEffect_Datashock : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilityDatashock Props
        {
            get
            {
                return (CompProperties_AbilityDatashock)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Thing t = target.Thing;
            float psysens = this.parent.pawn.GetStatValue(this.Props.effectMultiplier);
            if (t != null)
            {
                if (t is Pawn p)
                {
                    if (ModsConfig.OdysseyActive && p.RaceProps.IsDrone)
                    {
                        p.SetFaction(this.parent.pawn.Faction);
                        p.jobs.StopAll(false, true);
                        LordMaker.MakeNewLord(p.Faction, new LordJob_EscortPawn(this.parent.pawn), p.Map, Gen.YieldSingle<Pawn>(p));
                    } else if (p.RaceProps.IsFlesh) {
                        if (!p.InMentalState)
                        {
                            CompAbilityEffect_GiveMentalState.TryGiveMentalState(this.Props.mentalState, p, this.parent.def, this.Props.durationMultiplier, this.parent.pawn, false);
                            RestUtility.WakeUp(p, true);
                        }
                    } else {
                        p.stances.stunner.StunFor((int)(this.Props.stunTicksVsNonDroneMechs.RandomInRange * p.GetStatValue(this.Props.durationMultiplier)), this.parent.pawn, false, true, false);
                    }
                    return;
                }
                if (t is Building_Turret bt)
                {
                    bt.SetFaction(this.parent.pawn.Faction);
                    return;
                }
                if (t is Building_ResearchBench)
                {
                    ResearchProjectDef rpd = Find.ResearchManager.GetProject(null);
                    if (rpd != null)
                    {
                        Find.ResearchManager.AddProgress(rpd, this.Props.researchProgress * this.parent.pawn.GetStatValue(this.Props.effectMultiplier), this.parent.pawn);
                    }
                }
                CompBladelinkWeapon cbw = t.TryGetComp<CompBladelinkWeapon>();
                if (cbw != null)
                {
                    return;
                }
                CompBiocodable cbc = t.TryGetComp<CompBiocodable>();
                if (cbc != null && cbc.Biocoded)
                {
                    cbc.UnCode();
                    return;
                }
                CompHackable chc = t.TryGetComp<CompHackable>();
                if (chc != null && !chc.IsHacked)
                {
                    chc.Hack(this.Props.hackProgress*psysens,this.parent.pawn);
                    return;
                }
            }
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Thing t = target.Thing;
            if (t != null)
            {
                CompBladelinkWeapon cbw = t.TryGetComp<CompBladelinkWeapon>();
                if (cbw != null)
                {
                    return false;
                }
                CompBiocodable cbc = t.TryGetComp<CompBiocodable>();
                if (cbc != null && cbc.Biocoded)
                {
                    return true;
                }
                CompHackable chc = t.TryGetComp<CompHackable>();
                if (chc != null && !chc.IsHacked)
                {
                    return true;
                }
                if ((t is Pawn p && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon && !p.kindDef.isBoss && (!p.RaceProps.IsFlesh || !p.InMentalState)) || (t is Building_Turret && t.Faction != this.parent.pawn.Faction) || (t is Building_ResearchBench && Find.ResearchManager.GetProject(null) != null))
                {
                    return true;
                }
            }
            return false;
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return this.parent.pawn.Faction != Faction.OfPlayer && target.Pawn != null;
        }
    }
    public class CompProperties_AbilityTransferPsyfocus : CompProperties_EffectWithDest
    {
        public HediffDef hediffToSelf;
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
                    float psyfocusDrain = Math.Min(this.Props.maxPsyfocusStolen,pawn.psychicEntropy.CurrentPsyfocus);
                    float psyfocusGain = Math.Min(this.Props.maxPsyfocusReceived,psyfocusDrain);
                    pawn.psychicEntropy.OffsetPsyfocusDirectly(-psyfocusDrain);
                    float buffSeverity = psyfocusGain * this.Props.severityPerStolenPsyfocus;
                    Hediff extantHediff = pawn2.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffToSelf);
                    if (extantHediff != null)
                    {
                        extantHediff.Severity += buffSeverity;
                    } else {
                        Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffToSelf, pawn2, null);
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
                return !HautsUtility.IsHighFantasy() ? "HVP_NotAPsycaster".Translate() : "HVP_NotAPsycasterF".Translate();
            }
            return true;
        }
        private AcceptanceReport BothPartiesHavePsyfocus(LocalTargetInfo target, LocalTargetInfo selectedTarget)
        {
            if (!this.HasPsyfocusBarCheck(this.selectedTarget) || !this.HasPsyfocusBarCheck(target))
            {
                return !HautsUtility.IsHighFantasy() ? "HVP_NotAPsycaster".Translate() : "HVP_NotAPsycasterF".Translate();
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
                    pawnToDrop.story.traits.GainTrait(new Trait(this.Props.giveTraitToPassengers,0,true));
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
                } else {
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
                    LordMaker.MakeNewLord(f, new LordJob_DefendPoint(this.parent.pawn.Position,15f,40f,false,false), this.parent.pawn.Map, pawns2);
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
    }
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
            } else {
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
            } else {
                if (!this.linked)
                {
                    this.canExpire = true;
                    if (LinkedTornado.directionNoise == null)
                    {
                        LinkedTornado.directionNoise = new Perlin(0.0020000000949949026, 2.0, 0.5, 4, 1948573612, QualityMode.Medium);
                    }
                    this.direction += (float)LinkedTornado.directionNoise.GetValue((double)Find.TickManager.TicksAbs, (double)((float)(this.thingIDNumber % 500) * 1000f), 0.0) * 0.87f;
                    this.realPosition = this.realPosition.Moved(this.direction, this.wanderSpeed);
                } else if (this.targetCell.IsValid && this.targetCell.InBounds(this.Map) && this.targetCell.DistanceTo(base.Position) > 2f) {
                    this.direction = this.realPosition.AngleTo(this.targetCell.ToVector2());
                    this.realPosition = this.realPosition.Moved(this.direction, Math.Min(this.wanderSpeed,this.Position.DistanceTo(this.targetCell)));
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
                } else {
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
                        pawn.health.AddHediff(this.Props.hediffEntities,null);
                    } else if (pawn.RaceProps.IsFlesh) {
                        CompAbilityEffect_GiveMentalState.TryGiveMentalState(this.Props.mentalState, pawn, this.parent.def, StatDefOf.PsychicSensitivity, this.parent.pawn, false);
                        RestUtility.WakeUp(pawn, true);
                    } else {
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
