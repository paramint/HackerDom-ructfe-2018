using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Transmitter.Utils;

namespace Transmitter.Morse
{
	public class MixConverter : IEnumerator<double>
	{
		private readonly int rate;
		private readonly Dictionary<Message, MessageGenerator> generators = new Dictionary<Message, MessageGenerator>(Message.Comparer);

		public MixConverter(int rate)
		{
			this.rate = rate;
		}

		public void Sync(List<Message> messages)
		{
			if (messages == null)
				return;

			var add = messages.Except(generators.Keys, Message.Comparer).ToList();
			var remove = generators.Keys.Except(messages, Message.Comparer).ToList();

			Add(add);
			Remove(remove);
		}

		private void Add(IEnumerable<Message> messages) 
			=> messages.ForEach(message => generators[message] = new MessageGenerator(message, rate));

		private void Remove(IEnumerable<Message> messages)
			=> messages.ForEach(message => generators.Remove(message));

		public bool MoveNext()
		{
			Current = 0;
			var moveNext = false;
			foreach (var generator in generators)
			{
				if (generator.Value.MoveNext())
					moveNext = true;
				Current += generator.Value.Current / 8;
			}
			return moveNext;
		}

		public void Reset()
			=> generators.ForEach(pair => pair.Value.Reset());

		public double Current { get; private set; }

		object IEnumerator.Current
			=> Current;

		public void Dispose()
			=> generators.ForEach(pair => pair.Value.Dispose());
	}
}
