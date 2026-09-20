using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;

public class DaggerHoverCycle : MonoBehaviour
{
    [Header("Botões")]
    public GameObject[] botoes;

    [Header("Posição do Meio")]
    public Vector2 posicaoMeio;
    public float rotacaoMeio = 0f;

    [Header("Posição da Esquerda")]
    public Vector2 posicaoEsquerda;
    public float rotacaoEsquerda = 15f;

    [Header("Posição da Direita")]
    public Vector2 posicaoDireita;
    public float rotacaoDireita = -15f;

    [Header("Animação")]
    public float velocidade = 8f;

    private int estadoAtual = 0;

    private Vector2 destinoPosicao;
    private float destinoRotacao;

    private int ultimoBotaoHover = -1;

    void Start()
    {
        destinoPosicao = posicaoMeio;
        destinoRotacao = rotacaoMeio;

        transform.localPosition = posicaoMeio;
        transform.localRotation = Quaternion.Euler(0f, 0f, rotacaoMeio);
    }

    void Update()
    {
        VerificarBotoes();

        transform.localPosition = Vector2.Lerp(
            transform.localPosition,
            destinoPosicao,
            Time.deltaTime * velocidade
        );

        Quaternion rotacao = Quaternion.Euler(
            0f,
            0f,
            destinoRotacao
        );

        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            rotacao,
            Time.deltaTime * velocidade
        );
    }

    void VerificarBotoes()
    {
        for (int i = 0; i < botoes.Length; i++)
        {
            if (botoes[i] == null)
                continue;

            RectTransform rect = botoes[i].GetComponent<RectTransform>();

            if (rect == null)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                Input.mousePosition,
                null))
            {
                if (ultimoBotaoHover != i)
                {
                    ultimoBotaoHover = i;
                    AvancarEstado();
                }

                return;
            }
        }

        ultimoBotaoHover = -1;
    }

    void AvancarEstado()
    {
        estadoAtual++;

        // Sequência:
        // 0 = Meio
        // 1 = Direita
        // 2 = Meio
        // 3 = Esquerda
        // 4 = Meio
        // 5 = Direita...

        if (estadoAtual > 3)
        {
            estadoAtual = 0;
        }

        switch (estadoAtual)
        {
            case 0:
                destinoPosicao = posicaoMeio;
                destinoRotacao = rotacaoMeio;
                break;

            case 1:
                destinoPosicao = posicaoDireita;
                destinoRotacao = rotacaoDireita;
                break;

            case 2:
                destinoPosicao = posicaoMeio;
                destinoRotacao = rotacaoMeio;
                break;

            case 3:
                destinoPosicao = posicaoEsquerda;
                destinoRotacao = rotacaoEsquerda;
                break;
        }
    }
}