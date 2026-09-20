using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverAnimation : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Tamanho")]
    public float tamanhoNormal = 1f;
    public float tamanhoHover = 1.1f;
    public float tamanhoClique = 0.95f;

    [Header("Velocidade")]
    public float velocidade = 10f;

    private Vector3 escalaAlvo;

    void Start()
    {
        escalaAlvo = Vector3.one * tamanhoNormal;
        transform.localScale = escalaAlvo;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            escalaAlvo,
            Time.deltaTime * velocidade
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaAlvo = Vector3.one * tamanhoHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaAlvo = Vector3.one * tamanhoNormal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        escalaAlvo = Vector3.one * tamanhoClique;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        escalaAlvo = Vector3.one * tamanhoHover;
    }
}