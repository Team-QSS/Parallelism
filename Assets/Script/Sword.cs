using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Sword : NetworkBehaviour
{
    public                   Attacker        attacker;
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

    private                 float damage       = 20;

    private                 bool  attackTrigger;

    [SerializeField] private LayerMask throwBlockLayer;
    [SerializeField] private float     minThrowDistance = 3f;
    [SerializeField] private float     maxThrowDistance = 30f;
    [SerializeField] private float     scrollSpeed      = 100f;
    private                  float     throwDistance;

    private                  Coroutine throwCoroutine;

    private float attackDelay = .5f;
    private float timer;

    [SerializeField] private GameObject testAnchor;
    public Transform  camTr;

    private void Awake()
    {
        anim        = GetComponentInChildren<Animator>();
        boxCollider = GetComponentInChildren<BoxCollider>();

        testAnchor.transform.parent = null;
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
        if (IsStuck && IsOwner)
        {
            AttackerTrace();
        }
    }

    //선택 시 활성화
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

    //Attacker에 붙이기
    public void SetStuck(bool stuck)
    {
        IsStuck = stuck;
        attacker.SetInnerSword();
    }

    //Player2 - Attacker에 붙어있을 때 따라가는 기능
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

    //붙어있을 때 정보 셋팅
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
        SoundManager.Instance.PlayOneShot(SoundEffect.SwordSwing);
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
        SoundManager.Instance.PlayOneShot(SoundEffect.SwordSwing);
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
        SoundManager.Instance.PlayOneShot(SoundEffect.SwordSwing);
        // Debug.Log("collider on");
        yield return new WaitForSeconds(animTime - attackTiming + .1f);
        // Debug.Log("collider off");
        boxCollider.enabled = false;
        isSkill             = false;
    }

    //적중 시
    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log(other.gameObject.name);
        // Debug.Log(other.gameObject.tag);
        // Debug.Log(attacker.red);
        if (isThrow)
        {
            if (throwCoroutine == null) return;
            StopCoroutine(throwCoroutine);
            anim.SetBool(ThrowBool, false);
            StartCoroutine(Utill.Execute(.1f, () => isThrow = false));
        }
        
        if (attacker.red)
        {
            if (!other.CompareTag("MoverPlayerBlue")) return;
            // Debug.Log("red");
        }
        else
        {
            if (!other.CompareTag("MoverPlayerRed")) return;
            // Debug.Log("blue");
        }
        var hit = other.GetComponentInChildren<IHit>();
        if (hit != null)
        {
            if (timer + attackDelay > Time.time) return;
            timer = Time.time;
            // Debug.Log("Hit Target");
            SoundManager.Instance.PlayOneShot(SoundEffect.SwordHit);
            //공격자
            hit.Hit(damage);
        }
        
        HitServerRPC(Vector3.Lerp(transform.position, other.transform.position, .5f));
    }

    [ServerRpc (RequireOwnership = false)]
    private void HitServerRPC(Vector3 position)
    {
        HitClientRPC(position);
    }
    
    [ClientRpc]
    private void HitClientRPC(Vector3 position)
    {
        var vfx = Instantiate(Resources.Load<ParticleSystem>("Blood"), position, Quaternion.identity);
        vfx.Play();
        Destroy(vfx, 2f);
    }
}