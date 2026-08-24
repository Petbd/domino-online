using DominoOnline.Shared.Models;

namespace DominoOnline.Server.Services;

public class GameService
{
    private static readonly Dictionary<string, GameSession> _games = new();
    private static readonly object _lock = new();
    private static readonly List<MatchResult> _matchHistory = new();

    public GameSession CreateGame(string adminConnectionId, string adminNickname, int targetScore = 101)
    {
        lock (_lock)
        {
            var game = new GameSession { TargetScore = targetScore };
            var admin = new Player
            {
                ConnectionId = adminConnectionId,
                Nickname = adminNickname,
                IsAdmin = true,
                IsTurn = false
            };
            game.Players.Add(admin);
            _games[game.GameId] = game;
            return game;
        }
    }

    public GameSession? GetGame(string gameId)
    {
        lock (_lock)
        {
            return _games.TryGetValue(gameId, out var game) ? game : null;
        }
    }

    public GameSession? JoinGame(string gameId, string connectionId, string nickname)
    {
        lock (_lock)
        {
            if (!_games.TryGetValue(gameId, out var game)) return null;
            if (game.IsFull) return null;

            var player = new Player
            {
                ConnectionId = connectionId,
                Nickname = nickname,
                IsAdmin = false,
                IsTurn = false
            };
            game.Players.Add(player);
            return game;
        }
    }

    public void RemovePlayer(string connectionId)
    {
        lock (_lock)
        {
            var game = _games.Values.FirstOrDefault(g => g.Players.Any(p => p.ConnectionId == connectionId));
            if (game != null)
            {
                game.Players.RemoveAll(p => p.ConnectionId == connectionId);
                if (game.Players.Count == 0)
                    _games.Remove(game.GameId);
            }
        }
    }

    public bool StartGame(string gameId)
    {
        lock (_lock)
        {
            if (!_games.TryGetValue(gameId, out var game)) return false;
            if (!game.IsFull || game.IsStarted) return false;

            DealTiles(game);
            game.IsStarted = true;

            // Кто начинает: у кого старший дубль. Если нет дублей — админ.
            var starter = game.Players
                .Select(p => new { Player = p, MaxDouble = p.Hand.Where(t => t.IsDouble).Max(t => (int?)t.Left) })
                .OrderByDescending(x => x.MaxDouble ?? -1)
                .ThenBy(x => x.Player.IsAdmin ? 0 : 1)
                .First().Player;

            starter.IsTurn = true;
            game.CurrentTurnConnectionId = starter.ConnectionId;
            game.LeftEnd = -1;
            game.RightEnd = -1;

            return true;
        }
    }

    public MoveResult MakeMove(string gameId, string connectionId, DominoTile tile, bool placeLeft)
    {
        lock (_lock)
        {
            var game = GetGame(gameId);
            if (game == null) return new MoveResult { Success = false, Error = "Игра не найдена" };
            if (game.CurrentTurnConnectionId != connectionId) return new MoveResult { Success = false, Error = "Не ваш ход" };

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player == null) return new MoveResult { Success = false, Error = "Игрок не найден" };

            var handTile = player.Hand.FirstOrDefault(t => t.Left == tile.Left && t.Right == tile.Right);
            if (handTile == null) return new MoveResult { Success = false, Error = "У вас нет такой кости" };

            // Первая кость на столе
            if (game.Board.Count == 0)
            {
                var placed = new DominoTile(tile.Left, tile.Right);
                game.Board.Add(placed);
                game.LeftEnd = placed.Left;
                game.RightEnd = placed.Right;
            }
            else
            {
                int targetEnd = placeLeft ? game.LeftEnd : game.RightEnd;

                if (tile.Left != targetEnd && tile.Right != targetEnd)
                    return new MoveResult { Success = false, Error = "Кость не подходит к краю стола" };

                if (placeLeft)
                {
                    var placed = (tile.Left == targetEnd)
                        ? new DominoTile(tile.Right, tile.Left)
                        : new DominoTile(tile.Left, tile.Right);
                    game.Board.Insert(0, placed);
                    game.LeftEnd = placed.Left;
                }
                else
                {
                    var placed = (tile.Left == targetEnd)
                        ? new DominoTile(tile.Left, tile.Right)
                        : new DominoTile(tile.Right, tile.Left);
                    game.Board.Add(placed);
                    game.RightEnd = placed.Right;
                }
            }

            player.Hand.Remove(handTile);

            // === ПОДСЧЁТ ОЧКОВ ===
            if (player.Hand.Count == 0)
            {
                // Проигравшие получают штраф = сумма костей в руке
                int totalPenalty = 0;
                foreach (var loser in game.Players.Where(p => p.ConnectionId != connectionId))
                {
                    int penalty = loser.Hand.Sum(t => t.Left + t.Right);
                    loser.Score += penalty;
                    totalPenalty += penalty;
                }

                // Матч окончен, если кто-то набрал TargetScore штрафных очков
                var matchLoser = game.Players.FirstOrDefault(p => p.Score >= game.TargetScore);
                bool matchOver = matchLoser != null;

                if (matchOver)
                {
                    game.IsMatchOver = true;
                    // Победитель — тот, у кого МЕНЬШЕ всего штрафных очков
                    var matchWinner = game.Players.OrderBy(p => p.Score).First();
                    game.MatchWinnerConnectionId = matchWinner.ConnectionId;
                }

                return new MoveResult
                {
                    Success = true,
                    RoundOver = true,
                    RoundWinnerConnectionId = connectionId,
                    PointsAwarded = totalPenalty,
                    MatchOver = matchOver,
                    MatchWinnerConnectionId = game.MatchWinnerConnectionId,
                    TargetScore = game.TargetScore
                };
            }

            NextTurn(game);
            return new MoveResult { Success = true };
        }
    }

    public (DominoTile? tile, bool canPlay) DrawFromBoneyard(string gameId, string connectionId)
    {
        lock (_lock)
        {
            var game = GetGame(gameId);
            if (game == null || game.CurrentTurnConnectionId != connectionId) return (null, false);

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player == null || game.Boneyard.Count == 0) return (null, false);

            var tile = game.Boneyard.First();
            game.Boneyard.RemoveAt(0);
            player.Hand.Add(tile);

            bool canPlay = CanPlay(game, player);
            if (!canPlay && game.Boneyard.Count == 0)
                NextTurn(game);

            return (tile, canPlay);
        }
    }

    public bool SkipTurn(string gameId, string connectionId)
    {
        lock (_lock)
        {
            var game = GetGame(gameId);
            if (game == null || game.CurrentTurnConnectionId != connectionId) return false;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player == null) return false;

            if (CanPlay(game, player) || game.Boneyard.Count > 0) return false;

            NextTurn(game);
            return true;
        }
    }

    public bool StartNewRound(string gameId)
    {
        lock (_lock)
        {
            if (!_games.TryGetValue(gameId, out var game)) return false;
            if (game.IsMatchOver) return false;

            game.CurrentRound++;
            game.Board.Clear();
            game.LeftEnd = -1;
            game.RightEnd = -1;
            game.IsStarted = false;

            DealTiles(game);
            game.IsStarted = true;

            // Сбросить флаги хода
            foreach (var p in game.Players) p.IsTurn = false;

            // Первый ход: снова по старшему дублю
            var starter = game.Players
                .Select(p => new { Player = p, MaxDouble = p.Hand.Where(t => t.IsDouble).Max(t => (int?)t.Left) })
                .OrderByDescending(x => x.MaxDouble ?? -1)
                .ThenBy(x => x.Player.IsAdmin ? 0 : 1)
                .First().Player;

            starter.IsTurn = true;
            game.CurrentTurnConnectionId = starter.ConnectionId;

            return true;
        }
    }

    public List<PlayerDto> GetPlayerDtos(string gameId)
    {
        lock (_lock)
        {
            var game = GetGame(gameId);
            if (game == null) return new();
            return game.Players.Select(p => new PlayerDto
            {
                ConnectionId = p.ConnectionId,
                Nickname = p.Nickname,
                HandCount = p.Hand.Count,
                IsTurn = p.IsTurn,
                Score = p.Score,
                IsAdmin = p.IsAdmin
            }).ToList();
        }
    }

    private void DealTiles(GameSession game)
    {
        var tiles = new List<DominoTile>();
        for (int i = 0; i <= 6; i++)
            for (int j = i; j <= 6; j++)
                tiles.Add(new DominoTile(i, j));

        var rnd = new Random();
        tiles = tiles.OrderBy(x => rnd.Next()).ToList();

        foreach (var player in game.Players)
        {
            player.Hand = tiles.Take(7).ToList();
            tiles.RemoveRange(0, 7);
        }

        game.Boneyard = tiles;
    }

    private bool CanPlay(GameSession game, Player player)
    {
        if (game.Board.Count == 0) return player.Hand.Count > 0;
        return player.Hand.Any(t => t.Left == game.LeftEnd || t.Right == game.LeftEnd ||
                                      t.Left == game.RightEnd || t.Right == game.RightEnd);
    }

    private void NextTurn(GameSession game)
    {
        var currentIndex = game.Players.FindIndex(p => p.ConnectionId == game.CurrentTurnConnectionId);
        if (currentIndex >= 0) game.Players[currentIndex].IsTurn = false;

        var nextIndex = (currentIndex + 1) % game.Players.Count;
        game.Players[nextIndex].IsTurn = true;
        game.CurrentTurnConnectionId = game.Players[nextIndex].ConnectionId;
    }

    public void SaveMatchResult(string gameId, string winnerName, int targetScore, List<Player> players)
    {
        lock (_lock)
        {
            _matchHistory.Add(new MatchResult
            {
                GameId = gameId,
                FinishedAt = DateTime.UtcNow,
                WinnerName = winnerName,
                TargetScore = targetScore,
                PlayerScores = players.Select(p => new PlayerScore
                {
                    Nickname = p.Nickname,
                    Score = p.Score,
                    IsWinner = p.Nickname == winnerName
                }).ToList()
            });

            // Храним только последние 50 матчей
            if (_matchHistory.Count > 50)
                _matchHistory.RemoveAt(0);
        }
    }

    public List<MatchResult> GetHistory()
    {
        lock (_lock)
        {
            return _matchHistory.OrderByDescending(m => m.FinishedAt).Take(20).ToList();
        }
    }

    public GameSession? Rematch(string oldGameId, string adminConnectionId, string adminNickname)
    {
        lock (_lock)
        {
            if (!_games.TryGetValue(oldGameId, out var oldGame)) return null;

            // Создаём новую игру с теми же правилами
            var newGame = CreateGame(adminConnectionId, adminNickname, oldGame.TargetScore);
            return newGame;
        }
    }
}