using UnityEngine;

public class CameraMove : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float offset;
    
    private Vector3 movePoint;

    private void FixedUpdate()
    {
        movePoint = new Vector3(player.position.x, transform.position.y, player.position.z - offset);
        transform.position = Vector3.Lerp(transform.position, movePoint, 3f * Time.fixedDeltaTime);
    }
}
