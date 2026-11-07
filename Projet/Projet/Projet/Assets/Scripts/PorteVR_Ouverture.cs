using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class PorteVR : MonoBehaviour
{
    [SerializeField] float m_AngleOuvert = 90f;
    [SerializeField] float m_VitesseDegres = 140f;
    [SerializeField] float m_DistanceOuverture = 3f;
    [SerializeField] float m_DistanceFermeture = 4.5f;

    XRSimpleInteractable m_Interactable;
    Quaternion m_RotationFermee;
    Vector3 m_Centre;
    float m_Angle;
    bool m_Ouverte;
    bool m_Proche;

    void Awake()
    {
        m_Interactable = GetComponent<XRSimpleInteractable>();
        m_RotationFermee = transform.localRotation;
        m_Centre = transform.position - transform.right * 0.58f;
    }

    void OnEnable() => m_Interactable.selectEntered.AddListener(_ => m_Ouverte = !m_Ouverte);

    void OnDisable() => m_Interactable.selectEntered.RemoveAllListeners();

    void Update()
    {
        foreach (var interacteur in m_Interactable.interactorsHovering)
        {
            if (interacteur is XRBaseInputInteractor entree && entree.activateInput.ReadWasPerformedThisFrame())
            {
                m_Ouverte = !m_Ouverte;
                break;
            }
        }

        DetecterJoueur();

        float cible = m_Ouverte ? m_AngleOuvert : 0f;
        m_Angle = Mathf.MoveTowards(m_Angle, cible, m_VitesseDegres * Time.deltaTime);
        transform.localRotation = m_RotationFermee * Quaternion.Euler(0f, m_Angle, 0f);
    }

    void DetecterJoueur()
    {
        Camera tete = Camera.main;
        if (tete == null)
            return;

        Vector3 ecart = tete.transform.position - m_Centre;
        ecart.y = 0f;
        float distance = ecart.magnitude;

        if (!m_Proche && distance < m_DistanceOuverture)
        {
            m_Proche = true;
            m_Ouverte = true;
        }
        else if (m_Proche && distance > m_DistanceFermeture)
        {
            m_Proche = false;
            m_Ouverte = false;
        }
    }
}
