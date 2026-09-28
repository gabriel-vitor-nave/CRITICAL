using UnityEngine;
using System.Collections;

public class MenuTransition : MonoBehaviour
{
    [Header("TELAS")]
    public RectTransform menuPrincipal;
    public RectTransform menuOptions;

    [Header("TÍTULOS")]
    public RectTransform tituloJogo;
    public RectTransform tituloOptions;

    [Header("IMAGEM QUE DESCE")]
    public RectTransform imagemExtra;
    public float distanciaImagemExtra = 500f;

    [Header("BOTÕES DO MENU PRINCIPAL")]
    public RectTransform botoesPrincipal;

    [Header("BOTÃO VOLTAR")]
    public RectTransform botaoVoltar;

    [Header("ELEMENTOS DO MENU OPTIONS")]
    public RectTransform elementosOptions;

    [Header("ANIMAÇÃO")]
    public float distancia = 700f;
    public float duracao = 0.6f;

    [Header("POP")]
    public float escalaPopPrincipal = 1.15f;
    public float escalaPopOptions = 1.15f;

    [Header("FLUTUAÇÃO DO OPTIONS")]
    public float distanciaFlutuacao = 20f;
    public float velocidadeFlutuacao = 1f;
    public float suavidadeFlutuacao = 2f;

    // =========================
    // POSIÇÕES INICIAIS
    // =========================

    private Vector2 tituloJogoInicial;
    private Vector2 tituloOptionsInicial;
    private Vector2 imagemExtraInicial;

    // =========================
    // ESCALAS INICIAIS
    // =========================

    private Vector3 escalaBotoesPrincipal;
    private Vector3 escalaBotaoVoltar;
    private Vector3 escalaElementosOptions;

    // =========================
    // CONTROLE
    // =========================

    private bool emOpcoes = false;
    private bool animando = false;

    void Start()
    {
        // Guarda posições originais
        tituloJogoInicial =
            tituloJogo.anchoredPosition;

        tituloOptionsInicial =
            tituloOptions.anchoredPosition;

        imagemExtraInicial =
            imagemExtra.anchoredPosition;

        // Guarda escalas originais
        escalaBotoesPrincipal =
            botoesPrincipal.localScale;

        escalaBotaoVoltar =
            botaoVoltar.localScale;

        escalaElementosOptions =
            elementosOptions.localScale;

        // OPTIONS começa fora da tela
        tituloOptions.anchoredPosition =
            tituloOptionsInicial +
            Vector2.up * distancia;

        // Elementos de Options começam pequenos
        botaoVoltar.localScale =
            Vector3.zero;

        elementosOptions.localScale =
            Vector3.zero;

        // Menu Options começa escondido
        menuOptions.gameObject.SetActive(false);
    }

    void Update()
    {
        // Se estiver no menu de opções,
        // faz o título OPTIONS flutuar
        if (emOpcoes && !animando)
        {
            FlutuarTituloOptions();
        }
    }

    // =========================================================
    // FLUTUAÇÃO DO TÍTULO OPTIONS
    // =========================================================

    void FlutuarTituloOptions()
    {
        float movimento =
            Mathf.Sin(Time.time * velocidadeFlutuacao)
            * distanciaFlutuacao;

        Vector2 destino =
            tituloOptionsInicial;

        destino.y += movimento;

        tituloOptions.anchoredPosition =
            Vector2.Lerp(
                tituloOptions.anchoredPosition,
                destino,
                Time.deltaTime * suavidadeFlutuacao
            );
    }

    // =========================================================
    // ABRIR OPTIONS
    // =========================================================

    public void AbrirOpcoes()
    {
        if (animando || emOpcoes)
            return;

        StartCoroutine(TransicaoParaOpcoes());
    }

    // =========================================================
    // FECHAR OPTIONS
    // =========================================================

    public void FecharOpcoes()
    {
        if (animando || !emOpcoes)
            return;

        StartCoroutine(TransicaoParaPrincipal());
    }

    // =========================================================
    // TRANSIÇÃO PARA OPTIONS
    // =========================================================

    IEnumerator TransicaoParaOpcoes()
    {
        animando = true;

        // Ativa o menu de opções
        menuOptions.gameObject.SetActive(true);

        Vector2 tituloJogoInicio =
            tituloJogo.anchoredPosition;

        Vector2 tituloJogoDestino =
            tituloJogoInicial +
            Vector2.up * distancia;

        Vector2 tituloOptionsInicio =
            tituloOptionsInicial +
            Vector2.up * distancia;

        Vector2 tituloOptionsDestino =
            tituloOptionsInicial;

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(tempo / duracao);

            // Suavização
            t =
                Mathf.SmoothStep(0f, 1f, t);

            // -------------------------
            // TÍTULO DO JOGO SOBE
            // -------------------------

            tituloJogo.anchoredPosition =
                Vector2.Lerp(
                    tituloJogoInicio,
                    tituloJogoDestino,
                    t
                );

            // -------------------------
            // OPTIONS DESCE
            // -------------------------

            tituloOptions.anchoredPosition =
                Vector2.Lerp(
                    tituloOptionsInicio,
                    tituloOptionsDestino,
                    t
                );

            // -------------------------
            // IMAGEM EXTRA DESCE
            // -------------------------

            imagemExtra.anchoredPosition =
                Vector2.Lerp(
                    imagemExtraInicial,
                    imagemExtraInicial +
                    Vector2.down *
                    distanciaImagemExtra,
                    t
                );

            // -------------------------
            // BOTÕES PRINCIPAIS
            // POP OUT
            // -------------------------

            botoesPrincipal.localScale =
                Vector3.Lerp(
                    escalaBotoesPrincipal,
                    escalaBotoesPrincipal *
                    escalaPopPrincipal,
                    t
                );

            // -------------------------
            // BOTÃO VOLTAR
            // POP IN
            // -------------------------

            botaoVoltar.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    escalaBotaoVoltar,
                    t
                );

            // -------------------------
            // ELEMENTOS OPTIONS
            // POP IN
            // -------------------------

            elementosOptions.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    escalaElementosOptions,
                    t
                );

            yield return null;
        }

        // =========================
        // POSIÇÕES FINAIS
        // =========================

        tituloJogo.anchoredPosition =
            tituloJogoDestino;

        tituloOptions.anchoredPosition =
            tituloOptionsDestino;

        imagemExtra.anchoredPosition =
            imagemExtraInicial +
            Vector2.down *
            distanciaImagemExtra;

        // Botões principais terminam grandes
        botoesPrincipal.localScale =
            escalaBotoesPrincipal *
            escalaPopPrincipal;

        // Elementos Options normais
        botaoVoltar.localScale =
            escalaBotaoVoltar;

        elementosOptions.localScale =
            escalaElementosOptions;

        // Esconde menu principal
        menuPrincipal.gameObject.SetActive(false);

        // Reseta escala dos botões
        // para quando voltar
        botoesPrincipal.localScale =
            escalaBotoesPrincipal;

        emOpcoes = true;
        animando = false;
    }

    // =========================================================
    // TRANSIÇÃO PARA MENU PRINCIPAL
    // =========================================================

    IEnumerator TransicaoParaPrincipal()
    {
        animando = true;

        // Mostra menu principal
        menuPrincipal.gameObject.SetActive(true);

        // Botões principais começam maiores
        botoesPrincipal.localScale =
            escalaBotoesPrincipal *
            escalaPopPrincipal;

        // Elementos Options começam normais
        botaoVoltar.localScale =
            escalaBotaoVoltar;

        elementosOptions.localScale =
            escalaElementosOptions;

        Vector2 tituloJogoInicio =
            tituloJogoInicial +
            Vector2.up * distancia;

        Vector2 tituloJogoDestino =
            tituloJogoInicial;

        Vector2 tituloOptionsInicio =
            tituloOptionsInicial;

        Vector2 tituloOptionsDestino =
            tituloOptionsInicial +
            Vector2.up * distancia;

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(tempo / duracao);

            t =
                Mathf.SmoothStep(0f, 1f, t);

            // -------------------------
            // TÍTULO DO JOGO DESCE
            // -------------------------

            tituloJogo.anchoredPosition =
                Vector2.Lerp(
                    tituloJogoInicio,
                    tituloJogoDestino,
                    t
                );

            // -------------------------
            // OPTIONS SOBE
            // -------------------------

            tituloOptions.anchoredPosition =
                Vector2.Lerp(
                    tituloOptionsInicio,
                    tituloOptionsDestino,
                    t
                );

            // -------------------------
            // IMAGEM EXTRA SOBE
            // -------------------------

            imagemExtra.anchoredPosition =
                Vector2.Lerp(
                    imagemExtraInicial +
                    Vector2.down *
                    distanciaImagemExtra,
                    imagemExtraInicial,
                    t
                );

            // -------------------------
            // BOTÕES PRINCIPAIS
            // VOLTAM AO NORMAL
            // -------------------------

            botoesPrincipal.localScale =
                Vector3.Lerp(
                    escalaBotoesPrincipal *
                    escalaPopPrincipal,
                    escalaBotoesPrincipal,
                    t
                );

            // -------------------------
            // BOTÃO VOLTAR
            // POP OUT
            // -------------------------

            botaoVoltar.localScale =
                Vector3.Lerp(
                    escalaBotaoVoltar,
                    Vector3.zero,
                    t
                );

            // -------------------------
            // ELEMENTOS OPTIONS
            // POP OUT
            // -------------------------

            elementosOptions.localScale =
                Vector3.Lerp(
                    escalaElementosOptions,
                    Vector3.zero,
                    t
                );

            yield return null;
        }

        // =========================
        // POSIÇÕES FINAIS
        // =========================

        tituloJogo.anchoredPosition =
            tituloJogoDestino;

        tituloOptions.anchoredPosition =
            tituloOptionsDestino;

        imagemExtra.anchoredPosition =
            imagemExtraInicial;

        botoesPrincipal.localScale =
            escalaBotoesPrincipal;

        botaoVoltar.localScale =
            Vector3.zero;

        elementosOptions.localScale =
            Vector3.zero;

        // Esconde Options
        menuOptions.gameObject.SetActive(false);

        emOpcoes = false;
        animando = false;
    }
}