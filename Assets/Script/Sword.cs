using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class Sword : MonoBehaviour
{
    [HideInInspector] public Attacker        attacker;
    private                  BoxCollider     boxCollider;
    private                  Animator        anim;
    private static readonly  int             AttackTrigger = Animator.StringToHash("Attack");
    private static readonly  int             SkillTrigger  = Animator.StringToHash("Skill");
    private static readonly  int             ThrowBool     = Animator.StringToHash("Throw");
    [SerializeField] private ComboAnimInfo[] infos;


    [Serializable]
    public class ComboAnimInfo
    {
        public float   t;
        public float   sT;
        public float   eT;
        public float   sCoT;
        public float   eCoT;
    }

    [NonSerialized] public readonly float innerDistance = 2f;

    private                         bool  isAttack;
    private                         bool  isThrow;
    private                         bool  isSkill;
    public                          bool  IsStuck { get; private set; } = true;

    private                 int  index;
    private                 int  length;

    private                 float damage       = 40;

    private                 bool  attackTrigger;

    [SerializeField] private LayerMask throwBlockLayer;
    [SerializeField] private float     minThrowDistance = 3f;
    [SerializeField] private float     maxThrowDistance = 30f;
    [SerializeField] private float     scrollSpeed      = 100f;
    private                  float     throwDistance;

    private                  Coroutine throwCoroutine;

    [SerializeField] private GameObject testAnchor;
    [SerializeField] private Transform  camTr;

    private void Awake()
    {
        anim        = GetComponentInChildren<Animator>();
        boxCollider = GetComponentInChildren<BoxCollider>();

        testAnchor.transform.parent = null;
    }

    private void Start()
    {
        camTr  = Camera.main.transform;
    }

    private void OnEnable()
    {
        anim.Play("Idle");
        boxCollider.enabled = false;
        IsStuck             = true;
    }

    private void OnDisable()
    {
        anim.ResetTrigger(AttackTrigger);
        anim.SetBool(ThrowBool, false);
    }

    private void Update()
    {
        if (IsStuck)
        {
            AttackerTrace();
        }
    }

    public void Setting(bool enter)
    {
        if (enter)
        {
            throwDistance = minThrowDistance;
            testAnchor.SetActive(true);
        }
        else
        {
            testAnchor.SetActive(false);
        }
    }

    private void SetStuck(bool stuck)
    {
        IsStuck = stuck;
        attacker.SetInnerSword();
    }

    private void AttackerTrace()
    {
        float delta = Time.smoothDeltaTime;
        float traceMoveSpeed = 30;
        float traceRotateSpeed = 30;

        var rotation = attacker.transform.rotation *
                       Quaternion.AngleAxis((float)index / length * 360, new Vector3(0, 1, 0));
        var position = rotation * Vector3.forward * innerDistance + attacker.transform.position;

        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, traceRotateSpeed * delta);
        transform.position = Vector3.Slerp(transform.position, position, traceMoveSpeed * delta);
    }

    public void SetInnerProperty(int i, int l)
    {
        index  = i;
        length = l;
    }

    public void Attack()
    {
        if (Input.GetMouseButtonDown(0) && !isThrow && !isSkill)
        {
            if (isAttack)
            {
                attackTrigger = true;
            }
            else
            {
                SetStuck(false);
                StartCoroutine(AttackSequence(0));
            }
        }
    }

    public void Throw()
    {
        throwDistance =
            Mathf.Clamp(throwDistance + Input.GetAxis("Mouse ScrollWheel") * Time.smoothDeltaTime * scrollSpeed,
                        minThrowDistance, maxThrowDistance);

        if (Physics.Raycast(camTr.position, camTr.forward, out var hit,
                            throwDistance, throwBlockLayer))
        {
            testAnchor.transform.position = hit.point;
        }
        else
        {
            testAnchor.transform.position =
                camTr.forward * throwDistance + camTr.position;
        }

        if (Input.GetMouseButtonDown(1) && !isThrow && !isAttack && !isSkill)
        {
            SetStuck(false);
            throwCoroutine =
                StartCoroutine(ThrowSequence(testAnchor.transform.position,
                                             Quaternion.LookRotation(
                                                 testAnchor.transform.position - transform.position)));
        }
    }

    public void Return()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isThrow && !isAttack && !isSkill && !IsStuck)
        {
            SetStuck(true);
        }
    }

    public void Skill()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isAttack && !isThrow && !isSkill)
        {
            SetStuck(false);
            StartCoroutine(SkillSequence());
        }
    }

    IEnumerator AttackSequence(int i)
    {
        isAttack = true;
        anim.SetTrigger(AttackTrigger);
        float animTime       = infos[i].t;
        float startTime      = infos[i].sT;
        float endTime        = infos[i].eT;
        float comboStartTime = infos[i].sCoT;
        float comboEndTime   = infos[i].eCoT;
        startTime      = startTime * animTime;
        endTime        = endTime * animTime - startTime;
        comboStartTime = comboStartTime * animTime - startTime - endTime;
        comboEndTime   = comboEndTime * animTime - startTime - endTime - comboStartTime;
        float remTime = .1f + animTime - (startTime + endTime + comboStartTime + comboEndTime);
        yield return new WaitForSeconds(startTime);
        boxCollider.enabled = true;
        yield return new WaitForSeconds(endTime);
        boxCollider.enabled = false;
        yield return new WaitForSeconds(comboStartTime);
        attackTrigger = false;
        yield return new WaitForSeconds(comboEndTime);
        if (attackTrigger && infos.Length - 1 > i)
        {
            StartCoroutine(AttackSequence(i + 1));
            yield break;
        }
        yield return new WaitForSeconds(remTime);
        isAttack        = false;
    }

    private IEnumerator ThrowSequence(Vector3 targetPosition, Quaternion targetRotation)
    {
        float speed       = 30f;
        float rotateSpeed = 360f;
        StartCoroutine(Utill.Execute(quat => transform.rotation = quat, transform.rotation, targetRotation,
                                     Quaternion.Angle(transform.rotation, targetRotation) / rotateSpeed));

        isThrow = true;
        anim.SetBool(ThrowBool, true);

        yield return new WaitForSeconds(1.2f);

        boxCollider.enabled = true;

        yield return throwCoroutine = StartCoroutine(Utill.Execute(vec => transform.position = vec, transform.position,
                                                                   targetPosition,
                                                                   Vector3.Distance(
                                                                       transform.position, targetPosition) /
                                                                   speed));
        throwCoroutine = null;

        anim.SetBool(ThrowBool, false);
        StartCoroutine(Utill.Execute(.1f, () => isThrow = false));
    }

    private IEnumerator SkillSequence()
    {
        isSkill = true;
        float rotateSpeed = 360f;
        
        StartCoroutine(Utill.Execute(quat => transform.rotation = quat, transform.rotation, Quaternion.Euler(Vector3.up * transform.eulerAngles.y),
                                     Quaternion.Angle(transform.rotation, Quaternion.Euler(Vector3.up * transform.eulerAngles.y)) / rotateSpeed));
        anim.SetTrigger(SkillTrigger);
        float animTime     = 1.05f;
        float attackTiming = .46f * animTime;
        yield return new WaitForSeconds(attackTiming);
        boxCollider.enabled = true;
        yield return new WaitForSeconds(animTime - attackTiming + .1f);
        boxCollider.enabled = false;
        isSkill             = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isThrow)
        {
            if (throwCoroutine == null) return;
            StopCoroutine(throwCoroutine);
            anim.SetBool(ThrowBool, false);
            StartCoroutine(Utill.Execute(.1f, () => isThrow = false));
        }
        else if (other.CompareTag("Player"))
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