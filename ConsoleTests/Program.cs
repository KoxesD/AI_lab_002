using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MiniChess;

namespace MiniChessTests
{
    /// <summary>
    /// Консольные тесты игрового ядра (компилируется вместе с Assets/Scripts/Core).
    /// Запуск:  dotnet run --project ConsoleTests
    /// </summary>
    internal static class Program
    {
        private static int passed = 0, failed = 0;

        private static void Check(bool condition, string name)
        {
            if (condition) { passed++; Console.WriteLine("  [OK]   " + name); }
            else { failed++; Console.WriteLine("  [FAIL] " + name); }
        }

        private static string F(float v)
        {
            return v.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ResultName(GameResult r)
        {
            switch (r)
            {
                case GameResult.Ongoing: return "игра продолжается";
                case GameResult.WhiteCheckmated: return "мат белым (победа чёрных)";
                case GameResult.BlackCheckmated: return "мат чёрным (победа белых)";
                case GameResult.Stalemate: return "пат (ничья)";
                case GameResult.DrawByFiftyMoves: return "ничья: 50 ходов без взятий";
                case GameResult.DrawByBareKings: return "ничья: остались только короли";
                default: return r.ToString();
            }
        }

        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== Мини-шахматы 5x5: тесты игрового ядра ===\n");

            TestInitialMoves();
            TestPerft();
            TestMakeUndoRoundtrip();
            TestInvariantsAndPromotion();
            TestMateInOne();
            TestStalemate();
            TestTrap();
            TestSelfPlay();
            PrintReportTables();

            Console.WriteLine("\nИтого: " + passed + " OK, " + failed + " FAIL");
            Environment.ExitCode = failed == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------

        private static void TestInitialMoves()
        {
            Console.WriteLine("[1] Начальная позиция: генерация ходов");
            var s = new GameState();
            List<Move> moves = MoveGenerator.GenerateLegal(s);

            var names = moves.Select(m => m.ToString()).OrderBy(n => n).ToList();
            Console.WriteLine("  ходы белых (" + moves.Count + "): " + string.Join(", ", names));

            Check(moves.Count == 9, "в начальной позиции ровно 9 ходов белых");
            var expected = new HashSet<string> { "a2-a3", "b2-b3", "c2-c3", "d2-d3", "e2-e3", "a1-b1", "e1-d1", "c1-b1", "c1-d1" };
            Check(new HashSet<string>(names).SetEquals(expected), "состав ходов совпадает с ожидаемым");
            Check(MoveGenerator.InCheck(s, true) == false && MoveGenerator.InCheck(s, false) == false,
                "в начальной позиции никто не под шахом");
        }

        private static void TestPerft()
        {
            Console.WriteLine("[2] Перебор дерева игры (perft)");
            var s = new GameState();
            for (int d = 1; d <= 4; d++)
            {
                long nodes = MoveGenerator.Perft(s, d);
                Console.WriteLine("  perft(" + d + ") = " + nodes);
            }
            Check(MoveGenerator.Perft(s, 1) == 9, "perft(1) = 9");
            Check(MoveGenerator.Perft(s, 2) == 84, "perft(2) = 84");
            Check(MoveGenerator.Perft(s, 3) == 802, "perft(3) = 802");
            Check(MoveGenerator.Perft(s, 4) == 7512, "perft(4) = 7512 (регрессионное значение)");
        }

        private static void TestMakeUndoRoundtrip()
        {
            Console.WriteLine("[3] MakeMove/UndoMove полностью восстанавливает состояние");
            var rng = new Random(12345);
            int checkedUndos = 0;

            for (int game = 0; game < 300; game++)
            {
                var s = new GameState();
                for (int step = 0; step < 60; step++)
                {
                    List<Move> legal = MoveGenerator.GenerateLegal(s);
                    if (legal.Count == 0 || MoveGenerator.GetResult(s, legal) != GameResult.Ongoing)
                        break;

                    int[] boardCopy = (int[])s.Board.Clone();
                    int clock = s.HalfmoveClock, ply = s.PlyCount;
                    int wk = s.WhiteKingSquare, bk = s.BlackKingSquare;
                    int pawns = s.PawnsLeft, rooks = s.RooksLeft;
                    bool wtm = s.WhiteToMove;

                    Move move = legal[rng.Next(legal.Count)];
                    s.MakeMove(move);
                    s.UndoMove();

                    bool same = boardCopy.SequenceEqual(s.Board)
                        && clock == s.HalfmoveClock && ply == s.PlyCount
                        && wk == s.WhiteKingSquare && bk == s.BlackKingSquare
                        && pawns == s.PawnsLeft && rooks == s.RooksLeft
                        && wtm == s.WhiteToMove;
                    if (!same) { Check(false, "откат хода " + move + " не восстановил состояние"); return; }
                    checkedUndos++;

                    s.MakeMove(move);
                }
            }
            Check(true, "откат " + checkedUndos + " случайных ходов без расхождений");
        }

        private static void TestInvariantsAndPromotion()
        {
            Console.WriteLine("[4] Инварианты случайных партий + превращение пешки");

            var rng = new Random(777);
            int positions = 0, captures = 0;
            for (int game = 0; game < 200; game++)
            {
                var s = new GameState();
                for (int step = 0; step < 120; step++)
                {
                    List<Move> legal = MoveGenerator.GenerateLegal(s);
                    if (legal.Count == 0) break;
                    Move m = legal[rng.Next(legal.Count)];

                    if (GameState.PieceKind(m.Captured) == GameState.King) { Check(false, "взятие короля невозможно"); return; }

                    bool mover = s.WhiteToMove;
                    s.MakeMove(m);
                    positions++;
                    if (m.IsCapture) captures++;

                    if (s.Board[s.WhiteKingSquare] != GameState.King || s.Board[s.BlackKingSquare] != -GameState.King)
                    { Check(false, "оба короля должны оставаться на доске"); return; }
                    if (MoveGenerator.InCheck(s, mover))
                    { Check(false, "после легального хода свой король не может быть под шахом"); return; }
                }
            }
            Check(true, positions + " позиций: короли целы, шахов после своих ходов нет (" + captures + " взятий)");

            // превращение белой пешки a4-a5=R
            var sp = new GameState(new[] { ".....", "P....", "..K..", ".....", "...k." }, true);
            List<Move> promoMoves = MoveGenerator.GenerateLegal(sp).Where(m => m.Promotion).ToList();
            Check(promoMoves.Select(m => m.ToString()).Contains("a4-a5=R"), "пешка a4 может превратиться в ладью на a5");
            if (promoMoves.Count > 0)
            {
                sp.MakeMove(promoMoves[0]);
                Check(sp.Board[GameState.Idx(0, 0)] == GameState.Rook, "после хода на a5 стоит белая ладья");
                Check(sp.PawnsLeft == 0 && sp.RooksLeft == 1, "счётчики пешек/ладей обновлены");
            }

            // превращение чёрной пешки e2-e1=R (нотация превращения всегда с большой буквы)
            var sb = new GameState(new[] { "...k.", ".....", ".....", "....p", "K...." }, false);
            List<Move> bp = MoveGenerator.GenerateLegal(sb).Where(m => m.Promotion).ToList();
            Check(bp.Select(m => m.ToString()).Contains("e2-e1=R"), "чёрная пешка e2 превращается на e1");
        }

        private static void TestMateInOne()
        {
            Console.WriteLine("[5] Мат в 1 ход: ИИ находит Re5#");
            // Белые: Кр b3, Л e1. Чёрные: Кр a5, пешка b4. Ход белых.
            var s = new GameState(new[] { "k....", ".p...", ".K...", ".....", "....R" }, true);

            var stats = new SearchStats();
            Move best = MinimaxAI.FindBestMove(s, 2, stats);
            Console.WriteLine("  лучший ход: " + best + ", оценка " + F(stats.Score) +
                              " (мат = +" + MinimaxAI.MateScore.ToString("0") + " - ply)");

            Check(best.ToString() == "e1-e5", "ИИ играет e1-e5 с матом");
            Check(stats.Score == MinimaxAI.MateScore - 1, "оценка хода = MateScore - 1 (мат на 1-м полуходе)");

            s.MakeMove(best);
            Check(MoveGenerator.GetResult(s) == GameResult.BlackCheckmated, "после хода — мат чёрным");
        }

        private static void TestStalemate()
        {
            Console.WriteLine("[6] Пат распознаётся как ничья");
            // Белые: Кр a3, Л b2. Чёрный король a5 без ходов, не под шахом. Ход чёрных.
            var s = new GameState(new[] { "k....", ".....", "K....", ".R...", "....." }, false);
            GameResult result = MoveGenerator.GetResult(s);
            Console.WriteLine("  статус: " + ResultName(result));
            Check(result == GameResult.Stalemate, "позиция — пат");
            Check(MoveGenerator.GenerateLegal(s).Count == 0, "у чёрных нет допустимых ходов");

            // ИИ-оценка пата: если белые могут поставить пат вместо мата — оценка 0.
            var stats = new SearchStats();
            float eval = MinimaxAI.Minimax(s, 2, false, float.NegativeInfinity, float.PositiveInfinity, 0, stats);
            Check(eval == 0f, "Minimax возвращает 0 для пата");
        }

        private static void TestTrap()
        {
            Console.WriteLine("[7] Глубина 3: ИИ не берёт отравленную пешку");
            // Материал равный (ладья + пешка у каждого). Чёрная ладья a2 может взять
            // пешку c2, но тогда Лc1xЛ отбирает ладью: ладья (5) за пешку (1).
            var s = new GameState(new[] { "....k", "....p", ".....", "r.P..", "..R.K" }, false);

            var stats = new SearchStats();
            Move best = MinimaxAI.FindBestMove(s, 3, stats, exactRootScores: true);

            Console.WriteLine("  оценки корневых ходов чёрных (глубина 3, чем меньше — тем лучше чёрным):");
            foreach (var kv in stats.RootMoveScores.OrderBy(kv => kv.Value))
                Console.WriteLine("    " + kv.Key + "  " + F(kv.Value) + (kv.Key.ToString() == best.ToString() ? "   <-- выбран" : ""));

            float captureScore = stats.RootMoveScores.First(kv => kv.Key.ToString() == "a2xc2").Value;
            Check(captureScore > 3f, "жадное взятие a2xc2 отдаёт ладью за пешку (оценка > +3 у белых)");
            Check(best.ToString() != "a2xc2", "ИИ отказывается от отравленного взятия");
            Check(stats.Score < 1f, "выбранный ход сохраняет примерный баланс (оценка < +1 у белых)");
        }

        private static void PlayAiGame(string title, int whiteDepth, int blackDepth, bool printMoves)
        {
            var s = new GameState();
            var notation = new List<string>();
            var repetitions = new Dictionary<string, int>();
            int plies = 0;
            string drawByRepetition = null;

            repetitions[PositionKey(s)] = 1;

            while (plies < 600)
            {
                List<Move> legal = MoveGenerator.GenerateLegal(s);
                if (MoveGenerator.GetResult(s, legal) != GameResult.Ongoing)
                    break;

                var stats = new SearchStats();
                Move best = MinimaxAI.FindBestMove(s, s.WhiteToMove ? whiteDepth : blackDepth, stats);

                if (!legal.Contains(best))
                { Check(false, title + ": ИИ выбрал недопустимый ход " + best); return; }

                notation.Add(best.ToString());
                s.MakeMove(best);
                plies++;

                // троекратное повторение позиции — ничья (проверяется на уровне партии,
                // а не внутри поиска, чтобы не замедлять Minimax)
                string key = PositionKey(s);
                repetitions.TryGetValue(key, out int seen);
                seen++;
                repetitions[key] = seen;
                if (seen >= 3)
                {
                    drawByRepetition = "ничья: троекратное повторение позиции";
                    break;
                }
            }

            string verdict = drawByRepetition ?? ResultName(MoveGenerator.GetResult(s));
            Console.WriteLine("  " + title + ": " + verdict + " за " + (plies + 1) / 2 + " ходов");
            if (printMoves)
                Console.WriteLine("  партия: " + string.Join(" ", notation.Select((m, i) => (i % 2 == 0 ? (i / 2 + 1) + "." : "") + m)));
            Check(true, title + ": партия корректно завершилась");
        }

        private static string PositionKey(GameState s)
        {
            var sb = new StringBuilder(26);
            for (int i = 0; i < GameState.CellCount; i++)
                sb.Append(GameState.PieceChar(s.Board[i]));
            sb.Append(s.WhiteToMove ? 'w' : 'b');
            return sb.ToString();
        }

        private static void TestSelfPlay()
        {
            Console.WriteLine("[8] ИИ против ИИ (проверка легальности всех ходов и завершения)");
            PlayAiGame("глубина 3 (белые) против 2 (чёрные)", 3, 2, true);
            PlayAiGame("глубина 3 против 3", 3, 3, false);
            PlayAiGame("глубина 4 (белые) против 3 (чёрные)", 4, 3, false);
        }

        private static void PrintReportTables()
        {
            Console.WriteLine("[R] Данные для отчёта");

            Console.WriteLine("  -- Оценки ходов белых в начальной позиции (глубина 3, точный режим) --");
            var s0 = new GameState();
            var st0 = new SearchStats();
            MinimaxAI.FindBestMove(s0, 3, st0, exactRootScores: true);
            foreach (var kv in st0.RootMoveScores.OrderByDescending(kv => kv.Value))
                Console.WriteLine("     " + kv.Key + "   " + F(kv.Value) + (kv.Key.ToString() == st0.BestMove.ToString() ? "   <-- лучший" : ""));
            Console.WriteLine("     узлов: " + st0.Nodes + ", время: " + st0.Milliseconds.ToString("0.0") + " мс");

            Console.WriteLine("  -- Рост дерева поиска от глубины (начальная позиция, альфа-бета) --");
            foreach (int d in new[] { 2, 3, 4, 5, 6 })
            {
                var s = new GameState();
                var stats = new SearchStats();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Move best = MinimaxAI.FindBestMove(s, d, stats);
                sw.Stop();
                Console.WriteLine("     глубина " + d + ": узлов " + stats.Nodes.ToString().PadLeft(9) +
                                  ", время " + sw.Elapsed.TotalMilliseconds.ToString("0.0").PadLeft(8) + " мс, лучший ход " +
                                  best + " (" + F(stats.Score) + ")");
            }

            Console.WriteLine("  -- Эффект глубины на отравленную пешку (позиция из теста 7) --");
            foreach (int d in new[] { 1, 2, 3 })
            {
                var s = new GameState(new[] { "....k", "....p", ".....", "r.P..", "..R.K" }, false);
                var stats = new SearchStats();
                Move best = MinimaxAI.FindBestMove(s, d, stats);
                Console.WriteLine("     глубина " + d + ": ход " + best + " (" + F(stats.Score) + ")" +
                                  (best.ToString() == "a2xc2" ? "   <-- попадается в ловушку" : ""));
            }
        }
    }
}
