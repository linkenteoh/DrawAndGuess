using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DrawAndGuess
{

    // ============================================================================================
    // Class: Player
    // ============================================================================================
        
    public class Player
    {
        private const int STEP = 1;
        private const int FINISH = 100;

        public string Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public int Count { get; set; } = 0;
        public bool IsWin => Count >= FINISH;
        public bool IsDrawing { get; set; } = false;

        public Player(string id, string name) => (Id, Name) = (id, name);

        public void Run() => Count += STEP;
    }



    // ============================================================================================
    // Class: Game
    // ============================================================================================
    
    public class Game
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string name { get; set; }
        public string type { get; set; }
        public string pass { get; set; }
        public int noPlayers { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public bool IsWaiting { get; set; } = true;

        public bool IsEmpty => Players == null;
        public bool IsFull  => Players.Count == noPlayers;

        public string AddPlayer(Player player)
        {
            Players.Add(player);
            if(Players.Count == noPlayers){
                IsWaiting = false;
            }
            // if (PlayerA == null)
            // {
            //     PlayerA = player;
            //     IsWaiting = true;
            //     return "A";
            // }
            // else if (PlayerB == null)
            // {
            //     PlayerB = player;
            //     IsWaiting = false;
            //     return "B";
            // }

            return null;
        }
    }
    
    // ============================================================================================
    // Class: GameHub 👦🏻👧🏻
    // ============================================================================================
    
    public class GameHub : Hub
    {

        // ----------------------------------------------------------------------------------------
        // Class
        // ----------------------------------------------------------------------------------------
           public class Point
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
    
        // ----------------------------------------------------------------------------------------
        // General
        // ----------------------------------------------------------------------------------------
        private static int count = 0;

        private static List<Game> games = new List<Game>()
        {
            // new Game { PlayerA = new Player("P001", "👦🏻", "Boy") , IsWaiting = true },
            // new Game { PlayerA = new Player("P002", "👧🏻", "Girl"), IsWaiting = true },
        };


        public string Create(string roomName, string roomType, string roomPass, int roomPlayers)
        {
            var game = new Game();
            game.IsWaiting = true;
            game.name = roomName;
            game.type = roomType;
            game.pass = roomPass;
            game.noPlayers = roomPlayers;
            
            games.Add(game);
            return game.Id;
        }

        public string getRoomPass(string roomId){
            var game = games.Find(g=> g.Id == roomId);
            string pass = "";
            if(game.type == "Private"){
                pass += game.pass.ToString();
            }
            
            return pass;
        }

        // TODO: Start()
        public async Task Start(int p)
        {
            int player = 0;
            string gameId = Context.GetHttpContext().Request.Query["gameId"];

            Game game = games.Find(g => g.Id == gameId);
            if (game == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }
            
            
            
            await Clients.Group(gameId).SendAsync("Start");
            string drawId = game.Players[p].Id;
            await SendDrawText(drawId, p);
        }

        public async Task SendDrawText(string id, int player)
        {
            await Clients.Client(id).SendAsync("DrawText", player);
        }

        // ----------------------------------------------------------------------------------------
        // Drawings
        // ----------------------------------------------------------------------------------------
        public async Task SendLine(Point a, Point b, int size, string color, string gameId)
        {
            await Clients.Group(gameId).SendAsync("ReceiveLine", a, b, size, color);
        }

        public async Task SendCurve(Point a, Point b, Point c, int size, string color, string gameId)
        {
            await Clients.Group(gameId).SendAsync("ReceiveCurve", a, b, c, size, color);
        }

        public async Task SendClear(string gameId)
        {
            await Clients.Group(gameId).SendAsync("ReceiveClear");
        }
        // ----------------------------------------------------------------------------------------
        // Functions
        // ----------------------------------------------------------------------------------------
 
        private async Task UpdateList(string id = null)
        {
            var list = games;

            if (id == null)
            {
                await Clients.All.SendAsync("UpdateList", list);
            }
            else
            {
                await Clients.Client(id).SendAsync("UpdateList", list);
            }
        }

        // ----------------------------------------------------------------------------------------
        // Connected
        // ----------------------------------------------------------------------------------------

        public override async Task OnConnectedAsync()
        {
            count++;
            string page = Context.GetHttpContext().Request.Query["page"];
            await Clients.All.SendAsync("UpdateCount", count);
            switch (page)
            {
                case "list": await ListConnected(); break;
                case "game": await GameConnected(); break;
            }

            await base.OnConnectedAsync();
        }

        private async Task ListConnected()
        {
            string id = Context.ConnectionId;
            await UpdateList(id);
        }

        private static List<Usernames> usernames = new List<Usernames>();

        public class Usernames
        {
            public string Name { get; set; }
            public string GameId { get; set; }

            public Usernames(string name, string gameId)
            {
                Name = name;
                GameId = gameId;
            }
        }

        private async Task GameConnected()
        {
            string id     = Context.ConnectionId;
            string name   = Context.GetHttpContext().Request.Query["name"];
            string gameId = Context.GetHttpContext().Request.Query["gameId"];
            var username = new List<string>();

        
            usernames.Add(new Usernames(name, gameId));                

            await Groups.AddToGroupAsync(id, gameId);
            foreach (var item in usernames)
            {
                if(gameId == item.GameId){
                    username.Add(item.Name);
                }
            }

            string[] arrayOfStrings = username.ToArray();
            await Clients.Caller.SendAsync("UpdatePlayers", username);
            await Clients.OthersInGroup(gameId).SendAsync("NewPlayer", name);
            
            Game game = games.Find(g => g.Id == gameId);
            Player p = new Player(id, name);
            game.AddPlayer(p);
            await Groups.AddToGroupAsync(id, gameId);
            await Clients.Group(gameId).SendAsync("PlayerJoined", game);
            await UpdateList();
        }

        // ----------------------------------------------------------------------------------------
        // Disconnected
        // ----------------------------------------------------------------------------------------

        public override async Task OnDisconnectedAsync(Exception exception) 
        {
            count--;
            await Clients.All.SendAsync("UpdateCount", count);
            string page = Context.GetHttpContext().Request.Query["page"];

            switch (page)
            {
                case "list": ListDisconnected(); break;
                case "game": await GameDisconnected(); break;
            }

            await base.OnDisconnectedAsync(exception);
        }

        private void ListDisconnected()
        {
            // Nothing
        }

        private async Task GameDisconnected()
        {
            string id     = Context.ConnectionId;
            string name   = Context.GetHttpContext().Request.Query["name"];
            string gameId = Context.GetHttpContext().Request.Query["gameId"];
            
            usernames.RemoveAll(x => x.GameId == gameId && x.Name == name);
            Game game = games.Find(g => g.Id == gameId);
            Player player = game.Players.Find(p => p.Id == id);
            game.Players.Remove(player);
            await Clients.Group(gameId).SendAsync("Left", name);

            if(game.Players.Count == 0){
                games.Remove(game);
            }

            await UpdateList();


            // Game game = games.Find(g => g.Id == gameId);
            // if (game == null)
            // {
            //     await Clients.Caller.SendAsync("Reject");
            //     return;
            // }

            // if (game.PlayerA?.Id == id)
            // {
            //     game.PlayerA = null;
            //     await Clients.Group(gameId).SendAsync("Left", "A");
            // }
            // else if (game.PlayerB?.Id == id)
            // {
            //     game.PlayerB = null;
            //     await Clients.Group(gameId).SendAsync("Left", "B");
            // }

            // if (game.IsEmpty)
            // {
            //     games.Remove(game);
            //     await UpdateList();
            // }
        }

        // ----------------------------------------------------------------------------------------
        // Messages
        // ----------------------------------------------------------------------------------------
        public async Task SendText(string gameId, string message, string name)
        {
            await Clients.Group(gameId).SendAsync("ReceiveText", name, message);
        }

        // End of GameHub -------------------------------------------------------------------------
    }
}