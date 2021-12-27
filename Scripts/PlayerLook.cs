using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform orientation;
    [SerializeField] private LayerMask groundLayer;
    private PhysicMaterial pm;
    
    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float airSpeed = 3f;
    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float airAcceleration = 30f;
    [SerializeField] private float speedLimit = 100f;
    [SerializeField] private float jumpForce = 15f;

    [Header("Keybindings")]

    private float horizontalMovement;
    private float verticalMovement;

    private float groundDrag = 5f;
    private float airDrag = 0.5f;

    private bool isGrounded;
    private bool onSlope;
    private bool surfing;
    private bool canJump = true;

    private Vector3 wishVelocity;
    private Vector3 slopeVector;
    private Vector3 surfVector;
    
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<CapsuleCollider>().material;
    }

    void controlDrag()
    {
        if (isGrounded)
            rb.drag = groundDrag;
        else
            rb.drag = airDrag;
    }

    private void Update()
    {
        MyInput();
        controlDrag();
        
        if (Input.GetAxisRaw("Jump") != 0 && isGrounded)
        {
            Jump();
        }

        if (!isGrounded)
        {
            pm.staticFriction = 0f;
            pm.dynamicFriction = 0f;
            pm.frictionCombine = PhysicMaterialCombine.Minimum;
        }
    }

    private void FixedUpdate()
    {
        rb.velocity = Vector3.ClampMagnitude(rb.velocity, speedLimit);
        Movement();
    }

    void Jump()
    {
        if (canJump)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

            canJump = false;
            Invoke("resetJump", 0.1f);
        }
    }

    void resetJump()
    {
        canJump = true;
    }

    private void MyInput()
    {
        horizontalMovement = Input.GetAxisRaw("Horizontal");
        verticalMovement = Input.GetAxisRaw("Vertical");

        wishVelocity = orientation.forward * verticalMovement + orientation.right * horizontalMovement;
        var inSpeed = Vector3.ClampMagnitude(wishVelocity, 1);
        wishVelocity = wishVelocity.normalized * inSpeed.magnitude;
        wishVelocity *= GetWishSpeed();
    }

    private float GetWishSpeed()
    {
        if (!isGrounded)
            return airSpeed;
        return speed;
    }

    private void Movement()
    {
        if (isGrounded)
        {
            WalkMove();
        }
        else
        {
            AirMove();
        }
    }

    private void WalkMove()
    {
        var wishdir = wishVelocity.normalized;
        var wishspeed = wishVelocity.magnitude;

        wishVelocity.y = 0f;
        wishVelocity = wishVelocity.normalized * wishspeed;

        if (onSlope)
            wishdir = Vector3.ProjectOnPlane(wishdir, slopeVector);

        Accelerate(wishdir, wishspeed, speedLimit, acceleration);
    }
    
    private void AirMove()
    {
        var wishdir = (orientation.transform.forward * verticalMovement) + (orientation.right * horizontalMovement);
        wishdir = Vector3.Lerp(wishdir, wishVelocity, 0.5f);
        wishdir.y = 0f;

        var wishSpeed = wishVelocity.magnitude;

        AirAccelerate(wishdir, wishSpeed, airAcceleration);
	    
    }
    

    private void AirAccelerate(Vector3 wishdir, float wishSpeed, float accel)
    {
        if (surfing)
        {
            wishdir = ClipVelocity(wishdir, surfVector, 0.85f);
        }
        var currentSpeed = Vector3.Dot(rb.velocity, wishdir);

        var addspeed = wishSpeed - currentSpeed;

        if (addspeed <= 0)
            return;

        var accelspeed = accel * Time.deltaTime * wishSpeed;

        if (accelspeed > addspeed)
        {
            accelspeed = addspeed;
        }
        
        rb.AddForce(wishdir * accelspeed, ForceMode.VelocityChange);
    }

    private void Accelerate(Vector3 wishdir, float wishspeed, float speedLimit, float acceleration)
    {
        if (onSlope)
            wishdir = Vector3.ProjectOnPlane(wishdir, slopeVector);
        
        if ( speedLimit > 0 && wishspeed > speedLimit )
        {
            wishspeed = speedLimit;
        }

        var currentspeed = Vector3.Dot(rb.velocity, wishdir);
        
        var addspeed = wishspeed - currentspeed;
        
        if(addspeed <= 0)
            return;

        var accelspeed = this.acceleration * Time.deltaTime * wishspeed;

        if (accelspeed > addspeed)
            accelspeed = addspeed;
        
        //rb.velocity+=wishdir * accelspeed;
        rb.AddForce(wishdir * accelspeed, ForceMode.VelocityChange);
    }

    private bool IsFloor(Vector3 v)
    {
        return v == Vector3.up;
    }
    private bool IsSlope(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        if(v != Vector3.up)
            return angle < 45;
        return false;
    }
    private bool IsSurf(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        if(v != Vector3.up)
            return angle > 45;
        return false;
    }

    private void OnCollisionStay(Collision other)
    {
        int layer = other.gameObject.layer;
        if (groundLayer != (groundLayer | (1 << layer))) return;
    
        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;
            
            if (IsSurf(normal))
            {
                surfing = true;
                surfVector = normal;
                isGrounded = false;
            }
            else surfing = false;
            
            if (IsFloor(normal))
                isGrounded = true;

                if (IsSlope(normal))
            {
                isGrounded = true;
                onSlope = true;
                slopeVector = normal;
            }
            else
                onSlope = false;

            if (!IsFloor(normal) && !IsSlope(normal))
                isGrounded = false;
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        int layer = other.gameObject.layer;
        if (groundLayer != (groundLayer | (1 << layer))) return;

        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;

            if (IsFloor(normal) || IsSlope(normal))
                Invoke("EnableFriction", 0.1f); // enable friction late so bunnyhopping works consistently.
        }
    }

    private void OnCollisionExit(Collision other)
    {
        onSlope = false;
        surfing = false;
        isGrounded = false;
    }

    void EnableFriction()
    {
        pm.staticFriction = 0.6f;
        pm.dynamicFriction = 0.6f;
        pm.frictionCombine = PhysicMaterialCombine.Average;
    }

    private Vector3 ClipVelocity(Vector3 input, Vector3 normal, float overbounce)
    {
        var backoff = Vector3.Dot(input, normal) * overbounce;

        for (int i = 0; i < 3; i++)
        {
            var change = normal[i] * backoff;
            input[i] = input[i] - change;
        }

        var adjust = Vector3.Dot(input, normal);
        if (adjust < 0.0f)
        {
            input -= (normal * adjust);
        }
        return input;
    }

}
