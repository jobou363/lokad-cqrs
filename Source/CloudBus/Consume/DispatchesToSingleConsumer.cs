#region (c) 2010 Lokad Open Source - New BSD License 

// Copyright (c) Lokad 2010, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using CloudBus.Domain;
using Lokad;

namespace CloudBus.Consume
{
	public sealed class DispatchesToSingleConsumer : IMessageDispatcher
	{
		readonly ILifetimeScope _container;
		readonly IDictionary<Type, Type> _messageConsumers = new Dictionary<Type, Type>();
		readonly IMessageDirectory _messageDirectory;


		public DispatchesToSingleConsumer(ILifetimeScope container, IMessageDirectory messageDirectory)
		{
			_container = container;
			_messageDirectory = messageDirectory;
		}

		static void ThrowIfCommandHasMultipleConsumers(IEnumerable<MessageInfo> commands)
		{
			var multipleConsumers = commands
				.Where(c => c.DirectConsumers.Length > 1)
				.ToArray(c => c.MessageType.FullName);

			if (multipleConsumers.Any())
			{
				throw new InvalidOperationException(
					"These messages have multiple consumers. Did you intend to declare them as events? " +
						multipleConsumers.Join(Environment.NewLine));
			}
		}

		public void Init()
		{
			ThrowIfCommandHasMultipleConsumers(_messageDirectory.Messages);
			foreach (var messageInfo in _messageDirectory.Messages)
			{
				Enforce.That(messageInfo.DirectConsumers.Length == 1);
				_messageConsumers[messageInfo.MessageType] = messageInfo.DirectConsumers[0];
			}
		}

		public bool DispatchMessage(string topic, object message)
		{
			Type consumerType;
			var type = message.GetType();
			if (_messageConsumers.TryGetValue(type, out consumerType))
			{
				using (var scope = _container.BeginLifetimeScope())
				{
					var consumer = scope.Resolve(consumerType);
					_messageDirectory.InvokeConsume(consumer, message);
				}

				return true;
			}
			return false;
		}
	}
}