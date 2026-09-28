using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float velocidade = 6f;

    public float distanciaEntreFaixas = 2.5f;
    public float velocidadeLateral = 10f;

    public float forcaPulo = 7f;
    public float gravidade = -20f;

    private CharacterController controller;

    private int faixaAtual = 1;
    private float velocidadeVertical;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // =========================
        // TROCA DE FAIXAS
        // =========================

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (faixaAtual > 0)
            {
                faixaAtual--;
            }
        }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (faixaAtual < 2)
            {
                faixaAtual++;
            }
        }

        // =========================
        // PULO E GRAVIDADE
        // =========================

        if (controller.isGrounded)
        {
            velocidadeVertical = -1f;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                velocidadeVertical = forcaPulo;
            }
        }
        else
        {
            velocidadeVertical += gravidade * Time.deltaTime;
        }

        // =========================
        // MOVIMENTO
        // =========================

        float xDesejado = (faixaAtual - 1) * distanciaEntreFaixas;

        float diferencaX = xDesejado - transform.position.x;

        Vector3 movimento = new Vector3(
            diferencaX * velocidadeLateral,
            velocidadeVertical,
            velocidade
        );

        controller.Move(movimento * Time.deltaTime);
    }
}