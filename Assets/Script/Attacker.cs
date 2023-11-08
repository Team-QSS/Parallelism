using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class Attacker : MonoBehaviour
{
    [SerializeField] private Transform attackerAnchor;
    public                   Transform swordAnchor;

    [Header("Camera")] [SerializeField] private Vector2                 mouseSensitivity;
    private                                     CinemachineCameraOffset cineOffset;

    private float   MouseSensitivityRatio => currentSword && currentSword.IsAction ? .1f : 1f;
    private Vector2 MouseSensitivity      => mouseSensitivity * MouseSensitivityRatio;

    [Header("Sword")] [SerializeField] private int swordCount;

    private static readonly List<Sword> Swords = new List<Sword>();
    private                 Sword       currentSword;

    private void Awake()
    {
        cineOffset = GetComponentInChildren<CinemachineCameraOffset>();

        var swordPrefab = Resources.Load<Sword>("Sword");
        for (int i = 0; i < swordCount; i++)
        {
            var sword = Instantiate(swordPrefab);
            sword.attacker = this;
            Swords.Add(sword);
        }

        SetInnerSword();
        Select(0);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Update()
    {
        float smoothDelta = Time.smoothDeltaTime;

        CameraTrace(smoothDelta);
        CameraRotate(smoothDelta);

        if (currentSword)
        {
            if (!currentSword.IsInside)
            {
                swordAnchor.position = attackerAnchor.position;
            }

            if (!currentSword.IsAction)
            {
                AttackInput();
                ThrowInput();
            }
            
            if (!currentSword.gameObject.activeSelf)
            {
                Select(Swords.FindIndex(e => e.IsInside));
            }
        }
        if (!currentSword || !currentSword.IsAction)
        {
            SelectInput();
        }

        Test();
    }

    private void Test()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Time.timeScale = Time.timeScale - 1 != 0 ? 1f : 0.1f;
        }
    }

    private void AttackInput()
    {
        if (Input.GetMouseButtonDown(0) && !currentSword.IsAction)
        {
            currentSword.Attack();
        }
    }

    private void ThrowInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (currentSword.IsInside)
            {
                currentSword.Throw();
                SetInnerSword();
                StartCoroutine(Wait(() => !currentSword.IsAction, () => { Select(Swords.FindIndex(e => e.IsInside)); }));
            }
            else if (currentSword.Throw())
            {
                
            }
        }
    }

    private void SelectInput()
    {
        for (int i = 0; i < swordCount; i++)
        {
            if (Input.GetKeyDown((KeyCode)i + 49))
            {
                if (Swords[i].gameObject.activeSelf) Select(i);
            }
        }
    }

    public void Hit()
    {
        
    }

    private void Select(int index)
    {
        if (index == -1)
        {
            currentSword = null;
            return;
        }

        currentSword = Swords[index];
        if (currentSword.IsInside)
        {
            SetInnerSword();
            swordAnchor.parent        = transform;
            swordAnchor.localPosition = default;
            swordAnchor.rotation      = default;
        }
        else
        {
            swordAnchor.parent   = null;
            swordAnchor.rotation = Quaternion.Euler(0, swordAnchor.eulerAngles.y, 0);
        }

        CameraSetting();
    }

    private void SetInnerSword()
    {
        var       selected     = currentSword && currentSword.IsInside ? Swords.FindIndex(e => e == currentSword) : 0;
        List<int> innerIndexes = new List<int>();
        foreach (var sword in Swords)
        {
            if (sword.IsInside)
            {
                innerIndexes.Add(Swords.FindIndex(e => e == sword));
            }
        }

        bool check = false;
        for (int i = 0; i < innerIndexes.Count; i++)
        {
            var index = check ? i : i + 1;
            if (selected == innerIndexes[i])
            {
                check = true;
                index = 0;
            }

            Swords[innerIndexes[i]].SetInnerProperty(index, innerIndexes.Count);
        }
    }

    public void RespawnSword(Sword sword)
    {
        StartCoroutine(Delay(sword));
    }

    IEnumerator Delay(Sword sword)
    {
        float respawnTime = 3f;
        yield return new WaitForSeconds(respawnTime);
        sword.gameObject.SetActive(true);
        if (currentSword)
        {
            SetInnerSword();
        }
        else
        {
            Select(Swords.FindIndex(e => e == sword));
        }
    }

    #region Camera

    private void CameraSetting()
    {
        if (currentSword && !currentSword.IsInside)
        {
            cineOffset.m_Offset = new Vector3(0, 0, -Swords[0].innerDistance) + -Swords[0].innerOffset;
        }
        else
        {
            cineOffset.m_Offset = default;
        }
    }

    private void CameraTrace(float delta)
    {
        var targetPosition = attackerAnchor.position;
        if (currentSword && !currentSword.IsInside)
        {
            targetPosition = currentSword.transform.position;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, 10 * delta);
    }

    private void CameraRotate(float delta)
    {
        var x           = -Input.GetAxis("Mouse Y") * MouseSensitivity.y * delta;
        var y           = Input.GetAxis("Mouse X") * MouseSensitivity.x * delta;
        var transform1  = transform;
        var eulerAngles = transform1.eulerAngles;
        x += eulerAngles.x;
        y += eulerAngles.y;
        if (x is > 50 and < 310)
            x = x > 180 ? 310 : 50;
        transform.rotation = Quaternion.Euler(x, y, 0);
    }

    #endregion

    IEnumerator Wait(Func<bool> condition, Action endAction)
    {
        yield return new WaitUntil(condition);
        endAction();
    }
}