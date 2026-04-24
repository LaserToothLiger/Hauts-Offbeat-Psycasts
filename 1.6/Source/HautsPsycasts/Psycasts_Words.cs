using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HautsPsycasts
{
    //Word of Warning's buff is a DamageNegation comp that only works if the pawn isn't downed or sleeping. Regardless of whether that condition is met, the cost is paid on taking damage; as set up in XML, this causes its removal
    public class HediffCompProperties_Forewarned : HediffCompProperties_DamageNegation
    {
        public HediffCompProperties_Forewarned()
        {
            this.compClass = typeof(HediffComp_Forewarned);
        }
        public bool mustBeConscious;
    }
    public class HediffComp_Forewarned : HediffComp_DamageNegation
    {
        public new HediffCompProperties_Forewarned Props
        {
            get
            {
                return (HediffCompProperties_Forewarned)this.props;
            }
        }
        public override bool ShouldDoModificationInner(DamageInfo dinfo)
        {
            if (this.Props.mustBeConscious && (this.Pawn.Downed || !this.Pawn.Awake() || this.Pawn.Suspended))
            {
                return false;
            }
            return base.ShouldDoModificationInner(dinfo);
        }
    }
    //Word of Safety's ability comp almost identical to GiveHediff, except it specifically sets the faction of the hediff's HediffComp_Intervention to that of the caster
    public class CompAbilityEffect_Intervention : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilityGiveHediff Props
        {
            get
            {
                return (CompProperties_AbilityGiveHediff)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (this.Props.ignoreSelf && target.Pawn == this.parent.pawn)
            {
                return;
            }
            if (!this.Props.onlyApplyToSelf && this.Props.applyToTarget)
            {
                this.ApplyInner(target.Pawn, this.parent.pawn);
            }
            if (this.Props.applyToSelf || this.Props.onlyApplyToSelf)
            {
                this.ApplyInner(this.parent.pawn, target.Pawn);
            }
        }
        protected void ApplyInner(Pawn target, Pawn other)
        {
            if (target != null)
            {
                if (this.TryResist(target))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "Resisted".Translate(), -1f);
                    return;
                }
                if (this.Props.replaceExisting)
                {
                    Hediff firstHediffOfDef = target.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef, false);
                    if (firstHediffOfDef != null)
                    {
                        target.health.RemoveHediff(firstHediffOfDef);
                    }
                }
                Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, target, this.Props.onlyBrain ? target.health.hediffSet.GetBrain() : null);
                HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
                if (hediffComp_Disappears != null)
                {
                    hediffComp_Disappears.ticksToDisappear = base.GetDurationSeconds(target).SecondsToTicks();
                }
                if (this.Props.severity >= 0f)
                {
                    hediff.Severity = this.Props.severity;
                }
                HediffComp_Intervention hci = hediff.TryGetComp<HediffComp_Intervention>();
                if (hci != null)
                {
                    hci.faction = other.Faction ?? null;
                }
                target.health.AddHediff(hediff, null, null, null);
            }
        }
        protected virtual bool TryResist(Pawn pawn)
        {
            return false;
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return this.parent.pawn.Faction != Faction.OfPlayer && target.Pawn != null;
        }
    }
    /*hediffOnDowned: turns into this hediff after being downed
     * fleck and sound stuff: S/VFX as you would expect
     * stunTicks: whenever the teleportation effect triggers, stun the pawn for a random tick duration in this range. SHOULD be superfluous, but you never know, and it is a common conceit of non-modded skips that they stun
     * GoToBedOuter() determines where to teleport (if anywhere)*/
    public class HediffCompProperties_Intervention : HediffCompProperties
    {
        public HediffCompProperties_Intervention()
        {
            this.compClass = typeof(HediffComp_Intervention);
        }
        public HediffDef hediffOnDowned;
        public FleckDef fleckEntry;
        public SoundDef soundEntry;
        public FleckDef fleckExit1;
        public FleckDef fleckExit2;
        public SoundDef soundExit;
        public IntRange stunTicks;
    }
    public class HediffComp_Intervention : HediffComp
    {
        public HediffCompProperties_Intervention Props
        {
            get
            {
                return (HediffCompProperties_Intervention)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.Downed)
            {
                this.GoToBedOuter();
            }
        }
        public override void Notify_PawnKilled()
        {
            this.GoToBedOuter();
            base.Notify_PawnKilled();
        }
        private void GoToBedOuter()
        {
            if (this.faction == null)
            {
                this.faction = this.Pawn.Faction;
            }
            Faction pFaction = this.Pawn.Faction;
            Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffOnDowned, this.Pawn);
            this.Pawn.health.AddHediff(hediff, null);
            bool anyBedFound = false;
            if (this.Pawn.SpawnedOrAnyParentSpawned)
            {
                this.GoToBed(this.Pawn.MapHeld, out anyBedFound);
            }
            if (!anyBedFound)
            {
                if (pFaction == this.faction || this.faction.RelationKindWith(pFaction) == FactionRelationKind.Hostile)
                {
                    foreach (Map m in Find.Maps)
                    {
                        if (m.ParentFaction != null && m.ParentFaction == this.faction)
                        {
                            this.GoToBed(m, out anyBedFound);
                        }
                        if (anyBedFound)
                        {
                            break;
                        }
                    }
                    if (!anyBedFound)
                    {
                        foreach (Settlement sett in Find.WorldObjects.Settlements)
                        {
                            if (sett.Faction != null && sett.Faction == this.faction && sett.Map == null)
                            {
                                PawnUtility.ForceEjectFromContainer(this.Pawn);
                                this.Pawn.GetCaravan()?.RemovePawn(this.Pawn);
                                if (this.Pawn.Faction != null && faction.HostileTo(this.Pawn.Faction) && faction != Faction.OfPlayer)
                                {
                                    faction.kidnapped.Kidnap(this.Pawn, null);
                                    Messages.Message("HVP_PsychicKidnapping".Translate(this.faction.Name, this.Pawn.Name.ToStringShort), MessageTypeDefOf.NegativeEvent, false);
                                } else {
                                    if (this.Pawn.Spawned)
                                    {
                                        this.Pawn.ExitMap(false, this.Pawn.def.defaultPlacingRot);
                                    }
                                    if (!this.Pawn.IsWorldPawn())
                                    {
                                        Find.WorldPawns.PassToWorld(this.Pawn, PawnDiscardDecideMode.Decide);
                                    }
                                }
                                anyBedFound = true;
                            }
                        }
                    }
                }
            }
            if (this.Pawn.Faction != pFaction)
            {
                this.Pawn.SetFaction(pFaction);
            }
            this.Pawn.health.RemoveHediff(this.parent);
        }
        private void GoToBed(Map map, out bool anyBed)
        {
            anyBed = false;
            //grab all beds
            List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.Bed);
            if (list.Count > 0)
            {
                /*verify they are all beds because ThingRequestGroups are witchcraft to me. they've been accurate THUS FAR but who knows w mods.
                 also so I don't have to keep casting every Thing in the list as a Bed every time*/
                List<Building_Bed> beds = new List<Building_Bed>();
                foreach (Thing t in list)
                {
                    if (t is Building_Bed b)
                    {
                        beds.Add(b);
                    }
                }
                if (beds.Count > 0)
                {
                    //remove beds w current occupants
                    for (int i = beds.Count - 1; i >= 0; i--)
                    {
                        if (beds[i].CurOccupants.Count() > 0)
                        {
                            beds.RemoveAt(i);
                        }
                    }
                    if (beds.Count > 0)
                    {
                        //remove beds based on whether pawn is animal, baby, or grown human
                        if (this.Pawn.RaceProps.Animal)
                        {
                            for (int i = beds.Count - 1; i >= 0; i--)
                            {
                                if (beds[i].def.building.bed_humanlike)
                                {
                                    beds.RemoveAt(i);
                                }
                            }
                        } else if (this.Pawn.RaceProps.Humanlike && this.Pawn.DevelopmentalStage <= DevelopmentalStage.Baby) {
                            for (int i = beds.Count - 1; i >= 0; i--)
                            {
                                if (!beds[i].ForHumanBabies)
                                {
                                    beds.RemoveAt(i);
                                }
                            }
                        } else {
                            for (int i = beds.Count - 1; i >= 0; i--)
                            {
                                if (!beds[i].def.building.bed_humanlike || beds[i].ForHumanBabies)
                                {
                                    beds.RemoveAt(i);
                                }
                            }
                        }
                        if (beds.Count > 0)
                        {
                            /*effect is dependent on relation to caster: same/allied/neutral/null-faction should go to free-people beds, hostile-faction to prison
                             animal beds can't be prisoner or slave beds, so animals default to free-people*/
                            if (ModsConfig.IdeologyActive && this.Pawn.IsSlave && this.Pawn.Faction == this.faction)
                            {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (!beds[i].ForSlaves)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            } else if (this.Pawn.RaceProps.Animal) {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (beds[i].ForPrisoners || beds[i].ForSlaves)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            } else if (this.Pawn.IsPrisoner && this.Pawn.guest.HostFaction == this.faction) {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (!beds[i].ForPrisoners)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            } else if (this.Pawn.Faction != null) {
                                if (this.Pawn.Faction.AllyOrNeutralTo(this.faction))
                                {
                                    for (int i = beds.Count - 1; i >= 0; i--)
                                    {
                                        if (beds[i].ForPrisoners || beds[i].ForSlaves)
                                        {
                                            beds.RemoveAt(i);
                                        }
                                    }
                                } else if (map.ParentFaction != null && (map.ParentFaction == this.faction || map.ParentFaction.AllyOrNeutralTo(this.faction))) {
                                    for (int i = beds.Count - 1; i >= 0; i--)
                                    {
                                        if (!beds[i].ForPrisoners)
                                        {
                                            beds.RemoveAt(i);
                                        }
                                    }
                                } else {
                                    beds.Clear();
                                }
                            } else {
                                for (int i = beds.Count - 1; i >= 0; i--)
                                {
                                    if (!beds[i].ForPrisoners)
                                    {
                                        beds.RemoveAt(i);
                                    }
                                }
                            }
                            if (beds.Count > 0)
                            {
                                //preferentially we will only use beds actually owned by the caster's faction
                                if (this.faction != null)
                                {
                                    bool anyAlliedBeds = false;
                                    foreach (Building_Bed b in beds)
                                    {
                                        if (b.Faction != null && b.Faction == this.faction)
                                        {
                                            anyAlliedBeds = true;
                                            break;
                                        }
                                    }
                                    if (anyAlliedBeds)
                                    {
                                        for (int i = beds.Count - 1; i >= 0; i--)
                                        {
                                            if (beds[i].Faction == null || beds[i].Faction != this.faction)
                                            {
                                                beds.RemoveAt(i);
                                            }
                                        }
                                    }
                                }
                                if (beds.Count > 0)
                                {
                                    //now we prioritize medical beds
                                    List<Building_Bed> medicalBeds = new List<Building_Bed>();
                                    foreach (Building_Bed b in beds)
                                    {
                                        if (b.Medical)
                                        {
                                            medicalBeds.Add(b);
                                        }
                                    }
                                    if (medicalBeds.Count > 0)
                                    {
                                        this.GoToBedInner(medicalBeds);
                                    } else {
                                        this.GoToBedInner(beds);
                                    }
                                    anyBed = true;
                                } else {
                                    anyBed = false;
                                }
                            }
                        }
                    }
                }
            }
        }
        private void GoToBedInner(List<Building_Bed> beds)
        {
            beds.SortBy((Building_Bed m) => this.Pawn.Position.DistanceToSquared(m.Position));
            if (!this.Pawn.Destroyed)
            {
                PawnUtility.ForceEjectFromContainer(this.Pawn);
                HVPUtility.TetherSkipBack(this.Pawn, beds[0], this.Props.fleckEntry, this.Props.soundEntry, this.Props.fleckExit1, this.Props.fleckExit2, this.Props.soundExit, this.Props.stunTicks, false);
                if (RestUtility.CanUseBedNow(beds[0], this.Pawn, false, false, null) && beds[0].AnyUnoccupiedSleepingSlot && (beds[0].Medical || beds[0].AnyUnownedSleepingSlot || beds[0].IsOwner(this.Pawn)))
                {
                    this.Pawn.jobs.Notify_TuckedIntoBed(beds[0]);
                    this.Pawn.mindState.Notify_TuckedIntoBed();
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look<Faction>(ref this.faction, "faction", false);
        }
        public Faction faction;
    }
    /*Word of Sterility
     * unaffectedAddictionsOrDiseases: any hediff of the Hediff_Addiction class, or any hediff that makesSickThought, is eligible for destruction by Word of Sterility, UNLESS it is on this blacklist.
     * otherAffectedHediffs: a whitelist of destroyable hediffs that aren't addictions or sicknesses. If a hediff is here and in unaffectedAddictionsOrDiseases, it is eligible for destruction.
     * addsHediff: ewisott
     * goodwillImpactOnIncapacitating: there are situations where you would want to help out a pawn by using Word of Sterility on them, but they belong to a faction you could lose goodwill with if you used an ability with
     *   goodwillImpact on one of its members. This is LIKE goodwillImpact, except it only triggers if Word of Sterility was used to down the target - because that could be construed as a hostile act (and would be very
     *   cheeseable if it didn't have a goodwill penalty), but using it to save the life of a downed pawn should be ok*/
    public class CompProperties_AbilitySterilize : CompProperties_AbilitySpawn
    {
        public CompProperties_AbilitySterilize()
        {
            this.compClass = typeof(CompAbilityEffect_Sterilize);
        }
        public List<HediffDef> unaffectedAddictionsOrDiseases;
        public List<HediffDef> otherAffectedHediffs;
        public HediffDef addsHediff;
        public int goodwillImpactOnIncapacitating;
    }
    public class CompAbilityEffect_Sterilize : CompAbilityEffect
    {
        public new CompProperties_AbilitySterilize Props
        {
            get
            {
                return (CompProperties_AbilitySterilize)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null && target.Thing is Pawn p)
            {
                bool alreadyDowned = p.Downed;
                List<Hediff> curableHediffs = CompAbilityEffect_Sterilize.SterilizableHediffs(this.Props, p);
                if (curableHediffs.Count > 0)
                {
                    Hediff h = curableHediffs.RandomElement();
                    BodyPartRecord bpr = h.Part ?? null;
                    if (PawnUtility.ShouldSendNotificationAbout(this.parent.pawn))
                    {
                        Messages.Message("HVP_RemovedHediff".Translate(h.LabelBase, p.Name.ToStringShort), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.PositiveEvent, false);
                    }
                    p.health.RemoveHediff(h);
                    if (this.Props.addsHediff != null)
                    {
                        p.health.AddHediff(this.Props.addsHediff, bpr ?? null);
                        if (!alreadyDowned)
                        {
                            if (p != null && !p.IsSlaveOfColony)
                            {
                                Faction casterFaction = this.parent.pawn.Faction;
                                Faction homeFaction = p.HomeFaction;
                                if (this.Props.goodwillImpactOnIncapacitating != 0 && casterFaction == Faction.OfPlayer && homeFaction != null && !homeFaction.HostileTo(casterFaction) && (this.Props.applyGoodwillImpactToLodgers || !p.IsQuestLodger()) && !p.IsQuestHelper())
                                {
                                    Faction.OfPlayer.TryAffectGoodwillWith(homeFaction, this.Props.goodwillImpactOnIncapacitating, true, true, HistoryEventDefOf.UsedHarmfulAbility, null);
                                }
                            }
                        }
                    }
                }
            }
        }
        public static List<Hediff> SterilizableHediffs(CompProperties_AbilitySterilize cpas, Pawn p)
        {
            List<Hediff> curableHediffs = new List<Hediff>();
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (cpas.otherAffectedHediffs.Contains(h.def) || ((h.def.makesSickThought || h is Hediff_Addiction) && !cpas.unaffectedAddictionsOrDiseases.Contains(h.def)))
                {
                    curableHediffs.Add(h);
                }
            }
            return curableHediffs;
        }
    }
    /*Word of Melding can't set the first target as the second target. It can't first-target children, or anyone already unconscious.
     * This also handles the on-cursor text when selecting targets, and handles interactions with pawns loaned from other factions or pawns for whom being arrested has implications for a quest.*/
    public class CompAbilityEffect_Melding : CompAbilityEffect_GiveHediffPaired
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.Thing != null && target.Thing is Pawn p && !p.IsPrisoner && !p.IsSlave)
            {
                if (p.Faction != null)
                {
                    if (p.HomeFaction != Faction.OfPlayer)
                    {
                        p.SetFaction(p.HomeFaction);
                    }
                }
                QuestUtility.SendQuestTargetSignals(p.questTags, "Arrested", p.Named("SUBJECT"));
                if (p.Faction != null)
                {
                    QuestUtility.SendQuestTargetSignals(p.Faction.questTags, "FactionMemberArrested", p.Faction.Named("FACTION"));
                }
            }
            base.Apply(target, dest);
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return this.Valid(target, false);
        }
        public override bool CanHitTarget(LocalTargetInfo target)
        {
            return base.CanHitTarget(target) && target != this.selectedTarget;
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return base.Valid(target, throwMessages) && target.Thing != null && target.Thing is Pawn p && p.DevelopmentalStage.Adult() && p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) > 0.3f;
        }
        public override TargetingParameters targetParams
        {
            get
            {
                return new TargetingParameters
                {
                    canTargetSelf = true,
                    canTargetBuildings = false,
                    canTargetAnimals = false,
                    canTargetMechs = false
                };
            }
        }
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (this.selectedTarget.IsValid)
            {
                return "HVP_MeldInto".Translate();
            }
            return "HVP_Meld".Translate();
        }
    }
}
