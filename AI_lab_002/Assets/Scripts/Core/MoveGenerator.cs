using System.Collections.Generic;

namespace MiniChess
{
    public enum GameResult
    {
        Ongoing,
        WhiteCheckmated,   // мат белым — победа чёрных
        BlackCheckmated,   // мат чёрным — победа белых
        Stalemate,         // пат — ничья
        DrawByFiftyMoves,  // 50 полуходов без взятий и ходов пешками — ничья
        DrawByBareKings,   // остались только короли — ничья
        DrawByRepetition   // троекратное повторение позиции — ничья (проверяет контроллер партии)
    }

    /// <summary>
    /// Генерация ходов и правила игры.
    /// Фигуры: король, ладья, пешка. Без рокировки и взятия на проходе.
    /// Пешка ходит на 1 клетку вперёд, бьёт по диагонали вперёд, на последней
    /// горизонтали превращается в ладью (других фигур в игре нет).
    /// </summary>
    public static class MoveGenerator
    {
        private static readonly int[] OrthoRow = { -1, 1, 0, 0 };
        private static readonly int[] OrthoCol = { 0, 0, -1, 1 };
        private static readonly int[] KingRow = { -1, -1, -1, 0, 0, 1, 1, 1 };
        private static readonly int[] KingCol = { -1, 0, 1, -1, 1, -1, 0, 1 };

        /// <summary>
        /// Все псевдолегальные ходы стороны, чья очередь (без проверки шаха собственному королю).
        /// </summary>
        public static List<Move> GeneratePseudoLegal(GameState state)
        {
            var moves = new List<Move>(20);
            bool white = state.WhiteToMove;

            for (int sq = 0; sq < GameState.CellCount; sq++)
            {
                int piece = state.Board[sq];
                if (piece == GameState.Empty || GameState.IsWhitePiece(piece) != white)
                    continue;

                int row = GameState.Row(sq);
                int col = GameState.Col(sq);

                switch (GameState.PieceKind(piece))
                {
                    case GameState.Pawn:
                        GeneratePawnMoves(state, moves, sq, row, col, piece, white);
                        break;

                    case GameState.Rook:
                        GenerateRookMoves(state, moves, sq, row, col, piece, white);
                        break;

                    case GameState.King:
                        GenerateKingMoves(state, moves, sq, row, col, piece, white);
                        break;
                }
            }
            return moves;
        }

        private static void GeneratePawnMoves(GameState state, List<Move> moves, int sq, int row, int col, int piece, bool white)
        {
            int dir = white ? -1 : 1;                       // белые идут вверх (к строке 0)
            int promoRow = white ? 0 : GameState.BoardSize - 1;
            int row1 = row + dir;
            if (row1 < 0 || row1 >= GameState.BoardSize)
                return;

            // ход вперёд на пустую клетку
            int forward = GameState.Idx(row1, col);
            if (state.Board[forward] == GameState.Empty)
                moves.Add(new Move(sq, forward, piece, 0, row1 == promoRow));

            // взятия по диагонали
            for (int dc = -1; dc <= 1; dc += 2)
            {
                int col1 = col + dc;
                if (col1 < 0 || col1 >= GameState.BoardSize)
                    continue;
                int target = GameState.Idx(row1, col1);
                int enemy = state.Board[target];
                if (enemy != GameState.Empty && GameState.IsWhitePiece(enemy) != white)
                    moves.Add(new Move(sq, target, piece, enemy, row1 == promoRow));
            }
        }

        private static void GenerateRookMoves(GameState state, List<Move> moves, int sq, int row, int col, int piece, bool white)
        {
            for (int d = 0; d < 4; d++)
            {
                int row1 = row + OrthoRow[d];
                int col1 = col + OrthoCol[d];
                while (row1 >= 0 && row1 < GameState.BoardSize && col1 >= 0 && col1 < GameState.BoardSize)
                {
                    int target = GameState.Idx(row1, col1);
                    int occupant = state.Board[target];
                    if (occupant == GameState.Empty)
                    {
                        moves.Add(new Move(sq, target, piece, 0, false));
                    }
                    else
                    {
                        if (GameState.IsWhitePiece(occupant) != white)
                            moves.Add(new Move(sq, target, piece, occupant, false));
                        break; // дальше по лучу путь закрыт
                    }
                    row1 += OrthoRow[d];
                    col1 += OrthoCol[d];
                }
            }
        }

        private static void GenerateKingMoves(GameState state, List<Move> moves, int sq, int row, int col, int piece, bool white)
        {
            for (int d = 0; d < 8; d++)
            {
                int row1 = row + KingRow[d];
                int col1 = col + KingCol[d];
                if (row1 < 0 || row1 >= GameState.BoardSize || col1 < 0 || col1 >= GameState.BoardSize)
                    continue;
                int target = GameState.Idx(row1, col1);
                int occupant = state.Board[target];
                if (occupant == GameState.Empty || GameState.IsWhitePiece(occupant) != white)
                    moves.Add(new Move(sq, target, piece, occupant, false));
            }
        }

        /// <summary>
        /// Все допустимые (легальные) ходы: псевдолегальные ходы, после которых
        /// собственный король не остаётся под шахом.
        /// </summary>
        public static List<Move> GenerateLegal(GameState state)
        {
            List<Move> pseudo = GeneratePseudoLegal(state);
            var legal = new List<Move>(pseudo.Count);
            bool white = state.WhiteToMove;

            foreach (Move move in pseudo)
            {
                state.MakeMove(move);
                if (!InCheck(state, white))
                    legal.Add(move);
                state.UndoMove();
            }
            return legal;
        }

        /// <summary>Атакована ли клетка `square` фигурами цвета `byWhite`.</summary>
        public static bool IsSquareAttacked(GameState state, int square, bool byWhite)
        {
            int row = GameState.Row(square);
            int col = GameState.Col(square);

            // пешки: белая пешка бьёт вверх, значит стоит строкой ниже цели
            int pawnRow = byWhite ? row + 1 : row - 1;
            int pawn = byWhite ? GameState.Pawn : -GameState.Pawn;
            if (pawnRow >= 0 && pawnRow < GameState.BoardSize)
            {
                if (col - 1 >= 0 && state.Board[GameState.Idx(pawnRow, col - 1)] == pawn) return true;
                if (col + 1 < GameState.BoardSize && state.Board[GameState.Idx(pawnRow, col + 1)] == pawn) return true;
            }

            // ладьи: идём по четырём лучам до первой фигуры
            int rook = byWhite ? GameState.Rook : -GameState.Rook;
            for (int d = 0; d < 4; d++)
            {
                int row1 = row + OrthoRow[d];
                int col1 = col + OrthoCol[d];
                while (row1 >= 0 && row1 < GameState.BoardSize && col1 >= 0 && col1 < GameState.BoardSize)
                {
                    int occupant = state.Board[GameState.Idx(row1, col1)];
                    if (occupant != GameState.Empty)
                    {
                        if (occupant == rook) return true;
                        break;
                    }
                    row1 += OrthoRow[d];
                    col1 += OrthoCol[d];
                }
            }

            // король
            int king = byWhite ? GameState.King : -GameState.King;
            for (int d = 0; d < 8; d++)
            {
                int row1 = row + KingRow[d];
                int col1 = col + KingCol[d];
                if (row1 < 0 || row1 >= GameState.BoardSize || col1 < 0 || col1 >= GameState.BoardSize)
                    continue;
                if (state.Board[GameState.Idx(row1, col1)] == king) return true;
            }

            return false;
        }

        /// <summary>Стоит ли король цвета `white` под шахом.</summary>
        public static bool InCheck(GameState state, bool white)
        {
            int kingSquare = white ? state.WhiteKingSquare : state.BlackKingSquare;
            return IsSquareAttacked(state, kingSquare, !white);
        }

        /// <summary>
        /// Проверка терминального состояния. Правила завершения:
        /// нет ходов + шах = мат; нет ходов без шаха = пат; 50 полуходов без
        /// взятий и ходов пешками = ничья; остались только короли = ничья.
        /// </summary>
        public static GameResult GetResult(GameState state, List<Move> legalMoves = null)
        {
            List<Move> legal = legalMoves != null ? legalMoves : GenerateLegal(state);

            if (legal.Count == 0)
            {
                if (InCheck(state, state.WhiteToMove))
                    return state.WhiteToMove ? GameResult.WhiteCheckmated : GameResult.BlackCheckmated;
                return GameResult.Stalemate;
            }
            if (state.HalfmoveClock >= 50)
                return GameResult.DrawByFiftyMoves;
            if (state.OnlyKingsLeft)
                return GameResult.DrawByBareKings;

            return GameResult.Ongoing;
        }

        /// <summary>Число листьев дерева игры на заданной глубине (perft — для проверки генератора).</summary>
        public static long Perft(GameState state, int depth)
        {
            if (depth == 0)
                return 1;

            List<Move> moves = GenerateLegal(state);
            if (depth == 1)
                return moves.Count;

            long nodes = 0;
            foreach (Move move in moves)
            {
                state.MakeMove(move);
                nodes += Perft(state, depth - 1);
                state.UndoMove();
            }
            return nodes;
        }
    }
}
