using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class PixelCollider2D : MonoBehaviour
{
    [Header("Pixel Collider 2D")]

    [Tooltip("Quantidade de pixels que representam 1 Unity Unit.")]
    [SerializeField] private int pixelsPerUnit = 32;

    [Header("Tamanho em Pixels")]

    [Min(1)]
    [SerializeField] private int width = 16;

    [Min(1)]
    [SerializeField] private int height = 16;

    [Header("Offset em Pixels")]

    [SerializeField] private int offsetX = 0;
    [SerializeField] private int offsetY = 0;

    private BoxCollider2D boxCollider;

    private void Awake()
    {
        SetupCollider();
    }

    private void OnEnable()
    {
        SetupCollider();
    }

    private void OnValidate()
    {
        SetupCollider();
    }

    private void Reset()
    {
        SetupCollider();
    }

    private void SetupCollider()
    {
        if (pixelsPerUnit <= 0)
            pixelsPerUnit = 32;

        if (width < 1)
            width = 1;

        if (height < 1)
            height = 1;

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        if (boxCollider == null)
            return;

        float widthInUnits =
            (float)width / pixelsPerUnit;

        float heightInUnits =
            (float)height / pixelsPerUnit;

        float offsetXInUnits =
            (float)offsetX / pixelsPerUnit;

        float offsetYInUnits =
            (float)offsetY / pixelsPerUnit;

        boxCollider.size = new Vector2(
            widthInUnits,
            heightInUnits
        );

        boxCollider.offset = new Vector2(
            offsetXInUnits,
            offsetYInUnits
        );
    }

    // =========================================================
    // PROPRIEDADES
    // =========================================================

    public int Width
    {
        get => width;
        set
        {
            width = Mathf.Max(1, value);
            SetupCollider();
        }
    }

    public int Height
    {
        get => height;
        set
        {
            height = Mathf.Max(1, value);
            SetupCollider();
        }
    }

    public int OffsetX
    {
        get => offsetX;
        set
        {
            offsetX = value;
            SetupCollider();
        }
    }

    public int OffsetY
    {
        get => offsetY;
        set
        {
            offsetY = value;
            SetupCollider();
        }
    }

    public int PixelsPerUnit
    {
        get => pixelsPerUnit;
        set
        {
            pixelsPerUnit = Mathf.Max(1, value);
            SetupCollider();
        }
    }
}