using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniChess
{
    /// <summary>
    /// Точка входа игры (единственный объект в сцене): настраивает камеру,
    /// создаёт доску и интерфейс и ведёт партию «человек против ИИ».
    ///
    /// Ход ИИ ищется алгоритмом Minimax с альфа-бета отсечением
    /// (см. MinimaxAI), ход человека — кликами по доске.
    /// </summary>
    public sealed class GameLauncher : MonoBehaviour
    {
        private enum Phase { HumanTurn, AIThinking, Animating, GameOver }

        private const int DefaultDepth = 4;

        private GameState state = new GameState();
        private BoardView board;
        private GameUI ui;
        private Camera cam;

        private Phase phase = Phase.HumanTurn;
        private bool humanIsWhite = true;
        private int aiDepth = DefaultDepth;
        private int selectedSquare = -1;
        private List<Move> legalMoves = new List<Move>();

        /// <summary>Сколько раз встречалась каждая позиция — для правила троекратного повторения.</summary>
        private readonly Dictionary<string, int> positionCounts = new Dictionary<string, int>();

        private void Start()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cam = cameraObject.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.16f, 0.20f);
            cam.transform.position = new Vector3(0f, 0.2f, -10f);

            board = new BoardView();
            board.Build();

            ui = new GameUI();
            ui.Build();
            ui.NewGameWhite = () => NewGame(true);
            ui.NewGameBlack = () => NewGame(false);
            ui.UndoClicked = Undo;
            ui.PlayAgain = () => NewGame(humanIsWhite);
            ui.DepthSelected = depth => { aiDepth = depth; ui.SetDepthSelected(depth); };
            ui.SetDepthSelected(aiDepth);

            NewGame(true);
        }

        private void Update()
        {
            FitCamera();

            if (phase == Phase.HumanTurn && Input.GetMouseButtonDown(0))
            {
                int square = SquareAtMouse();
                if (square >= 0)
                    OnSquareClicked(square);
            }
        }

        /// <summary>Подогнать камеру, чтобы доска целиком влезала при любом соотношении сторон.</summary>
        private void FitCamera()
        {
            float aspect = (float)Screen.width / Screen.height;
            cam.orthographicSize = Mathf.Max(3.6f, 3.2f / aspect);
        }

        private int SquareAtMouse()
        {
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 local = board.Root.InverseTransformPoint(world);
            int col = Mathf.RoundToInt(local.x + 2f);
            int row = Mathf.RoundToInt(2f - local.y);
            if (col < 0 || col >= GameState.BoardSize || row < 0 || row >= GameState.BoardSize)
                return -1;
            return GameState.Idx(row, col);
        }

        // ---------------------------------------------------------------- ход человека

        private void OnSquareClicked(int square)
        {
            int piece = state.Board[square];
            bool ownPiece = piece != GameState.Empty && GameState.IsWhitePiece(piece) == humanIsWhite;

            if (selectedSquare >= 0)
            {
                foreach (Move move in legalMoves)
                {
                    if (move.From == selectedSquare && move.To == square)
                    {
                        StartCoroutine(PlayMoveRoutine(move));
                        return;
                    }
                }
            }

            selectedSquare = ownPiece ? square : -1;
            RefreshSelectionView();
        }

        private void RefreshSelectionView()
        {
            board.ClearHighlights();
            ApplyPersistentHighlights();

            if (selectedSquare >= 0)
            {
                board.ShowSelection(selectedSquare);
                var targets = new List<Move>();
                foreach (Move move in legalMoves)
                    if (move.From == selectedSquare)
                        targets.Add(move);
                board.ShowLegalTargets(targets);
            }
        }

        private void ApplyPersistentHighlights()
        {
            if (state.MovesMade > 0)
                board.ShowLastMove(state.LastMove);
            if (MoveGenerator.InCheck(state, state.WhiteToMove))
                board.ShowCheck(state.WhiteToMove ? state.WhiteKingSquare : state.BlackKingSquare);
        }

        // ---------------------------------------------------------------- партия

        /// <summary>Применить ход к состоянию и проиграть его анимацией на доске.</summary>
        private IEnumerator PlayMoveRoutine(Move move)
        {
            phase = Phase.Animating;
            selectedSquare = -1;
            board.ClearHighlights();

            state.MakeMove(move);
            string key = state.PositionKey();
            int seen;
            positionCounts.TryGetValue(key, out seen);
            positionCounts[key] = seen + 1;

            yield return board.AnimateMoveRoutine(move);

            AfterMove();
        }

        private void AfterMove()
        {
            legalMoves = MoveGenerator.GenerateLegal(state);
            GameResult result = CurrentResult();

            if (result != GameResult.Ongoing)
            {
                EndGame(result);
                return;
            }

            if (state.WhiteToMove == humanIsWhite)
            {
                phase = Phase.HumanTurn;
                UpdateStatus();
            }
            else
            {
                StartCoroutine(AITurnRoutine());
            }
        }

        /// <summary>Результат партии с учётом повторений (базовые правила — в MoveGenerator).</summary>
        private GameResult CurrentResult()
        {
            GameResult result = MoveGenerator.GetResult(state, legalMoves);
            if (result != GameResult.Ongoing)
                return result;

            int seen;
            positionCounts.TryGetValue(state.PositionKey(), out seen);
            if (seen >= 3)
                return GameResult.DrawByRepetition;
            return GameResult.Ongoing;
        }

        private void UpdateStatus()
        {
            string side = state.WhiteToMove ? "белые" : "чёрные";
            string message = (state.WhiteToMove == humanIsWhite ? "Ваш ход" : "Ход ИИ") + " (" + side + ")";
            if (MoveGenerator.InCheck(state, state.WhiteToMove))
                message += " — ШАХ!";
            if (state.MovesMade > 0 && state.LastMove.Promotion)
                message += " Пешка превратилась в ладью.";
            ui.SetStatus(message);
        }

        // ---------------------------------------------------------------- ход ИИ

        private IEnumerator AITurnRoutine()
        {
            phase = Phase.AIThinking;
            ui.SetStatus("ИИ думает…");
            yield return null; // кадр на перерисовку статуса

            var stats = new SearchStats();
            Move best = MinimaxAI.FindBestMove(state, aiDepth, stats);

            string evalText;
            if (MinimaxAI.IsMateScore(stats.Score))
                evalText = stats.Score > 0 ? "белые форсируют мат" : "чёрные форсируют мат";
            else
                evalText = "оценка " + stats.Score.ToString("+0.00;-0.00;0.0", System.Globalization.CultureInfo.InvariantCulture) + " у белых";

            ui.SetInfo("ИИ (глубина " + aiDepth + "): просмотрено " +
                       stats.Nodes.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) +
                       " позиций за " + stats.Milliseconds.ToString("0") + " мс\nход " + best + ", " + evalText);

            // короткая пауза, чтобы ход ИИ не появлялся мгновенно
            float elapsed = (float)stats.Milliseconds / 1000f;
            if (elapsed < 0.55f)
                yield return new WaitForSeconds(0.55f - elapsed);

            yield return PlayMoveRoutine(best);
        }

        // ---------------------------------------------------------------- управление партией

        private void NewGame(bool humanWhite)
        {
            StopAllCoroutines();
            humanIsWhite = humanWhite;
            state.ResetToInitial();

            positionCounts.Clear();
            positionCounts[state.PositionKey()] = 1;

            selectedSquare = -1;
            legalMoves = MoveGenerator.GenerateLegal(state);

            ui.HideGameOver();
            board.SetFlipped(!humanWhite);
            board.SyncFromState(state);
            board.ClearHighlights();

            ui.SetInfo(humanWhite
                ? "Вы играете белыми и ходите первыми.\nВыберите фигуру — подсветятся её возможные ходы."
                : "Вы играете чёрными, первым ходит ИИ.");

            if (humanWhite)
            {
                phase = Phase.HumanTurn;
                UpdateStatus();
            }
            else
            {
                StartCoroutine(AITurnRoutine());
            }
        }

        /// <summary>Откат ходов до позиции, где снова ход человека.</summary>
        private void Undo()
        {
            if (state.MovesMade == 0)
                return;
            if (phase != Phase.HumanTurn && phase != Phase.GameOver)
                return; // не мешаем ИИ и анимациям

            StopAllCoroutines();
            ui.HideGameOver();

            do
            {
                state.UndoMove();
                string key = state.PositionKey();
                int seen;
                positionCounts.TryGetValue(key, out seen);
                positionCounts[key] = Mathf.Max(0, seen - 1);
            }
            while (state.MovesMade > 0 && state.WhiteToMove != humanIsWhite);

            board.SyncFromState(state);
            selectedSquare = -1;
            legalMoves = MoveGenerator.GenerateLegal(state);
            board.ClearHighlights();
            ApplyPersistentHighlights();

            if (state.MovesMade == 0 && state.WhiteToMove != humanIsWhite)
            {
                StartCoroutine(AITurnRoutine()); // человек играет чёрными — ИИ начинает заново
                return;
            }

            GameResult result = CurrentResult();
            if (result != GameResult.Ongoing)
            {
                EndGame(result);
                return;
            }

            phase = Phase.HumanTurn;
            UpdateStatus();
        }

        private void EndGame(GameResult result)
        {
            phase = Phase.GameOver;

            string title;
            string subtitle;
            bool humanWon;

            switch (result)
            {
                case GameResult.WhiteCheckmated: // мат белым
                    humanWon = !humanIsWhite;
                    title = humanWon ? "Победа — мат!" : "Поражение — мат";
                    subtitle = humanWon ? "Вы поставили мат белому ИИ." : "ИИ поставил мат вашим белым.";
                    break;

                case GameResult.BlackCheckmated: // мат чёрным
                    humanWon = humanIsWhite;
                    title = humanWon ? "Победа — мат!" : "Поражение — мат";
                    subtitle = humanWon ? "Вы поставили мат чёрному ИИ." : "ИИ поставил мат вашим чёрным.";
                    break;

                default:
                    humanWon = false;
                    title = "Ничья";
                    subtitle = "Ничья: " + ResultText(result) + ".";
                    break;
            }

            ui.SetStatus(title);
            ui.SetInfo(subtitle + "\nМожно отменить последний ход или начать новую партию.");
            ui.ShowGameOver(title, subtitle);
        }

        private static string ResultText(GameResult result)
        {
            switch (result)
            {
                case GameResult.Stalemate: return "пат — нет ходов, но шаха нет";
                case GameResult.DrawByFiftyMoves: return "50 ходов без взятий и ходов пешками";
                case GameResult.DrawByBareKings: return "на доске остались только короли";
                case GameResult.DrawByRepetition: return "позиция повторилась трижды";
                default: return result.ToString();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// В режиме редактора рисует рамку будущей доски: вся графика игры
        /// (клетки, фигуры, интерфейс) создаётся кодом при запуске — процедурные
        /// спрайты невозможно сохранить в файл сцены.
        /// </summary>
        private void OnDrawGizmos()
        {
            const float half = GameState.BoardSize / 2f;
            Gizmos.color = new Color(0.95f, 0.85f, 0.35f, 0.9f);
            for (int i = 0; i <= GameState.BoardSize; i++)
            {
                Gizmos.DrawLine(new Vector3(i - half, -half, 0f), new Vector3(i - half, half, 0f));
                Gizmos.DrawLine(new Vector3(-half, i - half, 0f), new Vector3(half, i - half, 0f));
            }
        }
#endif
    }
}
