using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using NoxCore.Fittings.Devices;
using NoxCore.Fittings.Weapons;
using NoxCore.Helm;
using NoxCore.Placeables;
using NoxCore.Rules;
using NoxCore.Utilities;
using NoxCore.Controllers;

using Yvan.Fittings.Devices;

using Formaggio.Helm;

namespace Formaggio.Controllers
{
    [RequireComponent(typeof(BasicThreatEvaluator))]
    public class NicoCombatAI : AIStateController
    {
        BasicThreatEvaluator threatSys;

        //public List<Vector2> waypoints = new List<Vector2>();
        //public int currentWaypoint = 0;
        public bool forceWaypointNavigation;
        private Structure[] setTargets;
        public bool started;

        GameObject targetObject;

        protected SeekBehaviour seekBehaviour;
        protected NicoPursuitBehaviour pursuitBehaviour;

        public override void boot(Structure structure, HelmController helm = null)
        {
            base.boot(structure, helm);

            aiActions.Add("SEARCH", searchAction);
            aiActions.Add("COMBAT", combatAction);

            state = "SEARCH";

            seekBehaviour = Helm.getBehaviourByName("SEEK") as SeekBehaviour;

            pursuitBehaviour = gameObject.AddComponent<NicoPursuitBehaviour>();
            pursuitBehaviour.init(helm, "PURSUIT", 1, 1000);
            pursuitBehaviour.avoidFeelerLengths[0] = 300;
            pursuitBehaviour.avoidFeelerLengths[1] = 300;
            pursuitBehaviour.avoidFeelerLengths[2] = 300;
            Helm.addBehaviour(pursuitBehaviour);

            if (pursuitBehaviour != null && pursuitBehaviour.Active == false)
            {
                pursuitBehaviour.enable();
                LayerMask avoidLayerMask = structure.Gamemode.getCollisionMask("SHIP").GetValueOrDefault();
                pursuitBehaviour.setCollidables(avoidLayerMask);
                pursuitBehaviour.setStartingAvoidFeelerLength(pursuitBehaviour.avoidFeelerLengths[0]);
                if (Helm.ShipStructure.getDevice<Afterburner>())
                {
                    pursuitBehaviour.setAfterburner();
                }
            }

            foreach (FireGroup fireGroup in structure.FireGroupManager.FireGroups)
            {
                foreach (Weapon weapon in fireGroup.getAllWeapons())
                {
                    ProjectileLauncher launcher = weapon as ProjectileLauncher;

                    if (launcher != null)
                    {
                        launcher.WeaponFired += LauncherFired;
                    }
                }
            }

            setTargets = new Structure[structure.FireGroupManager.FireGroups.Length];
            threatSys = GetComponent<BasicThreatEvaluator>();

            booted = true;
        }

        public void LauncherFired(object sender, WeaponFiredEventArgs args)
        {
            //Gui.setMessage(args.weaponFired + " has fired!");
        }

        protected virtual Vector2 setHelmDestination()
        {
            Vector2 nextPoint = Vector2.zero;

            if (targetObject == null)
            {
                nextPoint = (Vector2)new Vector3(-2000,0,0) + Random.insideUnitCircle * 50;
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
                started = true;
                if (pursuitBehaviour != null)
                {
                    if (pursuitBehaviour.Active == false)
                    {
                        pursuitBehaviour.enableExclusively();
                    }
                }

                // get sorted threat ratios for all enemy ships and structures in range
                List<Tuple<GameObject, float>> threats = threatSys.calculateThreatRatios(structure, enemiesInRange);
                float distanceToTarget = 0;
                float smallestDistance = 0;
                if (targetObject == null)
                {
                    targetObject = threats[0]._1;
                }

                /*
                if (structure.FireGroupManager.FireGroups[1].getAllWeapons()[0].Ammo <= 0)
                    setTargets[1] = null;
                if (structure.FireGroupManager.FireGroups[2].getAllWeapons()[0].Ammo <= 0)
                    setTargets[2] = null;
                */

                //Loop through firegroups and detect the best option for firegroup based on weapons
                foreach (FireGroup fireGroup in structure.FireGroupManager.FireGroups)
                {
                    
                }
                if (setTargets[0] == null || setTargets[0].Destroyed || enemiesInRange.Contains(setTargets[0].gameObject))
                {
                    foreach (GameObject enemy in enemiesInRange)
                    {
                        distanceToTarget = Vector3.Distance(transform.position, enemy.transform.position);
                        if (distanceToTarget < smallestDistance || smallestDistance == 0)
                        {
                            if (setTargets[1] != null && setTargets[2] != null)
                            {
                                if (enemy != setTargets[1].gameObject && enemy != setTargets[2].gameObject)
                                {
                                    smallestDistance = distanceToTarget;
                                    targetObject = enemy;
                                    structure.FireGroupManager.FireGroups[0].setTarget(enemy);
                                    setTargets[0] = enemy.GetComponent<Structure>();
                                }
                                else
                                {
                                    int randomNo = Random.Range(0, enemiesInRange.Count);
                                    GameObject randomTurretTarget = enemiesInRange[randomNo];
                                    structure.FireGroupManager.FireGroups[0].setTarget(randomTurretTarget);
                                    setTargets[0] = randomTurretTarget.GetComponent<Structure>();
                                }
                            }
                            else
                            {
                                smallestDistance = distanceToTarget;
                                targetObject = enemy;
                                structure.FireGroupManager.FireGroups[0].setTarget(enemy);
                                setTargets[0] = enemy.GetComponent<Structure>();
                            }
                        }
                    }
                }

                //Launcher1 Aiming
                if (setTargets[1] == null || setTargets[1].Destroyed && structure.FireGroupManager.FireGroups[1].getAllWeapons()[0].Ammo > 0)
                {
                    int x = 0;
                    while (setTargets[1] == null || setTargets[1].Destroyed || (setTargets[2] != null && setTargets[1] == setTargets[2]))
                    {
                        if (threats.Count > 0)
                        {
                            structure.FireGroupManager.FireGroups[1].setTarget(threats[x]._1);
                            setTargets[1] = threats[x]._1.GetComponent<Structure>();
                            x++;
                        }
                        else
                        {
                            structure.FireGroupManager.FireGroups[1].unacquireTarget();
                            setTargets[1] = null;
                            break;
                        }
                    }
                }

                //Launcher2 Aiming
                if (setTargets[2] == null || setTargets[2].Destroyed && structure.FireGroupManager.FireGroups[2].getAllWeapons()[0].Ammo > 0)
                {
                    int x = 0;
                    while (setTargets[2] == null || setTargets[2].Destroyed || (setTargets[1] != null && setTargets[2] == setTargets[1]))
                    {
                        if (threats.Count > 0 && threats[x]._1 != null)
                        {
                            structure.FireGroupManager.FireGroups[2].setTarget(threats[x]._1);
                            setTargets[2] = threats[x]._1.GetComponent<Structure>();
                            x++;
                        }
                        else
                        {
                            structure.FireGroupManager.FireGroups[2].unacquireTarget();
                            setTargets[2] = null;
                            break;
                        }
                    }
                }

                //Bomb Aiming or Laser Turret

                if (structure.FireGroupManager.FireGroups[3].getAllWeapons()[0].Ammo <= 0 && structure.FireGroupManager.FireGroups[3].getAllWeapons().Count == 1)
                {
                    structure.FireGroupManager.swapFireGroup(structure.FireGroupManager.FireGroups[0].getAllWeapons()[0], structure.FireGroupManager.FireGroups[0], structure.FireGroupManager.FireGroups[3]);
                }
                if (structure.FireGroupManager.FireGroups[3].getAllWeapons()[0].Ammo > 0)
                {
                    int y = 0;
                    int targetNo = Random.Range(0, enemiesInRange.Count);
                    GameObject randomTarget = enemiesInRange[targetNo];
                    while (randomTarget.GetComponent<MovingStructure>() != null && y < 10)
                    {
                        targetNo = Random.Range(0, enemiesInRange.Count);
                        randomTarget = enemiesInRange[targetNo];
                        y++;
                    }
                    structure.FireGroupManager.FireGroups[3].setTarget(randomTarget);
                }
                else
                {
                    if (setTargets[3] == null || setTargets[3].Destroyed || enemiesInRange.Contains(setTargets[3].gameObject) || Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), setTargets[3].transform.position - transform.position) > 45)
                    {
                        Vector3 direction;
                        foreach (Tuple<GameObject, float> threat in threats)
                        {
                            direction = threat._1.transform.position - transform.position;
                            if (Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), direction.normalized) < 45)
                            {
                                structure.FireGroupManager.FireGroups[3].setTarget(threat._1);
                                setTargets[3] = threat._1.GetComponent<Structure>();
                                break;
                            }
                        }
                    }
                }

                // use the first target as the ship/structure to pursuit
                pursuitBehaviour.SetTargetEnemyToPursuit(targetObject);
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
                        if (structure.thermalcontrol != null)
                        {
                            if (structure.thermalcontrol.isOverheated())
                            {
                                structure.Call_ActivateUltimate(this);
                            }
                        }
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
    }
}