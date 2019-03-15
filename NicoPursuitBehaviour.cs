using UnityEngine;
using System.Collections;

using NoxCore.Controllers;
using NoxCore.Fittings.Devices;
using NoxCore.Fittings.Modules;
using NoxCore.Fittings.Weapons;
using NoxCore.Helm;
using NoxCore.Managers;
using NoxCore.Placeables;
using NoxCore.Placeables.Ships;
using NoxCore.Rules;
using NoxCore.Utilities;

using Yvan.Fittings.Devices;
using Formaggio.Controllers;

namespace Formaggio.Helm
{
    public class NicoPursuitBehaviour : SteeringBehaviour
    {
        //SEEKER VARIABLES
        protected float seekLength;
        public bool continuousSeek;

        public bool dynamicLookAhead;
        public float lookAheadDistance;

        [Range(0, 1)]
        public float dynamicLookAheadFactor = 1f;

        protected float rangeToDestination;
        Vector2 seekDesiredVector;

        //PURSUIT VARIABLES
        [SerializeField]
        [ShowOnly]
        protected GameObject _TargetObject;
        public GameObject TargetObject { get { return _TargetObject; } set { _TargetObject = value; } }

        float enemyLength;

        Vector2 pursuitDesiredVector;

        //AVOID VARIABLES
        protected float avoidLength, avoidWidth;
        public float[] avoidFeelerLengths;
        protected Vector2[] avoidFeelers, avoidFeelerDirection;
        float startingAvoidFeelerLength;
        Vector2 avoidDesiredVector;

        protected LayerMask collidables;
        protected Afterburner afterburner;

        void Awake()
        {
            //SEEK
            seekLength = GetComponent<Collider2D>().bounds.extents.y;
            lookAheadDistance = seekLength;

            //AVOID
            avoidLength = GetComponent<PolygonCollider2D>().bounds.extents.y;
            avoidWidth = GetComponent<PolygonCollider2D>().bounds.extents.x;

            avoidFeelerLengths = new float[3];
            avoidFeelerLengths[0] = 150;
            avoidFeelerLengths[1] = 150;
            avoidFeelerLengths[2] = 150;

            avoidFeelers = new Vector2[3];
            avoidFeelerDirection = new Vector2[3];
        }

        public void setCollidables(LayerMask mask)
        {
            collidables = mask;
        }

        public void setAfterburner()
        {
            afterburner = Helm.ShipStructure.getDevice<Afterburner>() as Afterburner;
        }

        public void setStartingAvoidFeelerLength(float feelerLength)
        {
            startingAvoidFeelerLength = feelerLength;
        }

        public bool IsPursuiting()
        {
            if (TargetObject != null)
            {
                if (rangeToDestination < 500 && Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), TargetObject.transform.TransformDirection(0, 1, 0)) < 25 && Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), (Helm.Destination - Helm.Position).normalized) < 90)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public void SetTargetEnemyToPursuit(GameObject targetEnemy)
        {
            TargetObject = targetEnemy;
            PolygonCollider2D enemyCollider = targetEnemy.GetComponent<PolygonCollider2D>();
            float tempEnemyBoundsX = enemyCollider.bounds.extents.x;
            float tempEnemyBoundsY = enemyCollider.bounds.extents.y;
            if (tempEnemyBoundsY > tempEnemyBoundsX)
            {
                enemyLength = tempEnemyBoundsY;
            }
            else
            {
                enemyLength = tempEnemyBoundsX;
            }
        }

        public void DisengageTarget()
        {
            TargetObject = null;
        }

        public override Vector2 execute()
        {
            //SEEK AND PURSUIT BEHAVIOUR

            if (Helm.destination != null && TargetObject == null)
            {
                //SEEK BEHAVIOUR
                seekDesiredVector = Vector2.zero;
                pursuitDesiredVector = Vector2.zero;

                Vector2 direction = Helm.destination.Value - Helm.Position;

                float angleToDestination = Vector2.SignedAngle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), direction.normalized);

                if (Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), direction.normalized) > 50)
                {
                    if (angleToDestination < 0)
                        steeringVector = Helm.ShipStructure.transform.TransformDirection(1, 0, 0);
                    else
                        steeringVector = Helm.ShipStructure.transform.TransformDirection(-1, 0, 0);
                }
                else
                {
                    steeringVector = Helm.Destination - Helm.Position;
                }

                rangeToDestination = (Helm.Destination - Helm.Position).magnitude;
                Helm.RangeToDestination = rangeToDestination;

                // calculate a look ahead distance based on the ship's length and its current speed
                if (dynamicLookAhead == true)
                {
                    lookAheadDistance = seekLength + (seekLength * Helm.ShipStructure.Speed * dynamicLookAheadFactor);
                }

                if (rangeToDestination < lookAheadDistance && continuousSeek == false)
                {
                    Helm.RangeToDestination = 0;
                    Helm.destination = null;
                    //Debug.Log("Reached destination");
                }
                else
                {
                    seekDesiredVector = steeringVector.normalized;
                    seekDesiredVector *= (Helm.ShipStructure.MaxSpeed * Helm.throttle);
                }


                RaycastHit2D hit = Physics2D.Raycast(Helm.Position, direction.normalized, 600, collidables);
                if (Helm.destination != null)
                {
                    Debug.DrawLine(Helm.Position, Helm.destination.Value, Color.yellow, Time.deltaTime, true);
                    Debug.DrawLine(Helm.Position, Helm.Position + seekDesiredVector, Color.magenta, Time.deltaTime, true);
                }


                if (!hit && Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), direction.normalized) < 30)
                {
                    if (afterburner != null && afterburner.Cooldown.enabled == false)
                    {
                        afterburner.engage();
                    }
                }

                if (rangeToDestination < 400)
                {
                    if (Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), direction.normalized) > 45)
                    {
                        float range = rangeToDestination / 400;
                        if (range > 0.4f)
                            Helm.desiredThrottle = range;
                        else
                            Helm.desiredThrottle = 0.4f;

                        if (afterburner != null)
                        {
                            if (afterburner.activeOn)
                                Helm.desiredThrottle *= 0.5f;
                        }

                        if (rangeToDestination < 30)
                        {
                            Helm.desiredThrottle *= 0.5f;
                        }
                    }
                    else
                    {
                        Helm.desiredThrottle = 1f;
                    }
                }

                seekDesiredVector -= Helm.ShipRigidbody.velocity;
            }

            else if (TargetObject != null)
            {
                //PURSUIT BEHAVIOUR
                seekDesiredVector = Vector2.zero;
                pursuitDesiredVector = Vector2.zero;

                Helm.Destination = (Vector2)TargetObject.transform.position + ((Vector2)TargetObject.transform.TransformDirection(0, -1, 0) * enemyLength);
                rangeToDestination = (Helm.Destination - Helm.Position).magnitude;
                Helm.RangeToDestination = rangeToDestination;
                float signedAngleToDestination = Vector2.SignedAngle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), (Helm.Destination - Helm.Position).normalized);
                float unSignedAngleToDestination = Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), (Helm.Destination - Helm.Position).normalized);
                float angleToShip = Vector2.Angle(Helm.ShipStructure.transform.TransformDirection(0, 1, 0), TargetObject.transform.TransformDirection(0, 1, 0));

                if (angleToShip > 90 && unSignedAngleToDestination <= 90)
                {
                    if (rangeToDestination > 1350)
                    {
                        if (signedAngleToDestination > 0)
                        {
                            Helm.Destination = ((Vector2)TargetObject.transform.position + ((Vector2)TargetObject.transform.TransformDirection(-1, 0, 0) * 750));
                        }
                        else
                        {
                            Helm.Destination = ((Vector2)TargetObject.transform.position + ((Vector2)TargetObject.transform.TransformDirection(1, 0, 0) * 750));
                        }
                    }
                    else if (rangeToDestination > 1000)
                    {
                        if (signedAngleToDestination > 0)
                        {
                            Helm.Destination = ((Vector2)TargetObject.transform.position + ((Vector2)TargetObject.transform.TransformDirection(-1, 0, 0) * 450));
                        }
                        else
                        {
                            Helm.Destination = ((Vector2)TargetObject.transform.position + ((Vector2)TargetObject.transform.TransformDirection(1, 0, 0) * 450));
                        }
                    }
                }


                Debug.DrawLine(transform.position, Helm.Destination, Color.yellow);

                if (unSignedAngleToDestination > 110)
                {
                    if (signedAngleToDestination < 0)
                        steeringVector = (Vector2)Helm.ShipStructure.transform.TransformDirection(1, 0f, 0);
                    else
                        steeringVector = (Vector2)Helm.ShipStructure.transform.TransformDirection(-1, 0f, 0);
                }
                else
                {
                    steeringVector = Helm.Destination - Helm.Position;
                }
                /*
                if (rangeToDestination > 200)
                {
                    if (unSignedAngleToDestination > 110)
                    {
                        if (signedAngleToDestination < 0)
                            steeringVector = (Vector2)Helm.ShipStructure.transform.TransformDirection(1, 0f, 0);
                        else
                            steeringVector = (Vector2)Helm.ShipStructure.transform.TransformDirection(-1, 0f, 0);
                    }
                    else
                    {
                        steeringVector = Helm.Destination - Helm.Position;
                    }
                }
                else
                {
                    if (unSignedAngleToDestination <= 90 && angleToShip > 90 && Vector2.SignedAngle(Helm.ShipStructure.transform.TransformDirection(1, 0, 0), TargetObject.transform.TransformDirection(0, 1, 0)) > 0)
                    {
                        if (signedAngleToDestination < 0)
                            steeringVector = (Vector2)Helm.ShipStructure.transform.TransformDirection(-1, 0f, 0);
                        else
                            steeringVector = (Vector2)Helm.ShipStructure.transform.TransformDirection(1, 0f, 0);
                    }
                    else
                    {
                        steeringVector = Helm.Destination - Helm.Position;
                    }
                }
                */

                pursuitDesiredVector = steeringVector.normalized;
                pursuitDesiredVector *= (Helm.ShipStructure.MaxSpeed * Helm.throttle);

                Debug.DrawLine(transform.position, Helm.Position + pursuitDesiredVector, Color.magenta);

                if (afterburner != null && afterburner.Cooldown.enabled == false)
                {
                    if (rangeToDestination > 700 && Helm.ShipRigidbody.velocity.magnitude >= 130 && angleToShip < 60 && unSignedAngleToDestination < 90)
                    {
                        afterburner.engage();
                    }
                }

                if (rangeToDestination < 750 && rangeToDestination > 500)
                {
                    if (unSignedAngleToDestination > 90 || angleToShip > 90)
                    {
                        float range = rangeToDestination / 750;
                        if (range > 0.65f)
                        {
                            Helm.desiredThrottle = range;
                        } 
                        else
                        {
                            Helm.desiredThrottle = 0.65f;
                        }

                        if (afterburner != null)
                        {
                            if (afterburner.activeOn)
                            {
                                Helm.desiredThrottle = 0.50f;
                            }   
                        }
                    }
                    else
                    {
                        Helm.desiredThrottle = 1f;
                    }
                }
                else if (rangeToDestination <= 500 && rangeToDestination > 250)
                {
                    Helm.desiredThrottle = 0.65f;
                }
                else if (rangeToDestination <= 250)
                {
                    Helm.desiredThrottle = 0.25f;
                }
                else
                {
                    Helm.desiredThrottle = 1f;
                }

                pursuitDesiredVector -= Helm.ShipRigidbody.velocity;
            }
            else
            {
                seekDesiredVector = Vector2.zero;
                pursuitDesiredVector = Vector2.zero;
            }


            //AVOID BEHAVIOUR
            Vector2 combinedAvoidFeelerForce = Vector2.zero;
            Vector2 avoidForce;
            float overshootCollision = 0;

            float shipBearing = Helm.ShipStructure.Bearing;
            Vector2 shipPos = Helm.Position;
            Vector2 destinationVector = Vector2.zero;

            float destinationAngle;
            if (Helm.destination != null)
            {
                destinationVector = Helm.destination.Value - shipPos;
                rangeToDestination = destinationVector.magnitude;

                if (Vector2.Distance(Helm.Position, Helm.destination.Value) >= 50)
                {
                    destinationAngle = (Vector2.SignedAngle(Helm.ShipStructure.transform.TransformDirection(new Vector2(0, 1)), destinationVector.normalized) * -1);
                    destinationAngle = Mathf.Clamp((destinationAngle * Mathf.Deg2Rad) * 2, -0.20f, 0.20f);
                }
                else
                {
                    destinationVector = Vector2.zero;
                    destinationAngle = 0;
                }
            }
            else
            {
                destinationAngle = 0;
            }

            // create feelers
            float offsetAngle = (shipBearing + 90) * Mathf.Deg2Rad;
            float xOffset = avoidWidth * Mathf.Sin(offsetAngle);
            float yOffset = avoidWidth * Mathf.Cos(offsetAngle);

            avoidFeelers[0] = new Vector2(shipPos.x + xOffset, shipPos.y + yOffset);
            avoidFeelers[1] = new Vector2(shipPos.x, shipPos.y);
            avoidFeelers[2] = new Vector2(shipPos.x - xOffset, shipPos.y - yOffset);

            avoidFeelerDirection[0] = Helm.ShipStructure.transform.TransformDirection(new Vector2(0.175f + destinationAngle, 1));
            avoidFeelerDirection[1] = Helm.ShipStructure.transform.TransformDirection(new Vector2(0 + destinationAngle, 1));
            avoidFeelerDirection[2] = Helm.ShipStructure.transform.TransformDirection(new Vector2(-0.175f + destinationAngle, 1));

            // go through each feeler
            for (int i = 0; i < avoidFeelers.Length; i++)
            {
                // Note: as this basic feeler is pointing forwards from the side of the ship, the vector direction is simply Vector3.forward
                // if you want a feeler that extends off in a different direction then simply make a unit vector in the x-z plane where
                // the forward direction is a bearing of 0 and right is a bearing of 90 degree

                Vector2 feelerEndPosition = Vector2.zero;

                // draw debug lines to show feelers in scene view (note this is not required but helps design the feeler system for the ship)
                if (i == 0)
                {
                    feelerEndPosition = avoidFeelers[i] + (avoidFeelerLengths[i] * avoidFeelerDirection[i]);
                    Debug.DrawLine(avoidFeelers[i], feelerEndPosition, Color.red, Time.deltaTime, true);
                }
                else if (i == 1)
                {
                    feelerEndPosition = avoidFeelers[i] + (avoidFeelerLengths[i] * avoidFeelerDirection[i]);
                    Debug.DrawLine(avoidFeelers[i], feelerEndPosition, Color.gray, Time.deltaTime, true);
                }
                else
                {
                    feelerEndPosition = avoidFeelers[i] + (avoidFeelerLengths[i] * avoidFeelerDirection[i]);
                    Debug.DrawLine(avoidFeelers[i], feelerEndPosition, Color.green, Time.deltaTime, true);
                }

                float newFeelerLength = Helm.ShipRigidbody.velocity.magnitude;
                avoidFeelerLengths[i] = Mathf.Clamp(newFeelerLength, startingAvoidFeelerLength, startingAvoidFeelerLength + 300);

                RaycastHit2D hit = Physics2D.Raycast(avoidFeelers[i], avoidFeelerDirection[i], avoidFeelerLengths[i], collidables);
                if (hit.collider != null)
                {
                    // feeler detected collidable object
                    overshootCollision = Vector2.Distance(feelerEndPosition, hit.point);

                    if (i == 1)
                    {
                        if (rangeToDestination > 200)
                        {
                            if (overshootCollision > 100)
                            {
                                float overshootMutliplier = 0.00075f;
                                float brake = Mathf.Clamp(overshootCollision * overshootMutliplier, 0.05f, 0.40f);
                                Helm.desiredThrottle = 1f - brake;

                                if (afterburner != null)
                                {
                                    if (afterburner.activeOn)
                                    {
                                        Helm.desiredThrottle *= 0.5f;
                                    }
                                }
                            }
                        }
                    }

                    Vector2 normal = new Vector2(hit.point.x - hit.transform.position.x, hit.point.y - hit.transform.position.y).normalized;
                    Vector2 reflectionVector = Vector2.Reflect(avoidFeelerDirection[i], normal);

                    if (destinationVector != Vector2.zero)
                    {
                        if (Vector2.Angle(reflectionVector, destinationVector) >= 120)
                        {
                            reflectionVector = normal;
                        }
                        if (Vector2.Angle(Helm.destination.Value, Helm.ShipStructure.transform.TransformDirection(0, 1, 0)) <= Vector2.Angle(Helm.destination.Value, reflectionVector * overshootCollision))
                        {
                            reflectionVector = normal;
                        }
                    }

                    avoidForce = reflectionVector * overshootCollision;
                    combinedAvoidFeelerForce += avoidForce;

                    if (Helm.Controller.Cam.followTarget != null && Helm.Controller.Cam.followTarget.gameObject == Helm.ShipStructure.gameObject)
                    {
                        Vector2 normalStart = new Vector2(hit.point.x, hit.point.y);
                        Vector2 normalEnd = hit.point + avoidForce;

                        Debug.DrawLine(normalStart, normalEnd, Color.white, Time.deltaTime, true);
                    }
                }
                else
                {
                    avoidForce = Vector2.zero;
                }
            }

            desiredVelocity = combinedAvoidFeelerForce + seekDesiredVector + pursuitDesiredVector;
            desiredVelocity = desiredVelocity.normalized * Helm.ShipStructure.MaxSpeed * 2;

            return desiredVelocity;
        }
    }
}
