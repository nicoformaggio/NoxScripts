using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

using NoxCore.Controllers;
using NoxCore.Fittings.Devices;
using NoxCore.Fittings.Weapons;
using NoxCore.Helm;
using NoxCore.Managers;
using NoxCore.Placeables;
using NoxCore.Placeables.Ships;
using NoxCore.Utilities;
using NoxCore.Rules;

namespace Formaggio.Controllers
{
	[RequireComponent(typeof(BasicThreatEvaluator))]
	public class NicoCarrierAI : AIStateController 
	{
		public List<Vector2> waypoints = new List<Vector2>();
        public int currentWaypoint = 0;
        public bool forceWaypointNavigation;

        protected SeekBehaviour seekBehaviour;
        protected OrbitBehaviour orbitBehaviour;
        protected AvoidBehaviour avoidBehaviour;

		BasicThreatEvaluator threatSys;

        protected List<Structure> squad;

        public override void boot(Structure structure, HelmController helm = null)
        {
            base.boot(structure, helm);

            aiActions.Add("SEARCH", searchAction);
            aiActions.Add("COMBAT", combatAction);

            state = "SEARCH";

            // note: leave this as false if you want the ship to orbit its target
            forceWaypointNavigation = false;

            waypoints.Add(new Vector2(-250, 0));
            waypoints.Add(new Vector2(0, 250));
            waypoints.Add(new Vector2(250, 0));
            waypoints.Add(new Vector2(0, -250));

            seekBehaviour = Helm.getBehaviourByName("SEEK") as SeekBehaviour;
            orbitBehaviour = Helm.getBehaviourByName("ORBIT") as OrbitBehaviour;
            avoidBehaviour = Helm.getBehaviourByName("AVOID") as AvoidBehaviour;

            if (orbitBehaviour != null)
            {
                orbitBehaviour.OrbitRange = structure.scanner.getRadius();
            }

            if (avoidBehaviour != null)
            {
                // note: could add additional layers to the following.
                // E.G. avoidLayerMask = structure.Gamemode.getCollisionMask("ship").GetValueOrDefault() ^ (1 << LayerMask.NameToLayer("NavPoint"));
                LayerMask avoidLayerMask = structure.Gamemode.getCollisionMask("SHIP").GetValueOrDefault();
                avoidBehaviour.setCollidables(avoidLayerMask);
            }

            foreach(FireGroup fireGroup in structure.FireGroupManager.FireGroups)
            {
                foreach(Weapon weapon in fireGroup.getAllWeapons())
                {
                    ProjectileLauncher launcher = weapon as ProjectileLauncher;

                    if (launcher != null)
                    {
                        launcher.WeaponFired += LauncherFired;
                    }
                }
            }

			GameEventManager.MatchIsWaitingToStart += AI_MatchIsWaitingToStart;

            threatSys = GetComponent<BasicThreatEvaluator>();

            booted = true;
        }

        public void LauncherFired(object sender, WeaponFiredEventArgs args)
        {
            //Gui.setMessage(args.weaponFired + " has fired!");
        }

        protected virtual Vector2 setHelmDestination()
        {
            Vector2 nextPoint = waypoints[currentWaypoint];

            currentWaypoint++;

            if (currentWaypoint == waypoints.Count)
            {
                currentWaypoint = 0;
            }

            return nextPoint;
        }

        public virtual string searchAction()
        {
            if (Helm != null)
            {
                if (structure.scanner.isActiveOn() == true)
                {
                    if (structure.scanner.getEnemiesInRange().Count == 0)
                    {
                        #region search pattern
                        if (seekBehaviour != null && seekBehaviour.Active == false)
                        {
                            seekBehaviour.enableExclusively();
                            currentWaypoint = 0;
                        }

                        if (avoidBehaviour != null && avoidBehaviour.Active == false)
                        {
                            avoidBehaviour.enable();
                        }

                        // run search pattern
                        if (Helm.destination == null)
                        {
                            Helm.destination = setHelmDestination();
                        }

                        // don't allow a destination outside of the arena
                        if (ArenaRules.radius > 0)
                        {
                            if (Helm.destination.GetValueOrDefault().magnitude > ArenaRules.radius)
                            {
                                Helm.destination = null;
                            }
                        }

                        // draw a line to the destination
                        if (Helm.destination != null && Cam.followTarget != null && Cam.followTarget.gameObject == Helm.ShipStructure.gameObject)
                        {
                            Debug.DrawLine(structure.transform.position, Helm.destination.GetValueOrDefault(), Color.blue, Time.deltaTime, true);
                        }

                        return "SEARCH";
                        #endregion
                    }
                    else
                    {
                        return "COMBAT";
                    }
                }
                else
                {
                    return "SEARCH";
                }
            }
            else
            {
                return null;
            }
        }

        protected override void newScannerData(Scanner sender)
        {
            List<GameObject> enemiesInRange = structure.scanner.getEnemiesInRange();
            if (state == "COMBAT" && enemiesInRange.Count > 0)
            {
                if (orbitBehaviour != null)
                {
                    if (orbitBehaviour.Active == false)
                    {
                        orbitBehaviour.enableExclusively();
                    }
                }

                // get sorted threat ratios for all enemy ships and structures in range
                List<Tuple<GameObject, float>> threats = threatSys.calculateThreatRatios(structure, enemiesInRange);

                // tell all fire groups to acquire the first target's hull (hence null for 2nd parameter)
                foreach (FireGroup fireGroup in structure.FireGroupManager.FireGroups)
                {
                    fireGroup.setTarget(threats[0]._1);
                }

                // use the first target as the ship/structure to orbit around
                orbitBehaviour.OrbitObject = threats[0]._1.transform;

                // use the first weapon's maximum range to determie a suitable orbit range
                if (structure.weapons.Count > 0)
                {
                    orbitBehaviour.OrbitRange = structure.weapons[0].MaxRange - 1;
                }
            }
        }

        public virtual string combatAction()
        {
            if (Helm != null)
            {
                if (structure.scanner.isActiveOn() == true)
                {
                    #region target and orbit enemy
                    List<GameObject> enemiesInRange = structure.scanner.getEnemiesInRange();

                    if (enemiesInRange.Count > 0)
                    {
                        // new scanner data?
                        return "COMBAT";
                    }
                    else
                    {
                        foreach (Weapon weap in structure.weapons)
                        {
                            TargetableWeapon tWeap = (TargetableWeapon)weap;

                            if (tWeap != null)
                            {
                                tWeap.unacquireTarget();
                            }
                        }

                        return "SEARCH";
                    }
                    #endregion
                }
                else
                {
                    return "SEARCH";
                }
            }
            else
            {
                return null;
            }
        }

		protected void AI_MatchIsWaitingToStart(object sender)
        {
            Faction faction = FactionManager.Instance.findFaction(structure.FactionName);

            List<Ship> ships = faction.fleetManager.getAllShips();

            squad = ships.Cast<Structure>().ToList();

            foreach (Ship ship in squad)
            {
                if (ship.Classification == ShipClassification.FIGHTER || ship.Classification == ShipClassification.BOMBER)
                {
                    ILand landingController = ship.Controller as ILand;

                    if (landingController != null)
                    {
                        landingController.setHangerStructure(structure);
                    }
                }
            }
        }
	}
}