using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using HarmonyLib;
using RimWorld.Planet;
using System.Reflection;

namespace HautsPsycasts
{
    [StaticConstructorOnStartup]
    public class HautsPsycasts
    {
            private static readonly Type patchType = typeof(HautsPsycasts);
        static HautsPsycasts()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hautspsycasts");
            harmony.Patch(AccessTools.Method(typeof(Caravan), nameof(Caravan.GetGizmos)),
                          postfix: new HarmonyMethod(patchType, nameof(HVPGetGizmosPostfix)));
            harmony.Patch(AccessTools.Method(typeof(ThingSetMaker), nameof(ThingSetMaker.Generate), new[] { typeof(ThingSetMakerParams) }),
                          postfix: new HarmonyMethod(patchType, nameof(HVP_ThingSetMaker_GeneratePostfix)));
            harmony.Patch(AccessTools.Method(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel)),
                          postfix: new HarmonyMethod(patchType, nameof(HVP_TryGiveAbilityOfLevelPostfix)));
            Log.Message("HVP_Initialize".Translate().CapitalizeFirst());
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        //adds the "retrieve everything" button(s) for any of the caravan members' Vault Skips to its gizmos
        public static IEnumerable<Gizmo> HVPGetGizmosPostfix(IEnumerable<Gizmo> __result, Caravan __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }
            foreach (Pawn p in __instance.PawnsListForReading)
            {
                p.health.hediffSet.TryGetHediff(HVPDefOf.HVP_Hammerspace, out Hediff hediff);
                if (hediff != null)
                {
                    HediffComp_Hammerspace hchs = hediff.TryGetComp<HediffComp_Hammerspace>();
                    if (hchs != null)
                    {
                        foreach (Gizmo giz2 in hchs.CompGetGizmos())
                        {
                            yield return giz2;
                        }
                    }
                }
            }
        }
        //handles the mod setting for selling extra psytrainers
        public static void HVP_ThingSetMaker_GeneratePostfix(ref List<Thing> __result, ThingSetMakerParams parms)
        {
            if (parms.traderDef != null && parms.traderDef.stockGenerators != null)
            {
                int numPsytrainers = 0;
                List<ThingDef> generatedDefs = new List<ThingDef>();
                foreach (Thing t in __result)
                {
                    if (t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.NeurotrainersPsycast))
                    {
                        numPsytrainers++;
                        generatedDefs.Add(t.def);
                    }
                }
                numPsytrainers *= (int)HVP_Mod.settings.psytrainersForSaleMultiplier - 1;
                if (numPsytrainers > 0)
                {
                    IEnumerable<ThingDef> descendantThingDefs = ThingCategoryDefOf.NeurotrainersPsycast.DescendantThingDefs;
                    while (numPsytrainers > 0)
                    {
                        ThingDef chosenThingDef;
                        if (!descendantThingDefs.Where((ThingDef t) => t.tradeability.TraderCanSell() && !generatedDefs.Contains(t)).TryRandomElement(out chosenThingDef))
                        {
                            break;
                        }
                        Thing toAdd = StockGeneratorUtility.TryMakeForStock(chosenThingDef, 1, parms.makingFaction).FirstOrDefault();
                        if (toAdd != null)
                        {
                            __result.Add(toAdd);
                            generatedDefs.Add(toAdd.def);
                        }
                        numPsytrainers--;
                    }
                }
            }
        }
        //handles the mod setting for gaining extra random level-appropriate psycasts on gaining a new psylink level. Only works up to level 6 to prevent you from getting extra level 7+ psycasts with Cooler Psycasts
        public static void HVP_TryGiveAbilityOfLevelPostfix(Hediff_Psylink __instance, int abilityLevel)
        {
            if (abilityLevel <= 6)
            {
                int psycastsToAward = (int)HVP_Mod.settings.psycastsLearnedPerLevel - 1;
                if (psycastsToAward > 0 && __instance.pawn.abilities != null)
                {
                    List<RimWorld.AbilityDef> psycastsOfLevel = new List<RimWorld.AbilityDef>();
                    foreach (RimWorld.AbilityDef a in DefDatabase<RimWorld.AbilityDef>.AllDefs)
                    {
                        if (a.IsPsycast && a.level == abilityLevel && __instance.pawn.abilities.GetAbility(a) == null)
                        {
                            psycastsOfLevel.Add(a);
                        }
                    }
                    while (psycastsToAward > 0 && psycastsOfLevel.Count > 0)
                    {
                        RimWorld.AbilityDef abilityDef = psycastsOfLevel.RandomElement();
                        __instance.pawn.abilities.GainAbility(abilityDef);
                        psycastsOfLevel.Remove(abilityDef);
                        psycastsToAward--;
                    }
                }
            }
        }
    }
}
