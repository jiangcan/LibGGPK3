﻿using System.Collections.Generic;
using System.IO;

namespace LibGGPK3.Records {
	/// <summary>
	/// A free record represents space in the pack file that has been marked as deleted. It's much cheaper to just
	/// mark areas as free and append data to the end of the pack file than it is to rebuild the entire pack file just
	/// to remove a piece of data.
	/// </summary>
	public class FreeRecord : BaseRecord {
		/// <summary>FREE</summary>
		public const uint Tag = 0x45455246;

		/// <summary>
		/// Offset of next FreeRecord
		/// </summary>
		public long NextFreeOffset { get; protected internal set; }

		protected internal FreeRecord(int length, GGPK ggpk) : base(length, ggpk) {
			Offset = ggpk.GGPKStream.Position - 8;
			NextFreeOffset = ggpk.GGPKStream.ReadInt64();
			ggpk.GGPKStream.Seek(Length - 16, SeekOrigin.Current);
		}

		/// <summary>
		/// Also calls the <see cref="WriteRecordData"/>.
		/// Please calls <see cref="UpdateOffset"/> after this to add the FreeRecord to <see cref="GGPK.FreeRecords"/>
		/// </summary>
		protected internal FreeRecord(long offset, int length, long nextFreeOffset, GGPK ggpk) : base(length, ggpk) {
			NextFreeOffset = nextFreeOffset;
			Ggpk.GGPKStream.Seek(offset, SeekOrigin.Begin);
			WriteRecordData();
		}

		protected internal override void WriteRecordData() {
			var s = Ggpk.GGPKStream;
			Offset = s.Position;
			s.Write(Length);
			s.Write(Tag);
			s.Write(NextFreeOffset);
			s.Seek(Length - 16, SeekOrigin.Current);
		}

		/// <summary>
		/// Remove this FreeRecord from the Linked FreeRecord List
		/// </summary>
		/// <param name="node">Node in <see cref="GGPK.FreeRecords"/> to remove</param>
		public virtual void RemoveFromList(LinkedListNode<FreeRecord>? node = null) {
			var s = Ggpk.GGPKStream;
			node ??= Ggpk.FreeRecords.Find(this);
			if (node == null)
				return;
			var previous = node.Previous?.Value;
			var next = node.Next?.Value;
			if (next == null)
				if (previous == null) {
					Ggpk.GgpkRecord.FirstFreeRecordOffset = 0;
					s.Seek(Ggpk.GgpkRecord.Offset + 20, SeekOrigin.Begin);
					s.Write((long)0);
				} else {
					previous.NextFreeOffset = 0;
					s.Seek(previous.Offset + 8, SeekOrigin.Begin);
					s.Write((long)0);
				}
			else if (previous == null) {
				Ggpk.GgpkRecord.FirstFreeRecordOffset = next.Offset;
				s.Seek(Ggpk.GgpkRecord.Offset + 20, SeekOrigin.Begin);
				s.Write(next.Offset);
			} else {
				previous.NextFreeOffset = next.Offset;
				s.Seek(previous.Offset + 8, SeekOrigin.Begin);
				s.Write(next.Offset);
			}
			Ggpk.FreeRecords.Remove(node);
			s.Flush();
		}

		/// <summary>
		/// Update the link after the Offset of this FreeRecord is changed
		/// </summary>
		/// <param name="node">Node in <see cref="GGPK.FreeRecords"/> to remove</param>
		public virtual LinkedListNode<FreeRecord> UpdateOffset(LinkedListNode<FreeRecord>? node = null) {
			var s = Ggpk.GGPKStream;
			node ??= Ggpk.FreeRecords.Find(this);
			var lastFree = node == null ? Ggpk.FreeRecords.Last?.Value : node.Previous?.Value;
			if (lastFree == null) { // First FreeRecord
				Ggpk.GgpkRecord.FirstFreeRecordOffset = Offset;
				s.Seek(Ggpk.GgpkRecord.Offset + 20, SeekOrigin.Begin);
				s.Write(Offset);
			} else {
				lastFree.NextFreeOffset = Offset;
				s.Seek(lastFree.Offset + 8, SeekOrigin.Begin);
				s.Write(Offset);
			}
			return node ?? Ggpk.FreeRecords.AddLast(this);
		}
	}
}