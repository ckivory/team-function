﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VSPlayerController : MonoBehaviour
{
    public Camera cam;
    public GameObject arrow;
    public float maxSpeed;
    public float accel;
    public float aimSpeed;
    public float aimDamping;
    public float scrollSensitivity;
    public float shotForce;
    public float maxSpread;
    public int controllerNum;
    public bool usingController;

    public GameObject core;
    public GameObject disk;
    public float coreSpeed;
    public float diskSpeed;

    public float playerMass;
    private int shield;
    private float shieldRemaining;

    public List<GameObject> collectedPropPrefabs;
    public List<GameObject> firedPropPrefabs;

    private Vector3 movementInput;
    private float currentAim;
    private float targetAim;
    private float currentAimSpeed;
    private const float AIM_MAX = 60f;
    private bool RTpressed;
    private bool LTpressed;
    public List<int> inventory;
    [HideInInspector]
    public int selectedProp;    //made public by Lin
    [HideInInspector]
    public int selectedCount;   //made public by Lin
    private Rigidbody rb;

    private void getJoystickMovement()
    {
        movementInput = Vector3.zero;
        if (controllerNum > 0)
        {
            movementInput.x = Input.GetAxis("J" + controllerNum + "Horizontal");
            movementInput.z = Input.GetAxis("J" + controllerNum + "Vertical");
        }
        else
        {
            throw new System.Exception("Make sure to set the controller number in the Unity editor");
        }

        if (movementInput.magnitude > 1)
        {
            movementInput = movementInput.normalized;
        }
    }

    private void getKeyboardMovement()
    {
        movementInput = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            movementInput.z += 1;
        }
        if (Input.GetKey(KeyCode.A))
        {
            movementInput.x -= 1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            movementInput.z -= 1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            movementInput.x += 1;
        }
    }

    private void alignMovement()
    {
        movementInput = movementInput.x * Vector3.Cross(Vector3.up, transform.forward).normalized + movementInput.z * transform.forward.normalized;
    }

    private void updateMovement()
    {
        if (usingController)
        {
            getJoystickMovement();
        }
        else
        {
            getKeyboardMovement();
        }

        alignMovement();

        rb.velocity += movementInput * accel / Mathf.Max(playerMass / 20, 1f);         // Additive controls, so it will intentionally feel a little floaty.
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
        // If movementInput is zero, they will slowly drift to a stop with a drag of 1.
    }

    private void updateRotation()
    {
        Vector3 newForward = (this.transform.position - cam.transform.position);
        if (newForward != Vector3.zero)
        {
            this.transform.forward = new Vector3(newForward.x, 0f, newForward.z);
        }
    }

    private void updateSpin()
    {
        core.transform.Rotate(0f, coreSpeed * Time.deltaTime, 0f);
        disk.transform.Rotate(0f, diskSpeed * Time.deltaTime, 0f);
    }

    private void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.CompareTag("Collectible"))
        {
            int propNum = col.gameObject.GetComponent<ObjectAttributes>().propNum;
            addToInventory(propNum);
            string invString = string.Join(",", inventory.ToArray());
            Debug.Log(invString);
            Destroy(col.gameObject);
        }
        if (col.gameObject.CompareTag("Fired"))
        {
            if (!(gameObject.GetInstanceID() == col.GetComponent<ObjectAttributes>().whoFired.GetInstanceID()))
            {
                if (shield == -1)
                {
                    Destroy(gameObject);
                }
                else
                {
                    float damageToDeal = ObjectAttributes.getDamage(col.gameObject.GetComponent<ObjectAttributes>().propNum);
                    Debug.Log("Taking " + damageToDeal + " damage");
                    while (damageToDeal > 0)
                    {
                        if(shieldRemaining > damageToDeal)
                        {
                            shieldRemaining -= damageToDeal;
                            damageToDeal = 0;
                            Destroy(col.gameObject);
                            return;
                        }
                        else
                        {
                            damageToDeal -= shieldRemaining;
                            inventory[shield]--;
                            GetComponent<DiskController>().RemoveFromDisk(shield);  //added by Lin
                            newShield();

                            // This gives the player one last chance to get new cover when their last shield breaks. Large objects can break the rest of your shield, but they can't kill you in one shot unless your inventory is empty.
                            if (shield == -1)
                            {
                                damageToDeal = 0f;      
                                Destroy(col.gameObject);
                                return;
                            }
                        }
                    }
                    Destroy(col.gameObject);
                }
            }
        }
    }

    private void addToInventory(int propNum)
    {
        inventory[propNum]++;
        if (inventory[selectedProp] == 0)
        {
            selectedProp = findNonEmpty();
        }
        updateMass();
        GetComponent<DiskController>().AddToDisk(propNum);  //added by Lin
        if (inventory[selectedProp] == 0)
        {
            selectedProp = findNonEmpty();
        }
        if (shield == -1)
        {
            newShield();
        }
    }

    private void updateAim()
    {
        if (usingController)
        {
            targetAim -= Input.GetAxis("J" + controllerNum + "X") * Time.deltaTime * (1 / aimSpeed);
            targetAim += Input.GetAxis("J" + controllerNum + "Y") * Time.deltaTime * (1 / aimSpeed);
        }
        else
        {
            targetAim -= Input.GetAxis("Mouse ScrollWheel") * scrollSensitivity * Time.deltaTime * (1 / aimSpeed);
        }

        targetAim = Mathf.Clamp(targetAim, 0f, AIM_MAX);
        currentAim = Mathf.SmoothDamp(currentAim, targetAim, ref currentAimSpeed, aimSpeed * aimDamping);

        arrow.transform.forward = this.transform.forward;
        arrow.transform.rotation *= Quaternion.Euler(-1 * currentAim, 0f, 0f);
    }

    private void initializeProjectiles(int shotCount)
    {
        for (int i = 0; i < shotCount; i++)
        {
            GameObject shot = Instantiate(firedPropPrefabs[selectedProp], rb.position, arrow.transform.rotation);
            GetComponent<DiskController>().RemoveFromDisk(selectedProp);    //Added by Lin
            shot.GetComponent<Rigidbody>().velocity = rb.velocity + (arrow.transform.forward + spread(shotCount)) * shotForce;
            shot.GetComponent<ObjectAttributes>().whoFired = gameObject;
        }
    }

    private Vector3 spread(int shotCount)
    {
        return Random.insideUnitSphere * maxSpread;
    }

    private bool triggerPulled()
    {
        if (usingController)
        {
            if (Input.GetButtonDown("J" + controllerNum + "RB"))
            {
                return true;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
        }
        return false;
    }

    private int numProjectiles()
    {
        if (selectedCount > inventory[selectedProp])
        {
            return inventory[selectedProp];
        }
        else
        {
            return selectedCount;
        }
    }

    private void fire()
    {
        if(inventory[selectedProp] < 1)
        {
            selectedProp = findNonEmpty();
        }
        if(inventory[selectedProp] > 0)
        {
            Debug.Log("Firing " + selectedProp + "s. ");            
            initializeProjectiles(numProjectiles());
            for (int i = 0; i < numProjectiles(); i++)
            {
                GetComponent<DiskController>().RemoveFromDisk(selectedProp);
            }
            inventory[selectedProp] -= numProjectiles();
            updateMass();
        }
        else
        {
            Debug.Log("Nothing to fire!");
        }
    }

    private int findNonEmpty()
    {
        for(int i = 0; i < inventory.Count; i++)
        {
            int index = (selectedProp + i) % inventory.Count;
            if (inventory[index] > 0)
            {
                return index;
            }
        }
        return selectedProp; 
    }

    private bool onTriggerDown(bool rightTrigger)
    {
        if (rightTrigger)
        {
            if (Input.GetAxis("J" + controllerNum + "RT") > 0)
            {
                if (!RTpressed)
                {
                    RTpressed = true;
                    return true;
                }
                return false;
            }
            else
            {
                RTpressed = false;
                return false;
            }
        }
        else
        {
            if (Input.GetAxis("J" + controllerNum + "LT") > 0)
            {
                if (!LTpressed)
                {
                    LTpressed = true;
                    return true;
                }
                return false;
            }
            else
            {
                LTpressed = false;
                return false;
            }
        }
    }

    private void updateSelected()
    {
        if (usingController)
        {
            if (onTriggerDown(true))
            {
                selectedProp = (selectedProp + 1) % inventory.Count;
                Debug.Log(selectedProp);
            }
            if (onTriggerDown(false))
            {
                selectedProp--;
                if (selectedProp < 0)
                {
                    selectedProp += inventory.Count;
                }
                Debug.Log(selectedProp);
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                selectedProp = (selectedProp + 1) % inventory.Count;
                Debug.Log(selectedProp);
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                selectedProp--;
                if (selectedProp < 0)
                {
                    selectedProp += inventory.Count;
                }
                Debug.Log(selectedProp);
            }
        }
    }

    private void updateSelectedCount()
    {
        if(usingController)
        {
            if(Input.GetButtonDown("J" + controllerNum + "A"))
            {
                selectedCount++;
                Debug.Log("Count: " + selectedCount);
            }
            else if(Input.GetButtonDown("J" + controllerNum + "B"))
            {
                selectedCount--;
                if(selectedCount < 1)
                {
                    selectedCount = 1;
                }
                Debug.Log("Count: " + selectedCount);
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                selectedCount++;
                Debug.Log("Count: " + selectedCount);
            }
            else if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                selectedCount--;
                if (selectedCount < 1)
                {
                    selectedCount = 1;
                }
                Debug.Log("Count: " + selectedCount);
            }
        }
    }

    private void updateMass()
    {
        playerMass = 0;
        for(int i = 0; i < inventory.Count; i++)
        {
            playerMass += ObjectAttributes.getMass(i) * inventory[i];
        }
        Debug.Log("Player mass is: " + playerMass);
    }

    private void newShield()
    {
        shield = -1;
        float bestShieldSoFar = 0f;
        for(int i = 0; i < inventory.Count; i++)
        {
            if(ObjectAttributes.getMass(i) > bestShieldSoFar && inventory[i] > 0)
            {
                bestShieldSoFar = ObjectAttributes.getMass(i);
                shield = i;
            }
        }
        if(shield > -1)
        {
            shieldRemaining = ObjectAttributes.getMass(shield);
        }
        else
        {
            shieldRemaining = 0f;
        }
        Debug.Log("New shield: " + shield);
    }

    void Start()
    {
        rb = this.GetComponent<Rigidbody>();
        inventory = new List<int>();
        for (int i = 0; i < collectedPropPrefabs.Count; i++)
        {
            inventory.Add(0);
        }
        currentAim = 0f;
        targetAim = 0f;
        RTpressed = false;
        LTpressed = false;
        selectedProp = 1;
        selectedCount = 1;
        shield = -1;
        if(!usingController)
        {
            Cursor.visible = false;
        }
    }

    void Update()
    {
        updateMovement();
        updateRotation();
        updateSpin();
        updateAim();
        updateSelected();
        updateSelectedCount();
        if(triggerPulled())
        {
            fire();
        }
    }
}
