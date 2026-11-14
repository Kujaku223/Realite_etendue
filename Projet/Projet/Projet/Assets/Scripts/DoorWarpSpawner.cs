using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Spawns one warp cube when the door's padlock unlocks.</summary>
[DisallowMultipleComponent]
public sealed class DoorWarpSpawner : MonoBehaviour
{
    [SerializeField] GreenCubeWarp m_CubePrefab;
    [SerializeField] Transform m_SpawnPoint;
    bool m_Spawned;

    public void SpawnOnce()
    {
        if (m_Spawned)
            return;
        if (m_CubePrefab == null || m_SpawnPoint == null)
        {
            Debug.LogError("DoorWarpSpawner needs a Cube Prefab and Spawn Point.", this);
            return;
        }

        GreenCubeWarp cube = Instantiate(m_CubePrefab, m_SpawnPoint.position, m_SpawnPoint.rotation);
        SceneManager.MoveGameObjectToScene(cube.gameObject, gameObject.scene);
        m_Spawned = true;
    }
}
