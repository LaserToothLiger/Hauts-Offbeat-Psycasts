using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HautsPsycasts
{
    public class HVPUtility
    {
        /*make a lightning bolt at the target point with the specified amount of damage and armor pen. Damage is attributed to the "caster" pawn
         * wow the joke behind this method's name was a lot funnier back when that PoE 2 pre-EA video came out showing off the crossbow for the first time*/
        public static void ArmourPiercingBolt(IntVec3 strikeLoc, Map map, Thing caster, int damage, float armorPenetration)
        {
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_ArmourPiercingBolt(map,strikeLoc));
            GenExplosion.DoExplosion(strikeLoc, map, 1.9f, DamageDefOf.Flame, caster, damage, armorPenetration, null, null, null, null, null, 0f, 1, null, null, 255, false, null, 0f, 1, 0f, false, null, null, null, true, 1f, 0f, true, null, 1f, null, null);
        }
        /*spawns the specified pawn at the specified "anchor" Thing's position. They don't even have to be on the same map, but the anchor must be spawned (otherwise the only effect of TetherSkipBack is to destroy the anchor).
         * Used by both the actual Tetherskip, and Word of Safety's teleportation effect*/
        public static void TetherSpawnPawn(Pawn p, Thing anchor)
        {
            GenSpawn.Spawn(p, anchor.Position, anchor.Map, Rot4.South, WipeMode.Vanish, false, false);
        }
        public static void TetherSkipBack(Pawn p, Thing anchor, FleckDef fleckEntry, SoundDef soundEntry, FleckDef fleckExit1, FleckDef fleckExit2, SoundDef soundExit, IntRange stunTicks, bool destroy = false, bool evenIfDespawned = true)
        {
            if (p != null && !p.Dead && !p.Destroyed && anchor != null && anchor.Spawned)
            {
                Faction pFaction = p.Faction ?? null;
                if (!p.Spawned)
                {
                    if (!evenIfDespawned)
                    {
                        return;
                    }
                    else
                    {
                        Caravan c = p.GetCaravan();
                        if (c != null)
                        {
                            bool shouldArriveAtAnchor = true;
                            foreach (Pawn caravaneer in c.pawns)
                            {
                                if (caravaneer != p && caravaneer.RaceProps.intelligence == Intelligence.Humanlike && caravaneer.Faction == c.Faction && !caravaneer.Downed && !caravaneer.InMentalState)
                                {
                                    shouldArriveAtAnchor = false;
                                    break;
                                }
                            }
                            if (shouldArriveAtAnchor)
                            {
                                List<Pawn> pawnsToSpawn = new List<Pawn>();
                                foreach (Pawn caravaneer in c.pawns)
                                {
                                    pawnsToSpawn.Add(caravaneer);
                                }
                                foreach (Pawn p2 in pawnsToSpawn)
                                {
                                    HVPUtility.TetherSpawnPawn(p2, anchor);
                                }
                                c.RemoveAllPawns();
                                if (c.pawns.Count == 0 && !c.Destroyed)
                                {
                                    c.Destroy();
                                }
                            }
                            else
                            {
                                HVPUtility.TetherSpawnPawn(p, anchor);
                                c.RemovePawn(p);
                                if (c.pawns.Count == 0 && !c.Destroyed)
                                {
                                    c.Destroy();
                                }
                            }
                        }
                        else
                        {
                            HVPUtility.TetherSpawnPawn(p, anchor);
                        }
                    }
                }
                if (fleckEntry != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(p.Position.ToVector3Shifted(), p.Map, fleckEntry, 1f);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    p.Map.flecks.CreateFleck(dataStatic);
                }
                if (soundEntry != null)
                {
                    soundEntry.PlayOneShot(new TargetInfo(p.Position, p.Map, false));
                }
                if (p.Map == null)
                {
                    HVPUtility.TetherSpawnPawn(p, anchor);
                }
                else if (p.Map != anchor.Map)
                {
                    p.ExitMap(false, p.Rotation);
                    HVPUtility.TetherSpawnPawn(p, anchor);
                }
                p.Position = anchor.Position;
                if (fleckExit1 != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(anchor.Position.ToVector3Shifted(), anchor.Map, fleckExit1, 1f);
                    dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                    dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                    anchor.Map.flecks.CreateFleck(dataStatic);
                    if (fleckExit2 != null)
                    {
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(anchor.Position.ToVector3Shifted(), anchor.Map, fleckExit2, 1f);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        anchor.Map.flecks.CreateFleck(dataStatic2);
                    }
                }
                if (soundExit != null)
                {
                    soundExit.PlayOneShot(new TargetInfo(p.Position, p.Map, false));
                }
                if ((p.Faction == Faction.OfPlayer || p.IsPlayerControlled) && p.Position.Fogged(p.Map))
                {
                    FloodFillerFog.FloodUnfog(p.Position, p.Map);
                }
                p.stances.stunner.StunFor(stunTicks.RandomInRange, null, false, false, false);
                p.Notify_Teleported(true, true);
                if (p.Faction != pFaction)
                {
                    p.SetFaction(pFaction);
                }
            }
            if (destroy && !anchor.Destroyed)
            {
                anchor.Destroy();
            }
        }
        //gizmos granted to the controller of a Link psycast, Tetherskip, or Vault Skip only appear if this returns true for the pawn. Basically, are you a player-controlled psycaster? Dev mode bypasses need for player control
        public static bool ShouldShowExtraPsycastGizmo(Pawn p)
        {
            return (p.IsPlayerControlled || (p.IsInCaravan() && p.IsColonist) || DebugSettings.ShowDevGizmos) && p.Awake() && !p.DeadOrDowned && !p.Suspended && !p.InMentalState && p.HasPsylink && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon;
        }
    }
}
