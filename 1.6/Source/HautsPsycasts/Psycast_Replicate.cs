using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    /*Replicate has a bunch of targeting parameters.
     * dis|allowedItemCategories: only things belonging to one or more thing categories can be targeted.
     *   Those categories (or their recursive parents) must be on the 'allowed' whitelist (which MUST be specified) and not on the 'disallowed' blacklist, although you can choose not to specify a blacklist.
     * marketValueLimitItem: any item whose market value exceeds this amount cannot be targeted. Evaluates the market value of just one unit of the item, even if it's in a stack
     * marketValueLimitStack: cannot create a stack whose sum market value exceeds this amount
     * precastTicks, fleckDefs, and scale are VFX-related stuff. In case you want to screw with that for some reason*/
    public class CompProperties_AbilityReplicate : CompProperties_AbilityEffect
    {
        public List<ThingCategoryDef> allowedItemCategories;
        public List<ThingCategoryDef> disallowedItemCategories;
        public List<FleckDef> fleckDefs;
        public int preCastTicks;
        public float scale = 1f;
        public int marketValueLimitItem;
        public int marketValueLimitStack;
    }
    public class CompAbilityEffect_Replicate : CompAbilityEffect
    {
        public new CompProperties_AbilityReplicate Props
        {
            get
            {
                return (CompProperties_AbilityReplicate)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing != null)
            {
                Thing thing = ThingMaker.MakeThing(target.Thing.def, target.Thing.Stuff ?? null);
                thing.stackCount = Math.Max(1, Math.Min(target.Thing.stackCount, (int)(this.Props.marketValueLimitStack / target.Thing.MarketValue)));
                GenSpawn.Spawn(thing, this.parent.pawn.Position, this.parent.pawn.Map, WipeMode.Vanish);
            }
            if (this.Props.preCastTicks <= 0)
            {
                SoundDef sound = this.Props.sound;
                if (sound != null)
                {
                    sound.PlayOneShot(new TargetInfo(this.parent.pawn.Position, this.parent.pawn.Map, false));
                }
                this.SpawnAllFlecks(this.parent.pawn);
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            bool fitsInCategories = false;
            if (target.Thing != null)
            {
                if (!target.Thing.def.thingCategories.NullOrEmpty() && target.Thing.def.thingCategories.ContainsAny((ThingCategoryDef tcd) => this.Props.allowedItemCategories.Contains(tcd) || tcd.Parents.ToList().ContainsAny((ThingCategoryDef tcd2) => this.Props.allowedItemCategories.Contains(tcd2))) && (this.Props.disallowedItemCategories.NullOrEmpty() || (!target.Thing.def.thingCategories.ContainsAny((ThingCategoryDef tcd) => this.Props.disallowedItemCategories.Contains(tcd) || tcd.Parents.ToList().ContainsAny((ThingCategoryDef tcd2) => this.Props.disallowedItemCategories.Contains(tcd2))))))
                {
                    fitsInCategories = true;
                }
                if (target.Thing.MarketValue > this.Props.marketValueLimitItem)
                {
                    if (throwMessages)
                    {
                        Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_ItemTooExpensive".Translate(this.Props.marketValueLimitItem), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            if (!fitsInCategories)
            {
                if (throwMessages)
                {
                    Messages.Message("CannotUseAbility".Translate(this.parent.def.label) + ": " + "HVP_WrongItemType".Translate(), target.ToTargetInfo(this.parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }
        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            if (this.Props.preCastTicks > 0)
            {
                yield return new PreCastAction
                {
                    action = delegate (LocalTargetInfo t, LocalTargetInfo d)
                    {
                        this.SpawnAllFlecks(this.parent.pawn);
                        SoundDef sound = this.Props.sound;
                        if (sound == null)
                        {
                            return;
                        }
                        sound.PlayOneShot(new TargetInfo(t.Cell, this.parent.pawn.Map, false));
                    },
                    ticksAwayFromCast = this.Props.preCastTicks
                };
            }
            yield break;
        }
        private void SpawnAllFlecks(LocalTargetInfo target)
        {
            if (!this.Props.fleckDefs.NullOrEmpty<FleckDef>())
            {
                for (int i = 0; i < this.Props.fleckDefs.Count; i++)
                {
                    this.SpawnFleck(target, this.Props.fleckDefs[i]);
                }
            }
        }
        private void SpawnFleck(LocalTargetInfo target, FleckDef def)
        {
            if (target.HasThing && target.Thing.SpawnedOrAnyParentSpawned)
            {
                FleckMaker.AttachedOverlay(target.Thing, def, Vector3.zero, this.Props.scale, -1f);
                return;
            }
            FleckMaker.Static(target.Cell, this.parent.pawn.Map, def, this.Props.scale);
        }
    }
}
