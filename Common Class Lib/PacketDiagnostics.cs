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

using NLog;

namespace MeteorXIV.Core.Common
{
    public static class PacketDiagnostics
    {
        private const int HEX_PREVIEW_BYTES = 128;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static void LogBasePacket(string serverName, string context, BasePacket packet)
        {
            if (packet == null)
                return;

            logger.Info(
                "[{0}] Base packet context={1} auth={2} compressed={3} connectionType=0x{4:X} size={5} subpackets={6} timestamp={7}{8}{9}",
                serverName,
                context,
                packet.header.isAuthenticated,
                packet.header.isCompressed,
                packet.header.connectionType,
                packet.header.packetSize,
                packet.header.numSubpackets,
                packet.header.timestamp,
                Environment.NewLine,
                GetPreview(packet.GetPacketBytes(), 0));
        }

        public static void LogBufferPreview(string serverName, string context, byte[] bytes, int offset, int count)
        {
            if (bytes == null || count <= 0 || offset < 0 || offset >= bytes.Length)
                return;

            logger.Info(
                "[{0}] Buffer context={1} offset={2} bytes={3}{4}{5}",
                serverName,
                context,
                offset,
                count,
                Environment.NewLine,
                GetPreview(bytes, offset, count));
        }

        public static void LogUnknownBasePacket(string serverName, string context, BasePacket packet)
        {
            if (packet == null)
                return;

            logger.Info(
                "[{0}] Unknown base packet context={1} auth={2} compressed={3} connectionType=0x{4:X} size={5} subpackets={6} timestamp={7}{8}{9}",
                serverName,
                context,
                packet.header.isAuthenticated,
                packet.header.isCompressed,
                packet.header.connectionType,
                packet.header.packetSize,
                packet.header.numSubpackets,
                packet.header.timestamp,
                Environment.NewLine,
                GetPreview(packet.GetPacketBytes(), 0));
        }

        public static void LogUnknownSubPacket(string serverName, string context, SubPacket subpacket)
        {
            if (subpacket == null)
                return;

            DevDiagnostics.TraceSubPacketClassification(context, subpacket);

            logger.Info(
                "[{0}] Unknown subpacket context={1} type=0x{2:X} opcode=0x{3:X} source=0x{4:X} target=0x{5:X} size={6} payload={7}{8}{9}",
                serverName,
                context,
                subpacket.header.type,
                subpacket.gameMessage.opcode,
                subpacket.header.sourceId,
                subpacket.header.targetId,
                subpacket.header.subpacketSize,
                subpacket.data == null ? 0 : subpacket.data.Length,
                Environment.NewLine,
                GetPreview(subpacket.GetBytes(), 0));
        }

        public static void LogUnknownGameMessage(string serverName, string context, SubPacket subpacket)
        {
            if (subpacket == null)
                return;

            DevDiagnostics.TraceSubPacketClassification(context, subpacket);

            logger.Info(
                "[{0}] Unknown game message context={1} opcode=0x{2:X} source=0x{3:X} target=0x{4:X} size={5} payload={6}{7}{8}",
                serverName,
                context,
                subpacket.gameMessage.opcode,
                subpacket.header.sourceId,
                subpacket.header.targetId,
                subpacket.header.subpacketSize,
                subpacket.data == null ? 0 : subpacket.data.Length,
                Environment.NewLine,
                GetPreview(subpacket.GetBytes(), 0));
        }

        private static string GetPreview(byte[] bytes, int offset)
        {
            return GetPreview(bytes, 0, bytes == null ? 0 : bytes.Length, offset);
        }

        private static string GetPreview(byte[] bytes, int sourceOffset, int count)
        {
            return GetPreview(bytes, sourceOffset, count, sourceOffset);
        }

        private static string GetPreview(byte[] bytes, int sourceOffset, int count, int displayOffset)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            if (sourceOffset < 0 || sourceOffset >= bytes.Length || count <= 0)
                return string.Empty;

            int available = Math.Min(bytes.Length - sourceOffset, count);
            int previewLength = Math.Min(available, HEX_PREVIEW_BYTES);
            byte[] preview = new byte[previewLength];
            Array.Copy(bytes, sourceOffset, preview, 0, previewLength);
            return Utils.ByteArrayToHex(preview, displayOffset);
        }
    }
}
