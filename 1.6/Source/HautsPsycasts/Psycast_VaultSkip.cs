using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    /*Vault Skip puts all haulable items within radius of the pawn (hardcoded as scaling with the skipcast range factor stat) into the thing holder of the specific hediff it creates.
     * If such a hediff already exists, then it just adds those items to its existing thing holder.
     * Naturally, this means hediffDef should be a hediff with the HediffComp_Hammerspace comp, otherwise all this will do is minify buildings in its radius.*/
    public class CompAbilityEffect_VaultSkip : CompAbilityEffect_WithDuration
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
            List<Thing> thingsToTake = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.parent.def.EffectRadius * this.parent.pawn.GetStatValue(HautsDefOf.Hauts_SkipcastRangeFactor), true))
            {
                if (t.def.EverHaulable)
                {
                    t.TryMakeMinified();
                    thingsToTake.Add(t);
                }
            }
            if (this.Props.ignoreSelf && target.Pawn == this.parent.pawn)
            {
                return;
            }
            if (!this.Props.onlyApplyToSelf && this.Props.applyToTarget)
            {
                this.ApplyInner(target.Pawn, this.parent.pawn, thingsToTake);
            }
            if (this.Props.applyToSelf || this.Props.onlyApplyToSelf)
            {
                this.ApplyInner(this.parent.pawn, target.Pawn, thingsToTake);
            }
        }
        protected void ApplyInner(Pawn target, Pawn other, List<Thing> thingsToTake)
        {
            if (target != null)
            {
                Hediff firstHediffOfDef = target.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef, false);
                if (firstHediffOfDef != null)
                {
                    this.FillStorage(firstHediffOfDef, thingsToTake);
                    return;
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
                HediffComp_Link hediffComp_Link = hediff.TryGetComp<HediffComp_Link>();
                if (hediffComp_Link != null)
                {
                    hediffComp_Link.other = other;
                    hediffComp_Link.drawConnection = (target == this.parent.pawn);
                }
                target.health.AddHediff(hediff, null, null, null);
                this.FillStorage(hediff, thingsToTake);
            }
        }
        private void FillStorage(Hediff hediff, List<Thing> thingsToTake)
        {
            HediffComp_Hammerspace hchs = hediff.TryGetComp<HediffComp_Hammerspace>();
            if (hchs != null)
            {
                hchs.radius = this.parent.def.EffectRadius;
                if (hchs.innerContainer == null)
                {
                    hchs.innerContainer = new ThingOwner<Thing>(hediff.pawn, false, LookMode.Deep);
                }
                foreach (Thing t in thingsToTake)
                {
                    if (t.holdingOwner != null)
                    {
                        t.holdingOwner.Remove(t);
                    }
                    hchs.innerContainer.TryAdd(t, t.stackCount);
                }
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return this.parent.pawn.Faction != Faction.OfPlayer && target.Pawn != null;
        }
    }
    /*The hediff that stores all the items is an IThingHolder.
     * eachItemLostOnDeathChance: when the pawn dies, the things stored inside this hediff are expelled around the pawn (or into its caravan's inventory). However, each such thing rolls a random chance to be destroyed instead
     * lostChanceIfNonPlayer: in cases where non-colonist pawns can use this ability (especially hostile ones), you would probably like to be able to loot what they sucked up! They use this chance,
     *   instead of eachItemLostOnDeathChance. This is set lower in the XML so that you have a higher chance of recovering their goods
     * severityPerThing: the hediff incurs a malus to psyfocus regen that scales with its severity (see its stage data in the XML). Its severity = [severityPerThing*number of discrete things in inventory].
     *   Stacks count as one thing
     * It also grants a gizmo to destroy the hediff, placing all its stored items around the pawn (or in its caravan's inventory; a Harmony patch adds the gizmo to caravans). This does not roll eachItemLostOnDeathChance.
     * dumpIcon: the icon of the gizmo that does that
     * fleckDump1, fleckDump2, and soundOnDump all play on the pawn's location if this is done while the pawn is spawned*/
    public class HediffCompProperties_Hammerspace : HediffCompProperties
    {
        public HediffCompProperties_Hammerspace()
        {
            this.compClass = typeof(HediffComp_Hammerspace);
        }
        public string dumpIcon;
        public float eachItemLostOnDeathChance;
        public float lostChanceIfNonPlayer;
        public float severityPerThing;
        public int displayLimit = 10;
        public FleckDef fleckDump1;
        public FleckDef fleckDump2;
        public SoundDef soundOnDump;
    }
    public class HediffComp_Hammerspace : HediffComp, IThingHolder
    {
        public HediffCompProperties_Hammerspace Props
        {
            get
            {
                return (HediffCompProperties_Hammerspace)this.props;
            }
        }
        public IThingHolder ParentHolder
        {
            get
            {
                return this.Pawn;
            }
        }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }
        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }
        public override string CompTipStringExtra
        {
            get
            {
                string storedStrings = "HVP_StoringTheFollowing".Translate();
                if (this.innerContainer != null)
                {
                    int displayLimit = this.Props.displayLimit;
                    foreach (Thing t in this.innerContainer)
                    {
                        storedStrings += "\n" + t.Label;
                        displayLimit--;
                        if (displayLimit <= 0)
                        {
                            break;
                        }
                    }
                    if (this.innerContainer.Count > this.Props.displayLimit)
                    {
                        storedStrings += "\n" + "HVP_AndYetMoreStored".Translate(this.innerContainer.Count - this.Props.displayLimit);
                    }
                }
                return base.CompTipStringExtra + storedStrings;
            }
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.dumpIcon, true);
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.innerContainer = new ThingOwner<Thing>(this);
        }
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (this.uiIcon == null)
            {
                this.uiIcon = ContentFinder<Texture2D>.Get(this.Props.dumpIcon, true);
            }
            if (this.innerContainer != null && HVPUtility.ShouldShowExtraPsycastGizmo(this.Pawn))
            {
                Command_Action cmdRecall = new Command_Action
                {
                    defaultLabel = "HVP_StorageDumpLabel".Translate(),
                    defaultDesc = "HVP_StorageDumpText".Translate(),
                    icon = this.uiIcon,
                    action = delegate ()
                    {
                        this.StorageDump(0f);
                    }
                };
                yield return cmdRecall;
            }
            yield break;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.innerContainer != null)
            {
                this.parent.Severity = this.innerContainer.Count * this.Props.severityPerThing;
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (this.innerContainer.Count > 0)
            {
                this.StorageDump(this.Pawn.IsColonist ? this.Props.eachItemLostOnDeathChance : this.Props.lostChanceIfNonPlayer, this.Pawn.Dead);
            }
        }
        private void StorageDump(float chanceToLose, bool sendMessage = false)
        {
            bool lostAnItem = false;
            if (this.Pawn.SpawnedOrAnyParentSpawned)
            {
                for (int i = this.innerContainer.Count - 1; i >= 0; i--)
                {
                    if (!Rand.Chance(chanceToLose) || !this.innerContainer[i].def.destroyable)
                    {
                        this.innerContainer.TryDrop(this.innerContainer[i], this.Pawn.PositionHeld, this.Pawn.MapHeld, ThingPlaceMode.Near, out Thing thing);
                    } else {
                        this.innerContainer[i].Destroy();
                        lostAnItem = true;
                    }
                }
            } else if (this.Pawn.IsCaravanMember()) {
                for (int i = this.innerContainer.Count - 1; i >= 0; i--)
                {
                    if (!Rand.Chance(chanceToLose) || !this.innerContainer[i].def.destroyable)
                    {
                        this.innerContainer.TryTransferToContainer(this.innerContainer[i], this.Pawn.inventory.innerContainer, this.innerContainer[i].stackCount, out Thing thing);
                    } else {
                        this.innerContainer[i].Destroy();
                        lostAnItem = true;
                    }
                }
            }
            if (PawnUtility.ShouldSendNotificationAbout(this.Pawn) && sendMessage)
            {
                string deathNotification = "HVP_VaultDeath".Translate().Formatted(this.Pawn.Named("PAWN")).AdjustedFor(this.Pawn, "PAWN", true).Resolve();
                if (lostAnItem)
                {
                    deathNotification += " " + "HVP_VaultDeathNoWarranty".Translate();
                }
                Messages.Message(deathNotification, this.Pawn, MessageTypeDefOf.NeutralEvent, false);
            }
            if (this.Pawn.SpawnedOrAnyParentSpawned)
            {
                if (this.Props.fleckDump1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(this.Pawn.PositionHeld.ToVector3Shifted(), this.Pawn.MapHeld, this.Props.fleckDump1, this.radius);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    this.Pawn.MapHeld.flecks.CreateFleck(dataStatic);
                    if (this.Props.fleckDump2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(this.Pawn.PositionHeld.ToVector3Shifted(), this.Pawn.MapHeld, this.Props.fleckDump2, this.radius);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        this.Pawn.MapHeld.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (this.Props.soundOnDump != null)
                {
                    this.Props.soundOnDump.PlayOneShot(new TargetInfo(this.Pawn.PositionHeld, this.Pawn.MapHeld, false));
                }
            }
            this.Pawn.health.RemoveHediff(this.parent);
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Deep.Look<ThingOwner<Thing>>(ref this.innerContainer, "innerContainer", new object[] { this });
            Scribe_Values.Look<float>(ref this.radius, "radius", 1f, false);
        }
        public ThingOwner<Thing> innerContainer;
        public Texture2D uiIcon;
        public float radius = 1f;
    }
}
