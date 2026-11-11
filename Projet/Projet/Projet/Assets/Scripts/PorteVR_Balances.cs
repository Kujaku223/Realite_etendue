using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class PorteVR_Balance : MonoBehaviour
{
    [SerializeField] float m_AngleOuvert = 90f;
    [SerializeField] float m_VitesseDegres = 140f;

    [Header("Verrouillage")]
    [SerializeField] bool m_Verrouillee = true;          // verrouillée jusqu'à la résolution du puzzle
    [SerializeField] UnityEvent m_QuandDeverrouillee;     // ex. son de "clic"
    [SerializeField] UnityEvent m_QuandEssaiVerrouillee;  // ex. son "verrouillé" quand le joueur essaie

    XRSimpleInteractable m_Interactable;
    Quaternion m_RotationFermee;
    float m_Angle;
    bool m_Ouverte;

    void Awake()
    {
        m_Interactable = GetComponent<XRSimpleInteractable>();
        m_RotationFermee = transform.localRotation;
    }

    void OnEnable() => m_Interactable.selectEntered.AddListener(_ => TenterBasculer());

    void OnDisable() => m_Interactable.selectEntered.RemoveAllListeners();

    // À brancher sur PuzzleManager > On All Solved ()
    public void Deverrouiller()
    {
        if (!m_Verrouillee) return;
        m_Verrouillee = false;
        m_Ouverte = true; // ouvre immédiatement
        m_QuandDeverrouillee?.Invoke();
    }

    // Optionnel : à brancher sur PuzzleManager > On No Longer Solved ()
    public void Verrouiller()
    {
        m_Verrouillee = true;
        m_Ouverte = false;
    }

    void TenterBasculer()
    {
        if (m_Verrouillee)
        {
            m_QuandEssaiVerrouillee?.Invoke();
            return;
        }
        m_Ouverte = !m_Ouverte;
    }

    void Update()
    {
        foreach (var interacteur in m_Interactable.interactorsHovering)
        {
            if (interacteur is XRBaseInputInteractor entree && entree.activateInput.ReadWasPerformedThisFrame())
            {
                TenterBasculer();
                break;
            }
        }

        float cible = m_Ouverte ? m_AngleOuvert : 0f;
        m_Angle = Mathf.MoveTowards(m_Angle, cible, m_VitesseDegres * Time.deltaTime);
        transform.localRotation = m_RotationFermee * Quaternion.Euler(0f, m_Angle, 0f);
    }
}