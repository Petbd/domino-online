using Microsoft.AspNetCore.SignalR;
using DominoOnline.Server.Services;
using DominoOnline.Shared.Models;

namespace DominoOnline.Server.Hubs;

public class GameHub : Hub
{
    private readonly GameService _gameService;
    private static readonly Dictionary<string, List<ChatMessage>> _chatHistory = new();
    private static readonly object _chatLock = new();

    public GameHub(GameService gameService)
    {
        _gameService = gameService;
    }


    public async Task<string> CreateGame(string nickname, int targetScore = 101)
    {
        var game = _gameService.CreateGame(Context.ConnectionId, nickname, targetScore);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.GameId);
        await Clients.Caller.SendAsync("GameCreated", game.GameId);
        return game.GameId;
    }

    public async Task<bool> JoinGame(string gameId, string nickname)
    {
        var game = _gameService.JoinGame(gameId, Context.ConnectionId, nickname);
        if (game == null) return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Group(gameId).SendAsync("PlayerJoined", nickname);
        if (game.IsFull)
            await Clients.Group(gameId).SendAsync("GameReady");
        return true;
    }

    public async Task StartGame(string gameId)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null || !game.Players[0].IsAdmin) return;

        if (!_gameService.StartGame(gameId)) return;
        game = _gameService.GetGame(gameId)!;

        foreach (var player in game.Players)
        {
            await Clients.Client(player.ConnectionId).SendAsync("HandDealt", player.Hand);
        }

        await BroadcastState(gameId);
        await Clients.Group(gameId).SendAsync("GameStarted", game.CurrentTurnConnectionId);
    }

    public async Task MakeMove(string gameId, DominoTile tile, bool placeLeft)
    {
        var result = _gameService.MakeMove(gameId, Context.ConnectionId, tile, placeLeft);
        if (!result.Success)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        var game = _gameService.GetGame(gameId)!;
        var placedTile = placeLeft ? game.Board.First() : game.Board.Last();

        await Clients.Group(gameId).SendAsync("MoveMade", placedTile, placeLeft,
            game.CurrentTurnConnectionId, game.LeftEnd, game.RightEnd);

        await BroadcastState(gameId);

        if (result.RoundOver)
        {
            var winner = game.Players.First(p => p.ConnectionId == result.RoundWinnerConnectionId);
            await Clients.Group(gameId).SendAsync("RoundOver",
                winner.Nickname,
                winner.ConnectionId,
                result.PointsAwarded,
                winner.Score,
                result.TargetScore);

            if (result.MatchOver)
            {
                winner = game.Players.First(p => p.ConnectionId == result.MatchWinnerConnectionId);

                // 💾 Сохраняем в историю
                _gameService.SaveMatchResult(gameId, winner.Nickname, game.TargetScore, game.Players);

                await Clients.Group(gameId).SendAsync("MatchOver",
                    winner.Nickname,
                    winner.ConnectionId,
                    winner.Score);
            }
        }
    }

    public async Task DrawFromBoneyard(string gameId)
    {
        var result = _gameService.DrawFromBoneyard(gameId, Context.ConnectionId);
        if (result.tile == null) return;

        await Clients.Caller.SendAsync("TileDrawn", result.tile, result.canPlay);
        await BroadcastState(gameId);

        if (!result.canPlay)
        {
            var game = _gameService.GetGame(gameId);
            if (game != null && game.Boneyard.Count == 0)
                await Clients.Group(gameId).SendAsync("TurnChanged", game.CurrentTurnConnectionId);
        }
    }

    public async Task SkipTurn(string gameId)
    {
        if (_gameService.SkipTurn(gameId, Context.ConnectionId))
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
                await Clients.Group(gameId).SendAsync("TurnChanged", game.CurrentTurnConnectionId);
            await BroadcastState(gameId);
        }
    }

    // НОВОЕ: синхронизация состояния при входе на страницу игры
    public async Task SyncState(string gameId)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null) return;

        var player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player != null && game.IsStarted)
        {
            await Clients.Caller.SendAsync("HandDealt", player.Hand);
        }

        if (game.Board.Count > 0)
            await Clients.Caller.SendAsync("BoardSynced", game.Board, game.LeftEnd, game.RightEnd);

        if (game.IsStarted)
            await Clients.Caller.SendAsync("GameStarted", game.CurrentTurnConnectionId);

        await Clients.Caller.SendAsync("StateUpdated", _gameService.GetPlayerDtos(gameId), game.Boneyard.Count);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _gameService.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastState(string gameId)
    {
        var players = _gameService.GetPlayerDtos(gameId);
        var game = _gameService.GetGame(gameId);
        int boneyardCount = game?.Boneyard.Count ?? 0;
        await Clients.Group(gameId).SendAsync("StateUpdated", players, boneyardCount);
    }

    public async Task StartNewRound(string gameId)
    {
        if (_gameService.StartNewRound(gameId))
        {
            var game = _gameService.GetGame(gameId)!;

            // Раздаём новые кости
            foreach (var player in game.Players)
            {
                await Clients.Client(player.ConnectionId).SendAsync("HandDealt", player.Hand);
            }

            // 🔥 ОЧИЩАЕМ СТОЛ НА ВСЕХ КЛИЕНТАХ
            await Clients.Group(gameId).SendAsync("BoardSynced", new List<DominoTile>(), -1, -1);

            await BroadcastState(gameId);
            await Clients.Group(gameId).SendAsync("GameStarted", game.CurrentTurnConnectionId);
            await Clients.Group(gameId).SendAsync("RoundStarted", game.CurrentRound);
        }
    }

    public async Task Rematch(string oldGameId)
    {
        var oldGame = _gameService.GetGame(oldGameId);
        if (oldGame == null) return;

        var admin = oldGame.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (admin == null || !admin.IsAdmin) return;

        var newGame = _gameService.Rematch(oldGameId, Context.ConnectionId, admin.Nickname);
        if (newGame == null) return;

        // Добавляем админа в группу новой игры
        await Groups.AddToGroupAsync(Context.ConnectionId, newGame.GameId);

        // Добавляем остальных игроков из старой игры
        foreach (var player in oldGame.Players.Where(p => p.ConnectionId != Context.ConnectionId))
        {
            var joined = _gameService.JoinGame(newGame.GameId, player.ConnectionId, player.Nickname);
            if (joined != null)
            {
                await Groups.AddToGroupAsync(player.ConnectionId, newGame.GameId);
                await Clients.Group(newGame.GameId).SendAsync("PlayerJoined", player.Nickname);
            }
        }

        if (newGame.IsFull)
            await Clients.Group(newGame.GameId).SendAsync("GameReady");

        // Отправляем ВСЕМ в старой игре ссылку на реванш
        await Clients.Group(oldGameId).SendAsync("RematchCreated", newGame.GameId);
    }

    public List<MatchResult> GetHistory() => _gameService.GetHistory();

    public async Task SendChatMessage(string gameId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 200) text = text[..200];

        var game = _gameService.GetGame(gameId);
        var player = game?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player == null) return;

        var message = new ChatMessage
        {
            SenderNickname = player.Nickname,
            Text = text.Trim(),
            SentAt = DateTime.UtcNow
        };

        lock (_chatLock)
        {
            if (!_chatHistory.ContainsKey(gameId))
                _chatHistory[gameId] = new List<ChatMessage>();

            _chatHistory[gameId].Add(message);
            if (_chatHistory[gameId].Count > 50)
                _chatHistory[gameId].RemoveAt(0);
        }

        await Clients.Group(gameId).SendAsync("ChatMessageReceived", message.SenderNickname, message.Text, message.SentAt);
    }

    public List<ChatMessage> GetChatHistory(string gameId)
    {
        lock (_chatLock)
        {
            return _chatHistory.TryGetValue(gameId, out var history)
                ? history.OrderBy(m => m.SentAt).ToList()
                : new List<ChatMessage>();
        }
    }
}