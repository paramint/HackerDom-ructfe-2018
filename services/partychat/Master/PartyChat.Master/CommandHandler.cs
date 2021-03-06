using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vostok.Logging.Abstractions;

namespace PartyChat.Master
{
    internal class CommandHandler
    {
        private static readonly TimeSpan HistoryRequestTimeout = TimeSpan.FromSeconds(10); 
        
        private readonly SessionStorage sessionStorage;
        private readonly HeartbeatStorage heartbeatStorage;
        private readonly ILog log;

        private string nick;

        public CommandHandler(SessionStorage sessionStorage, HeartbeatStorage heartbeatStorage, ILog log)
        {
            this.sessionStorage = sessionStorage;
            this.heartbeatStorage = heartbeatStorage;
            this.log = log.ForContext(GetType().Name);
        }

        public async Task HandleCommand(Command command, Session session)
        {
            Group group;
            switch (command.Name)
            {
                case Commands.Heartbeat:
                    if (!TrySetNick(command.Text) || !sessionStorage.TryRegister(nick, session))
                    {
                        log.Info("Failed to set nick '{nick}' for client at {endpoint}. Nick is already in use. Killing session..", 
                            command.Text, session.RemoteEndpoint);
                        await session.Kill(true);
                        return;
                    }

                    heartbeatStorage.RegisterHeartbeat(nick);
                    session.SendResponse(command.Id, "OK");
                    break;
                
                case Commands.End:
                    heartbeatStorage.RemoveSession(nick);
                    await session.Kill();
                    break;
                
                case Commands.Say:
                    group = Group.ExtractGroup(command.Text);
                    
                    //log.Info("Saying '{text}' to ({group})..", command.Text, group);
                    
                    foreach (var member in group)
                    {
                        var memberSession = sessionStorage[member];
                        if (memberSession == null || !memberSession.IsAlive)
                            continue;
                        
                        memberSession.SendCommand(Commands.Say, command.Text);
                    }
                    break;
                
                case Commands.History:
                    group = Group.ExtractGroup(command.Text);
                    group = group.Add(nick);
                    
                    log.Info("The history of group ({group}) was requested. Collecting..", group);
                    
                    var responses = new List<Response>();
                    foreach (var member in group)
                    {
                        var memberSession = sessionStorage[member];
                        if (memberSession == null || !memberSession.IsAlive)
                            continue;
                        
                        var response = await memberSession.SendCommandWithResponse(Commands.History, group.ToString(), HistoryRequestTimeout);
                        responses.Add(response);
                    }
                    
                    log.Info("Sending collected and merged history of group ({group}) back..", group);

                    var mergedResponse = HistoryMerger.Merge(responses);
                    session.SendResponse(command.Id, mergedResponse);
                    break;
                
                case Commands.List:
                    session.SendResponse(command.Id, new Response(sessionStorage.ListAlive().Where(s => heartbeatStorage.IsStable(s))));
                    break;
            }
        }

        private bool TrySetNick(string value)
        {
            if (nick != null && nick != value)
                return false;

            nick = value;
            return true;
        }
    }
}