using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoundEffect
{
    SwordHit,
    SwordSwing
}

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager                       Instance;
    private       AudioSource                        audioSource;
    private       Dictionary<SoundEffect, AudioClip> sfxDic;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);   
        }

        audioSource = GetComponent<AudioSource>();

        sfxDic = new Dictionary<SoundEffect, AudioClip>();
        var clips = Resources.LoadAll<AudioClip>($"Sound/SFX/");
        foreach (var clip in clips)
        {
            Debug.Log("Clip Detect");
            if (Enum.TryParse(clip.name, out SoundEffect name))
            {
                Debug.Log(clip.name);
                sfxDic.Add(name, clip);
            }
            else
            {
                Debug.Log("Add Dictionary Fail");
            }
        }
    }

    private void Start()
    {
        ResetBGM();
    }

    public void ResetBGM()
    {
        if (SceneManager.GetActiveScene().name == "Title")
        {
            Title();
        }
        else if (SceneManager.GetActiveScene().name == "Waiting Room")
        {
            WaitingRoom();
        }
        else
        {
            Battle();
        }
    }

    public void Title()
    {
        audioSource.volume = 1f;
        PlayBGM(Resources.Load<AudioClip>("Sound/BGM/Title"));
    }

    public void WaitingRoom()
    {
        audioSource.volume = .4f;
        PlayBGM(Resources.Load<AudioClip>("Sound/BGM/Waiting Room"));
    }

    public void Battle()
    {
        audioSource.volume = .5f;
        PlayBGM(Resources.Load<AudioClip>("Sound/BGM/Battle"));
    }

    public void PlayBGM(AudioClip audioClip)
    {
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void PlayOneShot(SoundEffect sfx)
    {
        audioSource.PlayOneShot(sfxDic[sfx], .8f);
        Debug.Log("Play");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            PlayOneShot(SoundEffect.SwordHit);
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            PlayOneShot(SoundEffect.SwordSwing);
        }
    }
}
