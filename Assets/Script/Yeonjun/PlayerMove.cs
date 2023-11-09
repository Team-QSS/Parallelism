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
    
    private float horizontalMove, verticalMove;
    private Quaternion lookDir;
    private Vector3 moveDir, dashPoint;

    private RaycastHit hit;
    
    private Rigidbody playerRigid;

    //TODO : 대쉬 기능 구현 [쿨타임 포함]
    private void Start()
    {
        playerRigid = GetComponent<Rigidbody>();
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
        // playerRigid.velocity = transform.forward * dashRange;
        Debug.DrawRay(transform.position, transform.forward * dashRange, Color.green, 1f);
        if (Physics.Raycast(transform.position, transform.forward * dashRange, out hit, dashRange))
        {
            dashPoint = hit.point * .5f;
        }
        else
        {
            dashPoint = (transform.position + transform.forward.normalized * dashRange) * .5f;
        }
        transform.Translate(transform.position + dashPoint);
        currentTime = 0f;
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
