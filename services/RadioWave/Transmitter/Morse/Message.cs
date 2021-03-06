using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Transmitter.Morse
{
	[DataContract]
	public class Message
	{
		[DataMember(Name = "text", Order = 1)] public string Text;
		[DataMember(Name = "dpm", Order = 2)] public int DPM;
		[DataMember(Name = "frequency", Order = 3)] public int Frequency;

		private sealed class MessageComparer : IEqualityComparer<Message>
		{
			public bool Equals(Message x, Message y)
			{
				if (ReferenceEquals(x, y)) return true;
				if (ReferenceEquals(x, null)) return false;
				if (ReferenceEquals(y, null)) return false;
				if (x.GetType() != y.GetType()) return false;
				return string.Equals(x.Text, y.Text) && x.DPM.Equals(y.DPM) && x.Frequency.Equals(y.Frequency);
			}

			public int GetHashCode(Message obj)
			{
				unchecked
				{
					var hashCode = (obj.Text != null ? obj.Text.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ obj.DPM.GetHashCode();
					hashCode = (hashCode * 397) ^ obj.Frequency.GetHashCode();
					return hashCode;
				}
			}
		}

		public static readonly IEqualityComparer<Message> Comparer = new MessageComparer();
	}
}