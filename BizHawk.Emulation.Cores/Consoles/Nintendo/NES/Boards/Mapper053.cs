﻿using BizHawk.Common;
using BizHawk.Common.NumberExtensions;

namespace BizHawk.Emulation.Cores.Nintendo.NES
{
	public sealed class Mapper053 : NES.NESBoardBase
	{
		private byte _reg0;
		private byte _reg1;

		private bool Prg16kMode { get { return _reg0.Bit(4); } }

		public override bool Configure(NES.EDetectionOrigin origin)
		{
			switch (Cart.board_type)
			{
				case "MAPPER053":
					break;
				// TODO
				//case "UNIF_BMC-Supervision16in1":
				//	break;
				default:
					return false;
			}

			SetMirrorType(Cart.pad_h, Cart.pad_v);

			return true;
		}

		public override void SyncState(Serializer ser)
		{
			base.SyncState(ser);
			ser.Sync("reg0", ref _reg0);
			ser.Sync("reg1", ref _reg1);
		}

		private void SetMirroring()
		{
			bool mir = _reg0.Bit(5);
			SetMirrorType(mir ? EMirrorType.Horizontal : EMirrorType.Vertical);
        }

		public override void WriteWRAM(int addr, byte value)
		{
			if (!_reg0.Bit(4))
			{
				_reg0 = value;
				SetMirroring();
			}
			else
			{
				base.WriteWRAM(addr, value);
			}
		}

		public override void WritePRG(int addr, byte value)
		{
			_reg1 = value;
		}

		public override byte ReadPRG(int addr)
		{
			if (Prg16kMode)
			{
				// First 32kb of PRG is for the intro game picker, 2 is to offset that
				int bank = addr < 0x4000
					? (((_reg0 & 0xF) << 3) | (_reg1 & 7)) + 2
					: (((_reg0 & 0xF) << 3) | 7) + 2;

				return ROM[(bank * 0x4000) + (addr & 0x3FFF)];
			}

			return base.ReadPRG(addr);
		}

		public override byte ReadWRAM(int addr)
		{
			// First 32kb of PRG is for the intro game picker, 4 is to offset that
			int bank = (((_reg0 & 0xF) << 4) | 0xF) + 4;
			return ROM[(bank * 0x2000) + (addr & 0x1FFF)];
		}
	}
}
