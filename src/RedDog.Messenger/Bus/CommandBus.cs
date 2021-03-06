﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using RedDog.Messenger.Contracts;
using RedDog.Messenger.Diagnostics;

namespace RedDog.Messenger.Bus
{
    public class CommandBus : MessageBus, ICommandBus
    {
        public CommandBus(IBusConfiguration<ICommandBusConfiguration> configuration)
            : base(configuration)
        {
        }

        /// <summary>
        /// Send a command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public Task SendAsync<TCommand>(TCommand command)
            where TCommand : class, ICommand
        {
            return SendAsync(new Envelope<TCommand>(command));
        }

        /// <summary>
        /// Send multiple commands.
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public Task SendAsync<TCommand>(IEnumerable<TCommand> commands)
            where TCommand : class, ICommand
        {
            return SendAsync(commands.Select(command => new Envelope<TCommand>(command)).ToArray());
        }

        /// <summary>
        /// Send a command wrapped in an envelope.
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        public async Task SendAsync<TCommand>(Envelope<TCommand> envelope)
            where TCommand : class, ICommand
        {
            try
            {
                MessagingEventSource.Log.SendingCommand(envelope.Body.GetType(), envelope);

                // Send.
                var sender = Configuration
                    .GetSender(typeof(TCommand));
                await sender
                    .SendAsync(await BuildBrokeredMessage(envelope))
                    .ConfigureAwait(false);

                // Complete.
                MessagingEventSource.Log.CommandSent(typeof(TCommand), sender, envelope);

            }
            catch (Exception ex)
            {
                MessagingEventSource.Log.SendCommandFailed(envelope.Body.GetType(), ex);

                // Rethrow.
                throw;
            }
        }

        /// <summary>
        /// Send multiple commands each wrapped in an envelope.
        /// </summary>
        /// <param name="envelopes"></param>
        /// <returns></returns>
        public async Task SendAsync<TCommand>(Envelope<TCommand>[] envelopes)
            where TCommand : class, ICommand
        {
            try
            {
                foreach (var envelope in envelopes)
                {
                    MessagingEventSource.Log.SendingCommand(typeof(TCommand), envelope);
                }

                // Send batch.
                var sender = Configuration
                    .GetSender(typeof(TCommand));
                await sender
                    .SendBatchAsync(await Task.WhenAll(envelopes.Select(async command => await BuildBrokeredMessage(command))))
                    .ConfigureAwait(false);

                // Complete.
                MessagingEventSource.Log.CommandBatchSent(typeof(TCommand), envelopes.Length, sender);
            }
            catch (Exception ex)
            {
                MessagingEventSource.Log.SendCommandBatchFailed(typeof(TCommand), envelopes.Length, ex);

                // Rethrow.
                throw;
            }
        }
    }
}