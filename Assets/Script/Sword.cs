using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class Sword : MonoBehaviour
{
    [HideInInspector] public Attacker attacker;
    private                  BoxCollider boxCollider;
    private                  Animator anim;
    private static readonly  int      AttackTrigger = Animator.StringToHash("Attack");
    private static readonly  int      ThrowBool     = Animator.StringToHash("Throw");

    [HideInInspector] public Vector3 innerOffset   = new Vector3(.6f, 0, 0);
    [HideInInspector] public float   innerDistance = 1.7f;

    public  bool IsAction { get; private set; }
    private bool isThrow;
    public  bool IsInside { get; private set; } = true;

    private int index;
    private int length;
    
    private Vector3 targetPosition;

    [SerializeField] private float outTime = 6;
    private                  float outTimer;

    private float damage = 40;

    private void Awake()
    {
        anim                            = GetComponentInChildren<Animator>();
        boxCollider                     = GetComponentInChildren<BoxCollider>();
        anim.keepAnimatorStateOnDisable = true;
    }

    private void Update()
    {
        if(!isThrow && IsInside)
        {
            InnerMove();
        }
        else
        {
            if (!isThrow) transform.rotation =  Quaternion.Lerp(transform.rotation, attacker.transform.rotation, 10 * Time.smoothDeltaTime);
            outTimer           += Time.deltaTime;
            if (outTimer > outTime)
            {
                Explosion();
            }
        }
    }

    private void OnEnable()
    {
        anim.Play("Idle");
        boxCollider.enabled = false;
        IsInside            = true;
        IsAction            = false;
        isThrow             = false;
        outTimer            = 0;
    }

    private void OnDisable()
    {
        anim.ResetTrigger(AttackTrigger);
        anim.SetBool(ThrowBool, false);
        attacker.RespawnSword(this);
    }

    private void InnerMove()
    {
        float delta  = Time.smoothDeltaTime;
        var   offset = IsAction ? default : innerOffset;

        var rotation = attacker.swordAnchor.rotation *
                       Quaternion.AngleAxis((float)index / length * 360, new Vector3(0, 1, 0));
        targetPosition = rotation * (Vector3.forward + offset) * innerDistance + attacker.swordAnchor.position;

        Transform transform1;
        (transform1 = transform).position = Vector3.Slerp(transform1.position, targetPosition, 10 * delta);
        transform1.rotation                = Quaternion.Lerp(transform1.rotation, rotation, 10 * delta);
    }

    public void SetInnerProperty(int i, int l)
    {
        index  = i;
        length = l;
    }

    public void Attack()
    {
        if(isThrow) return;
        StartCoroutine(AttackSequence());
    }

    public bool Throw()
    {
        if(isThrow)
        {
            isThrow = false;
            return isThrow;
        }
        
        StartCoroutine(ThrowSequence());
        return true;
    }

    private void Explosion()
    {
        gameObject.SetActive(false);
    }

    IEnumerator AttackSequence()
    {
        IsAction = true;
        anim.SetTrigger(AttackTrigger);
        float penaltyTime = 1.5f;
        float animTime    = 0.85f;
        float startTime   = 0.54f;
        float endTime     = 0.54f;
        startTime *= animTime;
        endTime   *= animTime;
        yield return new WaitForSeconds(startTime);
        boxCollider.enabled = true;
        yield return new WaitForSeconds(endTime - startTime);
        boxCollider.enabled = false;
        yield return new WaitForSeconds(Mathf.Max(penaltyTime - startTime - endTime, 0.5f));
        IsAction = false;
    }

    private IEnumerator ThrowSequence()
    {
        transform.rotation = attacker.transform.rotation;
        
        IsAction           = true;
        anim.SetBool(ThrowBool, true);
        yield return new WaitForSeconds(.7f);
        
        IsInside            = false;
        boxCollider.enabled = true;
        isThrow             = true;
        StartCoroutine(MoveForward());
        IsAction = false;
    }

    private IEnumerator MoveForward()
    {
        float speed = 10f;
        float distance = default;
        while (isThrow && distance < 20f)
        {
            float delta      = Time.deltaTime;
            distance += speed * delta;
            var   transform1 = transform;
            transform1.position += transform1.forward * (speed * delta);
            yield return null;
        }

        isThrow = false;
        anim.SetBool(ThrowBool, false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isThrow)
        {
            Explosion();
        }
        else if(other.CompareTag("Player"))
        {
            var hit = other.GetComponent<IHit>();
            if (hit != null)
            {
                attacker.Hit();
                hit.Hit(damage);
            }
        }
    }
}