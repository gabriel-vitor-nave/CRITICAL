using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPulsingDarkness : MonoBehaviour
{
    [Header("Configurações")]
    [Range(0f, 1f)]
    public float escurecimento = 0.15f;

    public float velocidade = 1f;

    private Image imagem;
    private Color corOriginal;

    void Start()
    {
        imagem = GetComponent<Image>();

        if (imagem != null)
        {
            corOriginal = imagem.color;
        }
    }

    void Update()
    {
        if (imagem == null)
            return;

        float pulso = (Mathf.Sin(Time.time * velocidade) + 1f) / 2f;

        imagem.color = Color.Lerp(
            corOriginal,
            corOriginal * (1f - escurecimento),
            pulso
        );
    }
}

