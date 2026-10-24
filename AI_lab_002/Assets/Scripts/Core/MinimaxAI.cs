using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MiniChess
{
    /// <summary>Статистика одного поиска лучшего хода.</summary>
    public sealed class SearchStats
    {
        public long Nodes;                          // сколько позиций просмотрено
        public double Milliseconds;                 // время поиска
        public float Score;                         // оценка выбранного хода
        public Move BestMove;                       // выбранный ход
        public List<KeyValuePair<Move, float>> RootMoveScores = new List<KeyValuePair<Move, float>>();
    }

    /// <summary>
    /// Искусственный игрок: поиск лучшего хода алгоритмом Minimax
    /// с альфа-бета отсечением.
    ///
    /// Minimax строит дерево возможных ходов до заданной глубины:
    ///   на уровнях хода белых выбирается максимум оценки (maximizing),
    ///   на уровнях хода чёрных — минимум (minimizing).
    /// Терминальные состояния: мат = ±(MateScore - ply) — чем быстрее мат,
    /// тем выше оценка; пат и ничья = 0.
    /// Если терминальное состояние не достигнуто, используется эвристическая
    /// оценка Evaluator.Evaluate.
    /// </summary>
    public static class MinimaxAI
    {
        /// <summary>Оценка мата. Вычитание ply делает быстрые маты привлекательнее отложенных.</summary>
        public const float MateScore = 100000f;

        public static bool IsMateScore(float score)
        {
            return Math.Abs(score) > MateScore - 1000f;
        }

        /// <summary>
        /// Найти лучший ход в текущей позиции.
        /// </summary>
        /// <param name="state">Текущая позиция (остаётся неизменной после вызова).</param>
        /// <param name="depth">Глубина поиска в полуходах.</param>
        /// <param name="stats">Сюда записывается статистика поиска (может быть null).</param>
        /// <param name="exactRootScores">true — оценивать каждый корневой ход полным окном
        /// (без отсечений на корневом уровне), чтобы получить точные оценки всех ходов
        /// для отладки и отчёта. Игра использует false (быстрее).</param>
        public static Move FindBestMove(GameState state, int depth, SearchStats stats = null, bool exactRootScores = false)
        {
            if (stats == null) stats = new SearchStats();
            List<Move> moves = MoveGenerator.GenerateLegal(state);
            if (moves.Count == 0)
            {
                stats.Nodes = 0;
                stats.BestMove = moves.Count > 0 ? moves[0] : default(Move);
                return stats.BestMove;
            }

            OrderMoves(moves);

            bool maximizing = state.WhiteToMove;
            float alpha = float.NegativeInfinity;
            float beta = float.PositiveInfinity;
            float bestScore = maximizing ? float.NegativeInfinity : float.PositiveInfinity;
            Move bestMove = moves[0];

            var stopwatch = Stopwatch.StartNew();
            foreach (Move move in moves)
            {
                state.MakeMove(move);
                float score = Minimax(state, depth - 1, !maximizing, alpha, beta, 1, stats);
                state.UndoMove();

                stats.RootMoveScores.Add(new KeyValuePair<Move, float>(move, score));

                if (maximizing)
                {
                    if (score > bestScore) { bestScore = score; bestMove = move; }
                    if (!exactRootScores && bestScore > alpha) alpha = bestScore;
                }
                else
                {
                    if (score < bestScore) { bestScore = score; bestMove = move; }
                    if (!exactRootScores && bestScore < beta) beta = bestScore;
                }
            }
            stopwatch.Stop();

            stats.Milliseconds = stopwatch.Elapsed.TotalMilliseconds;
            stats.Score = bestScore;
            stats.BestMove = bestMove;
            return bestMove;
        }

        /// <summary>
        /// Классический рекурсивный Minimax с альфа-бета отсечением.
        /// </summary>
        /// <param name="state">Позиция.</param>
        /// <param name="depth">Оставшаяся глубина поиска.</param>
        /// <param name="isMaximizing">Уровень максимизирующего игрока (ход белых).</param>
        /// <param name="alpha">Лучший уже найденный максимум на пути (для отсечения).</param>
        /// <param name="beta">Лучший уже найденный минимум на пути (для отсечения).</param>
        /// <param name="ply">Полуходов от корня (для приоритета быстрых матов).</param>
        /// <returns>Оценка позиции с точки зрения белых.</returns>
        public static float Minimax(GameState state, int depth, bool isMaximizing, float alpha, float beta, int ply, SearchStats stats = null)
        {
            if (stats != null) stats.Nodes++;

            // 1. Терминальные состояния (проверяются до ограничения глубины).
            List<Move> moves = MoveGenerator.GenerateLegal(state);
            if (moves.Count == 0)
            {
                if (!MoveGenerator.InCheck(state, state.WhiteToMove))
                    return 0f; // пат — ничья

                // мат стороне, чья очередь хода
                float mate = MateScore - ply;
                return state.WhiteToMove ? -mate : mate;
            }
            if (state.HalfmoveClock >= 50)
                return 0f; // ничья по правилу 50 ходов
            if (state.OnlyKingsLeft)
                return 0f; // только короли — ничья

            // 2. Достигнут предел глубины — эвристическая оценка позиции.
            if (depth <= 0)
                return Evaluator.Evaluate(state);

            // 3. Перебор ходов: максимум на своих уровнях, минимум на уровнях соперника.
            OrderMoves(moves);
            float best = isMaximizing ? float.NegativeInfinity : float.PositiveInfinity;

            foreach (Move move in moves)
            {
                state.MakeMove(move);
                float score = Minimax(state, depth - 1, !isMaximizing, alpha, beta, ply + 1, stats);
                state.UndoMove();

                if (isMaximizing)
                {
                    if (score > best) best = score;
                    if (best > alpha) alpha = best;
                }
                else
                {
                    if (score < best) best = score;
                    if (best < beta) beta = best;
                }

                // альфа-бета отсечение: ветвь уже не может повлиять на выбор
                if (beta <= alpha)
                    break;
            }

            return best;
        }

        /// <summary>
        /// Сортировка ходов: сначала взятия (дороже взятая фигура) и превращения.
        /// Лучший ход находится раньше — альфа-бета отсекает больше ветвей.
        /// </summary>
        public static void OrderMoves(List<Move> moves)
        {
            moves.Sort((a, b) => OrderKey(b).CompareTo(OrderKey(a)));
        }

        private static int OrderKey(Move move)
        {
            int key = 0;
            if (move.Captured != GameState.Empty)
                key += 10 * GameState.PieceKind(move.Captured);
            if (move.Promotion)
                key += 5;
            return key;
        }
    }
}
