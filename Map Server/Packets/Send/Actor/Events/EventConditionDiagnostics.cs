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

using MeteorXIV.Core.Common;
using MeteorXIV.Core.Map.actors;

namespace MeteorXIV.Core.Map.packets.send.actor.events
{
    static class EventConditionDiagnostics
    {
        public static void TraceTalk(uint sourceActorId, EventList.TalkEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "talk",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "unknown1", condition.unknown1,
                "isDisabled", condition.isDisabled);
        }

        public static void TraceNotice(uint sourceActorId, EventList.NoticeEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "notice",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "unknown1", condition.unknown1,
                "unknown2", condition.unknown2);
        }

        public static void TraceEmote(uint sourceActorId, EventList.EmoteEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "emote",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "unknown1", condition.unknown1,
                "unknown2", condition.unknown2,
                "emoteId", condition.emoteId);
        }

        public static void TracePushCircle(uint sourceActorId, EventList.PushCircleEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "pushCircle",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "radius", condition.radius,
                "outwards", condition.outwards,
                "silent", condition.silent);
        }

        public static void TracePushFan(uint sourceActorId, EventList.PushFanEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "pushFan",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "radius", condition.radius,
                "outwards", condition.outwards,
                "silent", condition.silent);
        }

        public static void TracePushBox(uint sourceActorId, EventList.PushBoxEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "pushBox",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "reactName", condition.reactName,
                "bgObj", Hex(condition.bgObj),
                "layout", Hex(condition.layout),
                "outwards", condition.outwards,
                "silent", condition.silent);
        }

        public static void TraceStatus(uint sourceActorId, bool enabled, byte type, string conditionName)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "event.condition.status",
                "action", "status",
                "conditionKind", StatusTypeName(type),
                "sourceActor", Hex(sourceActorId),
                "conditionName", conditionName,
                "enabled", enabled,
                "type", type);
        }

        private static string StatusTypeName(byte type)
        {
            switch (type)
            {
                case 1:
                    return "talk";
                case 2:
                    return "push";
                case 3:
                    return "emote";
                case 5:
                    return "notice";
                default:
                    return "unknown";
            }
        }

        private static string Hex(uint value)
        {
            return String.Format("0x{0:X}", value);
        }
    }
}
