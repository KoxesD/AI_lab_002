using System;
using System.Collections.Generic;

namespace MiniChess
{
    /// <summary>
    /// Игровое состояние мини-шахмат 5x5.
    ///
    /// Доска — массив из 25 клеток: индекс = строка * 5 + столбец.
    /// Строка 0 — верх (чёрные), строка 4 — низ (белые).
    ///
    /// Код фигуры — целое число со знаком:
    ///   0 — пустая клетка;
    ///   1 — пешка, 2 — ладья, 3 — король;
    ///   положительное значение — белые, отрицательное — чёрные.
    ///
    /// Начальная позиция:
    ///   r . k . r      (чёрные: ладья a5, король c5, ладья e5)
    ///   p p p p p
    ///   . . . . .
    ///   P P P P P
    ///   R . K . R      (белые: ладья a1, король c1, ладья e1)
    /// </summary>
    public sealed class GameState
    {
        public const int BoardSize = 5;
        public const int CellCount = BoardSize * BoardSize;

        public const int Empty = 0;
        public const int Pawn = 1;
        public const int Rook = 2;
        public const int King = 3;

        public readonly int[] Board = new int[CellCount];

        /// <summary>Чья очередь хода (true — белые).</summary>
        public bool WhiteToMove = true;

        /// <summary>Полуходов подряд без взятий и ходов пешками (правило 50 ходов — ничья).</summary>
        public int HalfmoveClock = 0;

        /// <summary>Всего полуходов, сделанных с начала партии.</summary>
        public int PlyCount = 0;

        public int WhiteKingSquare;
        public int BlackKingSquare;

        /// <summary>Сколько пешек/ладей осталось на доске (обе стороны) — для быстрой проверки «только короли».</summary>
        public int PawnsLeft;
        public int RooksLeft;

        private readonly Stack<Move> moveHistory = new Stack<Move>(128);
        private readonly Stack<int> clockHistory = new Stack<int>(128);

        public GameState()
        {
            ResetToInitial();
        }

        /// <summary>
        /// Создать позицию из массива строк (по одной на горизонталь, сверху вниз).
        /// Обозначения: '.', ' ' — пусто; 'P','R','K' — белые; 'p','r','k' — чёрные.
        /// </summary>
        public GameState(string[] rows, bool whiteToMove)
        {
            if (rows == null || rows.Length != BoardSize)
                throw new ArgumentException("Нужно ровно 5 строк");

            WhiteToMove = whiteToMove;
            for (int r = 0; r < BoardSize; r++)
            {
                string row = rows[r];
                if (row.Length != BoardSize)
                    throw new ArgumentException("В каждой строке должно быть 5 символов: " + row);
                for (int c = 0; c < BoardSize; c++)
                {
                    int piece = ParsePiece(row[c]);
                    int sq = Idx(r, c);
                    Board[sq] = piece;
                    switch (Math.Abs(piece))
                    {
                        case Pawn: PawnsLeft++; break;
                        case Rook: RooksLeft++; break;
                        case King:
                            if (piece > 0) WhiteKingSquare = sq; else BlackKingSquare = sq;
                            break;
                    }
                }
            }
            if (Board[WhiteKingSquare] != King || Board[BlackKingSquare] != -King)
                throw new ArgumentException("Позиция должна содержать обоих королей");
        }

        public static int ParsePiece(char ch)
        {
            switch (ch)
            {
                case 'P': return Pawn;
                case 'R': return Rook;
                case 'K': return King;
                case 'p': return -Pawn;
                case 'r': return -Rook;
                case 'k': return -King;
                default: return Empty;
            }
        }

        /// <summary>Сброс к начальной позиции.</summary>
        public void ResetToInitial()
        {
            Array.Clear(Board, 0, Board.Length);

            // чёрные
            Board[Idx(0, 0)] = -Rook;
            Board[Idx(0, 2)] = -King;
            Board[Idx(0, 4)] = -Rook;
            for (int c = 0; c < BoardSize; c++)
                Board[Idx(1, c)] = -Pawn;

            // белые
            for (int c = 0; c < BoardSize; c++)
                Board[Idx(3, c)] = Pawn;
            Board[Idx(4, 0)] = Rook;
            Board[Idx(4, 2)] = King;
            Board[Idx(4, 4)] = Rook;

            WhiteToMove = true;
            HalfmoveClock = 0;
            PlyCount = 0;
            WhiteKingSquare = Idx(4, 2);
            BlackKingSquare = Idx(0, 2);
            PawnsLeft = 10;
            RooksLeft = 4;
            moveHistory.Clear();
            clockHistory.Clear();
        }

        public static int Idx(int row, int col) { return row * BoardSize + col; }
        public static int Row(int square) { return square / BoardSize; }
        public static int Col(int square) { return square % BoardSize; }
        public static bool IsWhitePiece(int piece) { return piece > 0; }
        public static int PieceKind(int piece) { return Math.Abs(piece); }

        /// <summary>Применить ход к состоянию (с сохранением информации для отката).</summary>
        public void MakeMove(Move move)
        {
            moveHistory.Push(move);
            clockHistory.Push(HalfmoveClock);

            Board[move.From] = Empty;
            Board[move.To] = move.Promotion ? (move.Piece > 0 ? Rook : -Rook) : move.Piece;

            if (move.Piece == King) WhiteKingSquare = move.To;
            else if (move.Piece == -King) BlackKingSquare = move.To;

            if (move.Promotion)
            {
                PawnsLeft--;
                RooksLeft++;
            }
            if (move.Captured != Empty)
            {
                int kind = PieceKind(move.Captured);
                if (kind == Pawn) PawnsLeft--;
                else if (kind == Rook) RooksLeft++;
            }

            HalfmoveClock = (move.Captured != Empty || PieceKind(move.Piece) == Pawn) ? 0 : HalfmoveClock + 1;
            WhiteToMove = !WhiteToMove;
            PlyCount++;
        }

        /// <summary>Отменить последний ход (полностью восстанавливает состояние).</summary>
        public Move UndoMove()
        {
            Move move = moveHistory.Pop();
            HalfmoveClock = clockHistory.Pop();

            Board[move.From] = move.Piece;
            Board[move.To] = move.Captured;

            if (move.Piece == King) WhiteKingSquare = move.From;
            else if (move.Piece == -King) BlackKingSquare = move.From;

            if (move.Promotion)
            {
                PawnsLeft++;
                RooksLeft--;
            }
            if (move.Captured != Empty)
            {
                int kind = PieceKind(move.Captured);
                if (kind == Pawn) PawnsLeft++;
                else if (kind == Rook) RooksLeft--;
            }

            WhiteToMove = !WhiteToMove;
            PlyCount--;
            return move;
        }

        public int MovesMade { get { return moveHistory.Count; } }

        /// <summary>Последний сделанный ход (вызывать только при MovesMade > 0).</summary>
        public Move LastMove { get { return moveHistory.Peek(); } }

        public bool OnlyKingsLeft { get { return PawnsLeft == 0 && RooksLeft == 0; } }

        /// <summary>
        /// Ключ позиции (расстановка + очередь хода) для правила троекратного повторения.
        /// </summary>
        public string PositionKey()
        {
            var sb = new System.Text.StringBuilder(26);
            for (int i = 0; i < CellCount; i++)
                sb.Append(PieceChar(Board[i]));
            sb.Append(WhiteToMove ? 'w' : 'b');
            return sb.ToString();
        }

        /// <summary>Текстовая диаграмма позиции (для отладки и консольных тестов).</summary>
        public string ToAscii()
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < BoardSize; r++)
            {
                sb.Append(BoardSize - r);
                sb.Append(" |");
                for (int c = 0; c < BoardSize; c++)
                {
                    int p = Board[Idx(r, c)];
                    sb.Append(p == 0 ? "  " : " " + PieceChar(p));
                }
                sb.AppendLine(" |");
            }
            sb.AppendLine("    a b c d e");
            return sb.ToString();
        }

        public static char PieceChar(int piece)
        {
            switch (piece)
            {
                case Pawn: return 'P';
                case Rook: return 'R';
                case King: return 'K';
                case -Pawn: return 'p';
                case -Rook: return 'r';
                case -King: return 'k';
                default: return '.';
            }
        }
    }
}
