using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingUI : MonoBehaviour
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
    [Tooltip("Tempo mínimo antes de escolher um novo movimento.")]
    public float tempoMinimo = 1.5f;

    [Tooltip("Tempo máximo antes de escolher um novo movimento.")]
    public float tempoMaximo = 3.5f;

    private RectTransform rectTransform;
    private Vector2 posicaoInicial;
    private Vector2 destino;

    private float tempoMovimento;
    private float contadorTempo;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        posicaoInicial = rectTransform.anchoredPosition;
        destino = posicaoInicial;

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
        float movimento = Mathf.Sin(Time.time * velocidade) * distancia;

        Vector2 novaPosicao = posicaoInicial;
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

        // Move suavemente em direção ao destino
        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            destino,
            Time.deltaTime * suavidade
        );

        // Quando o tempo terminar, escolhe uma nova direção
        if (contadorTempo >= tempoMovimento)
        {
            NovoDestino();
        }
    }

    void NovoDestino()
    {
        destino = posicaoInicial + Random.insideUnitCircle * distancia;

        // Escolhe aleatoriamente quanto tempo ficará nesse movimento
        tempoMovimento = Random.Range(tempoMinimo, tempoMaximo);

        contadorTempo = 0f;
    }
}
