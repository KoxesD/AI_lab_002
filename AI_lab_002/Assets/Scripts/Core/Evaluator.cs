using System;

namespace MiniChess
{
    /// <summary>
    /// Оценочная функция позиции.
    ///
    /// Возвращает оценку с точки зрения белых: положительное значение —
    /// преимущество белых, отрицательное — преимущество чёрных.
    ///
    /// База — материал (по заданию): пешка = 1, ладья = 5, король = 1000.
    /// Плюс небольшие позиционные добавки (суммарно много меньше цены пешки),
    /// чтобы при равном материале ИИ предпочитал активное развитие.
    /// </summary>
    public static class Evaluator
    {
        public const float PawnValue = 1f;
        public const float RookValue = 5f;
        public const float KingValue = 1000f;

        public const float PawnAdvanceBonus = 0.06f;  // за каждую горизонталь продвижения пешки
        public const float CenterFileBonus = 0.02f;   // пешка на вертикалях b, c, d
        public const float RookOpenFileBonus = 0.15f; // ладья на вертикали без пешек

        public static float Evaluate(GameState state)
        {
            // количество пешек на каждой вертикали (обе стороны) — для бонуса «открытая линия»
            var pawnsOnFile = new int[GameState.BoardSize];
            for (int sq = 0; sq < GameState.CellCount; sq++)
                if (GameState.PieceKind(state.Board[sq]) == GameState.Pawn)
                    pawnsOnFile[GameState.Col(sq)]++;

            float score = 0f;

            for (int sq = 0; sq < GameState.CellCount; sq++)
            {
                int piece = state.Board[sq];
                if (piece == GameState.Empty)
                    continue;

                bool white = GameState.IsWhitePiece(piece);
                int row = GameState.Row(sq);
                int col = GameState.Col(sq);
                float value = 0f;

                switch (GameState.PieceKind(piece))
                {
                    case GameState.Pawn:
                        // продвинутая пешка ближе к превращению в ладью
                        int advanced = white ? GameState.BoardSize - 2 - row : row - 1;
                        value = PawnValue + PawnAdvanceBonus * advanced;
                        if (col >= 1 && col <= GameState.BoardSize - 2)
                            value += CenterFileBonus;
                        break;

                    case GameState.Rook:
                        value = RookValue;
                        if (pawnsOnFile[col] == 0)
                            value += RookOpenFileBonus;
                        break;

                    case GameState.King:
                        value = KingValue;
                        break;
                }

                score += white ? value : -value;
            }

            return score;
        }
    }
}
