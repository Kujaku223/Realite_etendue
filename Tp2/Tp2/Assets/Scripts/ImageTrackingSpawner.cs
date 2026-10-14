using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackingSpawner : MonoBehaviour
{
    [Serializable]
    public struct ImagePrefabPair
    {
        public string imageName;
        public GameObject prefab;
    }

    [SerializeField]
    List<ImagePrefabPair> m_ImagePrefabs = new();

    ARTrackedImageManager m_TrackedImageManager;
    readonly Dictionary<string, GameObject> m_PrefabsByName = new();
    readonly Dictionary<TrackableId, GameObject> m_SpawnedContent = new();

    void Awake()
    {
        m_TrackedImageManager = GetComponent<ARTrackedImageManager>();

        foreach (var pair in m_ImagePrefabs)
        {
            if (pair.prefab != null && !string.IsNullOrEmpty(pair.imageName))
                m_PrefabsByName[pair.imageName] = pair.prefab;
        }
    }

    void OnEnable() => m_TrackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);

    void OnDisable() => m_TrackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);

    void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var trackedImage in args.added)
            SpawnContent(trackedImage);

        foreach (var trackedImage in args.updated)
        {
            if (m_SpawnedContent.TryGetValue(trackedImage.trackableId, out var content))
                UpdateContentVisibility(trackedImage, content);
        }

        foreach (var removed in args.removed)
        {
            if (m_SpawnedContent.TryGetValue(removed.Key, out var content))
            {
                Destroy(content);
                m_SpawnedContent.Remove(removed.Key);
            }
        }
    }

    void SpawnContent(ARTrackedImage trackedImage)
    {
        var imageName = trackedImage.referenceImage.name;
        if (!m_PrefabsByName.TryGetValue(imageName, out var prefab))
            return;

        var content = Instantiate(prefab, trackedImage.transform);
        m_SpawnedContent[trackedImage.trackableId] = content;
    }

    void UpdateContentVisibility(ARTrackedImage trackedImage, GameObject content)
    {
        content.SetActive(trackedImage.trackingState == TrackingState.Tracking);
    }
}
