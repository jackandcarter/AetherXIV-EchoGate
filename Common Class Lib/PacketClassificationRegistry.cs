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

namespace MeteorXIV.Core.Common
{
    public static class PacketClassificationRegistry
    {
        public static string Classify(string context, SubPacket subpacket)
        {
            if (subpacket == null)
                return null;

            string normalizedContext = context == null ? String.Empty : context.ToLowerInvariant();

            if (normalizedContext.Contains("world") && subpacket.header.type == 0x08 && subpacket.header.subpacketSize == 0x18)
                return "world.session-heartbeat-candidate";

            if (normalizedContext.Contains("map") && subpacket.header.type == 0x03 && subpacket.gameMessage.opcode == 0x00CE)
                return "map.event-tutorial-ui-state-candidate";

            if (normalizedContext.Contains("map") && subpacket.header.type == 0x03 && subpacket.gameMessage.opcode == 0x0002)
                return "map.login-zone-bootstrap-candidate";

            return null;
        }
    }
}
