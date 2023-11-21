using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private float dashRange;
    [SerializeField] private float dashCol;
    private float moveSpeed;
    private float currentTime;

    private bool isRun;
    private bool isMove;
    
    private float horizontalMove, verticalMove;
    private Quaternion lookDir;
    private Vector3 moveDir;
    private Vector3 dashPoint;
    
    private Rigidbody playerRigid;
    private Animator animator;

    private void Start()
    {
        playerRigid = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        currentTime = dashCol;
    }

    private void Update()
    {
        GetInput();
        currentTime += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        MovePlayer();
        PlayerRotate();
    }

    private void GetInput()
    {
        verticalMove = Input.GetAxisRaw("Vertical");
        horizontalMove = Input.GetAxisRaw("Horizontal");
        
        if (Input.GetKeyDown(KeyCode.LeftShift)) isRun = true;
        if (Input.GetKeyUp(KeyCode.LeftShift)) isRun = false;
        
        if (Input.GetKeyDown(KeyCode.Space)) Dash();
    }

    private void Dash()
    {
        if (dashCol > currentTime) return; 
        animator.SetTrigger("Dash");
        playerRigid.velocity = transform.forward * dashRange;
        currentTime = 0f;
    }

    private void MovePlayer()
    {
        moveSpeed = isRun ? runSpeed : walkSpeed;
        moveDir = new Vector3(horizontalMove, 0f, verticalMove).normalized;
        
        animator.SetBool("isMove", moveDir != new Vector3(0f, 0f, 0f));
        animator.SetBool("isRun", isRun);
        
        playerRigid.MovePosition(transform.position + moveDir * (moveSpeed * Time.fixedDeltaTime));
    }
    
    private void PlayerRotate()
    {
        if (verticalMove == 0 && horizontalMove == 0) return;
        
        lookDir = Quaternion.LookRotation(moveDir);
        playerRigid.rotation = Quaternion.Slerp(playerRigid.rotation, lookDir, rotateSpeed * Time.fixedDeltaTime);
    }
}
