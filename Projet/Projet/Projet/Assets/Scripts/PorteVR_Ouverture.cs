using UnityEngine;
using Unity.XR.CoreUtils;

public class PorteVR : MonoBehaviour
{
    [SerializeField] float m_AngleOuvert = 90f;
    [SerializeField] float m_VitesseDegres = 140f;
    [Header("Passage (dimensions dans le repere parent)")]
    [SerializeField, Min(0.01f)] float m_LargeurPassage = 1.16f;
    [SerializeField, Min(0.01f)] float m_HauteurPassage = 2.38f;
    [SerializeField, Min(0f)] float m_MargePassage = 0.05f;

    Transform m_Repere;
    Quaternion m_RotationFermee;
    float m_Angle;
    bool m_EnMouvement;
    float m_DerniereCible;
    AudioSource m_Creak;

    void Awake()
    {
        m_Repere = transform.parent != null ? transform.parent : transform;
        m_RotationFermee = transform.localRotation;
    }

    void OnDisable()
    {
        m_EnMouvement = false;
        VRDoorAudio.Stop(m_Creak);
    }

    void OnDestroy() => VRDoorAudio.Release(m_Creak);

    void FixedUpdate()
    {
        // Player fit is the only opening condition, independent of distance or input.
        float cible = PassageSuffisant() ? m_AngleOuvert : 0f;
        bool mouvement = !Mathf.Approximately(m_Angle, cible);
        if (mouvement && (!m_EnMouvement || !Mathf.Approximately(cible, m_DerniereCible)))
            VRDoorAudio.Play(ref m_Creak, gameObject);
        m_EnMouvement = mouvement;
        m_DerniereCible = cible;
        m_Angle = Mathf.MoveTowards(m_Angle, cible, m_VitesseDegres * Time.fixedDeltaTime);
        transform.localRotation = m_RotationFermee * Quaternion.Euler(0f, m_Angle, 0f);
    }

    bool PassageSuffisant()
    {
        Camera tete = Camera.main;
        if (tete == null) return false;
        var origine = tete.GetComponentInParent<XROrigin>();
        if (origine == null) return false;
        var corps = origine.GetComponentInChildren<CharacterController>();
        float hauteur = Mathf.Max(0f, Vector3.Dot(tete.transform.position - origine.transform.position, Vector3.up));
        float largeur = 0.3f;
        if (corps != null)
        {
            hauteur = Mathf.Max(hauteur, corps.height * Mathf.Abs(corps.transform.lossyScale.y));
            largeur = 2f * corps.radius * Mathf.Max(Mathf.Abs(corps.transform.lossyScale.x),
                Mathf.Abs(corps.transform.lossyScale.z));
        }
        float hauteurPorte = m_HauteurPassage * m_Repere.TransformVector(Vector3.up).magnitude;
        float largeurPorte = m_LargeurPassage * m_Repere.TransformVector(Vector3.right).magnitude;
        return hauteurPorte >= hauteur + m_MargePassage && largeurPorte >= largeur + m_MargePassage;
    }
}
