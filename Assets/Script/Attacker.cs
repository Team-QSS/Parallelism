using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attacker : MonoBehaviour
{
    [Header("Camera")] [SerializeField] private Vector2 mouseSensitivity;

    private                 Animator anim;
    private                 bool     isAttacking;
    private static readonly int      AttackTrigger = Animator.StringToHash("Attack");

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        float smoothDelta = Time.smoothDeltaTime;
        
        Rotate(smoothDelta);
        Attack();
    }

    private void Attack()
    {
        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            StartCoroutine(AttackSequence());
        }
    }

    IEnumerator AttackSequence()
    {
        anim.SetTrigger(AttackTrigger);
        isAttacking = true;
        yield return new WaitForSeconds(.8f);
        isAttacking = false;
    }

    private void Rotate(float delta)
    {
        float x = -Input.GetAxis("Mouse Y") * mouseSensitivity.y * delta;
        float y = Input.GetAxis("Mouse X") * mouseSensitivity.x * delta;
        if (isAttacking)
        {
            (x, y) = (x * .1f,y * .1f);
        }
        var transform1  = transform;
        var eulerAngles = transform1.eulerAngles;
        x += eulerAngles.x;
        y += eulerAngles.y;
        if (x is > 50 and < 310)
            x = x > 180 ? 310 : 50;
        transform.rotation = Quaternion.Euler(x, y, 0);
    }
}
