using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour 
{
    protected SoundManager() { }
    public static SoundManager instance = null;

    public static SoundManager Instance
    {
        get
        {
            if (SoundManager.instance == null)
            {
                DontDestroyOnLoad(SoundManager.instance);
                SoundManager.instance = new SoundManager();
            }
            return SoundManager.instance;
        }
    }

    public AudioClip[] MusicClips;

    public float masterValue;
    public float musicValue;
    public float fxValue;

    AudioSource musicSource;
    AudioSource sfxSource;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        else if (instance != this)
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);

        musicSource = transform.GetChild(0).GetComponent<AudioSource>();
        sfxSource = transform.GetChild(1).GetComponent<AudioSource>();

        LoadMusicValues();
    }

    public void OnApplicationQuit()
    {
        SoundManager.instance = null;
    }


    public void LoadMusicValues()
    {
        musicSource.volume = musicValue * masterValue;
        sfxSource.volume = fxValue * masterValue;
    }

    public void PlayMusic(int musicSelection)
    {
        musicSource.clip = MusicClips[musicSelection];
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public void PlaySFXSound(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    public void SetVolume()
    {
        musicSource.volume = musicValue * masterValue;
        sfxSource.volume = fxValue * masterValue;
    }
}
