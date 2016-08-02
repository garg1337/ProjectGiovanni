using Google.Protobuf;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using POGOLib.Net;
using POGOLib.Net.Authentication;
using POGOLib.Pokemon.Data;
using POGOProtos.Map;
using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using ProjectGiovanni.Data;
using ProjectGiovanni.Navigation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ProjectGiovanni
{
    class Program
    {
        private const string pathToMapJson = @"C:\Users\Arjun Garg\PokeBot\ProjectGiovanni\ProjectGiovanni\web\";
        private const string argumentsFileName = "Arguments.json";

        private static Arguments args;

        public static Location NextLocation { get; set; }
        public static ConcurrentQueue<SearchJob> searchJobQueue = new ConcurrentQueue<SearchJob>();

        public static ConcurrentDictionary<string, PokemonJsonForWebServer> pokemonJsonDict = new ConcurrentDictionary<string, PokemonJsonForWebServer>();

        static void Main()
        {

            Log("Loading config");

            // load the config
            try
            {
                string jsonForArgs = File.ReadAllText(argumentsFileName);
                Program.args = new JavaScriptSerializer().Deserialize<Arguments>(jsonForArgs);
            }
            catch (Exception e)
            {
                Log($"Error loading config: {e.ToString()}");
            }

            SearchLoop();
        }

        private static void LoginAtPosition(Credentials credentials, Location location)
        {
            Log($"Attempting signing in for user: {credentials.Username}, pass: {credentials.Password} at lat: {location.Latitude} and lng: {location.Longitude}");

            // first check for an old session and expire it
            if (credentials.Session != null)
            {
                credentials.Session.AccessToken.Expire();
                credentials.Session = null;
            }

            while (credentials.Session == null)
            {
                try
                {
                    credentials.Session = Login.GetSession(
                        credentials.Username,
                        credentials.Password,
                        LoginProvider.PokemonTrainerClub,
                        location.Latitude,
                        location.Longitude);
                }
                catch (Exception e)
                {
                    Log($"Error signing in: {e.ToString()}. Trying again in {args.LoginDelay} seconds.");
                    Thread.Sleep(args.LoginDelay);
                }
            }
        }

        private static void SearchLoop()
        {
            int iteration = 0;

            while (true)
            {
                Log($"Search loop iteration {iteration} starting");
                try
                {
                    SearchLoopIteration(iteration);
                    Log($"Search loop {iteration} complete.");
                    DumpFindings(iteration);
                    pokemonJsonDict.Clear();
                    iteration++;
                }
                catch (Exception e)
                {
                    Log($"Error during search loop {iteration} : {e.ToString()}");
                }
                finally
                {
                    Log($"Waiting {args.ThreadDelay} seconds before starting next scan");
                    Thread.Sleep(args.ThreadDelay * 1000);
                }
            }
        }

        private static void DumpFindings(int iteration)
        {
            string json = JsonConvert.SerializeObject(pokemonJsonDict, Formatting.Indented);

            using (StreamWriter sr = new StreamWriter($"{pathToMapJson}pkmn.json", false))
            {
                sr.WriteLine(json);
            }
        }

        private static void SearchLoopIteration(int iteration)
        {
            // first check to see if a new location was set
            if (NextLocation != null)
            {
                HandleLocationUpdate();
            }

            foreach (Credentials credentials in args.Credentials)
            {
                // now check if each session needs refreshing
                if (credentials.Session != null)
                {
                    TimeSpan remainingTime = credentials.Session.AccessToken.Expiry - DateTime.Now;

                    // ticket almost expired, should login again
                    if (remainingTime.TotalSeconds > 60)
                    {
                        Log($"Skipping Pokemon Go login for {credentials.Username} since already logged in for another {remainingTime.TotalSeconds} seconds.");
                    }
                    else
                    {
                        LoginAtPosition(credentials, args.ScanLocation);
                    }
                }
                else
                {
                    LoginAtPosition(credentials, args.ScanLocation);
                }

            }

            int currentStepNumber = 1;

            IEnumerable<Location> locationSteps = LocationMath.GenerateLocationSteps(args.ScanLocation, args.StepLimit);

            object objLock = new object();

            foreach (Location locationStep in locationSteps)
            {
                //Log($"Queueing search iteration {iteration}, step {currentStepNumber}");
                SearchJob searchJob = new SearchJob()
                {
                    SearchIteration = iteration,
                    StepLocation = locationStep,
                    StepNumber = currentStepNumber,
                    LockObj = objLock
                };

                searchJobQueue.Enqueue(searchJob);
                //WriteToFile($"{searchJob.StepLocation.Latitude}, {searchJob.StepLocation.Longitude}");
                currentStepNumber++;
            }

            List<Task> searchTasks = new List<Task>();

            for(int threadNumber = 0; threadNumber < args.Credentials.Count; threadNumber++)
            {
                Task searchTask = CreateSearchJobProcessingThread(args.Credentials[threadNumber]);
                searchTask.Start();
                searchTasks.Add(searchTask);
            }

            foreach(Task searchTask in searchTasks)
            {
                searchTask.Wait();
            }
        }

        private static Task CreateSearchJobProcessingThread(Credentials credential)
        {
            Task processSearchJobTask = new Task(() =>
            {
                SearchJob searchJob;

                while (searchJobQueue.TryDequeue(out searchJob))
                {
                    if (NextLocation != null)
                    {
                        Log($"New location waiting. Flushing queue.");
                        continue;
                    }

                    Log($"Processing iteration {searchJob.SearchIteration} for step {searchJob.StepNumber}");

                    RepeatedField<MapCell> mapCells = SendMapRequest(credential, searchJob.StepLocation);

                    if (mapCells != null)
                    {
                        ScanMap(mapCells, searchJob.StepLocation);
                    }

                    // now sleep until scanning again
                    Thread.Sleep(4200);
                }
            });

            return processSearchJobTask;
        }


        private static RepeatedField<MapCell> SendMapRequest(Credentials credentials, Location location)
        {
            RepeatedField<MapCell> mapCells = null;
            try
            {
                // TODO: Check for reauth here?
                Session session = credentials.Session;

                // set copy position
                session.Player.SetCoordinates(location.Latitude, location.Longitude);


                var cellIds = MapUtil.GetCellIdsForLatLong(session.Player.Latitude,
                    session.Player.Longitude);

                var sinceTimeMs = new List<long>(cellIds.Length);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var response = session.RpcClient.SendRemoteProcedureCall(new Request
                {
                    RequestType = RequestType.GetMapObjects,
                    RequestMessage = new GetMapObjectsMessage
                    {
                        CellId =
                    {
                        cellIds
                    },
                        SinceTimestampMs =
                    {
                        sinceTimeMs.ToArray()
                    },
                        Latitude = session.Player.Latitude,
                        Longitude = session.Player.Longitude
                    }.ToByteString()
                });
                stopwatch.Stop();
                Log($"Map request took {stopwatch.ElapsedMilliseconds / 1000.0} seconds");


                var mapResponse = GetMapObjectsResponse.Parser.ParseFrom(response);

                mapCells = mapResponse.MapCells;

            } catch (Exception e)
            {
                Log($"Uncaught exception when downloading map {e.ToString()}");
            }

            return mapCells;
        }

        private static void ScanMap(RepeatedField<MapCell> mapCells, Location loc)
        {
            foreach (var mapCell in mapCells)
            {
                foreach (WildPokemon pokemon in mapCell.WildPokemons)
                {
                    PokemonJsonForWebServer pokemonJson = new PokemonJsonForWebServer();
                    pokemonJson.id = (int)pokemon.PokemonData.PokemonId;
                    pokemonJson.lat = pokemon.Latitude;
                    pokemonJson.lng = pokemon.Longitude;
                    pokemonJson.timeleft = pokemon.TimeTillHiddenMs;
                    pokemonJson.timestamp = pokemon.LastModifiedTimestampMs;
                    pokemonJson.name = pokemon.PokemonData.PokemonId.ToString();

                    pokemonJsonDict.GetOrAdd($"{pokemonJson.lat},{pokemonJson.lng}", pokemonJson);
                }
            }
        }

        private static void HandleLocationUpdate()
        {
            Log($"New location set. New Lat: {NextLocation.Latitude}, New Lng: {NextLocation.Longitude}");
            args.ScanLocation = NextLocation;
            NextLocation = null;

            // rewrite the config to file
            string json = JsonConvert.SerializeObject(args, Formatting.Indented);
            using (StreamWriter sr = new StreamWriter("Config.json", false))
            {
                sr.WriteLine(json);
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine(message);
            //using (StreamWriter w = File.AppendText("log.txt"))
            //{
            //    w.WriteLine($"{DateTime.Now} : { message}");
            //}
        }
    }
}
