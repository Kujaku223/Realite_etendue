using UnityEngine;

public class HandCursor : MonoBehaviour
{
    [SerializeField]
    HandTracker m_Tracker;

    [SerializeField]
    RectTransform m_Cursor;

    void Update()
    {
        if (m_Tracker.HandDetected)
        {
            m_Cursor.gameObject.SetActive(true);
            m_Cursor.position = m_Tracker.KeypointsScreen[HandTracker.IndexTip];
        }
        else
        {
            m_Cursor.gameObject.SetActive(false);
        }
    }
}
