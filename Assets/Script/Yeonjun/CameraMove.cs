using System;
using UnityEngine;

public class CameraMove : MonoBehaviour
{
    [SerializeField] private bool isRedCamera;
    [SerializeField] private float offset;
    
    private Transform player;
    private Vector3 movePoint;

    private void Start()
    {
        if (isRedCamera) player = GameObject.FindWithTag("MoverPlayerRed").GetComponent<Transform>();
        else player = GameObject.FindWithTag("MoverPlayerBlue")?.GetComponent<Transform>();
    }

    private void FixedUpdate()
    {
        movePoint = new Vector3(player.position.x, 5f, player.position.z - offset);
        transform.rotation = new Quaternion(30f, 0f, 0f, 0f);
        transform.position = Vector3.Lerp(transform.position, movePoint, 3f * Time.fixedDeltaTime);
    }
}
