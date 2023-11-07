using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotateSpeed;

    private float horizontalMove, verticalMove;
    private Quaternion lookDir;
    private Vector3 moveDir;
    private Rigidbody playerRigid;

    private void Start()
    {
        playerRigid = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        MovePlayer();
        PlayerRotate();
    }

    private void MovePlayer()
    {
        verticalMove = Input.GetAxisRaw("Vertical");
        horizontalMove = Input.GetAxisRaw("Horizontal");
        moveDir = new Vector3(horizontalMove, 0f, verticalMove).normalized;
        playerRigid.MovePosition(transform.position + moveDir * (moveSpeed * Time.fixedDeltaTime));
    }
    
    private void PlayerRotate()
    {
        if (moveDir.x == 0 && moveDir.z == 0) return;
        lookDir = Quaternion.LookRotation(moveDir);

        playerRigid.rotation = Quaternion.Slerp(playerRigid.rotation, lookDir, rotateSpeed * Time.fixedDeltaTime);
    }
}
