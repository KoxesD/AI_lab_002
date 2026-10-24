namespace MiniChess
{
    /// <summary>
    /// Ход на доске. Клетки нумеруются 0..24: индекс = строка * 5 + столбец.
    /// Строка 0 — верхняя (сторона чёрных), строка 4 — нижняя (сторона белых).
    /// </summary>
    public struct Move
    {
        public int From;      // откуда
        public int To;        // куда
        public int Piece;     // код двигающейся фигуры (со знаком цвета)
        public int Captured;  // код взятой фигуры или 0 (клетка To всегда, взятия на проходе нет)
        public bool Promotion; // пешка достигла последней горизонтали и превратилась в ладью

        public Move(int from, int to, int piece, int captured, bool promotion)
        {
            From = from;
            To = to;
            Piece = piece;
            Captured = captured;
            Promotion = promotion;
        }

        public bool IsCapture
        {
            get { return Captured != 0; }
        }

        /// <summary>Имя клетки в шахматной нотации: столбцы a..e, горизонтали 1..5.</summary>
        public static string SquareName(int square)
        {
            int row = square / GameState.BoardSize;
            int col = square % GameState.BoardSize;
            return "" + (char)('a' + col) + (GameState.BoardSize - row);
        }

        public override string ToString()
        {
            return SquareName(From) + (IsCapture ? "x" : "-") + SquareName(To) + (Promotion ? "=R" : "");
        }
    }
}
