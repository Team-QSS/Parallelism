using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private float moveSpeed;

    private bool isRun;
    
    private float horizontalMove, verticalMove;
    private Quaternion lookDir;
    private Vector3 moveDir;
    
    private Rigidbody playerRigid;

    private void Start()
    {
        playerRigid = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        GetInput();
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
    }

    private void MovePlayer()
    {
        moveSpeed = isRun ? runSpeed : walkSpeed;
        moveDir = new Vector3(horizontalMove, 0f, verticalMove).normalized;
        playerRigid.MovePosition(transform.position + moveDir * (moveSpeed * Time.fixedDeltaTime));
    }
    
    private void PlayerRotate()
    {
        if (verticalMove == 0 && horizontalMove == 0) return;
        
        lookDir = Quaternion.LookRotation(moveDir);
        playerRigid.rotation = Quaternion.Slerp(playerRigid.rotation, lookDir, rotateSpeed * Time.fixedDeltaTime);
    }
}
