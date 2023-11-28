using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerMove : NetworkBehaviour
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
    private NetworkAnimator animator;

    private void Start()
    {
        playerRigid = GetComponent<Rigidbody>();
        animator = GetComponent<NetworkAnimator>();
        currentTime = dashCol;
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }
        
        GetInput();
        currentTime += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
        {
            return;
        }
        
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
        animator.Animator.SetTrigger("Dash");
        playerRigid.velocity = transform.forward * dashRange;
        currentTime = 0f;
    }

    private void MovePlayer()
    {
        moveSpeed = isRun ? runSpeed : walkSpeed;
        moveDir = new Vector3(horizontalMove, 0f, verticalMove).normalized;
        
        animator.Animator.SetBool("isMove", moveDir != new Vector3(0f, 0f, 0f));
        animator.Animator.SetBool("isRun", isRun);
        
        playerRigid.MovePosition(transform.position + moveDir * (moveSpeed * Time.fixedDeltaTime));
    }
    
    private void PlayerRotate()
    {
        if (verticalMove == 0 && horizontalMove == 0) return;
        
        lookDir = Quaternion.LookRotation(moveDir);
        playerRigid.rotation = Quaternion.Slerp(playerRigid.rotation, lookDir, rotateSpeed * Time.fixedDeltaTime);
    }
}
