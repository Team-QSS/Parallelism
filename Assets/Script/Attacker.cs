using System.Collections.Generic;
using Cinemachine;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class Attacker : NetworkBehaviour
{
    public Transform moverTransform;

    [Header("Camera")] [SerializeField] private Vector2                 mouseSensitivity;

    [Header("Sword")] [SerializeField] private int swordCount;

    private static readonly List<Sword> Swords = new ();
    private                 Sword       currentSword;

    private void Awake()
    {
        Debug.Log("awake");
        if (NetworkManager.Singleton.IsHost)
        {
            var swordPrefab = Resources.Load<Sword>("Sword");
            
            for (var i = 0; i < swordCount; i++)
            {
                var sword = Instantiate(swordPrefab,transform.position,quaternion.identity);
                var sw = sword.GetComponent<Sword>();
                sw.attacker = this;
                sw.camTr = transform.GetComponentInChildren<CinemachineVirtualCamera>().transform;
                //sw.enabled = true;
                
                sword.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
                
            }
        }
        
        if (IsOwner)
        {
            SetInnerSword();
            Select(0);
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    [ClientRpc]
    private void SwordClientRpc(int HashCode)
    {
        var swords = GameObject.FindGameObjectsWithTag("Sword");
        foreach (var sword in swords)
        {
            if (sword.GetComponent<NetworkObject>().IsOwner)
            {
                var sw = sword.GetComponent<Sword>();
                Swords.Add(sw);
            }
        }
    }

    private void Update()
    {
        if (moverTransform is null || !IsOwner || Swords.Count == 0)
        {
            return;
        }
        
        float smoothDelta = Time.smoothDeltaTime;
        
        MoverTrace(smoothDelta);
        CameraRotate(smoothDelta);
        
        //현재 선택된 Sword컴포넌트 기능들 호출
        if (currentSword)
        {
            currentSword.Attack();
            currentSword.Throw();
            currentSword.Return();
            currentSword.Skill();

            if (!currentSword.gameObject.activeSelf)
            {
                Select(Swords.FindIndex(e => e.IsStuck));
            }
        }
        
        SelectInput();
    }

    //Sword 선택 입력
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

    //Sword 선택
    private void Select(int index)
    {
        if (currentSword)
        {
            if (Swords.FindIndex(e => e == currentSword) == index) return;
            currentSword.Setting(false);
        }
        if (index == -1)
        {
            currentSword = null;
            return;
        }

        
        currentSword = Swords[index];
        currentSword.Setting(true);
        if (currentSword.IsStuck)
        {
            SetInnerSword();
        }
    }

    //붙어있는 Sword 설정
    public void SetInnerSword()
    {
        var       selected     = currentSword && currentSword.IsStuck ? Swords.FindIndex(e => e == currentSword) : 0;
        List<int> innerIndexes = new List<int>();
        foreach (var sword in Swords)
        {
            if (sword.IsStuck)
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

    #region Camera

    //Player1 - Move 따라가는 것
    private void MoverTrace(float delta)
    {
        var targetPosition = moverTransform.position;
        transform.position = Vector3.Lerp(transform.position, targetPosition, 10 * delta);
    }

    //카메라 회전
    private void CameraRotate(float delta)
    {
        var x           = -Input.GetAxis("Mouse Y") * mouseSensitivity.y * delta;
        var y           = Input.GetAxis("Mouse X") * mouseSensitivity.x * delta;
        var transform1  = transform;
        var eulerAngles = transform1.eulerAngles;
        x += eulerAngles.x;
        y += eulerAngles.y;
        if (x is > 65 and < 295)
            x = x > 180 ? 295 : 65;
        transform.rotation = Quaternion.Euler(x, y, 0);
    }

    #endregion
}