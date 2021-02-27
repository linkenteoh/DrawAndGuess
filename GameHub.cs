using System;
using System.Timers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Linq;


namespace DrawAndGuess
{

    // ============================================================================================
    // Class: Player
    // ============================================================================================
        
    public class Player : IComparable<Player>
    {

        public string Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; } = 0;
        public int Score { get; set; } = 0;
        public bool IsDrawing { get; set; } = false;
        public bool IsCorrect { get; set; } = false;
        public bool IsHost { get; set; } = false;

        public Player(string id, string name) => (Id, Name) = (id, name);


        public int CompareTo(Player other)
        {
            return Score - other.Score;
        }
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
        public int noRounds { get; set; }
        public int currentRound { get; set; } 
        public string question { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public bool IsWaiting { get; set; } = true;

        public bool IsEnd => currentRound > noRounds;

        public bool IsEmpty => Players == null;
        public bool IsFull  => Players.Count == noPlayers;

        public Game(string id, string Name, int players ,int rounds, string password, string rType) => (Id, name, noPlayers, noRounds, pass, type) = (id, Name, players ,rounds, password, rType);
        public Game(){

        }

        public string AddPlayer(Player player)
        {
            Players.Add(player);
            if(Players.Count == noPlayers){
                IsWaiting = false;
            }

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

        private static string[] questions = {"Cat", "Dog", "Fish", "Water", "Apple", "Goldfish", "Crocodile"};

        public List<Game> RetrieveGames()
        {
            List<Game> PublicList = new List<Game>();
            foreach (var game in games)
            {
                if(game.type == "Public" && game.IsWaiting)
                {
                    PublicList.Add(game);
                }
            }
            return PublicList;
        }

        public string Create(string roomName, string roomType, string roomPass, int roomPlayers, int rounds)
        {
            var game = new Game();
            game.IsWaiting = true;
            game.name = roomName;
            game.type = roomType;
            game.pass = roomPass;
            game.noPlayers = roomPlayers;
            game.noRounds = rounds;
            
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

        public async Task PlayAgain(Game game) 
        {
            string name   = Context.GetHttpContext().Request.Query["name"];
            string gameId = Context.GetHttpContext().Request.Query["gameId"];

            // Game newGame = games.Find(g => g.Id == game.Id);
            // Player p = new Player(Context.ConnectionId, name);
            // game.AddPlayer(p);

            Game newGame = new Game();
            newGame.IsWaiting = true;
            newGame.name = game.name;
            newGame.noPlayers = game.noPlayers;
            newGame.noRounds = game.noRounds;
            newGame.pass = game.pass;
            newGame.Players = new List<Player>();
            //newGame.// AddPlayer(new Player(Context.ConnectionId, name)),
            newGame.type = game.type;

            games.Add(newGame);
            await Clients.Caller.SendAsync("NewGame", newGame.Id);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
            await Clients.Group(gameId).SendAsync("JoinNewGame", newGame.Id);

            games.Remove(game);
            await UpdateList();
        }

        

        public async Task InvitePlayer(string conId)
        {
            string gameId = Context.GetHttpContext().Request.Query["gameId"];

            await Clients.Client(conId).SendAsync("ReceiveNotification", gameId);
        }

        public async Task UpdateUsers(string name)
        {
            User user = new User(Context.ConnectionId, name);
            users.Add(user);           
            await Clients.All.SendAsync("OnlinePlayers", users);
        }

        public async Task Start(int drawIndex, int currentRound) // 1 , 2 
        {
            string gameId = Context.GetHttpContext().Request.Query["gameId"];
            
            Game game = games.Find(g => g.Id == gameId);

            //
            foreach (var player in game.Players)
            {
                player.IsCorrect = false;
            }

            game.currentRound = currentRound; //Game round 2 
            if (game == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }

            await Clients.Group(gameId).SendAsync("UpdatePlayers", game.Players, null);


            //await AssignQues(game, drawIndex);
            game.question = questions[drawIndex];
            
            if(drawIndex == game.noPlayers) //5th player's turn in a 4players room which does not exist - represents game ends.
            {
                if(game.IsEnd)
                {
                    game.Players = game.Players.OrderByDescending(x=> x.Score).ToList();

                    await Clients.Caller.SendAsync("End", game);
                    Player host = game.Players.Find(p => p.IsHost == true);

                    if(Context.ConnectionId == host.Id) // Host will be called this method only.
                    {
                        await Clients.Caller.SendAsync("EnableReplay");
                        await Clients.Others.SendAsync("WaitingReplay");
                    }


                    return;
                }else{
                    game.Players[drawIndex - 1].IsDrawing = false;
                    game.Players[0].IsDrawing = true;

                    await Clients.Caller.SendAsync("NewRound", game, 0, game.currentRound);
                    return;
                }
                
            }
            else
            {
                game.Players[drawIndex].IsDrawing = true;
                if(drawIndex > 0){
                    game.Players[drawIndex -1].IsDrawing = false;
                }
                await Clients.Caller.SendAsync("Start", game, Context.ConnectionId, drawIndex, game.currentRound);

            }
        }

        // public async Task AssignQues(Game game, int index){
        //     // Random pick one string from the array
        //     Random rand = new Random();  
        //     //int index = rand.Next(questions.Length);

        //     //Assign random question into specific game object
        //     game.question = questions[index];
        // }

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

            await Clients.All.SendAsync("OnlinePlayers", users);
        }

        // ----------------------------------------------------------------------------------------
        // Connected
        // ----------------------------------------------------------------------------------------
        public class User
        {
            public string conId { get; set; }
            public string name { get; set; }
            public User(string id, string username) => (conId, name) = (id, username);
        }       


        private static List<User> users = new List<User>()
        {
  
        };

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

        private async Task GameConnected()
        {
            string id     = Context.ConnectionId;
            string name   = Context.GetHttpContext().Request.Query["name"];
            string gameId = Context.GetHttpContext().Request.Query["gameId"];
            await Groups.AddToGroupAsync(id, gameId);

            Game game = games.Find(g => g.Id == gameId);
            if(game == null)
            {
                await Clients.Caller.SendAsync("YEST");
            }

            Player p = new Player(id, name);
            if(game.Players.Count == 0)
            {
                p.IsHost = true;   
            }
            game.AddPlayer(p);
        
       
            // await Clients.Caller.SendAsync("OnlinePlayers", users);
            await Clients.Group(gameId).SendAsync("UpdatePlayers", game.Players, p);
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

        private async void ListDisconnected()
        {
            User user = users.Find(u => u.conId == Context.ConnectionId);
            users.Remove(user);
            await Clients.All.SendAsync("OnlinePlayers", users);
        }

        private async Task GameDisconnected()
        {
            string id     = Context.ConnectionId;
            string name   = Context.GetHttpContext().Request.Query["name"];
            string gameId = Context.GetHttpContext().Request.Query["gameId"];
            Game game = games.Find(g => g.Id == gameId);

            await Groups.RemoveFromGroupAsync(id, gameId);


            Player player = game.Players.Find(p => p.Id == id);
            game.Players.Remove(player);
            await Clients.Group(gameId).SendAsync("Left", name);

            if(game.Players.Count == 0){
                games.Remove(game);
            }

            await UpdateList();
        }

        // ----------------------------------------------------------------------------------------
        // Messages
        // ----------------------------------------------------------------------------------------
        public async Task SendText(string gameId, string message, string name)
        {
            Game game = games.Find(g => g.Id == gameId);
            Player player = game.Players.Find(p => p.Id == Context.ConnectionId);

            string ans = game.question;

            if(message.Equals(ans, StringComparison.InvariantCultureIgnoreCase) && !player.IsDrawing && !player.IsCorrect)
            {
                await Clients.OthersInGroup(gameId).SendAsync("ReceiveText", name, "guessed correctly!" ,"Ans");
                await Clients.Caller.SendAsync("ReceiveText", name, "You've guessed right! + 25pts", "SysMsg");
                player.Score += 25;
                player.IsCorrect = true;

                await Clients.Group(gameId).SendAsync("UpdatePlayers", game.Players, null);
            }
            else if(message.Equals(ans, StringComparison.InvariantCultureIgnoreCase) && player.IsDrawing)
            {
                await Clients.Caller.SendAsync("ReceiveText", name, "You're not guessing.", "SysMsg");

            }
            else if(!message.Equals(ans, StringComparison.InvariantCultureIgnoreCase))
            {
                await Clients.Group(gameId).SendAsync("ReceiveText", name, message, "NormalChat");
            }
         
  
        }

        // End of GameHub -------------------------------------------------------------------------
    }
}