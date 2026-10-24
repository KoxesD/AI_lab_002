using System.Collections.Generic;
using UnityEngine;

namespace MiniChess
{
    /// <summary>
    /// Процедурная генерация всей графики: спрайты фигур и маркеров подсветки
    /// рисуются кодом в текстуры (с 4-кратным суперсэмплингом для сглаживания).
    /// Внешние ассеты не нужны.
    /// </summary>
    public static class PieceArt
    {
        private const int Res = 128;   // итоговое разрешение спрайта
        private const int SS = 4;      // суперсэмплинг (рисуем в 4 раза крупнее)

        private static readonly Color WhiteFill = new Color(0.95f, 0.93f, 0.86f);
        private static readonly Color WhiteOutline = new Color(0.28f, 0.25f, 0.21f);
        private static readonly Color BlackFill = new Color(0.25f, 0.23f, 0.21f);
        private static readonly Color BlackOutline = new Color(0.85f, 0.82f, 0.74f);

        private static Sprite whiteSprite;
        private static Sprite dotSprite;
        private static Sprite ringSprite;
        private static readonly Dictionary<int, Sprite> pieceSprites = new Dictionary<int, Sprite>();

        /// <summary>Белый квадрат 1x1 — основа для клеток и подсветки (красится через color).</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                    var px = new Color[64];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    tex.SetPixels(px);
                    tex.Apply();
                    tex.filterMode = FilterMode.Point;
                    whiteSprite = MakeSprite(tex);
                }
                return whiteSprite;
            }
        }

        public static Sprite DotSprite
        {
            get
            {
                if (dotSprite == null) dotSprite = MakeCircleSprite(0.17f, -1f);
                return dotSprite;
            }
        }

        public static Sprite RingSprite
        {
            get
            {
                if (ringSprite == null) ringSprite = MakeCircleSprite(0.44f, 0.35f);
                return ringSprite;
            }
        }

        /// <summary>Спрайт фигуры по её коду (1..3 белые, -1..-3 чёрные).</summary>
        public static Sprite GetPieceSprite(int piece)
        {
            Sprite sprite;
            if (!pieceSprites.TryGetValue(piece, out sprite))
            {
                bool white = piece > 0;
                sprite = MakePieceSprite(GameState.PieceKind(piece),
                                         white ? WhiteFill : BlackFill,
                                         white ? WhiteOutline : BlackOutline);
                pieceSprites[piece] = sprite;
            }
            return sprite;
        }

        // ---------------------------------------------------------------- рисование

        private static Sprite MakeCircleSprite(float outerRadius, float innerRadius)
        {
            int size = Res * SS;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inside = d <= outerRadius && (innerRadius < 0f || d >= innerRadius);
                    px[y * size + x] = inside ? Color.white : Color.clear;
                }
            return MakeSprite(Downsample(px, size), Res);
        }

        private static Sprite MakePieceSprite(int kind, Color fill, Color outline)
        {
            int size = Res * SS;
            var px = new Color32[size * size];
            DrawPiece(px, size, kind, outline, 0.035f); // контур — те же фигуры, чуть крупнее
            DrawPiece(px, size, kind, fill, 0f);
            return MakeSprite(Downsample(px, size), Res);
        }

        private static void DrawPiece(Color32[] px, int size, int kind, Color color, float grow)
        {
            switch (kind)
            {
                case GameState.Pawn:
                    FillRect(px, size, 0.26f, 0.05f, 0.74f, 0.15f, color, grow);   // основание
                    FillRect(px, size, 0.34f, 0.13f, 0.66f, 0.46f, color, grow);   // тело
                    FillCircle(px, size, 0.50f, 0.60f, 0.16f, color, grow);        // голова
                    break;

                case GameState.Rook:
                    FillRect(px, size, 0.24f, 0.05f, 0.76f, 0.15f, color, grow);   // основание
                    FillRect(px, size, 0.34f, 0.13f, 0.66f, 0.60f, color, grow);   // тело
                    FillRect(px, size, 0.30f, 0.58f, 0.70f, 0.66f, color, grow);   // пояс
                    FillRect(px, size, 0.30f, 0.64f, 0.42f, 0.79f, color, grow);   // зубцы
                    FillRect(px, size, 0.44f, 0.64f, 0.56f, 0.79f, color, grow);
                    FillRect(px, size, 0.58f, 0.64f, 0.70f, 0.79f, color, grow);
                    break;

                case GameState.King:
                    FillRect(px, size, 0.24f, 0.05f, 0.76f, 0.14f, color, grow);   // основание
                    FillRect(px, size, 0.32f, 0.12f, 0.68f, 0.46f, color, grow);   // тело
                    FillRect(px, size, 0.28f, 0.44f, 0.72f, 0.52f, color, grow);   // корона-пояс
                    FillCircle(px, size, 0.50f, 0.58f, 0.13f, color, grow);        // купол
                    FillRect(px, size, 0.47f, 0.66f, 0.53f, 0.90f, color, grow);   // крест
                    FillRect(px, size, 0.38f, 0.74f, 0.62f, 0.81f, color, grow);
                    break;
            }
        }

        private static void FillRect(Color32[] px, int size,
                                     float x0, float y0, float x1, float y1,
                                     Color color, float grow)
        {
            int xa = Mathf.Clamp(Mathf.RoundToInt((x0 - grow) * size), 0, size - 1);
            int xb = Mathf.Clamp(Mathf.RoundToInt((x1 + grow) * size), 0, size);
            int ya = Mathf.Clamp(Mathf.RoundToInt((y0 - grow) * size), 0, size - 1);
            int yb = Mathf.Clamp(Mathf.RoundToInt((y1 + grow) * size), 0, size);
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                    px[y * size + x] = color;
        }

        private static void FillCircle(Color32[] px, int size,
                                       float cx, float cy, float radius,
                                       Color color, float grow)
        {
            int xa = Mathf.Clamp(Mathf.RoundToInt((cx - radius - grow) * size), 0, size);
            int xb = Mathf.Clamp(Mathf.RoundToInt((cx + radius + grow) * size), 0, size);
            int ya = Mathf.Clamp(Mathf.RoundToInt((cy - radius - grow) * size), 0, size);
            int yb = Mathf.Clamp(Mathf.RoundToInt((cy + radius + grow) * size), 0, size);
            float r2 = (radius + grow) * (radius + grow) * size * size;
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    float dx = x + 0.5f - cx * size;
                    float dy = y + 0.5f - cy * size;
                    if (dx * dx + dy * dy <= r2)
                        px[y * size + x] = color;
                }
        }

        /// <summary>Усреднение блоков SS x SS — сглаживание краёв.</summary>
        private static Color[] Downsample(Color32[] source, int size)
        {
            int outSize = size / SS;
            var result = new Color[outSize * outSize];
            for (int y = 0; y < outSize; y++)
                for (int x = 0; x < outSize; x++)
                {
                    // суммировать нужно в int: каналы Color32 — байты и переполняются
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            Color32 c = source[(y * SS + sy) * size + (x * SS + sx)];
                            r += c.r;
                            g += c.g;
                            b += c.b;
                            a += c.a;
                        }
                    int n = SS * SS;
                    result[y * outSize + x] = new Color32(
                        (byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }
            return result;
        }

        private static Sprite MakeSprite(Color[] pixels, int size)
        {
            return MakeSprite(pixels, size, size);
        }

        private static Sprite MakeSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), tex.height);
        }

        private static Sprite MakeSprite(Color[] pixels, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                                 new Vector2(0.5f, 0.5f), pixelsPerUnit: height);
        }
    }
}
