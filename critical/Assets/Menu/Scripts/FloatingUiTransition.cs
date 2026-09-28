using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingUiTransition : MonoBehaviour
{
    public enum ModoFlutuar
    {
        Vertical,
        Aleatorio
    }

    [Header("Configurações")]
    public ModoFlutuar modo = ModoFlutuar.Vertical;

    [Tooltip("Distância máxima que a imagem pode se mover.")]
    public float distancia = 20f;

    [Tooltip("Velocidade da flutuação.")]
    public float velocidade = 1f;

    [Tooltip("Suavidade da movimentação.")]
    public float suavidade = 2f;

    [Header("Tempo Aleatório")]
    public float tempoMinimo = 1.5f;
    public float tempoMaximo = 3.5f;

    // NOVO:
    // Permite que outro script controle a posição base
    public Vector2 PosicaoBase
    {
        get => posicaoBase;
        set => posicaoBase = value;
    }

    private RectTransform rectTransform;

    private Vector2 posicaoInicial;
    private Vector2 posicaoBase;
    private Vector2 destino;

    private float tempoMovimento;
    private float contadorTempo;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        posicaoInicial = rectTransform.anchoredPosition;

        // A posição base começa sendo a posição original
        posicaoBase = posicaoInicial;

        destino = posicaoBase;

        if (modo == ModoFlutuar.Aleatorio)
        {
            NovoDestino();
        }
    }

    void Update()
    {
        if (modo == ModoFlutuar.Vertical)
        {
            FlutuarVertical();
        }
        else
        {
            FlutuarAleatorio();
        }
    }

    void FlutuarVertical()
    {
        float movimento =
            Mathf.Sin(Time.time * velocidade) * distancia;

        Vector2 novaPosicao = posicaoBase;

        novaPosicao.y += movimento;

        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            novaPosicao,
            Time.deltaTime * suavidade
        );
    }

    void FlutuarAleatorio()
    {
        contadorTempo += Time.deltaTime;

        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            destino,
            Time.deltaTime * suavidade
        );

        if (contadorTempo >= tempoMovimento)
        {
            NovoDestino();
        }
    }

    void NovoDestino()
    {
        destino =
            posicaoBase +
            Random.insideUnitCircle * distancia;

        tempoMovimento =
            Random.Range(tempoMinimo, tempoMaximo);

        contadorTempo = 0f;
    }
}