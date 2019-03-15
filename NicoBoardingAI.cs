using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

using NoxCore.Fittings.Devices;
using NoxCore.Fittings.Modules;
using NoxCore.Fittings.Sockets;
using NoxCore.Fittings.Weapons;
using NoxCore.GameModes;
using NoxCore.Helm;
using NoxCore.Managers;
using NoxCore.Placeables;
using NoxCore.Placeables.Ships;
using NoxCore.Utilities;
using NoxCore.Controllers;

namespace Formaggio.Controllers
{
    [RequireComponent(typeof(BasicThreatEvaluator))]
    public class NicoBoardingAI : AIStateController, IBoard, IPlayMusic
    {
        protected Ship ship;

        BasicThreatEvaluator threatSys;

        protected ArriveBehaviour arriveBehaviour;
        protected SeekBehaviour seekBehaviour;
        protected OrbitBehaviour orbitBehaviour;

        protected IComms comms;

        public Structure boardingTarget;
        protected DockingPort dockingPort;
        protected bool insideTetherRange;
        protected string previousDockingReport;
        protected bool docked;

        protected List<Structure> squad;

        AudioSource shipAudio;
        AudioClip dockingMusic;

        protected float smallestMaxRange;

        bool isReadyToBoard = false;

        Vector2 waitingVector;
        bool atWaitingVector = false;

        public override void boot(Structure structure, HelmController helm = null)
        {
            base.boot(structure, helm);

            GameEventManager.MatchIsWaitingToStart += AI_MatchIsWaitingToStart;

            ship = structure as Ship;

            shipAudio = ship.GetComponent<AudioSource>();
            dockingMusic = Resources.Load<AudioClip>("Audio/Blue Danube (shorter)");

            startMusic();

            threatSys = GetComponent<BasicThreatEvaluator>();

            aiActions.Add("SEARCH", searchAction);
            aiActions.Add("DISABLE", disableAction);
            aiActions.Add("DOCK", dockAction);
            aiActions.Add("BOARD", boardAction);
            aiActions.Add("WAIT", waitAction);

            state = "WAIT";

            arriveBehaviour = Helm.getBehaviourByName("ARRIVE") as ArriveBehaviour;
            seekBehaviour = Helm.getBehaviourByName("SEEK") as SeekBehaviour;
            orbitBehaviour = Helm.getBehaviourByName("ORBIT") as OrbitBehaviour;

            if (orbitBehaviour != null)
            {
                orbitBehaviour.OrbitRange = structure.scanner.getRadius();
            }

            comms = structure.getDevice<IComms>() as IComms;

            float smallestRange = 10000;

            waitingVector = new Vector2(-2000, 0);

            foreach (Weapon weapon in structure.weapons)
            {
                if (weapon.MaxRange < smallestRange)
                {
                    smallestRange = weapon.MaxRange;
                    smallestMaxRange = smallestRange;
                }
            }

            booted = true;
        }

        public void setBoardingTarget(Structure boardingTarget)
        {
            this.boardingTarget = boardingTarget;
        }

        public void startMusic()
        {
            if (shipAudio != null && dockingMusic != null)
            {
                if (!shipAudio.isPlaying)
                {
                    shipAudio.PlayOneShot(dockingMusic);
                }
            }
        }

        public void stopMusic()
        {
            if (shipAudio != null)
            {
                shipAudio.Stop();
            }
        }

        protected DockingPort findDockingPort(Structure dockingStructure)
        {
            foreach (StructureSocket socket in dockingStructure.structureSockets)
            {
                DockingPort dockingPort = socket as DockingPort;

                if (dockingPort != null)
                {
                    return dockingPort;
                }
            }

            return null;
        }

        protected virtual Vector2 setHelmDestination()
        {
            switch (state)
            {
                case "SEARCH":
                    if (boardingTarget != null)
                    {
                        return boardingTarget.gameObject.transform.position;
                    }
                    else
                    {
                        return Vector2.zero;
                    }

                case "DISABLE":
                    // maybe do something helm related here in order to attack your main target and any defenders
                    break;

                case "DOCK":
                    if (dockingPort == null)
                    {
                        dockingPort = findDockingPort(boardingTarget.GetComponent<Structure>());

                        if (dockingPort == null)
                        {
                            Gui.setMessage("No docking port on boarding target. Bugging out...");
                            return startSpot.GetValueOrDefault();
                        }
                        else
                        {
                            Gui.setMessage(structure.Name + " found " + dockingPort.gameObject.name + " on " + dockingPort.transform.parent.name);

                            if (Vector2.Distance(transform.position, dockingPort.transform.position) > dockingPort.tetherDistance)
                            {
                                Gui.setMessage(structure.Name + " is heading for the docking port on " + boardingTarget.Name);
                            }

                            return dockingPort.transform.position;
                        }
                    }
                    else
                    {
                        if (Vector2.Distance(transform.position, dockingPort.transform.position) > dockingPort.tetherDistance)
                        {
                            Gui.setMessage(structure.Name + " is heading for the docking port on " + boardingTarget.Name);
                        }

                        return dockingPort.transform.position;
                    }
                case "WAIT":
                    return waitingVector;
            }

            // if not in any valid AI state then return to start spot
            return startSpot.GetValueOrDefault();
        }

        //public override IEnumerator update()
        public override void update()
        {
            //while (booted)
            if (booted == true)
            {
                processState();

                foreach (ShieldGenerator shieldGenerator in structure.shields)
                {
                    if (shieldGenerator.isActiveOn() == false && shieldGenerator.getCurrentCharge() >= shieldGenerator.minCharge)
                    {
                        shieldGenerator.raiseShield();
                    }
                }

                // some kind of test to decide when to attempt to dock

                if (comms != null)
                {
                    if (comms.hasMessages() == true)
                    {
                        foreach (EventArgs message in comms.getMessages().Reverse<EventArgs>())
                        {
                            // check type of message
                            // e.g.
                            /*
                            DistressCallMessage distressCallMessage = message as DistressCallMessage;

                            if (distressCallMessage != null)
                            {
                                // do something with the information in the message
                                D.log("Oh, " + distressCallMessage.distressed + " is in distress");

                                comms.removeMessage(distressCallMessage);
                            }

                            // could be a different type of message so try to cast to each in turn and process the information contained inside the message's args
                            NewPrimaryTargetMessage newPrimaryTargetMessage = message as NewPrimaryTargetMessage;

                            if (newPrimaryTargetMessage != null)
                            {
                                // do something with the information in the message
                                D.log("Oh, I should be attacking " + newPrimaryTargetMessage.target.name);

                                comms.removeMessage(newPrimaryTargetMessage);
                            }
                            */
                        }
                    }

                    // example of sending a message
                    /*
                    if (structure.HullStrength < 0.2f * structure.MaxHullStrength)
                    {
                        if (comms.isSending() == false)
                        {
                            comms.broadcastMessage(squad, new DistressCallMessage(structure));
                        }
                    }
                    */
                }

                //yield return new WaitForSeconds(AITickRate);
            }
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
                        if (seekBehaviour != null)
                        {
                            if (seekBehaviour.Active == false)
                            {
                                seekBehaviour.enableExclusively();
                            }

                            // run search pattern
                            if (Helm.destination == null)
                            {
                                Helm.destination = setHelmDestination();
                            }

                            // draw a line to the destination
                            if (Helm.destination != null && Cam.followTarget != null && Cam.followTarget.gameObject == Helm.ShipStructure.gameObject)
                            {
                                Debug.DrawLine(structure.transform.position, Helm.destination.GetValueOrDefault(), Color.blue, Time.deltaTime, true);
                            }
                        }
                        else
                        {
                            return null;
                        }

                        return "SEARCH";
                        #endregion
                    }
                    else
                    {
                        if (structure.scanner.getEnemiesInRange().Contains(boardingTarget.gameObject))
                        {
                            Gui.setMessage("Enemy station acquired on scan");
                            Gui.setMessage("Evaluating mission options...");
                            return "DISABLE";
                        }
                        else
                        {
                            return "SEARCH";
                        }
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

        public string disableAction()
        {
            if (structure.scanner.isActiveOn() == true)
            {
                if (Helm != null)
                {
                    if (seekBehaviour != null)
                    {
                        if (seekBehaviour.Active == false)
                        {
                            seekBehaviour.enableExclusively();
                        }

                        // set helm
                        if (Helm.destination == null)
                        {
                            Helm.destination = setHelmDestination();
                        }

                        // normally, you'd evaluate your chances of docking and boarding successfully without getting killed
                        /*
                         * if (some kind of test == true)
                         * {
                         *     return "DOCK";
                         * }
                         * else
                         * {
                         *     return "DISABLE";
                         * }
                         */

                        // However... Guns? Who cares about a few guns? I'm going stright for the docking port...
                        Gui.setMessage("Going for a hot board.");

                        //startMusic();

                        Helm.destination = null;

                        return "DOCK";
                    }
                }
            }

            return "DISABLE";
        }

        protected override void newScannerData(Scanner sender)
        {
            List<GameObject> enemiesInRange = structure.scanner.getEnemiesInRange();
            if (state == "COMBAT" && enemiesInRange.Count > 0)
            {
                // get sorted threat ratios for all enemy ships and structures in range
                List<Tuple<GameObject, float>> threats = threatSys.calculateThreatRatios(structure, enemiesInRange);

                // tell all fire groups to acquire the first target's hull (hence null for 2nd parameter)
                foreach (FireGroup fireGroup in structure.FireGroupManager.FireGroups)
                {
                    fireGroup.setTarget(threats[0]._1);
                }
            }
        }


        public virtual string combatAction()
        {
            if (Helm != null)
            {
                if (structure.scanner.isActiveOn() == true)
                {
                    if (seekBehaviour != null)
                    {
                        if (seekBehaviour.Active == false)
                        {
                            seekBehaviour.enableExclusively();
                        }

                        // set helm
                        if (Helm.destination == null)
                        {
                            Helm.destination = setHelmDestination();
                        }
                    }

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

                        Helm.destination = null;

                        return "SEARCH";
                    }
                    #endregion
                }
                else
                {
                    Helm.destination = null;

                    return "SEARCH";
                }
            }
            else
            {
                Helm.destination = null;

                return "SEARCH";
            }
        }

        public string dockAction()
        {
            // D.log("Controller", "Processing DOCK state");

            if (boardingTarget.Destroyed == true) return "SEARCH";

            if (docked == true) return "DOCK";

            if (Helm != null)
            {
                if (structure != null)
                {
                    foreach (IEngine engine in (structure as Ship).engines)
                    {
                        engine.activate();
                    }

                    if (dockingPort == null)
                    {
                        dockingPort = findDockingPort(boardingTarget);

                        if (dockingPort == null) return "SEARCH";
                    }

                    if (arriveBehaviour != null)
                    {
                        if (arriveBehaviour.Active == false)
                        {
                            arriveBehaviour.enableExclusively();
                        }

                        // run search pattern
                        if (Helm.destination == null)
                        {
                            Helm.destination = setHelmDestination();
                            // D.log ("DESTINATION: " + _helm.destination.ToString());
                        }
                    }

                    // draw a line to the destination
                    if (Helm.destination != null && Cam.followTarget != null)
                    {
                        if (Cam.followTarget != null && Cam.followTarget.gameObject == structure.gameObject)
                        {
                            //Debug.DrawLine(structure.transform.position, Helm.destination.GetValueOrDefault(), Color.blue, Time.deltaTime, true);
                        }
                    }

                    if (dockingPort != null)
                    {
                        // have we reached the docking port?
                        if (Vector2.Distance(transform.position, dockingPort.transform.position) <= dockingPort.tetherDistance)
                        {
                            insideTetherRange = true;

                            Tuple<bool, string> dockingReport = dockingPort.requestDocking(structure as Ship);

                            if (dockingReport._1 == true)
                            {
                                // TODO - we could have a tether visibly pull the ship to the exact docking port position

                                // basically, we have achieved the requirements to dock at the docking port (right angle, speed etc.) so now there is a small amount of time before we are actually docked
                                Gui.setMessage(dockingReport._2);

                                // we should turn off our engines at this point so we don't drift away from the docking port
                                Helm.disableAllBehaviours();

                                foreach (IEngine engine in (structure as Ship).engines)
                                {
                                    engine.deactivate();
                                }

                                docked = true;

                                return "BOARD";
                            }
                            else
                            {
                                if (previousDockingReport != dockingReport._2)
                                {
                                    Gui.setMessage(dockingReport._2);
                                }
                            }

                            previousDockingReport = dockingReport._2;
                        }
                        else
                        {
                            if (insideTetherRange == true)
                            {
                                Gui.setMessage(structure.Name + " overflew the target docking port on " + boardingTarget.Name);

                                insideTetherRange = false;
                            }
                        }

                    }
                    else
                    {
                        Helm.destination = null;
                        return "SEARCH";
                    }
                }
            }

            return "DOCK";
        }

        public string boardAction()
        {
            // D.log("Controller", "Processing BOARD state");

            return "BOARD";
        }

        public string waitAction()
        {
            if (Helm != null)
            {
                if (isReadyToBoard)
                {
                    return "DOCK";
                }
                else
                {
                    if (seekBehaviour != null && !atWaitingVector)
                    {
                        if (seekBehaviour.Active == false)
                        {
                            seekBehaviour.enableExclusively();
                        }

                        // set helm
                        if (Helm.destination == null)
                        {
                            Helm.destination = setHelmDestination();
                        }

                        if (Vector2.Distance(transform.position, waitingVector) <= 10)
                        {
                            Helm.disableAllBehaviours();

                            foreach (IEngine engine in (structure as Ship).engines)
                            {
                                engine.deactivate();
                            }

                            atWaitingVector = true;
                        }

                        // normally, you'd evaluate your chances of docking and boarding successfully without getting killed
                        /*
                         * if (some kind of test == true)
                         * {
                         *     return "DOCK";
                         * }
                         * else
                         * {
                         *     return "DISABLE";
                         * }
                         */

                        // However... Guns? Who cares about a few guns? I'm going stright for the docking port...

                        return "WAIT";
                    }
                    else
                    {
                        return "WAIT";
                    }
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

            setBoardingTarget((GameManager.Instance.Gamemode as BoardingMode).getBoardingShip());
        }
    }
}
