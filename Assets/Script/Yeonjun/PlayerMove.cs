using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;

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
    private bool afterDash, isDash;
    
    private float horizontalMove, verticalMove;
    private Quaternion lookDir;
    private Vector3 moveDir;
    
    private Rigidbody playerRigid;
    private NetworkAnimator animator;

    private Transform camTr;

    private bool red;

    private void Start()
    {
        camTr     = Camera.main.transform;
        var movePoint = new Vector3(transform.position.x, 5f, transform.position.z - 6);
        camTr.position = movePoint;
        camTr.LookAt(transform.position);
        
        // GameObject.Find("Main Camera").GetComponent<Camera>().enabled = false;
        // if (this.CompareTag("MoverPlayerBlue")) GameObject.Find("CameraBlue").GetComponent<Camera>().enabled = true;
        // else GameObject.Find("CameraRed").GetComponent<Camera>().enabled                                     = true;
        playerRigid = GetComponent<Rigidbody>();
        animator    = GetComponent<NetworkAnimator>();
        currentTime = dashCol;

        red = gameObject.name == "PlayerRed(Clone)";
    }

    private void Update()
    {
        if (!IsOwner) return;
        
        GetInput();
        currentTime += Time.deltaTime;
        
        var movePoint = new Vector3(transform.position.x, 5f, transform.position.z - 6);
        camTr.position = Vector3.Lerp(camTr.position, movePoint, 3f * Time.fixedDeltaTime);
        
        
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        
        MovePlayer();
        PlayerRotate();
    }

    private void GetInput()
    {
        verticalMove = Input.GetAxisRaw("Vertical");
        horizontalMove = Input.GetAxisRaw("Horizontal");
        
        if (Input.GetKeyDown(KeyCode.LeftShift) && !isDash) isRun = true;
        if (Input.GetKeyUp(KeyCode.LeftShift)) isRun = false;
        
        if (Input.GetKeyDown(KeyCode.Space) && !afterDash) Dash();
    }

    private void Dash()
    {
        if (dashCol > currentTime) return;
        currentTime = 0f;
        isRun = false;
        animator.Animator.SetTrigger("Dash");
        playerRigid.velocity = transform.forward * dashRange;
        StartCoroutine(Penalty());
    }

    private IEnumerator Penalty()
    {
        isDash = true;
        yield return new WaitForSeconds(dashRange / 5f);
        isDash = false;
        afterDash = true;
        moveSpeed = walkSpeed / 2;
        yield return new WaitForSeconds(dashCol);
        afterDash = false;
    }

    private void MovePlayer()
    {
        if(!afterDash) moveSpeed = isRun ? runSpeed : walkSpeed;
        if(!isDash) moveDir = new Vector3(horizontalMove, 0f, verticalMove).normalized;
        
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
