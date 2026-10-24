using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess
{
    /// <summary>
    /// Визуализация доски: клетки, координаты, фигуры, подсветка выбранных клеток,
    /// доступных ходов, последнего хода и шаха. Всё создаётся из кода.
    /// Фигуры анимируются при ходе (плавное перемещение, исчезновение взятой фигуры).
    /// </summary>
    public sealed class BoardView
    {
        private static readonly Color LightSquare = new Color(0.90f, 0.85f, 0.74f);
        private static readonly Color DarkSquare = new Color(0.53f, 0.41f, 0.31f);
        private static readonly Color SelectionColor = new Color(1f, 0.85f, 0.25f, 0.45f);
        private static readonly Color LastMoveColor = new Color(1f, 0.9f, 0.3f, 0.30f);
        private static readonly Color CheckColor = new Color(0.95f, 0.2f, 0.15f, 0.55f);
        private static readonly Color DotColor = new Color(0.2f, 0.45f, 0.25f, 0.8f);
        private static readonly Color RingColor = new Color(0.9f, 0.25f, 0.2f, 0.9f);

        private readonly SpriteRenderer[] squares = new SpriteRenderer[GameState.CellCount];
        private readonly SpriteRenderer[] overlays = new SpriteRenderer[GameState.CellCount];
        private readonly GameObject[] pieces = new GameObject[GameState.CellCount];
        private readonly List<TextMesh> labels = new List<TextMesh>();
        private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
        private Transform root;
        private bool flipped;

        public Transform Root { get { return root; } }

        public void Build()
        {
            root = new GameObject("Board").transform;

            for (int r = 0; r < GameState.BoardSize; r++)
                for (int c = 0; c < GameState.BoardSize; c++)
                {
                    int sq = GameState.Idx(r, c);
                    squares[sq] = CreateSprite("Square_" + Move.SquareName(sq), PieceArt.WhiteSprite, 0, SquareCenter(sq));
                    squares[sq].color = (r + c) % 2 == 0 ? LightSquare : DarkSquare;
                    overlays[sq] = CreateSprite("Overlay_" + Move.SquareName(sq), PieceArt.WhiteSprite, 30, SquareCenter(sq));
                    overlays[sq].color = Color.clear;
                }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int c = 0; c < GameState.BoardSize; c++)
                AddLabel(font, "" + (char)('a' + c), new Vector3(c - 2f, -2.62f, 0f));
            for (int r = 0; r < GameState.BoardSize; r++)
                AddLabel(font, "" + (GameState.BoardSize - r), new Vector3(-2.66f, 2f - r, 0f));
        }

        /// <summary>Центр клетки в локальных координатах доски.</summary>
        public static Vector3 SquareCenter(int square)
        {
            return new Vector3(GameState.Col(square) - 2f, 2f - GameState.Row(square), 0f);
        }

        /// <summary>
        /// Повернуть доску на 180°, когда человек играет чёрными
        /// (чёрные фигуры оказываются снизу).
        /// </summary>
        public void SetFlipped(bool flip)
        {
            flipped = flip;
            root.rotation = flip ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            foreach (GameObject piece in pieces)
                if (piece != null)
                    piece.transform.localRotation = PieceOrientation();
            foreach (TextMesh label in labels)
                label.transform.localRotation = PieceOrientation();
        }

        private Quaternion PieceOrientation()
        {
            return flipped ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
        }

        /// <summary>Полная пересборка фигур из состояния (новая игра, отмена хода).</summary>
        public void SyncFromState(GameState state)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null)
                {
                    Object.Destroy(pieces[i]);
                    pieces[i] = null;
                }
            }
            for (int sq = 0; sq < GameState.CellCount; sq++)
            {
                int piece = state.Board[sq];
                if (piece != GameState.Empty)
                    pieces[sq] = CreatePieceObject(piece, sq);
            }
        }

        /// <summary>Анимация хода: перемещение фигуры, растворение взятой, превращение.</summary>
        public IEnumerator AnimateMoveRoutine(Move move)
        {
            GameObject moving = pieces[move.From];
            GameObject captured = move.IsCapture ? pieces[move.To] : null;
            SpriteRenderer capturedSprite = captured != null ? captured.GetComponent<SpriteRenderer>() : null;
            Color capturedBase = capturedSprite != null ? capturedSprite.color : Color.white;

            pieces[move.From] = null;
            pieces[move.To] = moving;

            if (capturedSprite != null)
                capturedSprite.sortingOrder = 5; // взятая фигура «уходит под» двигающуюся

            Vector3 from = SquareCenter(move.From);
            Vector3 to = SquareCenter(move.To);
            const float duration = 0.18f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float ease = k * k * (3f - 2f * k); // smoothstep
                moving.transform.localPosition = Vector3.Lerp(from, to, ease);

                if (captured != null)
                {
                    captured.transform.localScale = Vector3.one * (0.8f * Mathf.Lerp(1f, 0.05f, k));
                    Color c = capturedBase;
                    c.a = capturedBase.a * (1f - k);
                    if (capturedSprite != null) capturedSprite.color = c;
                }
                yield return null;
            }

            moving.transform.localPosition = to;
            if (captured != null)
                Object.Destroy(captured);

            // превращение пешки в ладью — смена спрайта с небольшим «попом»
            if (move.Promotion)
            {
                SpriteRenderer movingSprite = moving.GetComponent<SpriteRenderer>();
                movingSprite.sprite = PieceArt.GetPieceSprite(move.Piece > 0 ? GameState.Rook : -GameState.Rook);
                const float pop = 0.16f;
                float pt = 0f;
                while (pt < pop)
                {
                    pt += Time.deltaTime;
                    float kk = Mathf.Clamp01(pt / pop);
                    moving.transform.localScale = Vector3.one * (0.8f * Mathf.Lerp(1.45f, 1f, kk));
                    yield return null;
                }
                moving.transform.localScale = Vector3.one * 0.8f;
            }
        }

        // ---------------------------------------------------------------- подсветка

        public void ClearHighlights()
        {
            for (int i = 0; i < overlays.Length; i++)
                overlays[i].color = Color.clear;
            ClearMarkers();
        }

        public void ShowSelection(int square)
        {
            overlays[square].color = SelectionColor;
        }

        public void ShowLegalTargets(List<Move> moves)
        {
            foreach (Move move in moves)
            {
                SpriteRenderer marker = CreateSprite("Marker",
                    move.IsCapture ? PieceArt.RingSprite : PieceArt.DotSprite, 20, SquareCenter(move.To));
                marker.color = move.IsCapture ? RingColor : DotColor;
                markers.Add(marker);
            }
        }

        public void ShowLastMove(Move move)
        {
            overlays[move.From].color = LastMoveColor;
            overlays[move.To].color = LastMoveColor;
        }

        public void ShowCheck(int kingSquare)
        {
            overlays[kingSquare].color = CheckColor;
        }

        private void ClearMarkers()
        {
            foreach (SpriteRenderer marker in markers)
                if (marker != null)
                    Object.Destroy(marker.gameObject);
            markers.Clear();
        }

        // ---------------------------------------------------------------- внутреннее

        private SpriteRenderer CreateSprite(string name, Sprite sprite, int sortingOrder, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private GameObject CreatePieceObject(int piece, int square)
        {
            SpriteRenderer renderer = CreateSprite("Piece_" + GameState.PieceChar(piece),
                                                   PieceArt.GetPieceSprite(piece), 10, SquareCenter(square));
            renderer.transform.localScale = Vector3.one * 0.8f;
            renderer.transform.localRotation = PieceOrientation();
            return renderer.gameObject;
        }

        private void AddLabel(Font font, string text, Vector3 localPosition)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 48;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.72f, 0.74f, 0.78f);

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = font.material;
            meshRenderer.sortingOrder = 40;
            labels.Add(mesh);
        }
    }
}
