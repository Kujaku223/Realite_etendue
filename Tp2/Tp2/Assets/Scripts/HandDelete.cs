using UnityEngine;

public class HandCursorDelete : MonoBehaviour
{
    [SerializeField] private RectTransform cursor;
    [SerializeField] private Camera arCamera;
    [SerializeField] private Canvas cursorCanvas;

    [Tooltip("Parent direct des objets créés : Object Spawner si Spawn As Children est coché.")]
    [SerializeField] private Transform spawnedObjectsParent;

    private void LateUpdate()
    {
        if (cursor == null || arCamera == null ||
            cursorCanvas == null || spawnedObjectsParent == null)
        {
            return;
        }

        if (!cursor.gameObject.activeInHierarchy)
            return;

        // Convertir la position du curseur UI en position écran.
        Camera uiCamera =
            cursorCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : cursorCanvas.worldCamera;

        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(uiCamera, cursor.position);

        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Transform target = hit.collider.transform;

        // Ignorer les objets qui ne proviennent pas du spawner.
        if (target == spawnedObjectsParent ||
            !target.IsChildOf(spawnedObjectsParent))
        {
            return;
        }

        // Remonter jusqu'à la racine du prefab placé,
        // sans remonter jusqu'au spawner ou au rig AR.
        while (target.parent != spawnedObjectsParent)
        {
            target = target.parent;
        }

        Destroy(target.gameObject);
    }
}