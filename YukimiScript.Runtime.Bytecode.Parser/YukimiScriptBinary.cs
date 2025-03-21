using System;
using System.Collections.Generic;

namespace YukimiScript.Runtime.Bytecode.Parser
{
    public sealed class YukimiScriptBinary
    {
        const uint riffBlockID = 0x46464952;
        const uint yukiMagicCode = 0x494B5559;
        const uint cstrBlockID = 0x52545343;
        const uint extrBlockID = 0x52545845;
        const uint scenBlockID = 0x4E454353;
        const uint dbgsBlockID = 0x53474244;

        readonly byte[] bytes;

        uint ReadUInt32(uint offset)
        {
            if (offset + 4 >= bytes.Length)
                throw new InvalidYukimiScriptBinary();

            uint r = 0;
            r |= bytes[offset];
            r |= ((uint)bytes[offset + 1]) << 8;
            r |= ((uint)bytes[offset + 2]) << 16;
            r |= ((uint)bytes[offset + 3]) << 24;
            return r;
        }

        public sealed class InvalidYukimiScriptBinary : Exception { };

        public readonly struct Block
        {
            public readonly YukimiScriptBinary Bin;
            public readonly uint Type;
            public readonly uint DataPointer;
            public readonly uint DataEndPointer;
            public readonly uint Size;

            public Block(YukimiScriptBinary binary, ref uint offset)
            {
                Bin = binary;
                Type = binary.ReadUInt32(offset);
                Size = binary.ReadUInt32(offset + 4);
                DataPointer = offset + 8;
                DataEndPointer = offset = DataPointer + Size;

                if (offset > binary.bytes.Length)
                    throw new InvalidYukimiScriptBinary();
            }
        }

        public readonly IReadOnlyList<Block> SceneBlocks;
        readonly Block cstrBlock;
        readonly Block extrBlock;

        public sealed class SceneParser
        {
            public readonly Block Block;
            public readonly uint SceneNameCStrPointer;
            public uint Offset => offsetInFile - Block.DataPointer;
            uint offsetInFile;

            public SceneParser(Block scenBlock)
            {
                Block = scenBlock;
                SceneNameCStrPointer = scenBlock
                    .Bin
                    .ReadUInt32(scenBlock.DataPointer);

                offsetInFile = scenBlock.DataPointer + 4;
            }

            public bool ReadCommand(
                out ushort commandID,
                out ushort argumentsCount)
            {
                commandID = 0;
                argumentsCount = 0;

                if (offsetInFile + 4 >= Block.DataEndPointer)
                    return false;

                commandID |= Block.Bin.bytes[offsetInFile];
                commandID |= (ushort)(Block.Bin.bytes[offsetInFile + 1] << 8);
                argumentsCount |= Block.Bin.bytes[offsetInFile + 2];
                argumentsCount |= (ushort)(Block.Bin.bytes[offsetInFile + 3] << 8);
                offsetInFile += 4;
                return true;
            }

            public enum ArgumentType
            {
                Int = 0,
                Float = 1,
                String = 2,
                Symbol = 3
            }

            readonly byte[] temp = new byte[4];

            public ArgumentType ReadArgument(
                out int argInt,
                out float argFloat,
                out uint argCStrPointerString)
            {
                argInt = 0;
                argFloat = 0;
                argCStrPointerString = 0;

                var first = (int)Block.Bin.ReadUInt32(offsetInFile);
                offsetInFile += 4;

                if (first == 0)
                {
                    argInt = (int)Block.Bin.ReadUInt32(offsetInFile);
                    offsetInFile += 4;
                    return ArgumentType.Int;
                }
                else if (first == 1)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        temp[0] = Block.Bin.bytes[offsetInFile + 0];
                        temp[1] = Block.Bin.bytes[offsetInFile + 1];
                        temp[2] = Block.Bin.bytes[offsetInFile + 2];
                        temp[3] = Block.Bin.bytes[offsetInFile + 3];
                    }
                    else
                    {
                        temp[0] = Block.Bin.bytes[offsetInFile + 3];
                        temp[1] = Block.Bin.bytes[offsetInFile + 2];
                        temp[2] = Block.Bin.bytes[offsetInFile + 1];
                        temp[3] = Block.Bin.bytes[offsetInFile + 0];
                    }

                    argFloat = BitConverter.ToSingle(temp, 0);
                    offsetInFile += 4;
                    return ArgumentType.Float;
                }
                else if (first >= 2)
                {
                    argCStrPointerString = (uint)first - 2;
                    return ArgumentType.String;
                }
                else if (first <= -1)
                {
                    argCStrPointerString = (uint)Math.Abs(first + 1);
                    return ArgumentType.Symbol;
                }

                throw new InvalidYukimiScriptBinary();
            }
        }

        public IEnumerable<uint> ExternDefinitionCStrPointers
        {
            get
            {
                for (
                    uint offset = extrBlock.DataPointer;
                    offset < extrBlock.DataEndPointer;
                    offset += 4)
                {
                    yield return ReadUInt32(offset);
                }
            }
        }

        public YukimiScriptBinary(byte[] bytes)
        {
            this.bytes = bytes;

            if (ReadUInt32(0) != riffBlockID)
                throw new InvalidYukimiScriptBinary();

            if (ReadUInt32(4) + 8 != bytes.Length)
                throw new InvalidYukimiScriptBinary();

            if (ReadUInt32(8) != yukiMagicCode)
                throw new InvalidYukimiScriptBinary();

            Block? cstrBlock = null;
            Block? extrBlock = null;
            List<Block> scenBlocks = new();

            for (uint offset = 12; offset < bytes.Length;)
            {
                Block b = new(this, ref offset);

                if (b.Type == cstrBlockID && cstrBlock == null)
                    cstrBlock = b;
                else if (b.Type == extrBlockID && extrBlock == null)
                    extrBlock = b;
                else if (b.Type == scenBlockID)
                    scenBlocks.Add(b);
                else if (b.Type != dbgsBlockID) { }
                else
                    throw new InvalidYukimiScriptBinary();
            }

            if (cstrBlock == null)
                throw new InvalidYukimiScriptBinary();

            if (extrBlock == null)
                throw new InvalidYukimiScriptBinary();

            this.cstrBlock = cstrBlock.Value;
            this.extrBlock = extrBlock.Value;
            this.SceneBlocks = scenBlocks.ToArray();

            Console.WriteLine("Scen Blocks:" + scenBlocks.Count);
        }

        public string GetStringByCStrPointer(uint cstrPointer)
        {
            int len = 0;
            while (bytes[cstrBlock.DataPointer + cstrPointer + len] != '\0')
                len++;

            return System.Text.Encoding.UTF8.GetString(
                bytes, (int)(cstrBlock.DataPointer + cstrPointer), len);
        }
    }
}