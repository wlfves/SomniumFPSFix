using System;
using System.Collections.Generic;

namespace SomniumFPSFix
{
    // Applies the FPS fix directly to the compiled Somnium script, in memory.
    //
    // The script is standard Lua 5.1 bytecode. This class reads that bytecode into
    // a small in-memory model, walks to the one function that contains the bug (the
    // character update function, at a fixed location in the script), double-checks
    // that the exact instructions we expect are present, makes three small edits,
    // and writes the bytecode back out unchanged apart from those edits.
    //
    // If anything doesn't match what we expect, it throws, and Plugin.cs falls back
    // to the simpler safety-net fix. Nothing is read from or written to disk.
    //
    // (This is a C# port of the offline tools/patch_somnium.py script.)
    internal static class LuaPatch
    {
        // Path to the buggy function, as a chain of child-function indexes from the
        // top of the script (the character update function).
        static readonly int[] TargetPath   = { 18, 4, 4 };

        // Positions of the four instructions we edit, within that function.
        const int PC_MOVE_CEIL   = 138; // loads the "moving" limit (1.0)
        const int PC_STILL_FLOOR = 150; // loads the "standing still" limit (0.0)
        const int PC_FB_START    = 170; // first instruction of the runaway line
        const int PC_FB_END      = 177; // one past the last instruction of the runaway line

        const int OP_LOADK       = 1;    // the Lua "load a constant" instruction
        const double MIN         = 0.01; // the game's SOMNIUM_MIN_SPEED constant

        // Expected 12-byte Lua 5.1 file header (little-endian, int=4, size_t=8, num=8/float)
        static readonly byte[] ExpectedHeader =
        {
            0x1B, 0x4C, 0x75, 0x61, // \x1bLua
            0x51,                   // version 5.1
            0x00,                   // official format
            0x01,                   // little-endian
            0x04,                   // int size
            0x08,                   // size_t size
            0x04,                   // instruction size
            0x08,                   // lua_Number size (double)
            0x00,                   // integral flag (float)
        };

        // The exact bytes of the runaway line we expect to find (the 7 instructions
        // that compute "amount = (amount + MIN) / (1 - MIN)" and store it back).
        // We verify these are present before editing, as a safety check.
        internal static readonly byte[] FeedbackPattern =
        {
            0x84, 0x00, 0x80, 0x03,
            0xC4, 0x00, 0x00, 0x04,
            0x8C, 0xC0, 0x00, 0x01,
            0xC4, 0x00, 0x00, 0x04,
            0xCD, 0xC0, 0x00, 0x8C,
            0x8F, 0xC0, 0x00, 0x01,
            0x88, 0x00, 0x80, 0x03,
        };

        // Returns the patched bytecode, or throws InvalidOperationException describing
        // which assertion failed (caller should log and fall back to the NOP patch).
        public static byte[] Apply(byte[] stock)
        {
            if (stock.Length < 12)
                throw new InvalidOperationException("File too short to be Lua 5.1");
            for (int i = 0; i < 12; i++)
                if (stock[i] != ExpectedHeader[i])
                    throw new InvalidOperationException($"Header mismatch at byte {i}: got 0x{stock[i]:X2}, expected 0x{ExpectedHeader[i]:X2}");

            var r = new Reader(stock, 12);
            var top = ReadProto(r);
            if (r.Position != stock.Length)
                throw new InvalidOperationException($"Trailing bytes after main proto ({stock.Length - r.Position} extra)");

            // Round-trip sanity: prove the reader/writer is byte-exact before mutating.
            var w0 = new Writer();
            w0.WriteRaw(stock, 0, 12);
            WriteProto(w0, top);
            byte[] rt = w0.ToArray();
            if (rt.Length != stock.Length)
                throw new InvalidOperationException($"Round-trip length mismatch: {rt.Length} vs {stock.Length}");
            for (int i = 0; i < stock.Length; i++)
                if (rt[i] != stock[i])
                    throw new InvalidOperationException($"Round-trip byte mismatch at offset {i}");

            // Navigate to the target proto.
            var c = top;
            foreach (int j in TargetPath)
            {
                if (j >= c.Protos.Count)
                    throw new InvalidOperationException($"Proto index {j} out of range (count={c.Protos.Count})");
                c = c.Protos[j];
            }

            double stillTarget = MIN / (1 - MIN);       // 0.010101... : slow crawl when standing still
            double moveTarget  = (1 + MIN) / (1 - MIN); // 1.020202... : normal speed when moving

            // Check the "moving limit" instruction really does load 1.0.
            uint ci = c.Code[PC_MOVE_CEIL];
            if ((ci & 0x3f) != OP_LOADK)
                throw new InvalidOperationException($"pc{PC_MOVE_CEIL} opcode is not LOADK (got {ci & 0x3f})");
            int ceilKIdx = (int)((ci >> 14) & 0x3ffff);
            var ceilK = c.Consts[ceilKIdx];
            if (ceilK.Type != 3 || ceilK.NumVal != 1.0)
                throw new InvalidOperationException($"pc{PC_MOVE_CEIL} LOADK target is not 1.0 (got type={ceilK.Type} val={ceilK.NumVal})");

            // Check the "standing still limit" instruction really does load 0.0.
            uint fi = c.Code[PC_STILL_FLOOR];
            if ((fi & 0x3f) != OP_LOADK)
                throw new InvalidOperationException($"pc{PC_STILL_FLOOR} opcode is not LOADK (got {fi & 0x3f})");
            int floorKIdx = (int)((fi >> 14) & 0x3ffff);
            var floorK = c.Consts[floorKIdx];
            if (floorK.Type != 3 || floorK.NumVal != 0.0)
                throw new InvalidOperationException($"pc{PC_STILL_FLOOR} LOADK target is not 0.0 (got type={floorK.Type} val={floorK.NumVal})");

            // Check the runaway line matches the instructions we expect.
            for (int i = PC_FB_START; i < PC_FB_END; i++)
            {
                uint expected = BitConverter.ToUInt32(FeedbackPattern, (i - PC_FB_START) * 4);
                if (c.Code[i] != expected)
                    throw new InvalidOperationException($"Feedback pattern mismatch at pc{i}: got 0x{c.Code[i]:X8}, expected 0x{expected:X8}");
            }

            // Edit 1: add the two corrected target numbers to the script's list
            // of constants.
            int idxMove  = c.Consts.Count; c.Consts.Add(new Const { Type = 3, NumVal = moveTarget });
            int idxStill = c.Consts.Count; c.Consts.Add(new Const { Type = 3, NumVal = stillTarget });

            // Edit 2: point the two limit-loading instructions at those new
            // numbers instead of the original 1.0 and 0.0.
            int aMove  = (int)((ci >> 6) & 0xff);
            int aStill = (int)((fi >> 6) & 0xff);
            c.Code[PC_MOVE_CEIL]   = (uint)(OP_LOADK | (aMove  << 6) | (idxMove  << 14));
            c.Code[PC_STILL_FLOOR] = (uint)(OP_LOADK | (aStill << 6) | (idxStill << 14));

            // Edit 3: replace the runaway line with do-nothing instructions
            // (all-zero bytes), so the number is no longer fed back into itself.
            for (int i = PC_FB_START; i < PC_FB_END; i++)
                c.Code[i] = 0;

            var w = new Writer();
            w.WriteRaw(stock, 0, 12);
            WriteProto(w, top);
            return w.ToArray();
        }

        // ── Proto model ──────────────────────────────────────────────────────

        class Proto
        {
            public byte[] Source;
            public int LineDefined, LastLineDefined;
            public byte Nups, NumParams, IsVarArg, MaxStack;
            public uint[] Code;
            public List<Const> Consts;
            public List<Proto> Protos;
            public int[] LineInfo;
            public LocVar[] LocVars;
            public byte[][] UpValues;
        }

        class Const
        {
            public byte Type;      // 0=nil 1=bool 3=num 4=str
            public bool BoolVal;
            public double NumVal;
            public byte[] StrVal;
        }

        struct LocVar { public byte[] Name; public int StartPc, EndPc; }

        // ── Reader ───────────────────────────────────────────────────────────

        class Reader
        {
            readonly byte[] _d;
            int _p;
            public int Position => _p;
            public Reader(byte[] d, int p) { _d = d; _p = p; }

            public byte   ReadByte()  => _d[_p++];
            public int    ReadInt()   { var v = BitConverter.ToInt32(_d, _p);    _p += 4; return v; }
            public long   ReadSizeT() { var v = BitConverter.ToInt64(_d, _p);    _p += 8; return v; }
            public double ReadNum()   { var v = BitConverter.ToDouble(_d, _p);   _p += 8; return v; }
            public uint   ReadInstr() { var v = BitConverter.ToUInt32(_d, _p);   _p += 4; return v; }

            public byte[] ReadString()
            {
                long n = ReadSizeT();
                if (n == 0) return null;
                var s = new byte[n - 1]; // strip null terminator
                Array.Copy(_d, _p, s, 0, (int)(n - 1));
                _p += (int)n;
                return s;
            }
        }

        // ── Writer ───────────────────────────────────────────────────────────

        class Writer
        {
            readonly List<byte> _b = new List<byte>(262144);
            public byte[] ToArray() => _b.ToArray();

            public void WriteRaw(byte[] src, int offset, int count)
            {
                for (int i = offset; i < offset + count; i++) _b.Add(src[i]);
            }

            public void WriteByte(byte v) => _b.Add(v);

            public void WriteInt(int v)
            {
                var b = BitConverter.GetBytes(v);
                _b.Add(b[0]); _b.Add(b[1]); _b.Add(b[2]); _b.Add(b[3]);
            }

            public void WriteSizeT(long v)
            {
                var b = BitConverter.GetBytes(v);
                for (int i = 0; i < 8; i++) _b.Add(b[i]);
            }

            public void WriteNum(double v)
            {
                var b = BitConverter.GetBytes(v);
                for (int i = 0; i < 8; i++) _b.Add(b[i]);
            }

            public void WriteInstr(uint v)
            {
                var b = BitConverter.GetBytes(v);
                _b.Add(b[0]); _b.Add(b[1]); _b.Add(b[2]); _b.Add(b[3]);
            }

            public void WriteString(byte[] s)
            {
                if (s == null) { WriteSizeT(0); return; }
                WriteSizeT(s.Length + 1);
                for (int i = 0; i < s.Length; i++) _b.Add(s[i]);
                _b.Add(0);
            }
        }

        // ── Proto read / write ───────────────────────────────────────────────

        static Proto ReadProto(Reader r)
        {
            var p = new Proto
            {
                Source          = r.ReadString(),
                LineDefined     = r.ReadInt(),
                LastLineDefined = r.ReadInt(),
                Nups            = r.ReadByte(),
                NumParams       = r.ReadByte(),
                IsVarArg        = r.ReadByte(),
                MaxStack        = r.ReadByte(),
            };

            int n = r.ReadInt();
            p.Code = new uint[n];
            for (int i = 0; i < n; i++) p.Code[i] = r.ReadInstr();

            n = r.ReadInt();
            p.Consts = new List<Const>(n);
            for (int i = 0; i < n; i++)
            {
                var c = new Const { Type = r.ReadByte() };
                if      (c.Type == 1) c.BoolVal = r.ReadByte() != 0;
                else if (c.Type == 3) c.NumVal  = r.ReadNum();
                else if (c.Type == 4) c.StrVal  = r.ReadString();
                p.Consts.Add(c);
            }

            n = r.ReadInt();
            p.Protos = new List<Proto>(n);
            for (int i = 0; i < n; i++) p.Protos.Add(ReadProto(r));

            n = r.ReadInt();
            p.LineInfo = new int[n];
            for (int i = 0; i < n; i++) p.LineInfo[i] = r.ReadInt();

            n = r.ReadInt();
            p.LocVars = new LocVar[n];
            for (int i = 0; i < n; i++)
                p.LocVars[i] = new LocVar { Name = r.ReadString(), StartPc = r.ReadInt(), EndPc = r.ReadInt() };

            n = r.ReadInt();
            p.UpValues = new byte[n][];
            for (int i = 0; i < n; i++) p.UpValues[i] = r.ReadString();

            return p;
        }

        static void WriteProto(Writer w, Proto p)
        {
            w.WriteString(p.Source);
            w.WriteInt(p.LineDefined);
            w.WriteInt(p.LastLineDefined);
            w.WriteByte(p.Nups);
            w.WriteByte(p.NumParams);
            w.WriteByte(p.IsVarArg);
            w.WriteByte(p.MaxStack);

            w.WriteInt(p.Code.Length);
            foreach (uint instr in p.Code) w.WriteInstr(instr);

            w.WriteInt(p.Consts.Count);
            foreach (var c in p.Consts)
            {
                w.WriteByte(c.Type);
                if      (c.Type == 1) w.WriteByte((byte)(c.BoolVal ? 1 : 0));
                else if (c.Type == 3) w.WriteNum(c.NumVal);
                else if (c.Type == 4) w.WriteString(c.StrVal);
            }

            w.WriteInt(p.Protos.Count);
            foreach (var child in p.Protos) WriteProto(w, child);

            w.WriteInt(p.LineInfo.Length);
            foreach (int li in p.LineInfo) w.WriteInt(li);

            w.WriteInt(p.LocVars.Length);
            foreach (var lv in p.LocVars) { w.WriteString(lv.Name); w.WriteInt(lv.StartPc); w.WriteInt(lv.EndPc); }

            w.WriteInt(p.UpValues.Length);
            foreach (var uv in p.UpValues) w.WriteString(uv);
        }
    }
}
