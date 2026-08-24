using Microsoft.AspNetCore.SignalR.Client;
using DominoOnline.Shared.Models;

namespace DominoOnline.Client.Services;

public class GameHubService
{
    private HubConnection? _hubConnection;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public string? CurrentGameId { get; private set; }
    public string? MyConnectionId { get; private set; }

    public event Action<string>? OnGameCreated;
    public event Action<string>? OnPlayerJoined;
    public event Action? OnGameReady;
    public event Action<string>? OnGameStarted;
    public event Action<DominoTile, bool, string, int, int>? OnMoveMade;
    public event Action<List<DominoTile>>? OnHandDealt;
    public event Action<DominoTile, bool>? OnTileDrawn;
    public event Action<List<PlayerDto>, int>? OnStateUpdated;
    public event Action<string, string>? OnGameOver;
    public event Action<string>? OnTurnChanged;
    public event Action<string>? OnError;
    public event Action<List<DominoTile>, int, int>? OnBoardSynced;
    public event Action<string, string, int, int, int>? OnRoundOver;
    public event Action<string, string, int>? OnMatchOver;
    public event Action<int>? OnRoundStarted;
    public event Action<string>? OnRematchCreated;
    public event Action<string, string, DateTime>? OnChatMessageReceived;

    public async Task ConnectAsync(string hubUrl)
    {
        // 🔥 ГЛАВНОЕ: не переподключаемся, если уже есть соединение
        if (_hubConnection != null &&
            (_hubConnection.State == HubConnectionState.Connected ||
             _hubConnection.State == HubConnectionState.Connecting))
        {
            return;
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string>("GameCreated", id =>
        {
            CurrentGameId = id;
            OnGameCreated?.Invoke(id);
        });

        _hubConnection.On<string>("PlayerJoined", nick => OnPlayerJoined?.Invoke(nick));
        _hubConnection.On("GameReady", () => OnGameReady?.Invoke());

        _hubConnection.On<string>("GameStarted", turnId => OnGameStarted?.Invoke(turnId));

        _hubConnection.On<DominoTile, bool, string, int, int>("MoveMade",
            (tile, left, nextId, leftEnd, rightEnd) =>
                OnMoveMade?.Invoke(tile, left, nextId, leftEnd, rightEnd));

        _hubConnection.On<List<DominoTile>>("HandDealt", hand => OnHandDealt?.Invoke(hand));

        _hubConnection.On<DominoTile, bool>("TileDrawn", (tile, canPlay) =>
            OnTileDrawn?.Invoke(tile, canPlay));

        _hubConnection.On<List<PlayerDto>, int>("StateUpdated", (players, boneyard) =>
            OnStateUpdated?.Invoke(players, boneyard));

        _hubConnection.On<string, string>("GameOver", (winnerName, winnerId) =>
            OnGameOver?.Invoke(winnerName, winnerId));

        _hubConnection.On<string>("TurnChanged", nextId => OnTurnChanged?.Invoke(nextId));
        _hubConnection.On<string>("Error", msg => OnError?.Invoke(msg));

        _hubConnection.On<List<DominoTile>, int, int>("BoardSynced", (board, left, right) =>
            OnBoardSynced?.Invoke(board, left, right));

        _hubConnection.On<string>("RematchCreated", newGameId =>
            OnRematchCreated?.Invoke(newGameId));

        _hubConnection.On<string, string, DateTime>("ChatMessageReceived", (sender, text, time) =>
            OnChatMessageReceived?.Invoke(sender, text, time));

        _hubConnection.On<string, string, int, int, int>("RoundOver", (winnerName, winnerId, points, totalScore, targetScore) =>
    OnRoundOver?.Invoke(winnerName, winnerId, points, totalScore, targetScore));

        _hubConnection.On<string, string, int>("MatchOver", (winnerName, winnerId, finalScore) =>
            OnMatchOver?.Invoke(winnerName, winnerId, finalScore));

        _hubConnection.On<int>("RoundStarted", round => OnRoundStarted?.Invoke(round));

        await _hubConnection.StartAsync();
        MyConnectionId = _hubConnection.ConnectionId;
    }

    public async Task CreateGameAsync(string nickname, int targetScore) =>
        await _hubConnection!.InvokeAsync("CreateGame", nickname, targetScore);

    public async Task<bool> JoinGameAsync(string gameId, string nickname) =>
        await _hubConnection!.InvokeAsync<bool>("JoinGame", gameId, nickname);

    public async Task StartGameAsync(string gameId) =>
        await _hubConnection!.InvokeAsync("StartGame", gameId);

    public async Task MakeMoveAsync(string gameId, DominoTile tile, bool placeLeft) =>
        await _hubConnection!.InvokeAsync("MakeMove", gameId, tile, placeLeft);

    public async Task DrawFromBoneyardAsync(string gameId) =>
        await _hubConnection!.InvokeAsync("DrawFromBoneyard", gameId);

    public async Task SkipTurnAsync(string gameId) =>
        await _hubConnection!.InvokeAsync("SkipTurn", gameId);

    public async Task SyncStateAsync(string gameId) =>
        await _hubConnection!.InvokeAsync("SyncState", gameId);

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
            await _hubConnection.DisposeAsync();
    }

    public async Task StartNewRoundAsync(string gameId) =>
    await _hubConnection!.InvokeAsync("StartNewRound", gameId);

    public async Task RematchAsync(string gameId) =>
    await _hubConnection!.InvokeAsync("Rematch", gameId);

    public async Task<List<MatchResult>> GetHistoryAsync() =>
        await _hubConnection!.InvokeAsync<List<MatchResult>>("GetHistory");

    public async Task SendChatMessageAsync(string gameId, string text) =>
        await _hubConnection!.InvokeAsync("SendChatMessage", gameId, text);

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string gameId) =>
        await _hubConnection!.InvokeAsync<List<ChatMessage>>("GetChatHistory", gameId);
}