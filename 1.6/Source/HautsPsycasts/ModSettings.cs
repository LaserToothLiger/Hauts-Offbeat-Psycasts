using UnityEngine;
using Verse;

namespace HautsPsycasts
{
    /*Better Flashstorm: it's bad unless you make a roof trap. Turn this on, and it strikes way more often, occasionally with targeted bolts that do more damage and have armor pen.
     * the other two mod settings are intended to compensate for HOP's dilution of the psycast pool. You can make traders that sell psytrainers sell more psytrainers, or just get bonus random psycasts on level-up.*/
    public class HVP_Settings : ModSettings
    {
        public float psytrainersForSaleMultiplier = 1f;
        public float psycastsLearnedPerLevel = 1f;
        public bool buffFlashstorm = true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref psytrainersForSaleMultiplier, "psytrainersForSaleMultiplier", 1f);
            Scribe_Values.Look(ref psycastsLearnedPerLevel, "psycastsLearnedPerLevel", 1f);
            Scribe_Values.Look(ref buffFlashstorm, "buffFlashstorm", true);
            base.ExposeData();
        }
    }
    public class HVP_Mod : Mod
    {
        public HVP_Mod(ModContentPack content) : base(content)
        {
            HVP_Mod.settings = GetSettings<HVP_Settings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            //number of psytrainers for sale multiplier
            float x = inRect.xMin, y = inRect.yMin + 25, halfWidth = inRect.width * 0.5f;
            displayPsytrainerSaleMultiplier = ((int)settings.psytrainersForSaleMultiplier).ToString();
            float origR = settings.psytrainersForSaleMultiplier;
            Rect psytrainerSaleRect = new Rect(x + 10, y, halfWidth - 15, 32);
            settings.psytrainersForSaleMultiplier = Widgets.HorizontalSlider(psytrainerSaleRect, settings.psytrainersForSaleMultiplier, 1f, 3f, true, "HVP_SettingPSM".Translate(), "1x", "3x", 1f);
            TooltipHandler.TipRegion(psytrainerSaleRect.LeftPart(1f), "HVP_TooltipPsytrainerSaleMulti".Translate());
            if (origR != settings.psytrainersForSaleMultiplier)
            {
                displayPsytrainerSaleMultiplier = ((int)settings.psytrainersForSaleMultiplier).ToString() + "x";
            }
            y += 32;
            string origStringR = displayPsytrainerSaleMultiplier;
            displayPsytrainerSaleMultiplier = Widgets.TextField(new Rect(x + 10, y, 50, 32), displayPsytrainerSaleMultiplier);
            if (!displayPsytrainerSaleMultiplier.Equals(origStringR))
            {
                this.ParseInput(displayPsytrainerSaleMultiplier, settings.psytrainersForSaleMultiplier, out settings.psytrainersForSaleMultiplier);
            }
            y -= 32;
            //number of psycasts learned per level
            displayPsycastsLearnedPerLevel = ((int)settings.psycastsLearnedPerLevel).ToString();
            float origL = settings.psycastsLearnedPerLevel;
            Rect psycastsLearnedRect = new Rect(x + 5 + halfWidth, y, halfWidth - 15, 32);
            settings.psycastsLearnedPerLevel = Widgets.HorizontalSlider(psycastsLearnedRect, settings.psycastsLearnedPerLevel, 1f, 4f, true, "HVP_SettingPLPL".Translate(), "1", "4", 1f);
            TooltipHandler.TipRegion(psycastsLearnedRect.LeftPart(1f), "HVP_TooltipSettingPLPL".Translate());
            if (origL != settings.psycastsLearnedPerLevel)
            {
                displayPsycastsLearnedPerLevel = ((int)settings.psycastsLearnedPerLevel).ToString();
            }
            y += 32;
            string origStringL = displayPsycastsLearnedPerLevel;
            displayPsycastsLearnedPerLevel = Widgets.TextField(new Rect(x + 5 + halfWidth, y, 50, 32), displayPsycastsLearnedPerLevel);
            if (!displayPsycastsLearnedPerLevel.Equals(origStringL))
            {
                this.ParseInput(displayPsycastsLearnedPerLevel, settings.psycastsLearnedPerLevel, out settings.psycastsLearnedPerLevel);
            }
            //buff flashstorm? YES DEFINITELY ABSOLUTELY
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("HVP_SettingBuffFlashstorm".Translate(), ref settings.buffFlashstorm, "HVP_TooltipBuffFlashstorm".Translate());
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
        private void ParseInput(string buffer, float origValue, out float newValue)
        {
            if (!float.TryParse(buffer, out newValue))
                newValue = origValue;
            if (newValue < 0)
                newValue = origValue;
        }
        public override string SettingsCategory()
        {
            return "Hauts' Offbeat Psycasts";
        }
        public static HVP_Settings settings;
        public string displayPsytrainerSaleMultiplier;
        public string displayPsycastsLearnedPerLevel;
    }
}
