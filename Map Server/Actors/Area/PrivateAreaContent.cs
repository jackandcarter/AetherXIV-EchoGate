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

using MeteorXIV.Core.Map.actors.director;
using MeteorXIV.Core.Map.Actors;
using MeteorXIV.Core.Map.lua;
using MeteorXIV.Core.Common;
using System;

namespace MeteorXIV.Core.Map.actors.area
{

    class PrivateAreaContent : PrivateArea
    {
        private Director currentDirector;
        private bool isContentFinished = false;

        public static PrivateAreaContent CreateContentArea(String scriptPath)
        {
            return null;
        }

        public PrivateAreaContent(Zone parent, string classPath, string privateAreaName, uint privateAreaType, Director director, Player contentStarter) //TODO: Make it a list
            : base(parent, parent.actorId, classPath, privateAreaName, privateAreaType, 0, 0, 0)
        {
            currentDirector = director;
            DevDiagnostics.Trace(
                "content.area.create",
                "player", contentStarter == null ? "(none)" : contentStarter.customDisplayName,
                "actor", contentStarter == null ? "0x0" : String.Format("0x{0:X}", contentStarter.actorId),
                "zone", zoneName,
                "privateArea", privateAreaName,
                "privateAreaType", privateAreaType,
                "director", director == null ? "" : director.GetName());
            LuaEngine.GetInstance().CallLuaFunction(contentStarter, this, "onCreate", false, currentDirector);
        }
        
        public Director GetContentDirector()
        {
            return currentDirector;
        }

        public void ContentFinished()
        {
            isContentFinished = true;
            DevDiagnostics.Trace(
                "content.area.finished",
                "zone", zoneName,
                "privateArea", GetPrivateAreaName(),
                "privateAreaType", GetPrivateAreaType());
        }

        public void CheckDestroy()
        {
            lock (mActorList)
            {
                if (isContentFinished)
                {
                    bool noPlayersLeft = true;
                    foreach (Actor a in mActorList.Values)
                    {
                        if (a is Player)
                            noPlayersLeft = false;
                    }
                    if (noPlayersLeft)
                    {
                        DevDiagnostics.Trace(
                            "content.area.destroy",
                            "zone", zoneName,
                            "privateArea", GetPrivateAreaName(),
                            "privateAreaType", GetPrivateAreaType());
                        GetParentZone().DeleteContentArea(this);
                    }
                }
            }
        }

    }
}
