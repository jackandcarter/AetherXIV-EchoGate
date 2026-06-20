/*
===========================================================================
Copyright (C) 2015-2019 Project Meteor Dev Team

This file is part of Project Meteor Server.

Project Meteor Server is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Project Meteor Server is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with Project Meteor Server. If not, see <https:www.gnu.org/licenses/>.
===========================================================================
*/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using NLog;
using MeteorXIV.Core.Common;
using MeteorXIV.Core.World.DataObjects;
using MySql.Data.MySqlClient;

namespace MeteorXIV.Core.World
{
    class Program
    {
        private const int EXIT_OK = 0;
        private const int EXIT_CONFIG = 10;
        private const int EXIT_DATABASE = 20;
        private const int EXIT_STARTUP = 30;
        private const int EXIT_UNHANDLED = 50;

        public static Logger Log;

        static int Main(string[] args)
        {
            // set up logging
            Log = LogManager.GetCurrentClassLogger();

            Log.Info("==================================");
            Log.Info("MeteorXIV Core v1.2: World Server");
            Log.Info("Version: 1.2");
            Log.Info("==================================");

#if DEBUG
            TextWriterTraceListener myWriter = new TextWriterTraceListener(System.Console.Out);
            Debug.Listeners.Add(myWriter);

            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(5000);
            }

#endif

            bool smoke = HasFlag(args, "smoke");
            bool noConsole = HasFlag(args, "no-console");
            DevDiagnostics.Configure("World", args);

            try
            {
                ConfigConstants.Load();
                ConfigConstants.ApplyLaunchArgs(FilterLaunchArgs(args));
            }
            catch (Exception e)
            {
                return ExitOrPrompt(smoke, SmokeFail("World", "config", e.Message, EXIT_CONFIG));
            }

            try
            {
                TestDatabaseConnection();
            }
            catch (MySqlException e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("World", "database", e.Message, EXIT_DATABASE));
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("World", "unhandled", e.Message, EXIT_UNHANDLED));
            }

            try
            {
                LoadWorldInfo();
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("World", "unhandled", e.Message, EXIT_UNHANDLED));
            }

            try
            {
                Server server = new Server();                
                server.StartServer();

                if (smoke)
                    return SmokeOk("World", GetEndpoint());

                if (noConsole)
                {
                    Log.Info("Console input disabled; server running until process exit.");
                    while (true) Thread.Sleep(10000);
                }

                while (true)
                {
                    String input = Console.ReadLine();
                    if (input == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    Log.Info("[Console Input] " + input);
                    //cp.DoCommand(input, null);
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return ExitOrPrompt(smoke, SmokeFail("World", "startup", e.Message, EXIT_STARTUP));
            }
        }

        private static void TestDatabaseConnection()
        {
            Log.Info("Testing DB connection... ");
            using (MySqlConnection conn = new MySqlConnection(String.Format("Server={0}; Port={1}; Database={2}; UID={3}; Password={4}", ConfigConstants.DATABASE_HOST, ConfigConstants.DATABASE_PORT, ConfigConstants.DATABASE_NAME, ConfigConstants.DATABASE_USERNAME, ConfigConstants.DATABASE_PASSWORD)))
            {
                conn.Open();
                conn.Close();
                Log.Info("Connection ok.");
            }
        }

        private static void LoadWorldInfo()
        {
            DBWorld thisWorld = Database.GetServer(ConfigConstants.DATABASE_WORLDID);
            if (thisWorld != null)
            {
                Log.Info("Successfully pulled world info from DB. Server name is {0}.", thisWorld.name);
                ConfigConstants.PREF_SERVERNAME = thisWorld.name;
            }
            else
            {
                Log.Info("World info could not be retrieved from the DB. Welcome and MOTD will not be displayed.");
                ConfigConstants.PREF_SERVERNAME = "Unknown";
            }
        }

        private static bool HasFlag(string[] args, string flagName)
        {
            foreach (string arg in args)
            {
                if (arg.Trim().TrimStart('-').Equals(flagName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string[] FilterLaunchArgs(string[] args)
        {
            List<string> filtered = new List<string>();
            foreach (string arg in args)
            {
                if (!IsRuntimeFlag(arg) && !DevDiagnostics.IsFlag(arg))
                    filtered.Add(arg);
            }

            return filtered.ToArray();
        }

        private static bool IsRuntimeFlag(string arg)
        {
            string name = arg.Trim().TrimStart('-');
            return name.Equals("smoke", StringComparison.OrdinalIgnoreCase)
                || name.Equals("no-console", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEndpoint()
        {
            return String.Format("{0}:{1}", ConfigConstants.OPTIONS_BINDIP, ConfigConstants.OPTIONS_PORT);
        }

        private static int SmokeOk(string serverName, string endpoint)
        {
            Console.WriteLine("SMOKE_OK {0} {1}", serverName, endpoint);
            return EXIT_OK;
        }

        private static int SmokeFail(string serverName, string category, string message, int exitCode)
        {
            Console.WriteLine("SMOKE_FAIL {0} {1}: {2}", serverName, category, Sanitize(message));
            return exitCode;
        }

        private static string Sanitize(string message)
        {
            if (String.IsNullOrEmpty(message))
                return "unknown";

            return message.Replace(Environment.NewLine, " ").Replace("\n", " ").Replace("\r", " ");
        }

        private static int ExitOrPrompt(bool smoke, int exitCode)
        {
            if (smoke)
                return exitCode;

            Log.Info("Press any key to continue...");
            Console.ReadKey();
            return exitCode;
        }
    }
}
