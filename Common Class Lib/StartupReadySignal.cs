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
using System.IO;

namespace MeteorXIV.Core.Common
{
    public static class StartupReadySignal
    {
        private const string ReadyFileEnvironmentVariable = "METEOR_READY_FILE";

        public static void TryWrite(string serviceName, string endpoint)
        {
            string readyFile = Environment.GetEnvironmentVariable(ReadyFileEnvironmentVariable);
            if (String.IsNullOrWhiteSpace(readyFile))
                return;

            try
            {
                string directory = Path.GetDirectoryName(readyFile);
                if (!String.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(
                    readyFile,
                    String.Format(
                        "service={0}{1}endpoint={2}{1}utc={3:o}{1}",
                        serviceName,
                        Environment.NewLine,
                        endpoint,
                        DateTime.UtcNow));

                DevDiagnostics.Trace(
                    "server.ready",
                    "service", serviceName,
                    "endpoint", endpoint,
                    "readyFile", readyFile);
            }
            catch (Exception e)
            {
                DevDiagnostics.Trace(
                    "server.ready.failed",
                    "service", serviceName,
                    "endpoint", endpoint,
                    "readyFile", readyFile,
                    "error", e.Message);
            }
        }
    }
}
