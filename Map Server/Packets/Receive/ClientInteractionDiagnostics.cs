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
using System.Text;

using MeteorXIV.Core.Common;
using MeteorXIV.Core.Map.Actors;
using MeteorXIV.Core.Map.dataobjects;
using MeteorXIV.Core.Map.lua;
using MeteorXIV.Core.Map.packets.receive.events;

namespace MeteorXIV.Core.Map.packets.receive
{
    static class ClientInteractionDiagnostics
    {
        public static void TraceTarget(Session session, SetTargetPacket packet)
        {
            if (!DevDiagnostics.Enabled || packet == null)
                return;

            Actor targetActor = ResolveActor(session, packet.actorID);

            DevDiagnostics.Trace(
                "client.target",
                "player", PlayerName(session),
                "targetActor", Hex(packet.actorID),
                "targetActorName", ActorName(targetActor),
                "attackTarget", Hex(packet.attackTarget),
                "autoAttackRequested", packet.attackTarget != 0xE0000000,
                "invalidPacket", packet.invalidPacket);
        }

        public static void TraceLockTarget(Session session, LockTargetPacket packet)
        {
            if (!DevDiagnostics.Enabled || packet == null)
                return;

            Actor targetActor = ResolveActor(session, packet.actorID);

            DevDiagnostics.Trace(
                "client.lockTarget",
                "player", PlayerName(session),
                "targetActor", Hex(packet.actorID),
                "targetActorName", ActorName(targetActor),
                "otherVal", Hex(packet.otherVal),
                "invalidPacket", packet.invalidPacket);
        }

        public static void TraceEventStartOwnerMissing(Session session, EventStartPacket packet)
        {
            if (!DevDiagnostics.Enabled || packet == null)
                return;

            DevDiagnostics.Trace(
                "event.start.ownerMissing",
                "player", PlayerName(session),
                "triggerActor", Hex(packet.triggerActorID),
                "ownerActor", Hex(packet.ownerActorID),
                "serverCodes", Hex(packet.serverCodes),
                "unknown", Hex(packet.unknown),
                "eventName", packet.eventName,
                "params", LuaUtils.DumpParams(packet.luaParams));
        }

        public static void TraceStateMessage(Session session, SubPacket subpacket)
        {
            if (!DevDiagnostics.Enabled || subpacket == null)
                return;

            byte[] data = subpacket.data;
            uint value0 = ReadUInt32(data, 0);
            uint value16 = ReadUInt32(data, 16);
            uint tailValue = data != null && data.Length >= 4 ? ReadUInt32(data, data.Length - 4) : 0;
            string tokenAt4 = ReadFixedAscii(data, 4, 8);

            DevDiagnostics.Trace(
                "client.stateMessage",
                "classification", "map.event-tutorial-ui-state-candidate",
                "player", PlayerName(session),
                "source", Hex(subpacket.header.sourceId),
                "target", Hex(subpacket.header.targetId),
                "opcode", Hex(subpacket.gameMessage.opcode),
                "payloadLength", data == null ? 0 : data.Length,
                "value0", Hex(value0),
                "value16", Hex(value16),
                "tokenAt4", tokenAt4,
                "tokenAt4Upper", tokenAt4.ToUpperInvariant(),
                "tailValue", Hex(tailValue),
                "tailBytes", TailBytes(data),
                "printableRuns", PrintableRuns(data));
        }

        private static Actor ResolveActor(Session session, uint actorId)
        {
            Actor actor = Server.GetStaticActors(actorId);
            if (actor != null)
                return actor;

            if (session == null || session.GetActor() == null || session.GetActor().zone == null)
                return null;

            return session.GetActor().zone.FindActorInArea(actorId);
        }

        private static string PlayerName(Session session)
        {
            if (session == null || session.GetActor() == null)
                return "";

            if (!String.IsNullOrEmpty(session.GetActor().customDisplayName))
                return session.GetActor().customDisplayName;

            return session.GetActor().GetName();
        }

        private static string ActorName(Actor actor)
        {
            if (actor == null)
                return "";

            if (!String.IsNullOrEmpty(actor.customDisplayName))
                return actor.customDisplayName;

            return actor.GetName();
        }

        private static string ReadFixedAscii(byte[] data, int offset, int length)
        {
            if (data == null || data.Length <= offset)
                return "";

            int end = Math.Min(data.Length, offset + length);
            StringBuilder builder = new StringBuilder();
            for (int i = offset; i < end; i++)
            {
                byte value = data[i];
                if (value == 0)
                    break;

                builder.Append(IsPrintableAscii(value) ? (char)value : '.');
            }

            return builder.ToString();
        }

        private static string PrintableRuns(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";

            StringBuilder runs = new StringBuilder();
            StringBuilder current = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                if (IsPrintableAscii(data[i]))
                {
                    current.Append((char)data[i]);
                    continue;
                }

                AppendRun(runs, current);
                current.Length = 0;
            }

            AppendRun(runs, current);
            return runs.ToString();
        }

        private static void AppendRun(StringBuilder runs, StringBuilder current)
        {
            if (current.Length < 3)
                return;

            if (runs.Length > 0)
                runs.Append(",");

            runs.Append(current.ToString());
        }

        private static string TailBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";

            int start = Math.Max(0, data.Length - 8);
            StringBuilder builder = new StringBuilder();
            for (int i = start; i < data.Length; i++)
            {
                if (builder.Length > 0)
                    builder.Append(" ");

                builder.Append(data[i].ToString("X2"));
            }

            return builder.ToString();
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            if (data == null || offset < 0 || data.Length < offset + 4)
                return 0;

            return BitConverter.ToUInt32(data, offset);
        }

        private static bool IsPrintableAscii(byte value)
        {
            return value >= 0x20 && value <= 0x7E;
        }

        private static string Hex(uint value)
        {
            return String.Format("0x{0:X}", value);
        }
    }
}
