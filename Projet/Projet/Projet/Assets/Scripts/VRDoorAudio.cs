using UnityEngine;

/// <summary>Each door owns its source; the shared profile supplies the replaceable creak.</summary>
public static class VRDoorAudio
{
    public static void Play(ref AudioSource source, GameObject door)
    {
        var profile = VRFeedbackProfile.Default;
        if (profile == null || profile.doorCreakClip == null || door == null)
            return;
        if (source == null)
        {
            source = door.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 1f;
            source.maxDistance = 15f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
        source.Stop();
        source.clip = profile.doorCreakClip;
        source.volume = profile.doorCreakVolume;
        source.Play();
    }

    public static void Stop(AudioSource source)
    {
        if (source != null) source.Stop();
    }

    public static void Release(AudioSource source)
    {
        if (source != null) Object.Destroy(source);
    }
}
